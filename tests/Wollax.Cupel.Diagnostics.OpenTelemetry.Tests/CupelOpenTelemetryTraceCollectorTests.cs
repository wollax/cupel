using System.Diagnostics;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Diagnostics.OpenTelemetry;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Diagnostics.OpenTelemetry.Tests;

public class CupelOpenTelemetryTraceCollectorTests
{
    private static ContextItem CreateItem(string content = "test", int tokens = 10) =>
        new()
        {
            Content = content,
            Tokens = tokens,
            Kind = ContextKind.Message
        };

    [Test]
    public async Task StageOnly_EmitsRootAndFiveStageActivities()
    {
        // Arrange: register ActivityListener to capture all stopped Activities
        var captured = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CupelActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => captured.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

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
}
