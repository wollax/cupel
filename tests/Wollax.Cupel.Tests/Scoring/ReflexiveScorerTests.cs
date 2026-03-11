using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Scoring;

public class ReflexiveScorerTests
{
    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10,
        double? futureRelevanceHint = null) =>
        new() { Content = content, Tokens = tokens, FutureRelevanceHint = futureRelevanceHint };

    private readonly ReflexiveScorer _scorer = new();

    [Test]
    public async Task NullHint_ReturnsZero()
    {
        var item = CreateItem();
        var allItems = new List<ContextItem> { item };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task ValidHint_ReturnsSameValue()
    {
        var item = CreateItem(futureRelevanceHint: 0.7);
        var allItems = new List<ContextItem> { item };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.7);
    }

    [Test]
    public async Task HintAboveOne_ClampedToOne()
    {
        var item = CreateItem(futureRelevanceHint: 1.5);
        var allItems = new List<ContextItem> { item };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(1.0);
    }

    [Test]
    public async Task HintBelowZero_ClampedToZero()
    {
        var item = CreateItem(futureRelevanceHint: -0.3);
        var allItems = new List<ContextItem> { item };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task HintExactlyZero_ReturnsZero()
    {
        var item = CreateItem(futureRelevanceHint: 0.0);
        var allItems = new List<ContextItem> { item };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task HintExactlyOne_ReturnsOne()
    {
        var item = CreateItem(futureRelevanceHint: 1.0);
        var allItems = new List<ContextItem> { item };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(1.0);
    }

    [Test]
    public async Task IgnoresAllItems()
    {
        var item = CreateItem(futureRelevanceHint: 0.5);
        var smallList = new List<ContextItem> { item };
        var largeList = new List<ContextItem>
        {
            item,
            CreateItem(futureRelevanceHint: 0.9),
            CreateItem(futureRelevanceHint: 0.1)
        };

        var scoreSmall = _scorer.Score(item, smallList);
        var scoreLarge = _scorer.Score(item, largeList);

        await Assert.That(scoreSmall).IsEqualTo(0.5);
        await Assert.That(scoreLarge).IsEqualTo(0.5);
    }
}
