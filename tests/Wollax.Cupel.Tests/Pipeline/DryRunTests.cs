using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Pipeline;

public class DryRunTests
{
    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10,
        double? futureRelevanceHint = null,
        bool pinned = false) =>
        new()
        {
            Content = content,
            Tokens = tokens,
            FutureRelevanceHint = futureRelevanceHint,
            Pinned = pinned,
            Kind = ContextKind.Message
        };

    [Test]
    public async Task DryRun_ReportAlwaysPopulated()
    {
        var item = CreateItem("a", tokens: 10, futureRelevanceHint: 0.5);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var result = pipeline.DryRun([item]);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Included.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DryRun_MatchesExecuteResult()
    {
        var items = new[]
        {
            CreateItem("a", tokens: 30, futureRelevanceHint: 0.9),
            CreateItem("b", tokens: 30, futureRelevanceHint: 0.5),
            CreateItem("c", tokens: 30, futureRelevanceHint: 0.1)
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 60))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var trace = new DiagnosticTraceCollector();
        var executeResult = pipeline.Execute(items, trace);
        var dryRunResult = pipeline.DryRun(items);

        // Same items selected in same order
        await Assert.That(dryRunResult.Items.Count).IsEqualTo(executeResult.Items.Count);
        for (var i = 0; i < dryRunResult.Items.Count; i++)
        {
            await Assert.That(dryRunResult.Items[i]).IsEqualTo(executeResult.Items[i]);
        }
    }

    [Test]
    public async Task DryRun_IsIdempotent()
    {
        var items = new[]
        {
            CreateItem("a", tokens: 20, futureRelevanceHint: 0.9),
            CreateItem("b", tokens: 20, futureRelevanceHint: 0.5)
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var result1 = pipeline.DryRun(items);
        var result2 = pipeline.DryRun(items);

        await Assert.That(result1.Items.Count).IsEqualTo(result2.Items.Count);
        for (var i = 0; i < result1.Items.Count; i++)
        {
            await Assert.That(result1.Items[i]).IsEqualTo(result2.Items[i]);
        }

        await Assert.That(result1.Report).IsNotNull();
        await Assert.That(result2.Report).IsNotNull();
        await Assert.That(result1.Report!.Included.Count).IsEqualTo(result2.Report!.Included.Count);
        await Assert.That(result1.Report.Excluded.Count).IsEqualTo(result2.Report.Excluded.Count);
        await Assert.That(result1.Report.TotalCandidates).IsEqualTo(result2.Report.TotalCandidates);
    }

    [Test]
    public async Task DryRun_ReportContainsIncludedAndExcluded()
    {
        var items = new[]
        {
            CreateItem("a", tokens: 40, futureRelevanceHint: 0.9),
            CreateItem("b", tokens: 40, futureRelevanceHint: 0.5),
            CreateItem("c", tokens: 40, futureRelevanceHint: 0.1)
        };

        // Budget only fits 2 items
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 80))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var result = pipeline.DryRun(items);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Included.Count).IsEqualTo(2);
        await Assert.That(result.Report.Excluded.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task DryRun_WithPinnedItems_WorksCorrectly()
    {
        var pinned = CreateItem("pinned", tokens: 10, pinned: true);
        var normal = CreateItem("normal", tokens: 10, futureRelevanceHint: 0.5);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var result = pipeline.DryRun([pinned, normal]);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Items).Contains(pinned);
        await Assert.That(result.Items).Contains(normal);

        var pinnedIncluded = default(IncludedItem);
        for (var i = 0; i < result.Report!.Included.Count; i++)
        {
            if (ReferenceEquals(result.Report.Included[i].Item, pinned))
            {
                pinnedIncluded = result.Report.Included[i];
                break;
            }
        }
        await Assert.That(pinnedIncluded).IsNotNull();
        await Assert.That(pinnedIncluded!.Reason).IsEqualTo(InclusionReason.Pinned);
    }

    [Test]
    public async Task DryRun_NullItems_ThrowsArgumentNull()
    {
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .Build();

        await Assert.That(() => pipeline.DryRun(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DryRun_AlwaysPopulatesReport_WhileExecuteWithoutTraceDoesNot()
    {
        var item = CreateItem("a", tokens: 10, futureRelevanceHint: 0.5);
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var executeResult = pipeline.Execute([item]); // no trace collector
        var dryRunResult = pipeline.DryRun([item]);

        await Assert.That(executeResult.Report).IsNull();
        await Assert.That(dryRunResult.Report).IsNotNull();
    }
}
