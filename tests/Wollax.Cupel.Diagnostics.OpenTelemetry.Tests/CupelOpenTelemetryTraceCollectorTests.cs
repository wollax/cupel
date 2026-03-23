using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Diagnostics.OpenTelemetry;

#pragma warning disable CUPEL001 // Chat preset is experimental
#pragma warning disable CUPEL003 // Rag preset is experimental

namespace Wollax.Cupel.Diagnostics.OpenTelemetry.Tests;

/// <summary>
/// SDK-backed contract tests for the companion OpenTelemetry bridge package.
/// These tests set up a real OpenTelemetry SDK pipeline with an in-memory exporter
/// listening to the canonical "Wollax.Cupel" ActivitySource, then assert the exact
/// hierarchy, attributes, and events the spec requires for each verbosity tier.
/// </summary>
[NotInParallel(nameof(CupelOpenTelemetryTraceCollectorTests))]
public class CupelOpenTelemetryTraceCollectorTests
{
    /// <summary>Canonical ActivitySource name from the spec.</summary>
    private const string CupelSourceName = "Wollax.Cupel";

    private static readonly string[] ExpectedStageNames =
        ["classify", "score", "deduplicate", "slice", "place"];

    private static CupelPipeline BuildPipeline() =>
        CupelPipeline.CreateBuilder()
            .WithPolicy(CupelPresets.Chat())
            .WithBudget(new ContextBudget(maxTokens: 4096, targetTokens: 3072))
            .Build();

    private static IReadOnlyList<ContextItem> SampleItems() =>
    [
        new() { Content = "System prompt", Tokens = 50, Kind = ContextKind.SystemPrompt, Source = ContextSource.Chat, Pinned = true },
        new() { Content = "Alpha", Tokens = 30, Kind = ContextKind.Message, Source = ContextSource.Chat },
        new() { Content = "Beta", Tokens = 40, Kind = ContextKind.Message, Source = ContextSource.Chat },
        new() { Content = "Gamma", Tokens = 25, Kind = ContextKind.Message, Source = ContextSource.Chat },
        new() { Content = "Tool result", Tokens = 20, Kind = ContextKind.ToolOutput, Source = ContextSource.Tool },
    ];

    /// <summary>
    /// Runs a pipeline with the in-memory exporter capturing all "Wollax.Cupel" Activities.
    /// </summary>
    private static List<Activity> RunPipelineAndCapture(
        CupelOpenTelemetryVerbosity verbosity = CupelOpenTelemetryVerbosity.StageOnly,
        CupelPipeline? pipeline = null,
        IReadOnlyList<ContextItem>? items = null)
    {
        var exportedActivities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddCupelInstrumentation()
            .AddInMemoryExporter(exportedActivities)
            .Build()!;

        var collector = new CupelOpenTelemetryTraceCollector(verbosity);
        (pipeline ?? BuildPipeline()).Execute(items ?? SampleItems(), collector);

        tracerProvider.ForceFlush();
        return exportedActivities;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Canonical source name
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ActivitySource_Uses_Canonical_Name()
    {
        var activities = RunPipelineAndCapture();

        await Assert.That(activities.Count).IsGreaterThan(0)
            .Because("The bridge must emit Activities on the 'Wollax.Cupel' source");

        foreach (var activity in activities)
        {
            await Assert.That(activity.Source.Name).IsEqualTo(CupelSourceName);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // 5-Activity hierarchy
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Hierarchy_Has_Root_Pipeline_Activity_And_Five_Stage_Children()
    {
        var activities = RunPipelineAndCapture();

        await Assert.That(activities.Count).IsGreaterThan(0);

        var root = activities.FirstOrDefault(a => a.OperationName == "cupel.pipeline");
        await Assert.That(root).IsNotNull()
            .Because("Must have a 'cupel.pipeline' root Activity");

        var stageActivities = activities
            .Where(a => a.OperationName.StartsWith("cupel.stage.", StringComparison.Ordinal))
            .ToList();

        await Assert.That(stageActivities.Count).IsEqualTo(5)
            .Because("Exactly 5 stage Activities required: classify, score, deduplicate, slice, place");

        var stageNames = stageActivities
            .Select(a => a.OperationName.Replace("cupel.stage.", "", StringComparison.Ordinal))
            .OrderBy(n => n)
            .ToList();

        var expected = ExpectedStageNames.OrderBy(n => n).ToList();
        await Assert.That(stageNames).IsEquivalentTo(expected);

        // All stage activities must be children of the root
        foreach (var stage in stageActivities)
        {
            await Assert.That(stage.ParentId).IsEqualTo(root!.Id);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // StageOnly tier
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public async Task StageOnly_Root_Activity_Has_Budget_And_Verbosity_Attributes()
    {
        var activities = RunPipelineAndCapture();

        await Assert.That(activities.Count).IsGreaterThan(0);

        var root = activities.First(a => a.OperationName == "cupel.pipeline");

        var budgetTag = root.GetTagItem("cupel.budget.max_tokens");
        await Assert.That(budgetTag).IsNotNull()
            .Because("Root must carry cupel.budget.max_tokens");
        await Assert.That((int)budgetTag!).IsEqualTo(4096);

        var verbosityTag = root.GetTagItem("cupel.verbosity");
        await Assert.That(verbosityTag).IsNotNull()
            .Because("Root must carry cupel.verbosity");
    }

    [Test]
    public async Task StageOnly_Stage_Activities_Have_Name_And_ItemCount_Attributes()
    {
        var activities = RunPipelineAndCapture();

        await Assert.That(activities.Count).IsGreaterThan(0);

        var stageActivities = activities
            .Where(a => a.OperationName.StartsWith("cupel.stage.", StringComparison.Ordinal))
            .ToList();

        foreach (var stage in stageActivities)
        {
            var stageName = stage.GetTagItem("cupel.stage.name");
            await Assert.That(stageName).IsNotNull()
                .Because($"Stage '{stage.OperationName}' must carry cupel.stage.name");

            var inCount = stage.GetTagItem("cupel.stage.item_count_in");
            await Assert.That(inCount).IsNotNull()
                .Because($"Stage '{stage.OperationName}' must carry cupel.stage.item_count_in");

            var outCount = stage.GetTagItem("cupel.stage.item_count_out");
            await Assert.That(outCount).IsNotNull()
                .Because($"Stage '{stage.OperationName}' must carry cupel.stage.item_count_out");
        }
    }

    [Test]
    public async Task StageOnly_No_Exclusion_Events_Are_Emitted()
    {
        var activities = RunPipelineAndCapture();

        await Assert.That(activities.Count).IsGreaterThan(0);

        var allEvents = activities.SelectMany(a => a.Events).ToList();
        var exclusionEvents = allEvents.Where(e => e.Name == "cupel.exclusion").ToList();

        await Assert.That(exclusionEvents.Count).IsEqualTo(0)
            .Because("StageOnly tier must not emit cupel.exclusion events");
    }

    // ──────────────────────────────────────────────────────────────────────
    // StageAndExclusions tier
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public async Task StageAndExclusions_Emits_Exclusion_Events_With_Required_Attributes()
    {
        // Use a tight budget to force exclusions
        var pipeline = CupelPipeline.CreateBuilder()
            .WithPolicy(CupelPresets.Chat())
            .WithBudget(new ContextBudget(maxTokens: 100, targetTokens: 70))
            .Build();

        var items = new ContextItem[]
        {
            new() { Content = "A", Tokens = 30, Kind = ContextKind.Message, Source = ContextSource.Chat },
            new() { Content = "B", Tokens = 30, Kind = ContextKind.Message, Source = ContextSource.Chat },
            new() { Content = "C", Tokens = 30, Kind = ContextKind.Message, Source = ContextSource.Chat },
        };

        var activities = RunPipelineAndCapture(
            CupelOpenTelemetryVerbosity.StageAndExclusions,
            pipeline,
            items);

        await Assert.That(activities.Count).IsGreaterThan(0);

        var exclusionEvents = activities
            .SelectMany(a => a.Events)
            .Where(e => e.Name == "cupel.exclusion")
            .ToList();

        await Assert.That(exclusionEvents.Count).IsGreaterThan(0)
            .Because("StageAndExclusions must emit cupel.exclusion events when items are excluded");

        foreach (var evt in exclusionEvents)
        {
            var tags = evt.Tags.ToDictionary(t => t.Key, t => t.Value);

            await Assert.That(tags.ContainsKey("cupel.exclusion.reason")).IsTrue()
                .Because("Each exclusion event must carry cupel.exclusion.reason");
            await Assert.That(tags.ContainsKey("cupel.exclusion.item_kind")).IsTrue()
                .Because("Each exclusion event must carry cupel.exclusion.item_kind");
            await Assert.That(tags.ContainsKey("cupel.exclusion.item_tokens")).IsTrue()
                .Because("Each exclusion event must carry cupel.exclusion.item_tokens");
        }

        // Stage activities with exclusions must have cupel.exclusion.count attribute
        var stagesWithExclusions = activities
            .Where(a => a.OperationName.StartsWith("cupel.stage.", StringComparison.Ordinal))
            .Where(a => a.GetTagItem("cupel.exclusion.count") is not null)
            .ToList();

        await Assert.That(stagesWithExclusions.Count).IsGreaterThan(0)
            .Because("At least one stage must carry cupel.exclusion.count");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Full tier
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Full_Emits_Included_Item_Events_With_Required_Attributes()
    {
        var activities = RunPipelineAndCapture(CupelOpenTelemetryVerbosity.Full);

        await Assert.That(activities.Count).IsGreaterThan(0);

        var includedEvents = activities
            .SelectMany(a => a.Events)
            .Where(e => e.Name == "cupel.item.included")
            .ToList();

        await Assert.That(includedEvents.Count).IsGreaterThan(0)
            .Because("Full tier must emit cupel.item.included events");

        foreach (var evt in includedEvents)
        {
            var tags = evt.Tags.ToDictionary(t => t.Key, t => t.Value);

            await Assert.That(tags.ContainsKey("cupel.item.kind")).IsTrue()
                .Because("Each included-item event must carry cupel.item.kind");
            await Assert.That(tags.ContainsKey("cupel.item.tokens")).IsTrue()
                .Because("Each included-item event must carry cupel.item.tokens");
            await Assert.That(tags.ContainsKey("cupel.item.score")).IsTrue()
                .Because("Each included-item event must carry cupel.item.score");
        }
    }
}
