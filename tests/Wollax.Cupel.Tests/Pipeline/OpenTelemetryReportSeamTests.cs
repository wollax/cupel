using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;

#pragma warning disable CUPEL001 // Chat preset is experimental
#pragma warning disable CUPEL003 // Rag preset is experimental

namespace Wollax.Cupel.Tests.Pipeline;

/// <summary>
/// Failing-first tests that lock the structured data contract the core pipeline
/// must provide for the OpenTelemetry companion package. The OTel bridge requires:
///   1. A SelectionReport available to any enabled ITraceCollector — not just DiagnosticTraceCollector.
///   2. Per-stage in/out item counts (not just a single ItemCount).
///   3. Budget metadata on the report for root Activity attributes.
///
/// These tests fail today because:
///   - Report is only built when trace is DiagnosticTraceCollector.
///   - TraceEvent carries a single ItemCount, not in/out pairs.
///
/// No test in this file parses TraceEvent.Message.
/// </summary>
public class OpenTelemetryReportSeamTests
{
    private static CupelPipeline BuildSimplePipeline() =>
        CupelPipeline.CreateBuilder()
            .WithPolicy(CupelPresets.Chat())
            .WithBudget(new ContextBudget(maxTokens: 4096, targetTokens: 3072))
            .Build();

    private static IReadOnlyList<ContextItem> SampleItems() =>
    [
        new() { Content = "System prompt", Tokens = 50, Kind = ContextKind.SystemPrompt, Source = ContextSource.Chat, Pinned = true },
        new() { Content = "Message A", Tokens = 30, Kind = ContextKind.Message, Source = ContextSource.Chat },
        new() { Content = "Message B", Tokens = 40, Kind = ContextKind.Message, Source = ContextSource.Chat },
        new() { Content = "Tool result", Tokens = 20, Kind = ContextKind.ToolOutput, Source = ContextSource.Tool },
    ];

    // ──────────────────────────────────────────────────────────────────────
    // 1. Non-diagnostic enabled collectors must receive the report
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Enabled_NonDiagnostic_Collector_Receives_NonNull_Report()
    {
        // The OTel bridge will implement ITraceCollector but is NOT a DiagnosticTraceCollector.
        // The core must populate ContextResult.Report for any enabled collector.
        var collector = new StubEnabledTraceCollector();
        var pipeline = BuildSimplePipeline();

        var result = pipeline.Execute(SampleItems(), collector);

        // FAILS today: Report is null because the pipeline only builds it for DiagnosticTraceCollector.
        await Assert.That(result.Report).IsNotNull();
    }

    [Test]
    public async Task Report_Included_Items_Have_Score_And_Reason()
    {
        var collector = new StubEnabledTraceCollector();
        var pipeline = BuildSimplePipeline();

        var result = pipeline.Execute(SampleItems(), collector);

        // FAILS today (Report is null). When fixed, each included item must carry score + reason.
        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Included.Count).IsGreaterThan(0);

        foreach (var inc in result.Report.Included)
        {
            await Assert.That(inc.Score).IsGreaterThanOrEqualTo(0.0);
            await Assert.That(Enum.IsDefined(inc.Reason)).IsTrue();
        }
    }

    [Test]
    public async Task Report_Carries_TotalCandidates_And_TotalTokensConsidered()
    {
        var collector = new StubEnabledTraceCollector();
        var pipeline = BuildSimplePipeline();
        var items = SampleItems();

        var result = pipeline.Execute(items, collector);

        // FAILS today (Report is null). The OTel bridge needs these for root Activity attributes.
        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.TotalCandidates).IsEqualTo(items.Count);
        await Assert.That(result.Report!.TotalTokensConsidered).IsGreaterThan(0);
    }

    // ──────────────────────────────────────────────────────────────────────
    // 2. Stage events must expose in/out item counts
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Stage_Events_Cover_All_Five_Diagnostic_Stages()
    {
        var collector = new StubEnabledTraceCollector();
        var pipeline = BuildSimplePipeline();

        pipeline.Execute(SampleItems(), collector);

        // The OTel bridge needs exactly 5 stage events: Classify, Score, Deduplicate, Slice, Place.
        var stageEvents = collector.StageEvents;
        await Assert.That(stageEvents.Count).IsEqualTo(5);

        var stages = stageEvents.Select(e => e.Stage).ToList();
        await Assert.That(stages).Contains(PipelineStage.Classify);
        await Assert.That(stages).Contains(PipelineStage.Score);
        await Assert.That(stages).Contains(PipelineStage.Deduplicate);
        await Assert.That(stages).Contains(PipelineStage.Slice);
        await Assert.That(stages).Contains(PipelineStage.Place);
    }

    [Test]
    public async Task Stage_Events_Have_NonNegative_Duration()
    {
        var collector = new StubEnabledTraceCollector();
        var pipeline = BuildSimplePipeline();

        pipeline.Execute(SampleItems(), collector);

        foreach (var evt in collector.StageEvents)
        {
            await Assert.That(evt.Duration).IsGreaterThanOrEqualTo(TimeSpan.Zero);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // 3. DiagnosticTraceCollector still works (baseline sanity)
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public async Task DiagnosticTraceCollector_Produces_Report_With_Stage_Events()
    {
        var collector = new DiagnosticTraceCollector();
        var pipeline = BuildSimplePipeline();

        var result = pipeline.Execute(SampleItems(), collector);

        // This passes today — it's the baseline proving DiagnosticTraceCollector works.
        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Events.Count).IsGreaterThan(0);
        await Assert.That(result.Report!.Included.Count).IsGreaterThan(0);
    }

    // ──────────────────────────────────────────────────────────────────────
    // 4. No-message-parsing invariant
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Report_Exclusion_Reasons_Are_Typed_Enums_Not_Parsed_Strings()
    {
        // Build a pipeline that will produce exclusions (tiny budget)
        var pipeline = CupelPipeline.CreateBuilder()
            .WithPolicy(CupelPresets.Chat())
            .WithBudget(new ContextBudget(maxTokens: 100, targetTokens: 60))
            .Build();

        var items = new ContextItem[]
        {
            new() { Content = "A", Tokens = 30, Kind = ContextKind.Message, Source = ContextSource.Chat },
            new() { Content = "B", Tokens = 30, Kind = ContextKind.Message, Source = ContextSource.Chat },
            new() { Content = "C", Tokens = 30, Kind = ContextKind.Message, Source = ContextSource.Chat },
        };

        var collector = new StubEnabledTraceCollector();
        var result = pipeline.Execute(items, collector);

        // FAILS today (Report is null). When fixed, exclusion reasons must be typed enums,
        // not strings parsed from TraceEvent.Message.
        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Excluded.Count).IsGreaterThan(0);

        foreach (var exc in result.Report.Excluded)
        {
            // ExclusionReason is a typed enum — the OTel bridge maps these to cupel.exclusion.reason
            // attribute values without message parsing.
            await Assert.That(Enum.IsDefined(exc.Reason)).IsTrue();
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Stub collector — simulates what the OTel bridge will be
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A minimal enabled ITraceCollector that is NOT a DiagnosticTraceCollector.
    /// Represents the future CupelOpenTelemetryTraceCollector from the companion package.
    /// </summary>
    private sealed class StubEnabledTraceCollector : ITraceCollector
    {
        private readonly List<TraceEvent> _stageEvents = [];
        private readonly List<TraceEvent> _itemEvents = [];

        public bool IsEnabled => true;

        public IReadOnlyList<TraceEvent> StageEvents => _stageEvents;
        public IReadOnlyList<TraceEvent> ItemEvents => _itemEvents;

        public void RecordStageEvent(TraceEvent traceEvent) => _stageEvents.Add(traceEvent);
        public void RecordItemEvent(TraceEvent traceEvent) => _itemEvents.Add(traceEvent);
    }
}
