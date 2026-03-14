using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Diagnostics;

public class IncludedItemTests
{
    [Test]
    public async Task Construction_StoresAllProperties()
    {
        var item = new ContextItem { Content = "hello", Tokens = 50 };

        var included = new IncludedItem
        {
            Item = item,
            Score = 0.85,
            Reason = InclusionReason.Scored
        };

        await Assert.That(included.Item).IsEqualTo(item);
        await Assert.That(included.Score).IsEqualTo(0.85);
        await Assert.That(included.Reason).IsEqualTo(InclusionReason.Scored);
    }

    [Test]
    public async Task ValueEquality_SameValues_AreEqual()
    {
        var item = new ContextItem { Content = "hello", Tokens = 50 };

        var a = new IncludedItem
        {
            Item = item,
            Score = 0.85,
            Reason = InclusionReason.Scored
        };
        var b = new IncludedItem
        {
            Item = item,
            Score = 0.85,
            Reason = InclusionReason.Scored
        };

        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task ValueInequality_DifferentReason()
    {
        var item = new ContextItem { Content = "hello", Tokens = 50 };

        var a = new IncludedItem
        {
            Item = item,
            Score = 0.85,
            Reason = InclusionReason.Scored
        };
        var b = new IncludedItem
        {
            Item = item,
            Score = 0.85,
            Reason = InclusionReason.Pinned
        };

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task IsSealed()
    {
        await Assert.That(typeof(IncludedItem).IsSealed).IsTrue();
    }

    [Test]
    public async Task PinnedReason()
    {
        var item = new ContextItem { Content = "pinned", Tokens = 100, Pinned = true };

        var included = new IncludedItem
        {
            Item = item,
            Score = 1.0,
            Reason = InclusionReason.Pinned
        };

        await Assert.That(included.Reason).IsEqualTo(InclusionReason.Pinned);
    }

    [Test]
    public async Task ZeroTokenReason()
    {
        var item = new ContextItem { Content = "zero", Tokens = 0 };

        var included = new IncludedItem
        {
            Item = item,
            Score = 0.5,
            Reason = InclusionReason.ZeroToken
        };

        await Assert.That(included.Reason).IsEqualTo(InclusionReason.ZeroToken);
    }
}
