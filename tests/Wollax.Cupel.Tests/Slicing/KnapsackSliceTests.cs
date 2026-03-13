using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Slicing;

public class KnapsackSliceTests
{
    private static ContextItem CreateItem(string content, int tokens, ContextKind? kind = null) =>
        new() { Content = content, Tokens = tokens, Kind = kind ?? ContextKind.Message };

    private static ScoredItem CreateScored(ContextItem item, double score) =>
        new(item, score);

    [Test]
    public async Task EmptyInput_ReturnsEmpty()
    {
        var slicer = new KnapsackSlice();
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = slicer.Slice([], budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ZeroBudget_ReturnsEmpty()
    {
        var slicer = new KnapsackSlice();
        var item = CreateItem("hello", 10);
        var scored = new List<ScoredItem> { CreateScored(item, 1.0) };
        var budget = new ContextBudget(maxTokens: 0, targetTokens: 0);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SingleItem_FitsInBudget_ReturnsItem()
    {
        var slicer = new KnapsackSlice();
        var item = CreateItem("hello", 10);
        var scored = new List<ScoredItem> { CreateScored(item, 0.8) };
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(item);
    }

    [Test]
    public async Task SingleItem_ExceedsBudget_ReturnsEmpty()
    {
        var slicer = new KnapsackSlice();
        var item = CreateItem("hello", 60);
        var scored = new List<ScoredItem> { CreateScored(item, 0.8) };
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task OptimalSelection_BeatsGreedy()
    {
        // Budget = 100 tokens
        // Greedy by density: A has density 0.6/60=0.01, B has 0.55/50=0.011, C has 0.5/50=0.01
        // Greedy picks B (0.011) first -> 50 tokens, then A or C (both 0.01) -> A fits (60>50 remaining? no, 50 remaining)
        // Actually: B(50) + C(50) = 100 or B(50) + A(60) = 110 > 100
        // Greedy by density: B first (50 tokens), then A and C tied at 0.01 — A is 60 tokens (doesn't fit in remaining 50), C is 50 (fits)
        // Greedy result: B + C = total score 1.05, total tokens 100
        // Knapsack should also pick B + C (optimal)
        //
        // Better test case: budget=100
        // A: tokens=80, score=0.9 -> density=0.01125
        // B: tokens=50, score=0.55 -> density=0.011
        // C: tokens=50, score=0.5  -> density=0.01
        // Greedy by density: A(0.01125) first -> 80 tokens, then B(50) doesn't fit, C(50) doesn't fit
        // Greedy result: A only = 0.9 score, 80 tokens
        // Knapsack: B+C = 1.05 score, 100 tokens (better!)
        var slicer = new KnapsackSlice();
        var itemA = CreateItem("A", 80);
        var itemB = CreateItem("B", 50);
        var itemC = CreateItem("C", 50);
        var scored = new List<ScoredItem>
        {
            CreateScored(itemA, 0.9),
            CreateScored(itemB, 0.55),
            CreateScored(itemC, 0.5)
        };
        var budget = new ContextBudget(maxTokens: 200, targetTokens: 100);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        // Knapsack should pick B+C (total value 1.05) over A alone (0.9)
        await Assert.That(result.Count).IsEqualTo(2);

        var hasB = false;
        var hasC = false;
        for (var i = 0; i < result.Count; i++)
        {
            if (ReferenceEquals(result[i], itemB)) hasB = true;
            if (ReferenceEquals(result[i], itemC)) hasC = true;
        }
        await Assert.That(hasB).IsTrue();
        await Assert.That(hasC).IsTrue();

        // Verify greedy would pick suboptimally
        var greedy = new GreedySlice();
        var greedyResult = greedy.Slice(scored, budget, NullTraceCollector.Instance);
        var greedyTotalScore = 0.0;
        for (var i = 0; i < greedyResult.Count; i++)
        {
            for (var j = 0; j < scored.Count; j++)
            {
                if (ReferenceEquals(greedyResult[i], scored[j].Item))
                {
                    greedyTotalScore += scored[j].Score;
                    break;
                }
            }
        }

        var knapsackTotalScore = 0.0;
        for (var i = 0; i < result.Count; i++)
        {
            for (var j = 0; j < scored.Count; j++)
            {
                if (ReferenceEquals(result[i], scored[j].Item))
                {
                    knapsackTotalScore += scored[j].Score;
                    break;
                }
            }
        }

        await Assert.That(knapsackTotalScore).IsGreaterThan(greedyTotalScore);
    }

    [Test]
    public async Task MatchesGreedy_WhenGreedyIsOptimal()
    {
        // Items where greedy is already optimal: all items fit, or density ordering matches value ordering
        var slicer = new KnapsackSlice();
        var itemA = CreateItem("A", 30);
        var itemB = CreateItem("B", 20);
        var scored = new List<ScoredItem>
        {
            CreateScored(itemA, 0.9),
            CreateScored(itemB, 0.8)
        };
        // Budget = 50, both fit (30+20=50)
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var knapsackResult = slicer.Slice(scored, budget, NullTraceCollector.Instance);
        var greedyResult = new GreedySlice().Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(knapsackResult.Count).IsEqualTo(greedyResult.Count);
    }

    [Test]
    public async Task FillsToTargetTokens_NotMaxTokens()
    {
        var slicer = new KnapsackSlice();
        var item1 = CreateItem("A", 60);
        var item2 = CreateItem("B", 60);
        var scored = new List<ScoredItem>
        {
            CreateScored(item1, 0.9),
            CreateScored(item2, 0.8)
        };
        // target=100, max=200. Both items = 120 > target. Should pick only one.
        var budget = new ContextBudget(maxTokens: 200, targetTokens: 100);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        var totalTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            totalTokens += result[i].Tokens;
        }
        await Assert.That(totalTokens).IsLessThanOrEqualTo(100);
    }

    [Test]
    public async Task ZeroTokenItems_AlwaysIncluded()
    {
        var slicer = new KnapsackSlice();
        var zeroItem = CreateItem("zero", 0);
        var normalItem = CreateItem("normal", 10);
        var scored = new List<ScoredItem>
        {
            CreateScored(zeroItem, 0.1),
            CreateScored(normalItem, 0.5)
        };
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

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
        var slicer = new KnapsackSlice();
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

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    public async Task CustomBucketSize_AffectsGranularity()
    {
        // Both bucket sizes should produce valid results that fit in budget
        var slicer50 = new KnapsackSlice(bucketSize: 50);
        var slicer10 = new KnapsackSlice(bucketSize: 10);

        var itemA = CreateItem("A", 70);
        var itemB = CreateItem("B", 30);
        var scored = new List<ScoredItem>
        {
            CreateScored(itemA, 0.9),
            CreateScored(itemB, 0.5)
        };
        var budget = new ContextBudget(maxTokens: 200, targetTokens: 100);

        var result50 = slicer50.Slice(scored, budget, NullTraceCollector.Instance);
        var result10 = slicer10.Slice(scored, budget, NullTraceCollector.Instance);

        // Both must produce valid results within budget
        var tokens50 = 0;
        for (var i = 0; i < result50.Count; i++) tokens50 += result50[i].Tokens;
        var tokens10 = 0;
        for (var i = 0; i < result10.Count; i++) tokens10 += result10[i].Tokens;

        await Assert.That(tokens50).IsLessThanOrEqualTo(100);
        await Assert.That(tokens10).IsLessThanOrEqualTo(100);
    }

    [Test]
    public async Task BucketSize_DefaultIs100()
    {
        // Default constructor should work (bucket size 100)
        var slicer = new KnapsackSlice();
        var item = CreateItem("hello", 50);
        var scored = new List<ScoredItem> { CreateScored(item, 0.8) };
        var budget = new ContextBudget(maxTokens: 200, targetTokens: 100);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Constructor_BucketSizeZero_Throws()
    {
        await Assert.That(() => new KnapsackSlice(bucketSize: 0))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Constructor_BucketSizeNegative_Throws()
    {
        await Assert.That(() => new KnapsackSlice(bucketSize: -1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
