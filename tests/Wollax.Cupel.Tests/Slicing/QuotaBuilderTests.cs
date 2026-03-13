using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Slicing;

public class QuotaBuilderTests
{
    [Test]
    public async Task Build_EmptyBuilder_Throws()
    {
        var builder = new QuotaBuilder();

        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_RequireExceedsCap_SameKind_Throws()
    {
        var builder = new QuotaBuilder()
            .Require(ContextKind.Message, 60)
            .Cap(ContextKind.Message, 40);

        await Assert.That(() => builder.Build()).Throws<ArgumentException>();
    }

    [Test]
    public async Task Build_SumOfRequiresExceeds100_Throws()
    {
        var builder = new QuotaBuilder()
            .Require(ContextKind.Message, 60)
            .Require(ContextKind.Document, 50);

        await Assert.That(() => builder.Build()).Throws<ArgumentException>();
    }

    [Test]
    public async Task Build_SumOfRequiresExactly100_Succeeds()
    {
        var quotaSet = new QuotaBuilder()
            .Require(ContextKind.Message, 60)
            .Require(ContextKind.Document, 40)
            .Build();

        await Assert.That(quotaSet.GetRequire(ContextKind.Message)).IsEqualTo(60);
        await Assert.That(quotaSet.GetRequire(ContextKind.Document)).IsEqualTo(40);
    }

    [Test]
    public async Task Build_RequireAndCap_SameKind_Valid()
    {
        var quotaSet = new QuotaBuilder()
            .Require(ContextKind.Message, 20)
            .Cap(ContextKind.Message, 40)
            .Build();

        await Assert.That(quotaSet.GetRequire(ContextKind.Message)).IsEqualTo(20);
        await Assert.That(quotaSet.GetCap(ContextKind.Message)).IsEqualTo(40);
    }

    [Test]
    public async Task Build_CapOnly_Valid()
    {
        var quotaSet = new QuotaBuilder()
            .Cap(ContextKind.Message, 30)
            .Build();

        await Assert.That(quotaSet.GetRequire(ContextKind.Message)).IsEqualTo(0);
        await Assert.That(quotaSet.GetCap(ContextKind.Message)).IsEqualTo(30);
    }

    [Test]
    public async Task Build_RequireOnly_Valid()
    {
        var quotaSet = new QuotaBuilder()
            .Require(ContextKind.Message, 30)
            .Build();

        await Assert.That(quotaSet.GetRequire(ContextKind.Message)).IsEqualTo(30);
        await Assert.That(quotaSet.GetCap(ContextKind.Message)).IsEqualTo(100);
    }

    [Test]
    public async Task Build_DuplicateRequire_LastWins()
    {
        var quotaSet = new QuotaBuilder()
            .Require(ContextKind.Message, 10)
            .Require(ContextKind.Message, 25)
            .Build();

        await Assert.That(quotaSet.GetRequire(ContextKind.Message)).IsEqualTo(25);
    }

    [Test]
    public async Task Build_DuplicateCap_LastWins()
    {
        var quotaSet = new QuotaBuilder()
            .Cap(ContextKind.Message, 80)
            .Cap(ContextKind.Message, 50)
            .Build();

        await Assert.That(quotaSet.GetCap(ContextKind.Message)).IsEqualTo(50);
    }

    [Test]
    public async Task Require_NegativePercent_Throws()
    {
        var builder = new QuotaBuilder();

        await Assert.That(() => builder.Require(ContextKind.Message, -5))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Require_Over100Percent_Throws()
    {
        var builder = new QuotaBuilder();

        await Assert.That(() => builder.Require(ContextKind.Message, 101))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Cap_NegativePercent_Throws()
    {
        var builder = new QuotaBuilder();

        await Assert.That(() => builder.Cap(ContextKind.Message, -5))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Cap_Over100Percent_Throws()
    {
        var builder = new QuotaBuilder();

        await Assert.That(() => builder.Cap(ContextKind.Message, 101))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Cap_ZeroPercent_Valid()
    {
        var quotaSet = new QuotaBuilder()
            .Cap(ContextKind.Message, 0)
            .Build();

        await Assert.That(quotaSet.GetCap(ContextKind.Message)).IsEqualTo(0);
    }
}
