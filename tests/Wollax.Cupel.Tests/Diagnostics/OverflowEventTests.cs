using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Diagnostics;

public class OverflowEventTests
{
    [Test]
    public async Task InclusionReason_HasExpectedValues()
    {
        var values = Enum.GetValues<InclusionReason>();

        await Assert.That(values).Contains(InclusionReason.Scored);
        await Assert.That(values).Contains(InclusionReason.Pinned);
        await Assert.That(values).Contains(InclusionReason.ZeroToken);
        await Assert.That(values.Length).IsEqualTo(3);
    }

    [Test]
    public async Task ExclusionReason_HasExpected8Values()
    {
        var values = Enum.GetValues<ExclusionReason>();

        await Assert.That(values).Contains(ExclusionReason.BudgetExceeded);
        await Assert.That(values).Contains(ExclusionReason.ScoredTooLow);
        await Assert.That(values).Contains(ExclusionReason.Deduplicated);
        await Assert.That(values).Contains(ExclusionReason.QuotaCapExceeded);
        await Assert.That(values).Contains(ExclusionReason.QuotaRequireDisplaced);
        await Assert.That(values).Contains(ExclusionReason.NegativeTokens);
        await Assert.That(values).Contains(ExclusionReason.PinnedOverride);
        await Assert.That(values).Contains(ExclusionReason.Filtered);
        await Assert.That(values.Length).IsEqualTo(8);
    }

    [Test]
    public async Task OverflowStrategy_HasExpectedValues()
    {
        var values = Enum.GetValues<OverflowStrategy>();

        await Assert.That(values).Contains(OverflowStrategy.Throw);
        await Assert.That(values).Contains(OverflowStrategy.Truncate);
        await Assert.That(values).Contains(OverflowStrategy.Proceed);
        await Assert.That(values.Length).IsEqualTo(3);
    }

    [Test]
    public async Task OverflowEvent_Construction_StoresAllProperties()
    {
        var item = new ContextItem { Content = "test", Tokens = 100 };
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 800);

        var overflowEvent = new OverflowEvent
        {
            TokensOverBudget = 200,
            OverflowingItems = [item],
            Budget = budget
        };

        await Assert.That(overflowEvent.TokensOverBudget).IsEqualTo(200);
        await Assert.That(overflowEvent.OverflowingItems).Contains(item);
        await Assert.That(overflowEvent.OverflowingItems.Count).IsEqualTo(1);
        await Assert.That(overflowEvent.Budget).IsEqualTo(budget);
    }

    [Test]
    public async Task OverflowEvent_ValueEquality_SameValues_AreEqual()
    {
        var items = new[] { new ContextItem { Content = "test", Tokens = 100 } };
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 800);

        var a = new OverflowEvent
        {
            TokensOverBudget = 200,
            OverflowingItems = items,
            Budget = budget
        };
        var b = new OverflowEvent
        {
            TokensOverBudget = 200,
            OverflowingItems = items,
            Budget = budget
        };

        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task OverflowEvent_ValueInequality_DifferentTokensOverBudget()
    {
        var items = new[] { new ContextItem { Content = "test", Tokens = 100 } };
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 800);

        var a = new OverflowEvent
        {
            TokensOverBudget = 200,
            OverflowingItems = items,
            Budget = budget
        };
        var b = new OverflowEvent
        {
            TokensOverBudget = 300,
            OverflowingItems = items,
            Budget = budget
        };

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task OverflowEvent_IsSealed()
    {
        await Assert.That(typeof(OverflowEvent).IsSealed).IsTrue();
    }

    [Test]
    public async Task OverflowEvent_MultipleOverflowingItems()
    {
        var items = new[]
        {
            new ContextItem { Content = "a", Tokens = 100 },
            new ContextItem { Content = "b", Tokens = 200 }
        };
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 800);

        var overflowEvent = new OverflowEvent
        {
            TokensOverBudget = 300,
            OverflowingItems = items,
            Budget = budget
        };

        await Assert.That(overflowEvent.OverflowingItems.Count).IsEqualTo(2);
    }
}
