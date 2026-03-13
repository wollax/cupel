using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Scoring;

public class ScaledScorerTests
{
    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10,
        double? futureRelevanceHint = null,
        int? priority = null,
        DateTimeOffset? timestamp = null) =>
        new()
        {
            Content = content,
            Tokens = tokens,
            FutureRelevanceHint = futureRelevanceHint,
            Priority = priority,
            Timestamp = timestamp
        };

    [Test]
    public async Task NullInner_ThrowsArgumentNullException()
    {
        var action = () => new ScaledScorer(null!);

        await Assert.That(action).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task HighestScoringItem_ReturnsOne()
    {
        var scorer = new ScaledScorer(new ReflexiveScorer());
        var high = CreateItem(content: "high", futureRelevanceHint: 0.9);
        var low = CreateItem(content: "low", futureRelevanceHint: 0.1);
        var allItems = new List<ContextItem> { high, low };

        var score = scorer.Score(high, allItems);

        await Assert.That(score).IsEqualTo(1.0);
    }

    [Test]
    public async Task LowestScoringItem_ReturnsZero()
    {
        var scorer = new ScaledScorer(new ReflexiveScorer());
        var high = CreateItem(content: "high", futureRelevanceHint: 0.9);
        var low = CreateItem(content: "low", futureRelevanceHint: 0.1);
        var allItems = new List<ContextItem> { high, low };

        var score = scorer.Score(low, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task MiddleScoringItem_ReturnsBetweenZeroAndOne()
    {
        var scorer = new ScaledScorer(new ReflexiveScorer());
        var high = CreateItem(content: "high", futureRelevanceHint: 0.9);
        var mid = CreateItem(content: "mid", futureRelevanceHint: 0.5);
        var low = CreateItem(content: "low", futureRelevanceHint: 0.1);
        var allItems = new List<ContextItem> { high, mid, low };

        var score = scorer.Score(mid, allItems);

        await Assert.That(score).IsGreaterThan(0.0);
        await Assert.That(score).IsLessThan(1.0);
    }

    [Test]
    public async Task AllIdenticalScores_ReturnsHalf()
    {
        var scorer = new ScaledScorer(new ReflexiveScorer());
        var item1 = CreateItem(content: "a", futureRelevanceHint: 0.5);
        var item2 = CreateItem(content: "b", futureRelevanceHint: 0.5);
        var item3 = CreateItem(content: "c", futureRelevanceHint: 0.5);
        var allItems = new List<ContextItem> { item1, item2, item3 };

        var score1 = scorer.Score(item1, allItems);
        var score2 = scorer.Score(item2, allItems);
        var score3 = scorer.Score(item3, allItems);

        await Assert.That(score1).IsEqualTo(0.5);
        await Assert.That(score2).IsEqualTo(0.5);
        await Assert.That(score3).IsEqualTo(0.5);
    }

    [Test]
    public async Task TwoItems_NormalizesCorrectly()
    {
        var scorer = new ScaledScorer(new ReflexiveScorer());
        var itemA = CreateItem(content: "a", futureRelevanceHint: 0.2);
        var itemB = CreateItem(content: "b", futureRelevanceHint: 0.8);
        var allItems = new List<ContextItem> { itemA, itemB };

        var scoreA = scorer.Score(itemA, allItems);
        var scoreB = scorer.Score(itemB, allItems);

        await Assert.That(scoreA).IsEqualTo(0.0);
        await Assert.That(scoreB).IsEqualTo(1.0);
    }

    [Test]
    public async Task PreservesOrdinalRelationships()
    {
        var scorer = new ScaledScorer(new ReflexiveScorer());
        var high = CreateItem(content: "high", futureRelevanceHint: 0.9);
        var mid = CreateItem(content: "mid", futureRelevanceHint: 0.5);
        var low = CreateItem(content: "low", futureRelevanceHint: 0.1);
        var allItems = new List<ContextItem> { high, mid, low };

        var highScore = scorer.Score(high, allItems);
        var midScore = scorer.Score(mid, allItems);
        var lowScore = scorer.Score(low, allItems);

        await Assert.That(highScore).IsGreaterThan(midScore);
        await Assert.That(midScore).IsGreaterThan(lowScore);
    }

    [Test]
    public async Task ScaledRecencyScorer_OutputInZeroToOne()
    {
        var now = DateTimeOffset.UtcNow;
        var scorer = new ScaledScorer(new RecencyScorer());
        var items = new List<ContextItem>
        {
            CreateItem(content: "a", timestamp: now.AddHours(-5)),
            CreateItem(content: "b", timestamp: now.AddHours(-3)),
            CreateItem(content: "c", timestamp: now.AddHours(-1)),
            CreateItem(content: "d", timestamp: now)
        };

        for (var i = 0; i < items.Count; i++)
        {
            var score = scorer.Score(items[i], items);
            await Assert.That(score).IsGreaterThanOrEqualTo(0.0);
            await Assert.That(score).IsLessThanOrEqualTo(1.0);
        }
    }

    [Test]
    public async Task ScaledPriorityScorer_NormalizesRange()
    {
        var scorer = new ScaledScorer(new PriorityScorer());
        var items = new List<ContextItem>
        {
            CreateItem(content: "low", priority: 1),
            CreateItem(content: "mid", priority: 50),
            CreateItem(content: "high", priority: 100)
        };

        var scores = new double[items.Count];
        for (var i = 0; i < items.Count; i++)
            scores[i] = scorer.Score(items[i], items);

        await Assert.That(scores.Min()).IsEqualTo(0.0);
        await Assert.That(scores.Max()).IsEqualTo(1.0);
    }

    [Test]
    public async Task SingleItem_ReturnsHalf()
    {
        var scorer = new ScaledScorer(new ReflexiveScorer());
        var item = CreateItem(content: "only", futureRelevanceHint: 0.7);
        var allItems = new List<ContextItem> { item };

        var score = scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.5);
    }

    // === ScaledScorer wrapping CompositeScorer ===

    [Test]
    public async Task ScaledComposite_OutputInZeroToOne()
    {
        var now = DateTimeOffset.UtcNow;
        var composite = new CompositeScorer([
            (new RecencyScorer(), 2.0),
            (new PriorityScorer(), 1.0),
            (new ReflexiveScorer(), 1.0)
        ]);
        var scaled = new ScaledScorer(composite);

        var items = new List<ContextItem>
        {
            CreateItem(content: "a", futureRelevanceHint: 0.9, priority: 10, timestamp: now),
            CreateItem(content: "b", futureRelevanceHint: 0.1, priority: 1, timestamp: now.AddHours(-2)),
            CreateItem(content: "c", futureRelevanceHint: 0.5, priority: 5, timestamp: now.AddHours(-1))
        };

        for (var i = 0; i < items.Count; i++)
        {
            var score = scaled.Score(items[i], items);
            await Assert.That(score).IsGreaterThanOrEqualTo(0.0);
            await Assert.That(score).IsLessThanOrEqualTo(1.0);
        }
    }
}
