using System.Diagnostics;

namespace Wollax.Cupel.Diagnostics.OpenTelemetry;

/// <summary>
/// An <see cref="ITraceCollector"/> that emits Cupel pipeline execution data
/// as OpenTelemetry Activities and Events via the canonical
/// <c>Wollax.Cupel</c> <see cref="ActivitySource"/>.
/// </summary>
/// <remarks>
/// <para>
/// This collector does <b>not</b> emit item content or raw metadata values.
/// Only structural information (kind, tokens, score, reason, stage names,
/// budget, and verbosity) is included in trace attributes.
/// </para>
/// <para>
/// Create one instance per pipeline execution. This type is not thread-safe.
/// </para>
/// </remarks>
public sealed class CupelOpenTelemetryTraceCollector : ITraceCollector
{
    /// <summary>
    /// The canonical <see cref="ActivitySource"/> name used by this package.
    /// Register this name with your <c>TracerProviderBuilder</c> via
    /// <see cref="TracerProviderBuilderExtensions.AddCupelInstrumentation"/>
    /// or manually with <c>AddSource("Wollax.Cupel")</c>.
    /// </summary>
    public const string SourceName = "Wollax.Cupel";

    internal static readonly ActivitySource Source = new(SourceName);

    private readonly CupelOpenTelemetryVerbosity _verbosity;
    private Activity? _rootActivity;

    /// <summary>
    /// Initializes a new <see cref="CupelOpenTelemetryTraceCollector"/>.
    /// </summary>
    /// <param name="verbosity">
    /// Controls which events are emitted. See <see cref="CupelOpenTelemetryVerbosity"/> for details.
    /// </param>
    public CupelOpenTelemetryTraceCollector(
        CupelOpenTelemetryVerbosity verbosity = CupelOpenTelemetryVerbosity.StageOnly)
    {
        _verbosity = verbosity;
    }

    /// <inheritdoc />
    public bool IsEnabled => Source.HasListeners();

    /// <inheritdoc />
    public void RecordStageEvent(TraceEvent traceEvent)
    {
        // Stage events are recorded; the actual Activity emission happens in OnPipelineCompleted
        // where we have the structured StageTraceSnapshot data.
    }

    /// <inheritdoc />
    public void RecordItemEvent(TraceEvent traceEvent)
    {
        // Item events are recorded structurally in OnPipelineCompleted via the SelectionReport.
    }

    /// <inheritdoc />
    public void OnPipelineCompleted(
        SelectionReport report,
        ContextBudget budget,
        IReadOnlyList<StageTraceSnapshot> stageSnapshots)
    {
        if (!Source.HasListeners())
            return;

        // Start root pipeline Activity
        _rootActivity = Source.StartActivity("cupel.pipeline", ActivityKind.Internal);
        if (_rootActivity is null)
            return;

        _rootActivity.SetTag("cupel.budget.max_tokens", budget.MaxTokens);
        _rootActivity.SetTag("cupel.budget.target_tokens", budget.TargetTokens);
        _rootActivity.SetTag("cupel.verbosity", _verbosity.ToString());
        _rootActivity.SetTag("cupel.total_candidates", report.TotalCandidates);
        _rootActivity.SetTag("cupel.total_tokens_considered", report.TotalTokensConsidered);
        _rootActivity.SetTag("cupel.included_count", report.Included.Count);
        _rootActivity.SetTag("cupel.excluded_count", report.Excluded.Count);

        // Emit one child Activity per stage
        foreach (var snapshot in stageSnapshots)
        {
            var stageName = snapshot.Stage.ToString().ToLowerInvariant();
            using var stageActivity = Source.StartActivity(
                $"cupel.stage.{stageName}",
                ActivityKind.Internal,
                _rootActivity.Context);

            if (stageActivity is null)
                continue;

            stageActivity.SetTag("cupel.stage.name", stageName);
            stageActivity.SetTag("cupel.stage.item_count_in", snapshot.ItemCountIn);
            stageActivity.SetTag("cupel.stage.item_count_out", snapshot.ItemCountOut);
            stageActivity.SetTag("cupel.stage.duration_ms", snapshot.Duration.TotalMilliseconds);

            // StageAndExclusions: emit exclusion events on the relevant stage
            if (_verbosity >= CupelOpenTelemetryVerbosity.StageAndExclusions)
            {
                EmitExclusionEvents(stageActivity, snapshot.Stage, report);
            }

            // Full: emit included item events on the place stage (final selection)
            if (_verbosity >= CupelOpenTelemetryVerbosity.Full
                && snapshot.Stage == PipelineStage.Place)
            {
                EmitIncludedItemEvents(stageActivity, report);
            }
        }

        _rootActivity.Stop();
        _rootActivity = null;
    }

    private static void EmitExclusionEvents(
        Activity stageActivity,
        PipelineStage stage,
        SelectionReport report)
    {
        // Exclusion events are emitted on the slice stage (where budget decisions happen)
        // and deduplicate stage (where dedup decisions happen).
        var exclusionsForStage = GetExclusionsForStage(stage, report.Excluded);
        if (exclusionsForStage.Count == 0)
            return;

        stageActivity.SetTag("cupel.exclusion.count", exclusionsForStage.Count);

        foreach (var excluded in exclusionsForStage)
        {
            var tags = new ActivityTagsCollection
            {
                { "cupel.exclusion.reason", excluded.Reason.ToString() },
                { "cupel.exclusion.item_kind", excluded.Item.Kind.ToString() },
                { "cupel.exclusion.item_tokens", excluded.Item.Tokens }
            };
            stageActivity.AddEvent(new ActivityEvent("cupel.exclusion", tags: tags));
        }
    }

    private static void EmitIncludedItemEvents(
        Activity stageActivity,
        SelectionReport report)
    {
        foreach (var included in report.Included)
        {
            var tags = new ActivityTagsCollection
            {
                { "cupel.item.kind", included.Item.Kind.ToString() },
                { "cupel.item.tokens", included.Item.Tokens },
                { "cupel.item.score", included.Score },
                { "cupel.item.reason", included.Reason.ToString() }
            };
            stageActivity.AddEvent(new ActivityEvent("cupel.item.included", tags: tags));
        }
    }

    private static IReadOnlyList<ExcludedItem> GetExclusionsForStage(
        PipelineStage stage,
        IReadOnlyList<ExcludedItem> allExcluded)
    {
        return stage switch
        {
            PipelineStage.Deduplicate => allExcluded
                .Where(e => e.Reason == ExclusionReason.Deduplicated)
                .ToList(),
            PipelineStage.Slice => allExcluded
                .Where(e => e.Reason is ExclusionReason.BudgetExceeded
                    or ExclusionReason.PinnedOverride
                    or ExclusionReason.NegativeTokens
                    or ExclusionReason.CountCapExceeded
                    or ExclusionReason.CountRequireCandidatesExhausted
                    or ExclusionReason.QuotaCapExceeded
                    or ExclusionReason.QuotaRequireDisplaced)
                .ToList(),
            PipelineStage.Score => allExcluded
                .Where(e => e.Reason == ExclusionReason.ScoredTooLow)
                .ToList(),
            PipelineStage.Classify => allExcluded
                .Where(e => e.Reason == ExclusionReason.Filtered)
                .ToList(),
            _ => []
        };
    }
}
