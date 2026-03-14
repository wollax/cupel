using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Diagnostics;

public class ExcludedItemTests
{
    [Test]
    public async Task Construction_StoresAllProperties()
    {
        var item = new ContextItem { Content = "excluded", Tokens = 200 };

        var excluded = new ExcludedItem
        {
            Item = item,
            Score = 0.3,
            Reason = ExclusionReason.BudgetExceeded
        };

        await Assert.That(excluded.Item).IsEqualTo(item);
        await Assert.That(excluded.Score).IsEqualTo(0.3);
        await Assert.That(excluded.Reason).IsEqualTo(ExclusionReason.BudgetExceeded);
    }

    [Test]
    public async Task DeduplicatedAgainst_IsNullByDefault()
    {
        var item = new ContextItem { Content = "excluded", Tokens = 200 };

        var excluded = new ExcludedItem
        {
            Item = item,
            Score = 0.3,
            Reason = ExclusionReason.BudgetExceeded
        };

        await Assert.That(excluded.DeduplicatedAgainst).IsNull();
    }

    [Test]
    public async Task DeduplicatedAgainst_CanBeSet()
    {
        var item = new ContextItem { Content = "dup", Tokens = 100 };
        var winner = new ContextItem { Content = "dup", Tokens = 100 };

        var excluded = new ExcludedItem
        {
            Item = item,
            Score = 0.3,
            Reason = ExclusionReason.Deduplicated,
            DeduplicatedAgainst = winner
        };

        await Assert.That(excluded.DeduplicatedAgainst).IsNotNull();
        await Assert.That(excluded.DeduplicatedAgainst).IsEqualTo(winner);
    }

    [Test]
    public async Task ValueEquality_SameValues_AreEqual()
    {
        var item = new ContextItem { Content = "test", Tokens = 50 };

        var a = new ExcludedItem
        {
            Item = item,
            Score = 0.2,
            Reason = ExclusionReason.ScoredTooLow
        };
        var b = new ExcludedItem
        {
            Item = item,
            Score = 0.2,
            Reason = ExclusionReason.ScoredTooLow
        };

        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task ValueInequality_DifferentReason()
    {
        var item = new ContextItem { Content = "test", Tokens = 50 };

        var a = new ExcludedItem
        {
            Item = item,
            Score = 0.2,
            Reason = ExclusionReason.ScoredTooLow
        };
        var b = new ExcludedItem
        {
            Item = item,
            Score = 0.2,
            Reason = ExclusionReason.BudgetExceeded
        };

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task IsSealed()
    {
        await Assert.That(typeof(ExcludedItem).IsSealed).IsTrue();
    }
}
