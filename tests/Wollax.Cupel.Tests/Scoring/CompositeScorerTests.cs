using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Scoring;

public class CompositeScorerTests
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

    // === Constructor Validation ===

    [Test]
    public async Task NullEntries_ThrowsArgumentNullException()
    {
        var action = () => new CompositeScorer(null!);

        await Assert.That(action).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task EmptyEntries_ThrowsArgumentException()
    {
        var action = () => new CompositeScorer([]);

        await Assert.That(action).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task NullScorer_ThrowsArgumentNullException()
    {
        var action = () => new CompositeScorer([(null!, 1.0)]);

        await Assert.That(action).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task ZeroWeight_ThrowsArgumentOutOfRangeException()
    {
        var action = () => new CompositeScorer([(new ReflexiveScorer(), 0.0)]);

        await Assert.That(action).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task NegativeWeight_ThrowsArgumentOutOfRangeException()
    {
        var action = () => new CompositeScorer([(new ReflexiveScorer(), -1.0)]);

        await Assert.That(action).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task InfiniteWeight_ThrowsArgumentOutOfRangeException()
    {
        var action = () => new CompositeScorer([(new ReflexiveScorer(), double.PositiveInfinity)]);

        await Assert.That(action).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task NaNWeight_ThrowsArgumentOutOfRangeException()
    {
        var action = () => new CompositeScorer([(new ReflexiveScorer(), double.NaN)]);

        await Assert.That(action).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    // === Weighted Average Scoring ===

    [Test]
    public async Task SingleScorer_ReturnsInnerScore()
    {
        var composite = new CompositeScorer([(new ReflexiveScorer(), 1.0)]);
        var item = CreateItem(futureRelevanceHint: 0.7);
        var allItems = new List<ContextItem> { item };

        var score = composite.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.7);
    }

    [Test]
    public async Task EqualWeights_ProducesAverage()
    {
        // Two ReflexiveScorers with equal weights — the composite score
        // is the average of both inner scores (which are the same since
        // both read FutureRelevanceHint). With equal weights and same scorer,
        // the result should equal the inner score.
        var composite = new CompositeScorer([
            (new ReflexiveScorer(), 1.0),
            (new ReflexiveScorer(), 1.0)
        ]);

        var item1 = CreateItem(content: "a", futureRelevanceHint: 0.8);
        var item2 = CreateItem(content: "b", futureRelevanceHint: 0.4);
        var allItems = new List<ContextItem> { item1, item2 };

        var score1 = composite.Score(item1, allItems);
        var score2 = composite.Score(item2, allItems);

        // With two identical scorers equally weighted, the average equals the inner score
        await Assert.That(score1).IsEqualTo(0.8);
        await Assert.That(score2).IsEqualTo(0.4);
    }

    [Test]
    public async Task HigherWeight_DominatesScore()
    {
        // ReflexiveScorer weight 3, PriorityScorer weight 1
        var composite = new CompositeScorer([
            (new ReflexiveScorer(), 3.0),
            (new PriorityScorer(), 1.0)
        ]);

        // High relevance, low priority
        var highRelevance = CreateItem(content: "rel", futureRelevanceHint: 0.9, priority: 1);
        // Low relevance, high priority
        var highPriority = CreateItem(content: "pri", futureRelevanceHint: 0.1, priority: 100);
        var allItems = new List<ContextItem> { highRelevance, highPriority };

        var relevanceScore = composite.Score(highRelevance, allItems);
        var priorityScore = composite.Score(highPriority, allItems);

        // Relevance dominates because weight is 3x
        await Assert.That(relevanceScore).IsGreaterThan(priorityScore);
    }

    [Test]
    public async Task RelativeWeights_ProduceIdenticalResults()
    {
        var compositeA = new CompositeScorer([
            (new ReflexiveScorer(), 2.0),
            (new PriorityScorer(), 1.0)
        ]);

        var compositeB = new CompositeScorer([
            (new ReflexiveScorer(), 0.6),
            (new PriorityScorer(), 0.3)
        ]);

        var item1 = CreateItem(content: "a", futureRelevanceHint: 0.8, priority: 5);
        var item2 = CreateItem(content: "b", futureRelevanceHint: 0.3, priority: 10);
        var allItems = new List<ContextItem> { item1, item2 };

        var scoreA1 = compositeA.Score(item1, allItems);
        var scoreB1 = compositeB.Score(item1, allItems);
        var scoreA2 = compositeA.Score(item2, allItems);
        var scoreB2 = compositeB.Score(item2, allItems);

        await Assert.That(scoreA1).IsEqualTo(scoreB1).Within(1e-14);
        await Assert.That(scoreA2).IsEqualTo(scoreB2).Within(1e-14);
    }

    [Test]
    public async Task ThreeScorers_WeightedAverage()
    {
        var now = DateTimeOffset.UtcNow;

        // Recency(2), Reflexive(1), Priority(1)
        var composite = new CompositeScorer([
            (new RecencyScorer(), 2.0),
            (new ReflexiveScorer(), 1.0),
            (new PriorityScorer(), 1.0)
        ]);

        // Item A: newest, low relevance hint, low priority
        var itemA = CreateItem(content: "a", futureRelevanceHint: 0.1, priority: 1, timestamp: now);
        // Item B: oldest, high relevance hint, high priority
        var itemB = CreateItem(content: "b", futureRelevanceHint: 0.9, priority: 10, timestamp: now.AddHours(-2));
        // Item C: middle time, medium hint, medium priority
        var itemC = CreateItem(content: "c", futureRelevanceHint: 0.5, priority: 5, timestamp: now.AddHours(-1));
        var allItems = new List<ContextItem> { itemA, itemB, itemC };

        var scoreA = composite.Score(itemA, allItems);
        var scoreB = composite.Score(itemB, allItems);
        var scoreC = composite.Score(itemC, allItems);

        // Item A: recency=1.0(w2), reflexive=0.1(w1), priority=0.0(w1) → (2+0.1+0)/4 = 0.525
        // Item B: recency=0.0(w2), reflexive=0.9(w1), priority=1.0(w1) → (0+0.9+1)/4 = 0.475
        // Item C: recency=0.5(w2), reflexive=0.5(w1), priority=0.5(w1) → (1+0.5+0.5)/4 = 0.5
        // So: A > C > B
        await Assert.That(scoreA).IsGreaterThan(scoreC);
        await Assert.That(scoreC).IsGreaterThan(scoreB);
    }

    // === Nesting ===

    [Test]
    public async Task NestedComposite_ProducesValidScores()
    {
        var inner = new CompositeScorer([
            (new ReflexiveScorer(), 1.0),
            (new PriorityScorer(), 1.0)
        ]);

        var outer = new CompositeScorer([
            (inner, 1.0),
            (new RecencyScorer(), 1.0)
        ]);

        var now = DateTimeOffset.UtcNow;
        var item = CreateItem(futureRelevanceHint: 0.5, priority: 5, timestamp: now);
        var allItems = new List<ContextItem>
        {
            item,
            CreateItem(content: "other", futureRelevanceHint: 0.3, priority: 3, timestamp: now.AddHours(-1))
        };

        var score = outer.Score(item, allItems);

        await Assert.That(score).IsGreaterThanOrEqualTo(0.0);
        await Assert.That(score).IsLessThanOrEqualTo(1.0);
    }

    [Test]
    public async Task NestedComposite_OrdinalRelationships()
    {
        var now = DateTimeOffset.UtcNow;

        // Inner composite emphasizes recency
        var inner = new CompositeScorer([
            (new RecencyScorer(), 3.0),
            (new ReflexiveScorer(), 1.0)
        ]);

        // Outer composite combines inner with priority
        var outer = new CompositeScorer([
            (inner, 2.0),
            (new PriorityScorer(), 1.0)
        ]);

        // Item A: newest, low hint, low priority — inner emphasizes recency, so inner score is high
        var itemA = CreateItem(content: "a", futureRelevanceHint: 0.1, priority: 1, timestamp: now);
        // Item B: oldest, high hint, high priority — inner score is low (old), but priority helps in outer
        var itemB = CreateItem(content: "b", futureRelevanceHint: 0.9, priority: 10, timestamp: now.AddHours(-2));
        var allItems = new List<ContextItem> { itemA, itemB };

        var scoreA = outer.Score(itemA, allItems);
        var scoreB = outer.Score(itemB, allItems);

        // Inner for A: recency=1.0*0.75 + reflexive=0.1*0.25 = 0.775
        // Inner for B: recency=0.0*0.75 + reflexive=0.9*0.25 = 0.225
        // Outer for A: inner=0.775*(2/3) + priority=0.0*(1/3) = 0.517
        // Outer for B: inner=0.225*(2/3) + priority=1.0*(1/3) = 0.483
        // A wins
        await Assert.That(scoreA).IsGreaterThan(scoreB);
    }

    // === Cycle Detection (valid DAGs — no false positives) ===

    [Test]
    public async Task DeepNesting_Succeeds()
    {
        // A contains B, B contains C, C contains RecencyScorer — three levels deep
        var c = new CompositeScorer([(new RecencyScorer(), 1.0)]);
        var b = new CompositeScorer([(c, 1.0)]);
        var a = new CompositeScorer([(b, 1.0)]);

        var now = DateTimeOffset.UtcNow;
        var item = CreateItem(timestamp: now);
        var allItems = new List<ContextItem>
        {
            item,
            CreateItem(content: "other", timestamp: now.AddHours(-1))
        };

        var score = a.Score(item, allItems);

        // Should produce a valid score without throwing
        await Assert.That(score).IsGreaterThanOrEqualTo(0.0);
        await Assert.That(score).IsLessThanOrEqualTo(1.0);
    }

    [Test]
    public async Task SameScorerInstanceReused_Succeeds()
    {
        // Same RecencyScorer instance in two branches — diamond DAG, not a cycle
        var recency = new RecencyScorer();
        var composite = new CompositeScorer([
            (recency, 2.0),
            (recency, 1.0)
        ]);

        var now = DateTimeOffset.UtcNow;
        var item = CreateItem(timestamp: now);
        var allItems = new List<ContextItem>
        {
            item,
            CreateItem(content: "other", timestamp: now.AddHours(-1))
        };

        var score = composite.Score(item, allItems);

        await Assert.That(score).IsGreaterThanOrEqualTo(0.0);
        await Assert.That(score).IsLessThanOrEqualTo(1.0);
    }

    [Test]
    public async Task DuplicateScorerType_DifferentInstances_Succeeds()
    {
        // Two separate RecencyScorer instances — valid, no cycle
        var composite = new CompositeScorer([
            (new RecencyScorer(), 1.0),
            (new RecencyScorer(), 1.0)
        ]);

        var now = DateTimeOffset.UtcNow;
        var item = CreateItem(timestamp: now);
        var allItems = new List<ContextItem>
        {
            item,
            CreateItem(content: "other", timestamp: now.AddHours(-1))
        };

        var score = composite.Score(item, allItems);

        await Assert.That(score).IsGreaterThanOrEqualTo(0.0);
        await Assert.That(score).IsLessThanOrEqualTo(1.0);
    }

    [Test]
    public async Task CompositeInDiamondDAG_Succeeds()
    {
        // Shared inner composite used in two branches of an outer composite — diamond DAG
        var shared = new CompositeScorer([(new ReflexiveScorer(), 1.0)]);
        var outer = new CompositeScorer([
            (shared, 1.0),
            (shared, 2.0)
        ]);

        var item = CreateItem(futureRelevanceHint: 0.6);
        var allItems = new List<ContextItem> { item };

        var score = outer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.6);
    }
}
