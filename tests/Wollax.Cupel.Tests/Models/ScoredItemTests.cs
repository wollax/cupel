using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Wollax.Cupel.Tests.Models;

public class ScoredItemTests
{
    private static ContextItem CreateItem(string content = "test", int tokens = 10) =>
        new() { Content = content, Tokens = tokens };

    [Test]
    public async Task Construction_StoresItemAndScore()
    {
        var item = CreateItem();
        var scored = new ScoredItem(item, 0.85);

        await Assert.That(scored.Item).IsEqualTo(item);
        await Assert.That(scored.Score).IsEqualTo(0.85);
    }

    [Test]
    public async Task ValueEquality_SameItemAndScore_AreEqual()
    {
        var item = CreateItem();
        var a = new ScoredItem(item, 0.85);
        var b = new ScoredItem(item, 0.85);

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task ValueInequality_DifferentScore_AreNotEqual()
    {
        var item = CreateItem();
        var a = new ScoredItem(item, 0.85);
        var b = new ScoredItem(item, 0.50);

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task IsReadonlyRecordStruct()
    {
        await Assert.That(typeof(ScoredItem).IsValueType).IsTrue();
    }

    [Test]
    public async Task Deconstruction_Works()
    {
        var item = CreateItem();
        var scored = new ScoredItem(item, 0.75);

        var (deconstructedItem, deconstructedScore) = scored;

        await Assert.That(deconstructedItem).IsEqualTo(item);
        await Assert.That(deconstructedScore).IsEqualTo(0.75);
    }
}
