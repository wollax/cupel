using System.Diagnostics;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Diagnostics.OpenTelemetry;

/// <summary>
/// An <see cref="ITraceCollector"/> that emits OpenTelemetry-compatible
/// <see cref="Activity"/> objects via <see cref="CupelActivitySource"/>.
/// </summary>
/// <remarks>
/// Call <see cref="Complete"/> after <c>pipeline.DryRun</c> or <c>pipeline.Execute</c>
/// to flush the buffered stage data as Activities. Activities are only created when
/// an <see cref="ActivityListener"/> is registered for <see cref="CupelActivitySource.SourceName"/>.
/// </remarks>
public sealed class CupelOpenTelemetryTraceCollector : ITraceCollector, IDisposable
{
    private readonly CupelVerbosity _verbosity;
    private readonly List<(PipelineStage Stage, TimeSpan Duration, int ItemCountOut)> _stages = [];

    /// <summary>Initializes a new trace collector with the specified verbosity tier.</summary>
    /// <param name="verbosity">Controls how much detail is emitted per pipeline run.</param>
    public CupelOpenTelemetryTraceCollector(CupelVerbosity verbosity = CupelVerbosity.StageOnly)
    {
        _verbosity = verbosity;
    }

    /// <inheritdoc/>
    public bool IsEnabled => true;

    /// <inheritdoc/>
    public void RecordStageEvent(TraceEvent traceEvent)
    {
        _stages.Add((traceEvent.Stage, traceEvent.Duration, traceEvent.ItemCount));
    }

    /// <inheritdoc/>
    public void RecordItemEvent(TraceEvent traceEvent)
    {
        // No-op: item-level detail is sourced from SelectionReport in Complete().
    }

    /// <summary>
    /// Maps an <see cref="ExclusionReason"/> to the <see cref="PipelineStage"/> that produced it.
    /// </summary>
    private static PipelineStage MapReasonToStage(ExclusionReason reason) => reason switch
    {
        ExclusionReason.NegativeTokens => PipelineStage.Classify,
        ExclusionReason.Filtered => PipelineStage.Classify,
        ExclusionReason.ScoredTooLow => PipelineStage.Score,
        ExclusionReason.Deduplicated => PipelineStage.Deduplicate,
        _ => PipelineStage.Slice  // BudgetExceeded, PinnedOverride, CountCapExceeded, QuotaCapExceeded, QuotaRequireDisplaced, CountRequireCandidatesExhausted
    };

    /// <summary>
    /// Creates the root <c>cupel.pipeline</c> Activity and one child Activity per stage,
    /// then stops them all. Call once after the pipeline run completes.
    /// </summary>
    /// <param name="report">The selection report; may be null if the pipeline produced no report.</param>
    /// <param name="budget">The budget used for the pipeline run.</param>
    public void Complete(SelectionReport? report, ContextBudget budget)
    {
        if (_stages.Count == 0)
            return;

        var rootEnd = DateTimeOffset.UtcNow;
        var totalDuration = _stages.Aggregate(TimeSpan.Zero, (acc, s) => acc + s.Duration);
        var rootStart = rootEnd - totalDuration;

        var rootActivity = CupelActivitySource.Source.StartActivity(
            "cupel.pipeline",
            ActivityKind.Internal,
            default(ActivityContext),
            startTime: rootStart);

        rootActivity?.SetTag("cupel.budget.max_tokens", budget.MaxTokens);
        rootActivity?.SetTag("cupel.verbosity", _verbosity.ToString());

        var offset = TimeSpan.Zero;
        int? previousItemCountOut = null;

        for (var i = 0; i < _stages.Count; i++)
        {
            var stageData = _stages[i];
            var stageStart = rootStart + offset;
            var stageName = stageData.Stage.ToString().ToLowerInvariant();

            var stageActivity = CupelActivitySource.Source.StartActivity(
                $"cupel.stage.{stageName}",
                ActivityKind.Internal,
                rootActivity?.Context ?? default(ActivityContext),
                startTime: stageStart);

            stageActivity?.SetTag("cupel.stage.name", stageName);
            stageActivity?.SetTag("cupel.stage.item_count_out", stageData.ItemCountOut);

            // item_count_in: for Classify (first stage) use TotalCandidates from report;
            // for all other stages use the previous stage's ItemCountOut.
            int itemCountIn;
            if (stageData.Stage == PipelineStage.Classify)
            {
                itemCountIn = report?.TotalCandidates ?? stageData.ItemCountOut;
            }
            else
            {
                itemCountIn = previousItemCountOut ?? stageData.ItemCountOut;
            }

            stageActivity?.SetTag("cupel.stage.item_count_in", itemCountIn);

            // StageAndExclusions tier: add exclusion count tag and per-exclusion events
            if (_verbosity >= CupelVerbosity.StageAndExclusions && report != null)
            {
                var stage = stageData.Stage;
                var stageExclusions = report.Excluded
                    .Where(e => MapReasonToStage(e.Reason) == stage)
                    .ToList();

                stageActivity?.SetTag("cupel.exclusion.count", stageExclusions.Count);

                foreach (var excluded in stageExclusions)
                {
                    stageActivity?.AddEvent(new ActivityEvent(
                        "cupel.exclusion",
                        tags: new ActivityTagsCollection
                        {
                            ["cupel.exclusion.reason"] = excluded.Reason.ToString(),
                            ["cupel.exclusion.item_kind"] = excluded.Item.Kind.ToString(),
                            ["cupel.exclusion.item_tokens"] = excluded.Item.Tokens
                        }));
                }
            }

            // Full tier: add per-included-item events on the Place stage
            if (_verbosity >= CupelVerbosity.Full && report != null && stageData.Stage == PipelineStage.Place)
            {
                foreach (var included in report.Included)
                {
                    stageActivity?.AddEvent(new ActivityEvent(
                        "cupel.item.included",
                        tags: new ActivityTagsCollection
                        {
                            ["cupel.item.kind"] = included.Item.Kind.ToString(),
                            ["cupel.item.tokens"] = included.Item.Tokens,
                            ["cupel.item.score"] = included.Score
                        }));
                }
            }

            stageActivity?.SetEndTime((stageStart + stageData.Duration).UtcDateTime);
            stageActivity?.Stop();

            previousItemCountOut = stageData.ItemCountOut;
            offset += stageData.Duration;
        }

        rootActivity?.SetEndTime(rootEnd.UtcDateTime);
        rootActivity?.Stop();

        _stages.Clear();
    }

    /// <summary>
    /// No-op. The underlying <see cref="CupelActivitySource.Source"/> is a process-wide
    /// static singleton and must not be disposed by individual collector instances.
    /// This method exists solely to satisfy <see cref="IDisposable"/> so callers can
    /// use <c>using</c> patterns without risk.
    /// </summary>
    public void Dispose()
    {
        // Intentionally empty. See CupelActivitySource for lifecycle notes.
    }
}
