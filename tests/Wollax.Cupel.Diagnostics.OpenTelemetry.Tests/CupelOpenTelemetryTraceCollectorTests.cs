using System.Diagnostics;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Diagnostics.OpenTelemetry;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Diagnostics.OpenTelemetry.Tests;

/// <summary>
/// Tests run NotInParallel because ActivityListeners are global and tests would
/// otherwise cross-contaminate each other's captured Activity lists.
/// </summary>
[NotInParallel]
public class CupelOpenTelemetryTraceCollectorTests
{
    private static ContextItem CreateItem(string content = "test", int tokens = 10) =>
        new()
        {
            Content = content,
            Tokens = tokens,
            Kind = ContextKind.Message
        };

    private static ActivityListener CreateListener(List<Activity> captured)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CupelActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => captured.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Test]
    public async Task StageOnly_EmitsRootAndFiveStageActivities()
    {
        // Arrange: register ActivityListener to capture all stopped Activities
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var budget = new ContextBudget(1000, 500);
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(budget)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var items = new[]
        {
            CreateItem("item1", tokens: 50),
            CreateItem("item2", tokens: 60),
            CreateItem("item3", tokens: 70),
        };

        var tracer = new CupelOpenTelemetryTraceCollector(CupelVerbosity.StageOnly);

        // Execute with the OTel tracer to buffer stage events, then DryRun for the report
        pipeline.Execute(items, tracer);
        var dryResult = pipeline.DryRun(items);

        // Act: flush stage data as Activities
        tracer.Complete(dryResult.Report!, budget);

        // Assert: one root Activity
        var root = captured.FirstOrDefault(a => a.OperationName == "cupel.pipeline");
        await Assert.That(root).IsNotNull();

        // Assert: cupel.verbosity tag on root
        await Assert.That(root!.GetTagItem("cupel.verbosity")?.ToString()).IsEqualTo("StageOnly");

        // Assert: cupel.budget.max_tokens tag on root
        await Assert.That(root.GetTagItem("cupel.budget.max_tokens")).IsNotNull();

        // Assert: exactly 5 stage Activities
        var stageNames = new[]
        {
            "cupel.stage.classify",
            "cupel.stage.score",
            "cupel.stage.deduplicate",
            "cupel.stage.slice",
            "cupel.stage.place"
        };

        foreach (var stageName in stageNames)
        {
            var stageActivity = captured.FirstOrDefault(a => a.OperationName == stageName);
            await Assert.That(stageActivity).IsNotNull();
        }

        var stageActivities = captured.Where(a => a.OperationName.StartsWith("cupel.stage.", StringComparison.Ordinal)).ToList();
        await Assert.That(stageActivities.Count).IsEqualTo(5);

        // Assert: classify stage has correct cupel.stage.name tag
        var classifyActivity = captured.First(a => a.OperationName == "cupel.stage.classify");
        await Assert.That(classifyActivity.GetTagItem("cupel.stage.name")?.ToString()).IsEqualTo("classify");
    }

    [Test]
    public async Task StageAndExclusions_EmitsExclusionCountAndEvents()
    {
        // Arrange: very tight budget so items get excluded (BudgetExceeded → Slice stage)
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        // Budget: only 50 tokens fit, items are 40+40+40 = one fits, two are excluded
        var budget = new ContextBudget(50, 25);
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(budget)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var items = new[]
        {
            CreateItem("item1", tokens: 40),
            CreateItem("item2", tokens: 40),
            CreateItem("item3", tokens: 40),
        };

        var tracer = new CupelOpenTelemetryTraceCollector(CupelVerbosity.StageAndExclusions);

        pipeline.Execute(items, tracer);
        var dryResult = pipeline.DryRun(items);

        // Act
        tracer.Complete(dryResult.Report!, budget);

        // Assert: root exists
        var root = captured.FirstOrDefault(a => a.OperationName == "cupel.pipeline");
        await Assert.That(root).IsNotNull();

        // Assert: at least one stage Activity has cupel.exclusion.count >= 1
        var stageActivities = captured
            .Where(a => a.OperationName.StartsWith("cupel.stage.", StringComparison.Ordinal))
            .ToList();

        var hasExclusionCount = stageActivities.Any(a =>
        {
            var tag = a.GetTagItem("cupel.exclusion.count");
            return tag is int count && count >= 1;
        });
        await Assert.That(hasExclusionCount).IsTrue();

        // Assert: at least one Activity has at least one cupel.exclusion event with cupel.exclusion.reason
        var hasExclusionEvent = stageActivities.Any(a =>
            a.Events.Any(e =>
                e.Name == "cupel.exclusion" &&
                e.Tags.Any(t => t.Key == "cupel.exclusion.reason")));
        await Assert.That(hasExclusionEvent).IsTrue();

        // Assert: the exclusion reason is a known ExclusionReason name
        var exclusionEvent = stageActivities
            .SelectMany(a => a.Events)
            .FirstOrDefault(e => e.Name == "cupel.exclusion");
        var reasonTag = exclusionEvent.Tags.FirstOrDefault(t => t.Key == "cupel.exclusion.reason");
        await Assert.That(reasonTag.Value?.ToString()).IsNotNull();
        var reasonName = reasonTag.Value!.ToString()!;
        await Assert.That(Enum.TryParse<ExclusionReason>(reasonName, out _)).IsTrue();
    }

    [Test]
    public async Task Full_EmitsIncludedItemEventsOnPlaceStage()
    {
        // Arrange: generous budget so items get included
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var budget = new ContextBudget(1000, 500);
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(budget)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var items = new[]
        {
            CreateItem("item1", tokens: 50),
            CreateItem("item2", tokens: 60),
            CreateItem("item3", tokens: 70),
        };

        var tracer = new CupelOpenTelemetryTraceCollector(CupelVerbosity.Full);

        pipeline.Execute(items, tracer);
        var dryResult = pipeline.DryRun(items);

        // Act
        tracer.Complete(dryResult.Report!, budget);

        // Assert: Place stage Activity has at least one cupel.item.included event
        var placeActivity = captured.FirstOrDefault(a => a.OperationName == "cupel.stage.place");
        await Assert.That(placeActivity).IsNotNull();

        var includedEvents = placeActivity!.Events
            .Where(e => e.Name == "cupel.item.included")
            .ToList();
        await Assert.That(includedEvents.Count).IsGreaterThan(0);

        // Assert: cupel.item.score is a double value
        var firstEvent = includedEvents.First();
        var scoreTag = firstEvent.Tags.FirstOrDefault(t => t.Key == "cupel.item.score");
        await Assert.That(scoreTag.Value).IsNotNull();

        // Score is stored as double (object-boxed)
        var scoreIsDouble = scoreTag.Value is double;
        await Assert.That(scoreIsDouble).IsTrue();
    }

    [Test]
    public async Task NullReport_WithFullVerbosity_NoExceptionAndStageActivitiesExist()
    {
        // Arrange: register listener
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var budget = new ContextBudget(1000, 500);
        var tracer = new CupelOpenTelemetryTraceCollector(CupelVerbosity.Full);

        // Manually record 5 dummy stage events (simulating pipeline execution without a report)
        var stages = new[]
        {
            PipelineStage.Classify,
            PipelineStage.Score,
            PipelineStage.Deduplicate,
            PipelineStage.Slice,
            PipelineStage.Place
        };

        foreach (var stage in stages)
        {
            tracer.RecordStageEvent(new TraceEvent
            {
                Stage = stage,
                Duration = TimeSpan.FromMilliseconds(10),
                ItemCount = 3
            });
        }

        // Act: Complete with null report — must not throw
        tracer.Complete(null, budget);

        // Assert: root Activity exists
        var root = captured.FirstOrDefault(a => a.OperationName == "cupel.pipeline");
        await Assert.That(root).IsNotNull();

        // Assert: 5 stage Activities exist
        var stageActivities = captured
            .Where(a => a.OperationName.StartsWith("cupel.stage.", StringComparison.Ordinal))
            .ToList();
        await Assert.That(stageActivities.Count).IsEqualTo(5);

        // Assert: no cupel.item.included events (report is null, so no per-item events)
        var hasIncludedEvents = stageActivities.Any(a =>
            a.Events.Any(e => e.Name == "cupel.item.included"));
        await Assert.That(hasIncludedEvents).IsFalse();

        // Assert: no cupel.exclusion events (report is null)
        var hasExclusionEvents = stageActivities.Any(a =>
            a.Events.Any(e => e.Name == "cupel.exclusion"));
        await Assert.That(hasExclusionEvents).IsFalse();
    }
}
