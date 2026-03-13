using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Slicing;

public class GreedySliceTests
{
    private static ContextItem CreateItem(string content, int tokens) =>
        new() { Content = content, Tokens = tokens };

    private static ScoredItem CreateScored(ContextItem item, double score) =>
        new(item, score);

    private readonly GreedySlice _slicer = new();

    [Test]
    public async Task EmptyInput_ReturnsEmpty()
    {
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);
        var result = _slicer.Slice([], budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ZeroBudget_ReturnsEmpty()
    {
        var item = CreateItem("hello", 10);
        var scored = new List<ScoredItem> { CreateScored(item, 1.0) };
        var budget = new ContextBudget(maxTokens: 0, targetTokens: 0);

        var result = _slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SingleItem_FitsInBudget_ReturnsItem()
    {
        var item = CreateItem("hello", 10);
        var scored = new List<ScoredItem> { CreateScored(item, 0.8) };
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = _slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(item);
    }

    [Test]
    public async Task SingleItem_ExceedsBudget_ReturnsEmpty()
    {
        var item = CreateItem("hello", 60);
        var scored = new List<ScoredItem> { CreateScored(item, 0.8) };
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = _slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SelectsByValueDensity_NotRawScore()
    {
        // Item A: score=0.8, tokens=100, density=0.008
        // Item B: score=0.5, tokens=10, density=0.05
        // Budget=50 -> B selected (higher density), A doesn't fit after B
        var itemA = CreateItem("A", 100);
        var itemB = CreateItem("B", 10);
        var scored = new List<ScoredItem>
        {
            CreateScored(itemA, 0.8),
            CreateScored(itemB, 0.5)
        };
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = _slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(itemB);
    }

    [Test]
    public async Task FillsToTargetTokens_NotMaxTokens()
    {
        // Budget: target=100, max=200
        // Items: 3 items of 50 tokens each (total 150)
        // Should select only items fitting within target (100), so 2 items = 100 tokens
        var item1 = CreateItem("A", 50);
        var item2 = CreateItem("B", 50);
        var item3 = CreateItem("C", 50);
        var scored = new List<ScoredItem>
        {
            CreateScored(item1, 0.9),
            CreateScored(item2, 0.8),
            CreateScored(item3, 0.7)
        };
        var budget = new ContextBudget(maxTokens: 200, targetTokens: 100);

        var result = _slicer.Slice(scored, budget, NullTraceCollector.Instance);

        var totalTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            totalTokens += result[i].Tokens;
        }

        await Assert.That(totalTokens).IsLessThanOrEqualTo(100);
    }

    [Test]
    public async Task GreedyFill_StopsAtBudget()
    {
        // 5 items with varying density, budget=100
        var items = new[]
        {
            (CreateItem("A", 30), 0.9),  // density = 0.03
            (CreateItem("B", 20), 0.8),  // density = 0.04
            (CreateItem("C", 40), 0.6),  // density = 0.015
            (CreateItem("D", 10), 0.5),  // density = 0.05
            (CreateItem("E", 50), 0.4),  // density = 0.008
        };
        // Density order: D(0.05), B(0.04), A(0.03), C(0.015), E(0.008)
        // Greedy fill: D(10) + B(20) + A(30) = 60, then C(40) = 100
        // All fit in budget=100
        var scored = new List<ScoredItem>();
        for (var i = 0; i < items.Length; i++)
        {
            scored.Add(CreateScored(items[i].Item1, items[i].Item2));
        }

        var budget = new ContextBudget(maxTokens: 200, targetTokens: 100);

        var result = _slicer.Slice(scored, budget, NullTraceCollector.Instance);

        var totalTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            totalTokens += result[i].Tokens;
        }

        await Assert.That(totalTokens).IsLessThanOrEqualTo(100);
        // Should include D, B, A, C (100 tokens exactly) but not E
        await Assert.That(result.Count).IsEqualTo(4);
    }

    [Test]
    public async Task ZeroTokenItems_AlwaysIncluded()
    {
        var zeroItem = CreateItem("zero", 0);
        var normalItem = CreateItem("normal", 10);
        var scored = new List<ScoredItem>
        {
            CreateScored(zeroItem, 0.1),
            CreateScored(normalItem, 0.5)
        };
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = _slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(2);
        var containsZero = false;
        for (var i = 0; i < result.Count; i++)
        {
            if (ReferenceEquals(result[i], zeroItem))
            {
                containsZero = true;
            }
        }
        await Assert.That(containsZero).IsTrue();
    }

    [Test]
    public async Task AllItemsFit_ReturnsAll()
    {
        var items = new[]
        {
            CreateItem("A", 10),
            CreateItem("B", 20),
            CreateItem("C", 15)
        };
        var scored = new List<ScoredItem>
        {
            CreateScored(items[0], 0.9),
            CreateScored(items[1], 0.7),
            CreateScored(items[2], 0.5)
        };
        // Total = 45, budget = 50 -> all fit
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = _slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    public async Task StableOrder_EqualDensity()
    {
        // Same score and same tokens -> same density -> original order preserved
        var item1 = CreateItem("first", 10);
        var item2 = CreateItem("second", 10);
        var item3 = CreateItem("third", 10);
        var scored = new List<ScoredItem>
        {
            CreateScored(item1, 0.5),
            CreateScored(item2, 0.5),
            CreateScored(item3, 0.5)
        };
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = _slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0]).IsEqualTo(item1);
        await Assert.That(result[1]).IsEqualTo(item2);
        await Assert.That(result[2]).IsEqualTo(item3);
    }
}
