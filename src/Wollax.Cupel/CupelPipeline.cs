using System.Diagnostics;
using Wollax.Cupel.Diagnostics;

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

    internal CupelPipeline(
        IScorer scorer,
        ISlicer slicer,
        IPlacer placer,
        ContextBudget budget,
        bool deduplicationEnabled)
    {
        _scorer = scorer;
        _slicer = slicer;
        _placer = placer;
        _budget = budget;
        _deduplicationEnabled = deduplicationEnabled;
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
    public ContextResult Execute(
        IReadOnlyList<ContextItem> items,
        ITraceCollector? traceCollector = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var trace = traceCollector ?? NullTraceCollector.Instance;
        var sw = trace.IsEnabled ? Stopwatch.StartNew() : null;

        // CLASSIFY: partition into pinned and scoreable, skip negative tokens
        var pinned = new List<ContextItem>();
        var scoreable = new List<ContextItem>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Tokens < 0)
            {
                continue;
            }

            if (item.Pinned)
            {
                pinned.Add(item);
            }
            else
            {
                scoreable.Add(item);
            }
        }

        if (sw is not null)
        {
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Classify,
                Duration = sw.Elapsed,
                ItemCount = pinned.Count + scoreable.Count
            });
            sw.Restart();
        }

        // VALIDATE PINNED BUDGET
        var pinnedTokens = 0;
        for (var i = 0; i < pinned.Count; i++)
            pinnedTokens += pinned[i].Tokens;

        var availableForPinned = _budget.MaxTokens - _budget.OutputReserve;
        if (pinnedTokens > availableForPinned)
        {
            throw new InvalidOperationException(
                $"Pinned items require {pinnedTokens} tokens, but only {availableForPinned} tokens are available (MaxTokens={_budget.MaxTokens} - OutputReserve={_budget.OutputReserve}).");
        }

        // SCORE: score each scoreable item
        var scored = new ScoredItem[scoreable.Count];
        for (var i = 0; i < scoreable.Count; i++)
        {
            scored[i] = new ScoredItem(scoreable[i], _scorer.Score(scoreable[i], scoreable));
        }

        if (sw is not null)
        {
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Score,
                Duration = sw.Elapsed,
                ItemCount = scored.Length
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
            }
        }
        else
        {
            deduped = scored;
        }

        if (sw is not null)
        {
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Deduplicate,
                Duration = sw.Elapsed,
                ItemCount = deduped.Length
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
        var effectiveMax = Math.Max(0, _budget.MaxTokens - _budget.OutputReserve - pinnedTokens);
        var effectiveTarget = Math.Max(0, _budget.TargetTokens - pinnedTokens);
        effectiveTarget = Math.Min(effectiveTarget, effectiveMax);

        var adjustedBudget = new ContextBudget(
            maxTokens: effectiveMax,
            targetTokens: effectiveTarget);

        var slicedItems = _slicer.Slice(sorted, adjustedBudget, trace);

        if (sw is not null)
        {
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Slice,
                Duration = sw.Elapsed,
                ItemCount = slicedItems.Count
            });
            sw.Restart();
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

        // PLACE
        var placed = _placer.Place(merged, trace);

        if (sw is not null)
        {
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Place,
                Duration = sw.Elapsed,
                ItemCount = placed.Count
            });
        }

        // BUILD RESULT
        SelectionReport? report = null;
        if (traceCollector is DiagnosticTraceCollector diagnosticCollector)
        {
            report = new SelectionReport { Events = diagnosticCollector.Events };
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
}
