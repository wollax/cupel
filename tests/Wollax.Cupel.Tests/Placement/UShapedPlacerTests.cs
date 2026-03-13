using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Placement;

public class UShapedPlacerTests
{
    private static ContextItem CreateItem(string content, int tokens = 10) =>
        new() { Content = content, Tokens = tokens };

    private static ScoredItem CreateScored(ContextItem item, double score) =>
        new(item, score);

    private readonly UShapedPlacer _placer = new();

    [Test]
    public async Task EmptyInput_ReturnsEmpty()
    {
        var result = _placer.Place([], NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SingleItem_ReturnsSameItem()
    {
        var item = CreateItem("only");
        var scored = new List<ScoredItem> { CreateScored(item, 0.5) };

        var result = _placer.Place(scored, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(item);
    }

    [Test]
    public async Task TwoItems_ReturnsBothInScoreOrder()
    {
        var low = CreateItem("low");
        var high = CreateItem("high");
        var scored = new List<ScoredItem>
        {
            CreateScored(low, 0.3),
            CreateScored(high, 0.9)
        };

        var result = _placer.Place(scored, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(2);
        // Highest score at position 0 (left edge), lowest at position 1 (right edge)
        await Assert.That(result[0]).IsEqualTo(high);
        await Assert.That(result[1]).IsEqualTo(low);
    }

    [Test]
    public async Task HighestScored_AtEdges()
    {
        // 5 items with scores: 0.1, 0.3, 0.5, 0.7, 0.9
        var item1 = CreateItem("A"); // 0.9 -> highest
        var item2 = CreateItem("B"); // 0.7 -> 2nd highest
        var item3 = CreateItem("C"); // 0.5 -> 3rd
        var item4 = CreateItem("D"); // 0.3 -> 4th
        var item5 = CreateItem("E"); // 0.1 -> lowest

        var scored = new List<ScoredItem>
        {
            CreateScored(item1, 0.9),
            CreateScored(item2, 0.7),
            CreateScored(item3, 0.5),
            CreateScored(item4, 0.3),
            CreateScored(item5, 0.1)
        };

        var result = _placer.Place(scored, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(5);
        // Edges should have highest-scored items
        // Sort by score desc: A(0.9), B(0.7), C(0.5), D(0.3), E(0.1)
        // Even indices (0,2,4) go left: A at 0, C at 1, E at 2
        // Odd indices (1,3) go right: B at 4, D at 3
        // Result: [A, C, E, D, B]
        await Assert.That(result[0]).IsEqualTo(item1); // A at left edge
        await Assert.That(result[4]).IsEqualTo(item2); // B at right edge
    }

    [Test]
    public async Task EqualScores_StableOrder()
    {
        var item1 = CreateItem("first");
        var item2 = CreateItem("second");
        var item3 = CreateItem("third");
        var scored = new List<ScoredItem>
        {
            CreateScored(item1, 0.5),
            CreateScored(item2, 0.5),
            CreateScored(item3, 0.5)
        };

        var result = _placer.Place(scored, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(3);
        // All same score -> sort by index -> items 0,1,2
        // Even indices (0,2) go left: item1 at 0, item3 at 1
        // Odd index (1) goes right: item2 at 2
        await Assert.That(result[0]).IsEqualTo(item1);
        await Assert.That(result[1]).IsEqualTo(item3);
        await Assert.That(result[2]).IsEqualTo(item2);
    }
}
