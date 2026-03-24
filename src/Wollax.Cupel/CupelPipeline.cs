using System.Diagnostics;
using System.Runtime.CompilerServices;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel;

/// <summary>
/// The context selection pipeline. Executes a fixed sequence of stages:
/// Classify, Score, Deduplicate, Sort, Slice, Place.
/// </summary>
/// <remarks>
/// Create instances via <see cref="CreateBuilder"/>.
/// This class is sealed and immutable — all configuration is locked at build time.
/// </remarks>
public sealed class CupelPipeline
{
    private readonly IScorer _scorer;
    private readonly ISlicer _slicer;
    private readonly IPlacer _placer;
    private readonly ContextBudget _budget;
    private readonly bool _deduplicationEnabled;
    private readonly IAsyncSlicer? _asyncSlicer;
    private readonly OverflowStrategy _overflowStrategy;
    private readonly Action<OverflowEvent>? _overflowObserver;

    internal IScorer Scorer => _scorer;
    internal ISlicer Slicer => _slicer;
    internal IPlacer Placer => _placer;
    internal IAsyncSlicer? AsyncSlicer => _asyncSlicer;
    internal bool DeduplicationEnabled => _deduplicationEnabled;
    internal OverflowStrategy OverflowStrategy => _overflowStrategy;

    internal CupelPipeline(
        IScorer scorer,
        ISlicer slicer,
        IPlacer placer,
        ContextBudget budget,
        bool deduplicationEnabled,
        IAsyncSlicer? asyncSlicer = null,
        OverflowStrategy overflowStrategy = OverflowStrategy.Throw,
        Action<OverflowEvent>? overflowObserver = null)
    {
        _scorer = scorer;
        _slicer = slicer;
        _placer = placer;
        _budget = budget;
        _deduplicationEnabled = deduplicationEnabled;
        _asyncSlicer = asyncSlicer;
        _overflowStrategy = overflowStrategy;
        _overflowObserver = overflowObserver;
    }

    /// <summary>
    /// Creates a new <see cref="PipelineBuilder"/> for configuring a pipeline.
    /// </summary>
    public static PipelineBuilder CreateBuilder() => new();

    /// <summary>
    /// Executes the pipeline on the given items.
    /// </summary>
    /// <param name="items">The context items to process.</param>
    /// <param name="traceCollector">Optional trace collector for diagnostics.</param>
    /// <returns>The pipeline result containing selected and ordered items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Pinned items exceed available token budget.</exception>
    /// <exception cref="OverflowException">The default <see cref="OverflowStrategy.Throw"/> is configured and selected items exceed <see cref="ContextBudget.TargetTokens"/> after merging with pinned items.</exception>
    public ContextResult Execute(
        IReadOnlyList<ContextItem> items,
        ITraceCollector? traceCollector = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var trace = traceCollector ?? NullTraceCollector.Instance;
        return ExecuteCore(items, trace);
    }

    /// <summary>
    /// Executes the pipeline in dry-run mode. Always produces a <see cref="SelectionReport"/>
    /// regardless of whether a <see cref="DiagnosticTraceCollector"/> was provided externally.
    /// The result is identical to <see cref="Execute"/> for the same input.
    /// </summary>
    /// <param name="items">The context items to process.</param>
    /// <returns>The pipeline result with a fully populated <see cref="SelectionReport"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="OverflowException">The default <see cref="OverflowStrategy.Throw"/> is configured and selected items exceed <see cref="ContextBudget.TargetTokens"/> after merging with pinned items.</exception>
    public ContextResult DryRun(IReadOnlyList<ContextItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var trace = new DiagnosticTraceCollector();
        return ExecuteCore(items, trace);
    }

    /// <summary>
    /// Internal dry-run with a temporary budget override. Used by budget-simulation
    /// extension methods to execute the full pipeline at different budget levels
    /// without mutating the pipeline's stored <see cref="_budget"/>.
    /// </summary>
    internal ContextResult DryRunWithBudget(IReadOnlyList<ContextItem> items, ContextBudget temporaryBudget)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(temporaryBudget);
        var trace = new DiagnosticTraceCollector();
        return ExecuteCore(items, trace, temporaryBudget);
    }

    /// <summary>
    /// Executes the pipeline in dry-run mode using the given <paramref name="policy"/> instead
    /// of this pipeline's own scorer, slicer, and placer. Useful for comparing configurations
    /// without building separate pipelines.
    /// </summary>
    /// <param name="items">The context items to process.</param>
    /// <param name="budget">The token budget to use. Must be provided explicitly — policies do not carry a budget.</param>
    /// <param name="policy">The policy that drives scorer, slicer, placer, deduplication, and overflow strategy.</param>
    /// <returns>The pipeline result with a fully populated <see cref="SelectionReport"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>CountQuota limitation:</b> <see cref="CupelPolicy"/> does not support count-based quota
    /// configurations (<c>CountQuotaSlice</c>). Callers needing count-quota fork diagnostics
    /// must use the pipeline-based <see cref="PolicySensitivityExtensions.PolicySensitivity"/>
    /// overload with pre-constructed pipelines.
    /// </para>
    /// <para>
    /// <b>Stream slicer fallback:</b> When <paramref name="policy"/> specifies
    /// <see cref="SlicerType.Stream"/>, the synchronous <see cref="Slicing.GreedySlice"/> is used
    /// as the slicer (the same fallback applied by <see cref="PipelineBuilder.WithPolicy"/>).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="items"/>, <paramref name="budget"/>, or <paramref name="policy"/> is <see langword="null"/>.
    /// </exception>
    public ContextResult DryRunWithPolicy(
        IReadOnlyList<ContextItem> items,
        ContextBudget budget,
        CupelPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(policy);
        var tempPipeline = CreateBuilder()
            .WithBudget(budget)
            .WithPolicy(policy)
            .Build();
        return tempPipeline.DryRunWithBudget(items, budget);
    }

    private ContextResult ExecuteCore(
        IReadOnlyList<ContextItem> items,
        ITraceCollector trace,
        ContextBudget? budgetOverride = null)
    {
        var budget = budgetOverride ?? _budget;
        var sw = trace.IsEnabled ? Stopwatch.StartNew() : null;
        ReportBuilder? reportBuilder = trace.IsEnabled ? new ReportBuilder() : null;
        List<StageTraceSnapshot>? stageSnapshots = trace.IsEnabled ? [] : null;

        // CLASSIFY: partition into pinned and scoreable, skip negative tokens
        var pinned = new List<ContextItem>();
        var scoreable = new List<ContextItem>();
        var totalTokensConsidered = 0;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Tokens < 0)
            {
                reportBuilder?.AddExcluded(item, 0.0, ExclusionReason.NegativeTokens);
                continue;
            }

            totalTokensConsidered += item.Tokens;

            if (item.Pinned)
            {
                pinned.Add(item);
            }
            else
            {
                scoreable.Add(item);
            }
        }

        reportBuilder?.SetTotalCandidates(items.Count);
        reportBuilder?.SetTotalTokensConsidered(totalTokensConsidered);

        if (sw is not null)
        {
            var classifyDuration = sw.Elapsed;
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Classify,
                Duration = classifyDuration,
                ItemCount = pinned.Count + scoreable.Count
            });
            stageSnapshots!.Add(new StageTraceSnapshot
            {
                Stage = PipelineStage.Classify,
                ItemCountIn = items.Count,
                ItemCountOut = pinned.Count + scoreable.Count,
                Duration = classifyDuration
            });
            sw.Restart();
        }

        // VALIDATE PINNED BUDGET
        var pinnedTokens = 0;
        for (var i = 0; i < pinned.Count; i++)
            pinnedTokens += pinned[i].Tokens;

        var availableForPinned = budget.MaxTokens - budget.OutputReserve - budget.TotalReservedTokens;
        if (pinnedTokens > availableForPinned)
        {
            throw new InvalidOperationException(
                $"Pinned items require {pinnedTokens} tokens, but only {availableForPinned} tokens are available (MaxTokens={budget.MaxTokens} - OutputReserve={budget.OutputReserve} - ReservedSlots={budget.TotalReservedTokens}).");
        }

        // SCORE: score each scoreable item
        var scored = new ScoredItem[scoreable.Count];
        for (var i = 0; i < scoreable.Count; i++)
        {
            scored[i] = new ScoredItem(scoreable[i], _scorer.Score(scoreable[i], scoreable));
        }

        if (sw is not null)
        {
            var scoreDuration = sw.Elapsed;
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Score,
                Duration = scoreDuration,
                ItemCount = scored.Length
            });
            stageSnapshots!.Add(new StageTraceSnapshot
            {
                Stage = PipelineStage.Score,
                ItemCountIn = scoreable.Count,
                ItemCountOut = scored.Length,
                Duration = scoreDuration
            });
            sw.Restart();
        }

        // DEDUPLICATE
        ScoredItem[] deduped;
        if (_deduplicationEnabled && scored.Length > 0)
        {
            var bestByContent = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < scored.Length; i++)
            {
                var content = scored[i].Item.Content;
                if (bestByContent.TryGetValue(content, out var existingIndex))
                {
                    if (scored[i].Score > scored[existingIndex].Score)
                    {
                        bestByContent[content] = i;
                    }
                }
                else
                {
                    bestByContent[content] = i;
                }
            }

            // Collect surviving indices via indexed iteration (zero-allocation discipline)
            deduped = new ScoredItem[bestByContent.Count];
            var dedupIdx = 0;
            for (var i = 0; i < scored.Length; i++)
            {
                if (bestByContent.TryGetValue(scored[i].Item.Content, out var bestIdx) && bestIdx == i)
                {
                    deduped[dedupIdx++] = scored[i];
                }
                else if (reportBuilder is not null)
                {
                    // This item was deduplicated — find the surviving item
                    var survivingIdx = bestByContent[scored[i].Item.Content];
                    reportBuilder.AddExcluded(
                        scored[i].Item,
                        scored[i].Score,
                        ExclusionReason.Deduplicated,
                        deduplicatedAgainst: scored[survivingIdx].Item);
                }
            }
        }
        else
        {
            deduped = scored;
        }

        if (sw is not null)
        {
            var dedupDuration = sw.Elapsed;
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Deduplicate,
                Duration = dedupDuration,
                ItemCount = deduped.Length
            });
            stageSnapshots!.Add(new StageTraceSnapshot
            {
                Stage = PipelineStage.Deduplicate,
                ItemCountIn = scored.Length,
                ItemCountOut = deduped.Length,
                Duration = dedupDuration
            });
            sw.Restart();
        }

        // SORT: stable sort by score descending
        var sortKeys = new (double Score, int Index)[deduped.Length];
        for (var i = 0; i < deduped.Length; i++)
        {
            sortKeys[i] = (deduped[i].Score, i);
        }

        Array.Sort(sortKeys, static (a, b) =>
        {
            var scoreComparison = b.Score.CompareTo(a.Score);
            return scoreComparison != 0 ? scoreComparison : a.Index.CompareTo(b.Index);
        });

        var sorted = new ScoredItem[deduped.Length];
        for (var i = 0; i < sortKeys.Length; i++)
        {
            sorted[i] = deduped[sortKeys[i].Index];
        }

        // SLICE: create adjusted budget and slice
        var effectiveMax = Math.Max(0, budget.MaxTokens - budget.OutputReserve - pinnedTokens - budget.TotalReservedTokens);
        var effectiveTarget = Math.Max(0, budget.TargetTokens - pinnedTokens - budget.TotalReservedTokens);

        if (budget.EstimationSafetyMarginPercent > 0)
        {
            var multiplier = 1.0 - budget.EstimationSafetyMarginPercent / 100.0;
            effectiveMax = (int)(effectiveMax * multiplier);
            effectiveTarget = (int)(effectiveTarget * multiplier);
            effectiveTarget = Math.Min(effectiveTarget, effectiveMax);
        }

        effectiveTarget = Math.Min(effectiveTarget, effectiveMax);

        var adjustedBudget = new ContextBudget(
            maxTokens: effectiveMax,
            targetTokens: effectiveTarget);

        var slicedItems = _slicer.Slice(sorted, adjustedBudget, trace);

        // Wire CountQuotaSlice shortfalls into the report when present.
        // Read LastShortfalls after Slice() returns — it is only populated post-call (D087).
        if (_slicer is CountQuotaSlice countQuotaSlicer
            && reportBuilder is not null
            && countQuotaSlicer.LastShortfalls.Count > 0)
        {
            reportBuilder.SetCountRequirementShortfalls(countQuotaSlicer.LastShortfalls);
        }

        if (sw is not null)
        {
            var sliceDuration = sw.Elapsed;
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Slice,
                Duration = sliceDuration,
                ItemCount = slicedItems.Count
            });
            stageSnapshots!.Add(new StageTraceSnapshot
            {
                Stage = PipelineStage.Slice,
                ItemCountIn = sorted.Length,
                ItemCountOut = slicedItems.Count,
                Duration = sliceDuration
            });
            sw.Restart();
        }

        // Build per-kind count from sliced output for cap-classification in the re-association loop.
        // Only constructed when CountQuotaSlice is active — null otherwise to avoid overhead on hot paths.
        Dictionary<ContextKind, int>? selectedKindCounts = null;
        if (reportBuilder is not null && _slicer is CountQuotaSlice)
        {
            selectedKindCounts = new Dictionary<ContextKind, int>();
            for (var i = 0; i < slicedItems.Count; i++)
            {
                var k = slicedItems[i].Kind;
                selectedKindCounts.TryGetValue(k, out var c);
                selectedKindCounts[k] = c + 1;
            }
        }

        // RE-ASSOCIATE SCORES: match slicer output back to scored items
        var slicedSet = new HashSet<ContextItem>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < slicedItems.Count; i++)
        {
            slicedSet.Add(slicedItems[i]);
        }

        var slicedScored = new List<ScoredItem>();
        for (var i = 0; i < sorted.Length; i++)
        {
            if (slicedSet.Contains(sorted[i].Item))
            {
                slicedScored.Add(sorted[i]);
            }
            else if (reportBuilder is not null)
            {
                // Classify exclusion reason: budget-exceeded wins when item exceeds target tokens;
                // cap-exceeded is used when the item fits the budget but its kind has reached the cap.
                var exclusionReason = ExclusionReason.BudgetExceeded;
                if (selectedKindCounts is not null && _slicer is CountQuotaSlice cqs)
                {
                    var kind = sorted[i].Item.Kind;
                    var entry = cqs.Entries.FirstOrDefault(e => e.Kind == kind);
                    if (entry is not null
                        && sorted[i].Item.Tokens <= adjustedBudget.TargetTokens
                        && selectedKindCounts.TryGetValue(kind, out var kindCount)
                        && kindCount >= entry.CapCount)
                    {
                        exclusionReason = ExclusionReason.CountCapExceeded;
                    }
                }

                reportBuilder.AddExcluded(sorted[i].Item, sorted[i].Score, exclusionReason);
            }
        }

        // MERGE PINNED: add pinned items with effective score 1.0 (highest possible ordinal ranking)
        var merged = new ScoredItem[pinned.Count + slicedScored.Count];
        for (var i = 0; i < pinned.Count; i++)
        {
            merged[i] = new ScoredItem(pinned[i], 1.0);
        }
        for (var i = 0; i < slicedScored.Count; i++)
        {
            merged[pinned.Count + i] = slicedScored[i];
        }

        // PINNED+QUOTA CONFLICT DETECTION
        // Note: This path is only entered when QuotaSlice is configured — not the hot path.
        // Dictionary foreach is acceptable here given the diagnostic-only context.
        if (pinned.Count > 0 && _slicer is QuotaSlice quotaSlicer)
        {
            var pinnedTokensByKind = new Dictionary<ContextKind, int>();
            for (var i = 0; i < pinned.Count; i++)
            {
                var kind = pinned[i].Kind;
                pinnedTokensByKind.TryGetValue(kind, out var current);
                pinnedTokensByKind[kind] = current + pinned[i].Tokens;
            }

            foreach (var kvp in pinnedTokensByKind)
            {
                var capPercent = quotaSlicer.Quotas.GetCap(kvp.Key);
                if (capPercent < 100)
                {
                    var capTokens = (int)(capPercent / 100.0 * budget.TargetTokens);
                    if (kvp.Value > capTokens)
                    {
                        trace.RecordItemEvent(new TraceEvent
                        {
                            Stage = PipelineStage.Slice,
                            Duration = TimeSpan.Zero,
                            ItemCount = 0,
                            Message = $"WARNING: Pinned items of Kind '{kvp.Key}' use {kvp.Value} tokens, exceeding the {capPercent}% Cap ({capTokens} tokens). Pinned items override quotas by design."
                        });
                    }
                }
            }
        }

        // OVERFLOW DETECTION: check if merged tokens exceed TargetTokens
        var mergedTokens = 0;
        for (var i = 0; i < merged.Length; i++)
            mergedTokens += merged[i].Item.Tokens;

        if (mergedTokens > budget.TargetTokens)
        {
            switch (_overflowStrategy)
            {
                case OverflowStrategy.Throw:
                    throw new OverflowException(
                        $"Selected items require {mergedTokens} tokens, exceeding the target budget of {budget.TargetTokens} tokens ({mergedTokens - budget.TargetTokens} tokens over budget).");

                case OverflowStrategy.Truncate:
                    {
                        var hasPinned = pinned.Count > 0;
                        var truncateReason = hasPinned
                            ? ExclusionReason.PinnedOverride
                            : ExclusionReason.BudgetExceeded;

                        // Single-pass: keep items from the front (highest-scored), skip from the back
                        // merged = [pinned items (score 1.0)] + [sliced items (score desc)]
                        // Walk forward, accumulate until budget exhausted, then exclude the rest
                        var kept = new List<ScoredItem>(merged.Length);
                        var currentTokens = 0;
                        for (var i = 0; i < merged.Length; i++)
                        {
                            if (merged[i].Item.Pinned || currentTokens + merged[i].Item.Tokens <= budget.TargetTokens)
                            {
                                kept.Add(merged[i]);
                                currentTokens += merged[i].Item.Tokens;
                            }
                            else
                            {
                                reportBuilder?.AddExcluded(merged[i].Item, merged[i].Score, truncateReason);
                            }
                        }

                        // Handle case where pinned items alone exceed target (best-effort)
                        if (currentTokens > budget.TargetTokens)
                        {
                            trace.RecordItemEvent(new TraceEvent
                            {
                                Stage = PipelineStage.Slice,
                                Duration = TimeSpan.Zero,
                                ItemCount = 0,
                                Message = $"WARNING: After truncation, selected items still exceed TargetTokens ({currentTokens} > {budget.TargetTokens}). Pinned items cannot be removed."
                            });
                        }

                        merged = kept.ToArray();
                        break;
                    }

                case OverflowStrategy.Proceed:
                    {
                        var overflowItems = new ContextItem[merged.Length];
                        for (var i = 0; i < merged.Length; i++)
                            overflowItems[i] = merged[i].Item;

                        _overflowObserver?.Invoke(new OverflowEvent
                        {
                            TokensOverBudget = mergedTokens - budget.TargetTokens,
                            OverflowingItems = overflowItems,
                            Budget = budget
                        });
                        break;
                    }
            }
        }

        // Record included items AFTER overflow handling (merged may have been modified by Truncate)
        if (reportBuilder is not null)
        {
            for (var i = 0; i < merged.Length; i++)
            {
                if (merged[i].Item.Pinned)
                {
                    reportBuilder.AddIncluded(merged[i].Item, merged[i].Score, InclusionReason.Pinned);
                }
                else
                {
                    var reason = merged[i].Item.Tokens == 0
                        ? InclusionReason.ZeroToken
                        : InclusionReason.Scored;
                    reportBuilder.AddIncluded(merged[i].Item, merged[i].Score, reason);
                }
            }
        }

        // PLACE
        var placed = _placer.Place(merged, trace);

        if (sw is not null)
        {
            var placeDuration = sw.Elapsed;
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Place,
                Duration = placeDuration,
                ItemCount = placed.Count
            });
            stageSnapshots!.Add(new StageTraceSnapshot
            {
                Stage = PipelineStage.Place,
                ItemCountIn = merged.Length,
                ItemCountOut = placed.Count,
                Duration = placeDuration
            });
        }

        // BUILD RESULT
        SelectionReport? report = null;
        if (reportBuilder is not null)
        {
            // Use DiagnosticTraceCollector events when available; otherwise provide
            // the stage events that were recorded via RecordStageEvent.
            IReadOnlyList<TraceEvent> events = trace is DiagnosticTraceCollector diagnosticCollector
                ? diagnosticCollector.Events
                : [];

            report = reportBuilder.Build(events);

            // Invoke the structured completion hook for any enabled collector.
            trace.OnPipelineCompleted(report, _budget, stageSnapshots!);
        }

        return new ContextResult { Items = placed, Report = report };
    }

    /// <summary>
    /// Executes the pipeline by first materializing items from the source.
    /// </summary>
    /// <param name="source">The context source to materialize items from.</param>
    /// <param name="traceCollector">Optional trace collector for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pipeline result.</returns>
    public async Task<ContextResult> ExecuteAsync(
        IContextSource source,
        ITraceCollector? traceCollector = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var items = await source.GetItemsAsync(cancellationToken).ConfigureAwait(false);
        return Execute(items, traceCollector);
    }

    /// <summary>
    /// Executes the pipeline on a streaming source using the configured <see cref="IAsyncSlicer"/>.
    /// Items are scored in micro-batches to provide meaningful context for relative scorers.
    /// </summary>
    /// <remarks>
    /// Pinned items are not supported in streaming mode — all items are scored and sliced.
    /// Use the synchronous <see cref="Execute"/> method if pinned item support is required.
    /// </remarks>
    /// <param name="source">The streaming context items to process.</param>
    /// <param name="traceCollector">Optional trace collector for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pipeline result containing selected and ordered items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No async slicer configured.</exception>
    public async Task<ContextResult> ExecuteStreamAsync(
        IAsyncEnumerable<ContextItem> source,
        ITraceCollector? traceCollector = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (_asyncSlicer is null)
        {
            throw new InvalidOperationException(
                "No async slicer configured. Call WithAsyncSlicer() on the builder.");
        }

        var trace = traceCollector ?? NullTraceCollector.Instance;

        // Use 32 as default batch size for scoring alignment. StreamSlice exposes
        // its BatchSize for alignment, but other IAsyncSlicer implementations use default.
        var scoringBatchSize = _asyncSlicer is StreamSlice ss ? ss.BatchSize : 32;
        var scoredStream = ScoreStreamAsync(source, scoringBatchSize, cancellationToken);

        var effectiveMax = Math.Max(0, _budget.MaxTokens - _budget.OutputReserve - _budget.TotalReservedTokens);
        var effectiveTarget = Math.Max(0, _budget.TargetTokens - _budget.TotalReservedTokens);

        if (_budget.EstimationSafetyMarginPercent > 0)
        {
            var multiplier = 1.0 - _budget.EstimationSafetyMarginPercent / 100.0;
            effectiveMax = (int)(effectiveMax * multiplier);
            effectiveTarget = (int)(effectiveTarget * multiplier);
            effectiveTarget = Math.Min(effectiveTarget, effectiveMax);
        }

        effectiveTarget = Math.Min(effectiveTarget, effectiveMax);
        var adjustedBudget = new ContextBudget(
            maxTokens: effectiveMax,
            targetTokens: effectiveTarget);

        var slicedItems = await _asyncSlicer.SliceAsync(
            scoredStream, adjustedBudget, trace, cancellationToken)
            .ConfigureAwait(false);

        // Streaming path: scores are computed during streaming and consumed by the slicer.
        // After slicing, original scores are not available (items passed through IAsyncSlicer
        // which returns ContextItem, not ScoredItem). All items receive equal placement score.
        var scoredForPlacer = new ScoredItem[slicedItems.Count];
        for (var i = 0; i < slicedItems.Count; i++)
        {
            scoredForPlacer[i] = new ScoredItem(slicedItems[i], 0.5);
        }
        var placed = _placer.Place(scoredForPlacer, trace);

        SelectionReport? report = null;
        if (traceCollector is DiagnosticTraceCollector diagnosticCollector)
        {
            report = new SelectionReport
            {
                Events = diagnosticCollector.Events,
                Included = [],
                Excluded = [],
                TotalCandidates = 0,
                TotalTokensConsidered = 0
            };
        }

        return new ContextResult { Items = placed, Report = report };
    }

    /// <summary>
    /// Scores items from a streaming source in micro-batches, providing
    /// meaningful allItems context for relative scorers within each batch.
    /// </summary>
    private async IAsyncEnumerable<ScoredItem> ScoreStreamAsync(
        IAsyncEnumerable<ContextItem> source,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var batch = new List<ContextItem>(batchSize);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                for (var i = 0; i < batch.Count; i++)
                {
                    yield return new ScoredItem(batch[i], _scorer.Score(batch[i], batch));
                }
                batch.Clear();
            }
        }

        // Process remaining items in final partial batch
        if (batch.Count > 0)
        {
            for (var i = 0; i < batch.Count; i++)
            {
                yield return new ScoredItem(batch[i], _scorer.Score(batch[i], batch));
            }
        }
    }
}
