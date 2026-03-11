using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Scoring;

public class RecencyScorerTests
{
    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10,
        DateTimeOffset? timestamp = null) =>
        new() { Content = content, Tokens = tokens, Timestamp = timestamp };

    private readonly RecencyScorer _scorer = new();

    [Test]
    public async Task NullTimestamp_ReturnsZero()
    {
        var item = CreateItem();
        var allItems = new List<ContextItem>
        {
            item,
            CreateItem(timestamp: DateTimeOffset.UtcNow)
        };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task SingleItemWithTimestamp_ReturnsOne()
    {
        var item = CreateItem(timestamp: DateTimeOffset.UtcNow);
        var allItems = new List<ContextItem>
        {
            CreateItem(),
            item,
            CreateItem()
        };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(1.0);
    }

    [Test]
    public async Task MostRecent_ScoresHigher_ThanOlder()
    {
        var now = DateTimeOffset.UtcNow;
        var older = CreateItem(content: "older", timestamp: now.AddHours(-2));
        var newer = CreateItem(content: "newer", timestamp: now);
        var allItems = new List<ContextItem> { older, newer };

        var olderScore = _scorer.Score(older, allItems);
        var newerScore = _scorer.Score(newer, allItems);

        await Assert.That(newerScore).IsGreaterThan(olderScore);
    }

    [Test]
    public async Task ThreeItems_LinearInterpolation()
    {
        var now = DateTimeOffset.UtcNow;
        var oldest = CreateItem(content: "oldest", timestamp: now.AddHours(-2));
        var middle = CreateItem(content: "middle", timestamp: now.AddHours(-1));
        var newest = CreateItem(content: "newest", timestamp: now);
        var allItems = new List<ContextItem> { oldest, middle, newest };

        var oldestScore = _scorer.Score(oldest, allItems);
        var middleScore = _scorer.Score(middle, allItems);
        var newestScore = _scorer.Score(newest, allItems);

        await Assert.That(oldestScore).IsEqualTo(0.0);
        await Assert.That(newestScore).IsEqualTo(1.0);
        await Assert.That(middleScore).IsEqualTo(0.5);
    }

    [Test]
    public async Task TiedTimestamps_ProduceEqualScores()
    {
        var now = DateTimeOffset.UtcNow;
        var item1 = CreateItem(content: "a", timestamp: now);
        var item2 = CreateItem(content: "b", timestamp: now);
        var allItems = new List<ContextItem> { item1, item2 };

        var score1 = _scorer.Score(item1, allItems);
        var score2 = _scorer.Score(item2, allItems);

        await Assert.That(score1).IsEqualTo(score2);
    }

    [Test]
    public async Task MixedNullAndValid_NullGetsZero()
    {
        var now = DateTimeOffset.UtcNow;
        var nullItem = CreateItem(content: "null");
        var validItem = CreateItem(content: "valid", timestamp: now);
        var allItems = new List<ContextItem> { nullItem, validItem };

        var nullScore = _scorer.Score(nullItem, allItems);
        var validScore = _scorer.Score(validItem, allItems);

        await Assert.That(nullScore).IsEqualTo(0.0);
        await Assert.That(validScore).IsEqualTo(1.0);
    }

    [Test]
    public async Task AllNullTimestamps_AllReturnZero()
    {
        var item1 = CreateItem(content: "a");
        var item2 = CreateItem(content: "b");
        var allItems = new List<ContextItem> { item1, item2 };

        var score1 = _scorer.Score(item1, allItems);
        var score2 = _scorer.Score(item2, allItems);

        await Assert.That(score1).IsEqualTo(0.0);
        await Assert.That(score2).IsEqualTo(0.0);
    }

    [Test]
    public async Task ScoresAreInZeroToOneRange()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new List<ContextItem>
        {
            CreateItem(content: "a", timestamp: now.AddHours(-5)),
            CreateItem(content: "b", timestamp: now.AddHours(-3)),
            CreateItem(content: "c", timestamp: now.AddHours(-1)),
            CreateItem(content: "d", timestamp: now),
            CreateItem(content: "e") // null timestamp
        };

        for (var i = 0; i < items.Count; i++)
        {
            var score = _scorer.Score(items[i], items);
            await Assert.That(score).IsGreaterThanOrEqualTo(0.0);
            await Assert.That(score).IsLessThanOrEqualTo(1.0);
        }
    }
}
