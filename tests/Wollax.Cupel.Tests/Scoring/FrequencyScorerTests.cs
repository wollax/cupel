using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Scoring;

public class FrequencyScorerTests
{
    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10,
        IReadOnlyList<string>? tags = null) =>
        new() { Content = content, Tokens = tokens, Tags = tags ?? [] };

    private readonly FrequencyScorer _scorer = new();

    [Test]
    public async Task EmptyTags_ReturnsZero()
    {
        var item = CreateItem();
        var other = CreateItem(content: "other", tags: ["api"]);
        var allItems = new List<ContextItem> { item, other };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task SingleItem_ReturnsZero()
    {
        var item = CreateItem(tags: ["api"]);
        var allItems = new List<ContextItem> { item };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task AllItemsShareTag_ReturnsOne()
    {
        var item = CreateItem(content: "a", tags: ["api"]);
        var other1 = CreateItem(content: "b", tags: ["api"]);
        var other2 = CreateItem(content: "c", tags: ["api"]);
        var allItems = new List<ContextItem> { item, other1, other2 };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(1.0);
    }

    [Test]
    public async Task NoItemsShareTag_ReturnsZero()
    {
        var item = CreateItem(content: "a", tags: ["api"]);
        var other1 = CreateItem(content: "b", tags: ["database"]);
        var other2 = CreateItem(content: "c", tags: ["frontend"]);
        var allItems = new List<ContextItem> { item, other1, other2 };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task PartialOverlap_ScoresBetweenZeroAndOne()
    {
        var item = CreateItem(content: "a", tags: ["api"]);
        var shares = CreateItem(content: "b", tags: ["api"]);
        var noShare = CreateItem(content: "c", tags: ["database"]);
        var allItems = new List<ContextItem> { item, shares, noShare };

        var score = _scorer.Score(item, allItems);

        // 1 out of 2 peers shares a tag → 0.5
        await Assert.That(score).IsGreaterThan(0.0);
        await Assert.That(score).IsLessThan(1.0);
        await Assert.That(score).IsEqualTo(0.5);
    }

    [Test]
    public async Task CaseInsensitive_TagComparison()
    {
        var item = CreateItem(content: "a", tags: ["API"]);
        var other = CreateItem(content: "b", tags: ["api"]);
        var allItems = new List<ContextItem> { item, other };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(1.0);
    }

    [Test]
    public async Task SelfExclusion()
    {
        // Item should not count itself as a match
        var item = CreateItem(content: "a", tags: ["api"]);
        var allItems = new List<ContextItem> { item, item }; // same reference twice

        // Only 1 peer (itself again), but self-exclusion via ReferenceEquals
        // means we skip both references to self. With count=2, peers=1,
        // but ReferenceEquals skips all instances of self.
        // Actually: allItems.Count - 1 = 1 peer, but ReferenceEquals(item, item) skips it → 0/1 = 0.0
        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task MoreSharedItems_ScoresHigher()
    {
        var item = CreateItem(content: "target", tags: ["api"]);
        var peer1 = CreateItem(content: "p1", tags: ["api"]);
        var peer2 = CreateItem(content: "p2", tags: ["api"]);
        var peer3 = CreateItem(content: "p3", tags: ["api"]);
        var noShare = CreateItem(content: "ns", tags: ["database"]);

        // Scenario 1: 1 out of 4 peers shares → 0.25
        var allItems1 = new List<ContextItem> { item, peer1, noShare, noShare, noShare };
        var score1 = _scorer.Score(item, allItems1);

        // Scenario 2: 3 out of 4 peers shares → 0.75
        var allItems2 = new List<ContextItem> { item, peer1, peer2, peer3, noShare };
        var score2 = _scorer.Score(item, allItems2);

        await Assert.That(score2).IsGreaterThan(score1);
    }

    [Test]
    public async Task MultipleSharedTags_CountsOnce()
    {
        // Sharing 2 tags with same item still counts as 1 match, not 2
        var item = CreateItem(content: "a", tags: ["api", "backend"]);
        var other = CreateItem(content: "b", tags: ["api", "backend"]);
        var allItems = new List<ContextItem> { item, other };

        var score = _scorer.Score(item, allItems);

        // 1 peer shares tags → 1/1 = 1.0, not 2.0
        await Assert.That(score).IsEqualTo(1.0);
    }
}
