using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Slicing;

public class QuotaSetTests
{
    [Test]
    public async Task ExposesRequireAndCap()
    {
        var quotaSet = new QuotaBuilder()
            .Require(ContextKind.Message, 20)
            .Cap(ContextKind.Document, 50)
            .Build();

        await Assert.That(quotaSet.GetRequire(ContextKind.Message)).IsEqualTo(20);
        await Assert.That(quotaSet.GetCap(ContextKind.Document)).IsEqualTo(50);
    }

    [Test]
    public async Task DefaultRequireIsZero()
    {
        var quotaSet = new QuotaBuilder()
            .Cap(ContextKind.Message, 50)
            .Build();

        await Assert.That(quotaSet.GetRequire(ContextKind.Document)).IsEqualTo(0);
    }

    [Test]
    public async Task DefaultCapIs100()
    {
        var quotaSet = new QuotaBuilder()
            .Require(ContextKind.Message, 20)
            .Build();

        await Assert.That(quotaSet.GetCap(ContextKind.Document)).IsEqualTo(100);
    }

    [Test]
    public async Task KindsProperty_ReturnsAllConfiguredKinds()
    {
        var quotaSet = new QuotaBuilder()
            .Require(ContextKind.Message, 20)
            .Cap(ContextKind.Document, 50)
            .Require(ContextKind.ToolOutput, 10)
            .Build();

        await Assert.That(quotaSet.Kinds.Count).IsEqualTo(3);

        var kinds = new HashSet<ContextKind>(quotaSet.Kinds);
        await Assert.That(kinds.Contains(ContextKind.Message)).IsTrue();
        await Assert.That(kinds.Contains(ContextKind.Document)).IsTrue();
        await Assert.That(kinds.Contains(ContextKind.ToolOutput)).IsTrue();
    }
}
