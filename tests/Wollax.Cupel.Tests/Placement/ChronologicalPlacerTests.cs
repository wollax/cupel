using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Placement;

public class ChronologicalPlacerTests
{
    private static ContextItem CreateItem(
        string content,
        int tokens = 10,
        DateTimeOffset? timestamp = null) =>
        new() { Content = content, Tokens = tokens, Timestamp = timestamp };

    private static ScoredItem CreateScored(ContextItem item, double score) =>
        new(item, score);

    private readonly ChronologicalPlacer _placer = new();

    [Test]
    public async Task EmptyInput_ReturnsEmpty()
    {
        var result = _placer.Place([], NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SingleItem_ReturnsSameItem()
    {
        var item = CreateItem("only", timestamp: DateTimeOffset.UtcNow);
        var scored = new List<ScoredItem> { CreateScored(item, 0.5) };

        var result = _placer.Place(scored, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(item);
    }

    [Test]
    public async Task OrdersByTimestamp_Ascending()
    {
        var now = DateTimeOffset.UtcNow;
        var t1 = CreateItem("T1", timestamp: now.AddHours(-2));
        var t2 = CreateItem("T2", timestamp: now.AddHours(-1));
        var t3 = CreateItem("T3", timestamp: now);

        // Input in reverse order
        var scored = new List<ScoredItem>
        {
            CreateScored(t3, 0.5),
            CreateScored(t1, 0.5),
            CreateScored(t2, 0.5)
        };

        var result = _placer.Place(scored, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0]).IsEqualTo(t1);
        await Assert.That(result[1]).IsEqualTo(t2);
        await Assert.That(result[2]).IsEqualTo(t3);
    }

    [Test]
    public async Task NullTimestamps_SortToEnd()
    {
        var now = DateTimeOffset.UtcNow;
        var t1 = CreateItem("T1", timestamp: now.AddHours(-1));
        var nullItem = CreateItem("null-ts");
        var t2 = CreateItem("T2", timestamp: now);

        var scored = new List<ScoredItem>
        {
            CreateScored(t1, 0.5),
            CreateScored(nullItem, 0.5),
            CreateScored(t2, 0.5)
        };

        var result = _placer.Place(scored, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0]).IsEqualTo(t1);
        await Assert.That(result[1]).IsEqualTo(t2);
        await Assert.That(result[2]).IsEqualTo(nullItem);
    }

    [Test]
    public async Task AllNullTimestamps_PreservesOriginalOrder()
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
        await Assert.That(result[0]).IsEqualTo(item1);
        await Assert.That(result[1]).IsEqualTo(item2);
        await Assert.That(result[2]).IsEqualTo(item3);
    }

    [Test]
    public async Task MixedTimestampsAndNull_StableWithinGroups()
    {
        var now = DateTimeOffset.UtcNow;
        var nullA = CreateItem("null-A");
        var nullB = CreateItem("null-B");
        var t1 = CreateItem("T1", timestamp: now);

        var scored = new List<ScoredItem>
        {
            CreateScored(nullA, 0.5),
            CreateScored(t1, 0.5),
            CreateScored(nullB, 0.5)
        };

        var result = _placer.Place(scored, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0]).IsEqualTo(t1);
        // Null items maintain relative order: nullA before nullB
        await Assert.That(result[1]).IsEqualTo(nullA);
        await Assert.That(result[2]).IsEqualTo(nullB);
    }
}
