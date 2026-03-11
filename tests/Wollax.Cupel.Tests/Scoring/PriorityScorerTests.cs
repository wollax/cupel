using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Scoring;

public class PriorityScorerTests
{
    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10,
        int? priority = null) =>
        new() { Content = content, Tokens = tokens, Priority = priority };

    private readonly PriorityScorer _scorer = new();

    [Test]
    public async Task NullPriority_ReturnsZero()
    {
        var item = CreateItem();
        var allItems = new List<ContextItem>
        {
            item,
            CreateItem(priority: 5)
        };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task SingleItemWithPriority_ReturnsOne()
    {
        var item = CreateItem(priority: 5);
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
    public async Task HigherPriority_ScoresHigher()
    {
        var low = CreateItem(content: "low", priority: 1);
        var high = CreateItem(content: "high", priority: 10);
        var allItems = new List<ContextItem> { low, high };

        var lowScore = _scorer.Score(low, allItems);
        var highScore = _scorer.Score(high, allItems);

        await Assert.That(highScore).IsGreaterThan(lowScore);
    }

    [Test]
    public async Task ThreeItems_LinearInterpolation()
    {
        var lowest = CreateItem(content: "lowest", priority: 1);
        var middle = CreateItem(content: "middle", priority: 5);
        var highest = CreateItem(content: "highest", priority: 10);
        var allItems = new List<ContextItem> { lowest, middle, highest };

        var lowestScore = _scorer.Score(lowest, allItems);
        var middleScore = _scorer.Score(middle, allItems);
        var highestScore = _scorer.Score(highest, allItems);

        await Assert.That(lowestScore).IsEqualTo(0.0);
        await Assert.That(highestScore).IsEqualTo(1.0);
        await Assert.That(middleScore).IsEqualTo(0.5);
    }

    [Test]
    public async Task TiedPriorities_ProduceEqualScores()
    {
        var item1 = CreateItem(content: "a", priority: 5);
        var item2 = CreateItem(content: "b", priority: 5);
        var allItems = new List<ContextItem> { item1, item2 };

        var score1 = _scorer.Score(item1, allItems);
        var score2 = _scorer.Score(item2, allItems);

        await Assert.That(score1).IsEqualTo(score2);
    }

    [Test]
    public async Task MixedNullAndValid_NullGetsZero()
    {
        var nullItem = CreateItem(content: "null");
        var validItem = CreateItem(content: "valid", priority: 5);
        var allItems = new List<ContextItem> { nullItem, validItem };

        var nullScore = _scorer.Score(nullItem, allItems);
        var validScore = _scorer.Score(validItem, allItems);

        await Assert.That(nullScore).IsEqualTo(0.0);
        await Assert.That(validScore).IsEqualTo(1.0);
    }

    [Test]
    public async Task AllNullPriorities_AllReturnZero()
    {
        var item1 = CreateItem(content: "a");
        var item2 = CreateItem(content: "b");
        var allItems = new List<ContextItem> { item1, item2 };

        var score1 = _scorer.Score(item1, allItems);
        var score2 = _scorer.Score(item2, allItems);

        await Assert.That(score1).IsEqualTo(0.0);
        await Assert.That(score2).IsEqualTo(0.0);
    }
}
