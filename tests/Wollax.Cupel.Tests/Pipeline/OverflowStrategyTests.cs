using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Pipeline;

public class OverflowStrategyTests
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
    public async Task DefaultStrategy_IsThrow()
    {
        // Pipeline built without WithOverflowStrategy should default to Throw.
        // Create overflow scenario: target=50, but items total 100 tokens.
        var item1 = CreateItem("a", tokens: 60, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 60, futureRelevanceHint: 0.8);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 50))
            .WithScorer(new ReflexiveScorer())
            .Build();

        // GreedySlice respects target, so this may not overflow.
        // Use a slicer that passes everything through to force overflow.
        var passAllSlicer = new PassAllSlicer();
        var pipelineForced = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 50))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(passAllSlicer)
            .Build();

        await Assert.That(() => pipelineForced.Execute([item1, item2]))
            .Throws<OverflowException>();
    }

    [Test]
    public async Task Throw_WhenOverflow_ThrowsOverflowException()
    {
        var item1 = CreateItem("a", tokens: 60, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 60, futureRelevanceHint: 0.8);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 50))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Throw)
            .Build();

        await Assert.That(() => pipeline.Execute([item1, item2]))
            .Throws<OverflowException>();
    }

    [Test]
    public async Task Throw_MessageContainsTokenCounts()
    {
        var item1 = CreateItem("a", tokens: 60, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 60, futureRelevanceHint: 0.8);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 50))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Throw)
            .Build();

        OverflowException? caught = null;
        try
        {
            pipeline.Execute([item1, item2]);
        }
        catch (OverflowException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Message).Contains("120");
        await Assert.That(caught.Message).Contains("50");
    }

    [Test]
    public async Task Truncate_RemovesLowestScoredToFitBudget()
    {
        // Target = 100, items total 150 (3 items x 50 tokens)
        var high = CreateItem("high", tokens: 50, futureRelevanceHint: 0.9);
        var mid = CreateItem("mid", tokens: 50, futureRelevanceHint: 0.5);
        var low = CreateItem("low", tokens: 50, futureRelevanceHint: 0.1);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Truncate)
            .Build();

        var result = pipeline.Execute([high, mid, low]);

        // Should keep high and mid (100 tokens), remove low
        await Assert.That(result.Items).Contains(high);
        await Assert.That(result.Items).Contains(mid);
        await Assert.That(result.Items.Contains(low)).IsFalse();
        await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(100);
    }

    [Test]
    public async Task Truncate_WithPinnedItems_MarksExcludedAsPinnedOverride()
    {
        // Pinned takes 60 tokens, sliced item takes 60, target = 100
        // After merge: 120 tokens > 100 target → overflow
        // Truncate should remove the sliced item with PinnedOverride reason
        var pinned = CreateItem("pinned", tokens: 60, pinned: true);
        var sliced = CreateItem("sliced", tokens: 60, futureRelevanceHint: 0.5);
        var trace = new DiagnosticTraceCollector();

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Truncate)
            .Build();

        var result = pipeline.Execute([pinned, sliced], trace);

        await Assert.That(result.Items).Contains(pinned);
        await Assert.That(result.Items.Contains(sliced)).IsFalse();
        await Assert.That(result.Report).IsNotNull();

        var pinnedOverrideFound = false;
        for (var i = 0; i < result.Report!.Excluded.Count; i++)
        {
            if (result.Report.Excluded[i].Reason == ExclusionReason.PinnedOverride)
            {
                pinnedOverrideFound = true;
                break;
            }
        }
        await Assert.That(pinnedOverrideFound).IsTrue();
    }

    [Test]
    public async Task Truncate_WithoutPinnedItems_MarksExcludedAsBudgetExceeded()
    {
        // All sliced items, no pinned, overflow purely from sliced items
        var high = CreateItem("high", tokens: 60, futureRelevanceHint: 0.9);
        var low = CreateItem("low", tokens: 60, futureRelevanceHint: 0.1);
        var trace = new DiagnosticTraceCollector();

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Truncate)
            .Build();

        var result = pipeline.Execute([high, low], trace);

        await Assert.That(result.Report).IsNotNull();

        // The excluded item should have BudgetExceeded reason (no pinned involved)
        var budgetExceededFound = false;
        for (var i = 0; i < result.Report!.Excluded.Count; i++)
        {
            if (result.Report.Excluded[i].Reason == ExclusionReason.BudgetExceeded)
            {
                budgetExceededFound = true;
                break;
            }
        }
        await Assert.That(budgetExceededFound).IsTrue();
    }

    [Test]
    public async Task Proceed_InvokesCallback()
    {
        var item1 = CreateItem("a", tokens: 60, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 60, futureRelevanceHint: 0.8);

        OverflowEvent? capturedEvent = null;

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 50))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Proceed, e => capturedEvent = e)
            .Build();

        var result = pipeline.Execute([item1, item2]);

        await Assert.That(capturedEvent).IsNotNull();
        await Assert.That(capturedEvent!.TokensOverBudget).IsEqualTo(70); // 120 - 50
        await Assert.That(capturedEvent.Budget.TargetTokens).IsEqualTo(50);
        await Assert.That(capturedEvent.OverflowingItems.Count).IsGreaterThan(0);
        // All items should still be in result
        await Assert.That(result.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Proceed_WithoutCallback_NoException()
    {
        var item1 = CreateItem("a", tokens: 60, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 60, futureRelevanceHint: 0.8);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 50))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Proceed)
            .Build();

        var result = pipeline.Execute([item1, item2]);

        await Assert.That(result.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task PinnedAloneExceedingMaxTokens_ThrowsInvalidOperationException()
    {
        // This should still throw InvalidOperationException, not OverflowException
        var pinned = CreateItem("pinned", tokens: 90, pinned: true);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(100, 80, outputReserve: 20))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Proceed)
            .Build();

        await Assert.That(() => pipeline.Execute([pinned]))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task NoOverflow_AllStrategiesBehaveIdentically()
    {
        var item = CreateItem("a", tokens: 10, futureRelevanceHint: 0.5);

        foreach (var strategy in new[] { OverflowStrategy.Throw, OverflowStrategy.Truncate, OverflowStrategy.Proceed })
        {
            var pipeline = CupelPipeline.CreateBuilder()
                .WithBudget(new ContextBudget(1000, 500))
                .WithScorer(new ReflexiveScorer())
                .WithSlicer(new PassAllSlicer())
                .WithOverflowStrategy(strategy)
                .Build();

            var result = pipeline.Execute([item]);
            await Assert.That(result.Items.Count).IsEqualTo(1);
        }
    }

    /// <summary>Slicer that passes all items through without filtering.</summary>
    private sealed class PassAllSlicer : ISlicer
    {
        public IReadOnlyList<ContextItem> Slice(
            IReadOnlyList<ScoredItem> scoredItems,
            ContextBudget budget,
            ITraceCollector traceCollector)
        {
            var result = new ContextItem[scoredItems.Count];
            for (var i = 0; i < scoredItems.Count; i++)
                result[i] = scoredItems[i].Item;
            return result;
        }
    }
}
