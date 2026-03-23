using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Pipeline;

/// <summary>
/// Failing-first tests for the budget-simulation API: GetMarginalItems and FindMinBudgetFor.
/// These tests lock the public API signatures, diff semantics, guard messages, and search behavior
/// before implementation exists. They are expected to fail until T02/T03 implement the API.
/// </summary>
public class BudgetSimulationTests
{
    private static ContextItem CreateItem(string content, int tokens, double? futureRelevanceHint = null) =>
        new()
        {
            Content = content,
            Tokens = tokens,
            FutureRelevanceHint = futureRelevanceHint,
            Kind = ContextKind.Message
        };

    private static CupelPipeline CreateGreedyPipeline(int maxTokens, int targetTokens) =>
        CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(maxTokens, targetTokens))
            .WithScorer(new ReflexiveScorer())
            .Build();

    // ──────────────────────────────────────────────
    // GetMarginalItems — happy path
    // ──────────────────────────────────────────────

    [Test]
    public async Task GetMarginalItems_ReturnsDiffByReferenceEquality()
    {
        // 3 items: budget fits all 3 at full, only 2 at reduced
        var itemA = CreateItem("A", 30, futureRelevanceHint: 0.9);
        var itemB = CreateItem("B", 30, futureRelevanceHint: 0.5);
        var itemC = CreateItem("C", 30, futureRelevanceHint: 0.1);
        var items = new[] { itemA, itemB, itemC };

        var pipeline = CreateGreedyPipeline(maxTokens: 200, targetTokens: 100);
        var budget = new ContextBudget(maxTokens: 200, targetTokens: 100);

        // slackTokens = 40 -> reduced target = 60, which fits 2 items (60 tokens)
        // full target = 100, which fits 3 items (90 tokens)
        // marginal = the item in full but not in reduced
        var marginal = pipeline.GetMarginalItems(items, budget, slackTokens: 40);

        // At least one item should be marginal
        await Assert.That(marginal.Count).IsGreaterThan(0);

        // The returned items must be reference-equal to the originals (not copies)
        for (var i = 0; i < marginal.Count; i++)
        {
            var found = false;
            for (var j = 0; j < items.Length; j++)
            {
                if (ReferenceEquals(marginal[i], items[j]))
                {
                    found = true;
                    break;
                }
            }

            await Assert.That(found).IsTrue();
        }
    }

    [Test]
    public async Task GetMarginalItems_DiffIsFullMinusReduced()
    {
        // 4 items of 20 tokens each
        // Full budget target = 80 -> fits all 4 (80 tokens)
        // Reduced by 30 -> target = 50 -> fits 2 items (40 tokens)
        // Marginal should be 2 items
        var itemA = CreateItem("A", 20, futureRelevanceHint: 0.9);
        var itemB = CreateItem("B", 20, futureRelevanceHint: 0.7);
        var itemC = CreateItem("C", 20, futureRelevanceHint: 0.5);
        var itemD = CreateItem("D", 20, futureRelevanceHint: 0.3);
        var items = new[] { itemA, itemB, itemC, itemD };

        var pipeline = CreateGreedyPipeline(maxTokens: 200, targetTokens: 80);
        var budget = new ContextBudget(maxTokens: 200, targetTokens: 80);

        var marginal = pipeline.GetMarginalItems(items, budget, slackTokens: 30);

        await Assert.That(marginal.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetMarginalItems_EmptyWhenSlackIsZero()
    {
        var itemA = CreateItem("A", 30, futureRelevanceHint: 0.9);
        var itemB = CreateItem("B", 30, futureRelevanceHint: 0.5);
        var items = new[] { itemA, itemB };

        var pipeline = CreateGreedyPipeline(maxTokens: 200, targetTokens: 100);
        var budget = new ContextBudget(maxTokens: 200, targetTokens: 100);

        var marginal = pipeline.GetMarginalItems(items, budget, slackTokens: 0);

        await Assert.That(marginal.Count).IsEqualTo(0);
    }

    // ──────────────────────────────────────────────
    // GetMarginalItems — QuotaSlice guard
    // ──────────────────────────────────────────────

    [Test]
    public async Task GetMarginalItems_ThrowsForQuotaSlice()
    {
        var item = CreateItem("A", 30, futureRelevanceHint: 0.9);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithQuotas(qb => qb.Require(ContextKind.Message, 1.0))
            .Build();

        var budget = new ContextBudget(maxTokens: 200, targetTokens: 100);

        var ex = await Assert.That(() => pipeline.GetMarginalItems(new[] { item }, budget, slackTokens: 10))
            .Throws<InvalidOperationException>();

        await Assert.That(ex!.Message).IsEqualTo(
            "GetMarginalItems requires monotonic item inclusion. QuotaSlice produces non-monotonic inclusion as budget changes shift percentage allocations.");
    }

    // ──────────────────────────────────────────────
    // FindMinBudgetFor — happy path
    // ──────────────────────────────────────────────

    [Test]
    public async Task FindMinBudgetFor_ReturnsMinBudgetForTargetItem()
    {
        var highDensity = CreateItem("high", 10, futureRelevanceHint: 0.9);
        var lowDensity = CreateItem("low", 100, futureRelevanceHint: 0.2);
        var items = new[] { highDensity, lowDensity };

        var pipeline = CreateGreedyPipeline(maxTokens: 500, targetTokens: 500);

        var minBudget = pipeline.FindMinBudgetFor(items, lowDensity, searchCeiling: 500);

        await Assert.That(minBudget).IsNotNull();
        await Assert.That(minBudget!.Value).IsGreaterThanOrEqualTo(lowDensity.Tokens);
    }

    [Test]
    public async Task FindMinBudgetFor_ReturnsNullWhenTargetCannotFit()
    {
        var itemA = CreateItem("A", 50, futureRelevanceHint: 0.9);
        var itemB = CreateItem("B", 50, futureRelevanceHint: 0.1);
        var items = new[] { itemA, itemB };

        var pipeline = CreateGreedyPipeline(maxTokens: 500, targetTokens: 500);

        // searchCeiling of 60: only 60 tokens total, but itemA (higher density) takes 50,
        // leaving only 10 for itemB which needs 50
        var minBudget = pipeline.FindMinBudgetFor(items, itemB, searchCeiling: 60);

        await Assert.That(minBudget).IsNull();
    }

    [Test]
    public async Task FindMinBudgetFor_ReturnsFirstSuccessfulBudget()
    {
        // Single item: needs exactly its own token count
        var item = CreateItem("only", 50, futureRelevanceHint: 0.9);
        var items = new[] { item };

        var pipeline = CreateGreedyPipeline(maxTokens: 500, targetTokens: 500);

        var minBudget = pipeline.FindMinBudgetFor(items, item, searchCeiling: 200);

        await Assert.That(minBudget).IsNotNull();
        await Assert.That(minBudget!.Value).IsEqualTo(50);
    }

    // ──────────────────────────────────────────────
    // FindMinBudgetFor — argument guards
    // ──────────────────────────────────────────────

    [Test]
    public async Task FindMinBudgetFor_ThrowsWhenTargetNotInItems()
    {
        var item = CreateItem("in-list", 30, futureRelevanceHint: 0.9);
        var outsider = CreateItem("outsider", 30, futureRelevanceHint: 0.5);
        var items = new[] { item };

        var pipeline = CreateGreedyPipeline(maxTokens: 200, targetTokens: 100);

        var ex = await Assert.That(() => pipeline.FindMinBudgetFor(items, outsider, searchCeiling: 200))
            .Throws<ArgumentException>();

        await Assert.That(ex!.Message).Contains("targetItem must be an element of items");
    }

    [Test]
    public async Task FindMinBudgetFor_ThrowsWhenSearchCeilingBelowTokens()
    {
        var item = CreateItem("item", 50, futureRelevanceHint: 0.9);
        var items = new[] { item };

        var pipeline = CreateGreedyPipeline(maxTokens: 200, targetTokens: 100);

        var ex = await Assert.That(() => pipeline.FindMinBudgetFor(items, item, searchCeiling: 30))
            .Throws<ArgumentException>();

        await Assert.That(ex!.Message).Contains("searchCeiling must be >= targetItem.Tokens");
    }

    // ──────────────────────────────────────────────
    // FindMinBudgetFor — QuotaSlice + CountQuotaSlice guard
    // ──────────────────────────────────────────────

    [Test]
    public async Task FindMinBudgetFor_ThrowsForQuotaSlice()
    {
        var item = CreateItem("A", 30, futureRelevanceHint: 0.9);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithQuotas(qb => qb.Require(ContextKind.Message, 1.0))
            .Build();

        var ex = await Assert.That(() => pipeline.FindMinBudgetFor(new[] { item }, item, searchCeiling: 200))
            .Throws<InvalidOperationException>();

        await Assert.That(ex!.Message).IsEqualTo(
            "FindMinBudgetFor requires monotonic item inclusion. QuotaSlice and CountQuotaSlice produce non-monotonic inclusion as budget changes shift allocations. Use a GreedySlice or KnapsackSlice inner slicer for budget simulation.");
    }

    [Test]
    public async Task FindMinBudgetFor_ThrowsForCountQuotaSlice()
    {
        var item = CreateItem("A", 30, futureRelevanceHint: 0.9);

        var countQuotaSlice = new CountQuotaSlice(
            innerSlicer: new GreedySlice(),
            entries: [new CountQuotaEntry(ContextKind.Message, requireCount: 0, capCount: 10)]);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(countQuotaSlice)
            .Build();

        var ex = await Assert.That(() => pipeline.FindMinBudgetFor(new[] { item }, item, searchCeiling: 200))
            .Throws<InvalidOperationException>();

        await Assert.That(ex!.Message).IsEqualTo(
            "FindMinBudgetFor requires monotonic item inclusion. QuotaSlice and CountQuotaSlice produce non-monotonic inclusion as budget changes shift allocations. Use a GreedySlice or KnapsackSlice inner slicer for budget simulation.");
    }
}
