using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Wollax.Cupel.Tests.Models;

public class ContextBudgetTests
{
    [Test]
    public async Task MinimalConstruction_MaxTokensAndTargetTokens_Works()
    {
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000);

        await Assert.That(budget.MaxTokens).IsEqualTo(100_000);
        await Assert.That(budget.TargetTokens).IsEqualTo(80_000);
    }

    [Test]
    public async Task FullConstruction_AllParameters_ReturnsCorrectValues()
    {
        var slots = new Dictionary<ContextKind, int>
        {
            [ContextKind.SystemPrompt] = 2_000,
            [ContextKind.Memory] = 5_000,
        };

        var budget = new ContextBudget(
            maxTokens: 128_000,
            targetTokens: 100_000,
            outputReserve: 4_096,
            reservedSlots: slots,
            estimationSafetyMarginPercent: 10.5);

        await Assert.That(budget.MaxTokens).IsEqualTo(128_000);
        await Assert.That(budget.TargetTokens).IsEqualTo(100_000);
        await Assert.That(budget.OutputReserve).IsEqualTo(4_096);
        await Assert.That(budget.ReservedSlots.Count).IsEqualTo(2);
        await Assert.That(budget.ReservedSlots[ContextKind.SystemPrompt]).IsEqualTo(2_000);
        await Assert.That(budget.ReservedSlots[ContextKind.Memory]).IsEqualTo(5_000);
        await Assert.That(budget.EstimationSafetyMarginPercent).IsEqualTo(10.5);
    }

    [Test]
    public async Task Defaults_OutputReserve_IsZero()
    {
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000);

        await Assert.That(budget.OutputReserve).IsEqualTo(0);
    }

    [Test]
    public async Task Defaults_ReservedSlots_IsEmpty()
    {
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000);

        await Assert.That(budget.ReservedSlots).IsEmpty();
    }

    [Test]
    public async Task Defaults_EstimationSafetyMarginPercent_IsZero()
    {
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000);

        await Assert.That(budget.EstimationSafetyMarginPercent).IsEqualTo(0);
    }

    [Test]
    public async Task Validation_NegativeMaxTokens_Throws()
    {
        await Assert.That(() => new ContextBudget(maxTokens: -1, targetTokens: 0))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_NegativeTargetTokens_Throws()
    {
        await Assert.That(() => new ContextBudget(maxTokens: 100_000, targetTokens: -1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_NegativeOutputReserve_Throws()
    {
        await Assert.That(() => new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, outputReserve: -1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_NegativeEstimationSafetyMargin_Throws()
    {
        await Assert.That(() => new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, estimationSafetyMarginPercent: -0.01))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_EstimationSafetyMarginOver100_Throws()
    {
        await Assert.That(() => new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, estimationSafetyMarginPercent: 100.01))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_TargetTokensExceedsMaxTokens_Throws()
    {
        await Assert.That(() => new ContextBudget(maxTokens: 100_000, targetTokens: 100_001))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Validation_OutputReserveExceedsMaxTokens_Throws()
    {
        await Assert.That(() => new ContextBudget(maxTokens: 100, targetTokens: 50, outputReserve: 101))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Validation_NegativeReservedSlotValue_Throws()
    {
        var slots = new Dictionary<ContextKind, int>
        {
            [ContextKind.SystemPrompt] = -1,
        };

        await Assert.That(() => new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, reservedSlots: slots))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_OutputReserveEqualsMaxTokens_IsValid()
    {
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50, outputReserve: 100);

        await Assert.That(budget.OutputReserve).IsEqualTo(100);
    }

    [Test]
    public async Task Equality_IdenticalBudgets_AreEqual()
    {
        var slots = new Dictionary<ContextKind, int> { [ContextKind.SystemPrompt] = 2_000 };

        var a = new ContextBudget(maxTokens: 128_000, targetTokens: 100_000, outputReserve: 4_096,
            reservedSlots: slots, estimationSafetyMarginPercent: 10.5);
        var b = new ContextBudget(maxTokens: 128_000, targetTokens: 100_000, outputReserve: 4_096,
            reservedSlots: new Dictionary<ContextKind, int> { [ContextKind.SystemPrompt] = 2_000 },
            estimationSafetyMarginPercent: 10.5);

        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task Equality_DifferentMaxTokens_AreNotEqual()
    {
        var a = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000);
        var b = new ContextBudget(maxTokens: 200_000, targetTokens: 80_000);

        await Assert.That(a.Equals(b)).IsFalse();
    }

    [Test]
    public async Task Equality_DifferentReservedSlots_AreNotEqual()
    {
        var a = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000,
            reservedSlots: new Dictionary<ContextKind, int> { [ContextKind.SystemPrompt] = 2_000 });
        var b = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000,
            reservedSlots: new Dictionary<ContextKind, int> { [ContextKind.SystemPrompt] = 3_000 });

        await Assert.That(a.Equals(b)).IsFalse();
    }

    [Test]
    public async Task Equality_NullOther_ReturnsFalse()
    {
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000);

        await Assert.That(budget.Equals((ContextBudget?)null)).IsFalse();
    }

    [Test]
    public async Task EdgeCase_ZeroMaxTokensAndZeroTargetTokens_IsValid()
    {
        var budget = new ContextBudget(maxTokens: 0, targetTokens: 0);

        await Assert.That(budget.MaxTokens).IsEqualTo(0);
        await Assert.That(budget.TargetTokens).IsEqualTo(0);
    }

    [Test]
    public async Task EdgeCase_EstimationSafetyMarginZero_IsValid()
    {
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, estimationSafetyMarginPercent: 0);

        await Assert.That(budget.EstimationSafetyMarginPercent).IsEqualTo(0);
    }

    [Test]
    public async Task EdgeCase_EstimationSafetyMargin100_IsValid()
    {
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, estimationSafetyMarginPercent: 100);

        await Assert.That(budget.EstimationSafetyMarginPercent).IsEqualTo(100);
    }

    [Test]
    public async Task EdgeCase_ReservedSlotsWithContextKindKeys_Works()
    {
        var slots = new Dictionary<ContextKind, int>
        {
            [ContextKind.Document] = 10_000,
            [new ContextKind("Custom")] = 3_000,
        };

        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, reservedSlots: slots);

        await Assert.That(budget.ReservedSlots[ContextKind.Document]).IsEqualTo(10_000);
        await Assert.That(budget.ReservedSlots[new ContextKind("Custom")]).IsEqualTo(3_000);
    }

    [Test]
    public async Task JsonRoundTrip_PreservesAllProperties()
    {
        var slots = new Dictionary<ContextKind, int>
        {
            [ContextKind.SystemPrompt] = 2_000,
            [ContextKind.Memory] = 5_000,
        };

        var original = new ContextBudget(
            maxTokens: 128_000,
            targetTokens: 100_000,
            outputReserve: 4_096,
            reservedSlots: slots,
            estimationSafetyMarginPercent: 10.5);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ContextBudget>(json);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.MaxTokens).IsEqualTo(original.MaxTokens);
        await Assert.That(deserialized.TargetTokens).IsEqualTo(original.TargetTokens);
        await Assert.That(deserialized.OutputReserve).IsEqualTo(original.OutputReserve);
        await Assert.That(deserialized.EstimationSafetyMarginPercent).IsEqualTo(original.EstimationSafetyMarginPercent);
        await Assert.That(deserialized.ReservedSlots.Count).IsEqualTo(2);
        await Assert.That(deserialized.ReservedSlots[ContextKind.SystemPrompt]).IsEqualTo(2_000);
        await Assert.That(deserialized.ReservedSlots[ContextKind.Memory]).IsEqualTo(5_000);
    }

    [Test]
    public async Task JsonSerialization_ReservedSlots_UsesContextKindStringsAsKeys()
    {
        var slots = new Dictionary<ContextKind, int>
        {
            [ContextKind.SystemPrompt] = 2_000,
        };

        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, reservedSlots: slots);
        var json = JsonSerializer.Serialize(budget);

        await Assert.That(json).Contains("\"SystemPrompt\"");
    }

    [Test]
    public async Task JsonSerialization_UsesCamelCasePropertyNames()
    {
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000);
        var json = JsonSerializer.Serialize(budget);

        await Assert.That(json).Contains("\"maxTokens\"");
        await Assert.That(json).Contains("\"targetTokens\"");
        await Assert.That(json).Contains("\"outputReserve\"");
        await Assert.That(json).Contains("\"reservedSlots\"");
        await Assert.That(json).Contains("\"estimationSafetyMarginPercent\"");
    }

    [Test]
    public async Task JsonRoundTrip_WithDefaults_PreservesDefaults()
    {
        var original = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000);
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ContextBudget>(json);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.OutputReserve).IsEqualTo(0);
        await Assert.That(deserialized.ReservedSlots).IsEmpty();
        await Assert.That(deserialized.EstimationSafetyMarginPercent).IsEqualTo(0);
    }

    [Test]
    public async Task TotalReserved_WithSlots_ReturnsOutputReservePlusSlotSum()
    {
        var slots = new Dictionary<ContextKind, int>
        {
            [ContextKind.SystemPrompt] = 2_000,
            [ContextKind.Memory] = 3_000,
        };
        var budget = new ContextBudget(maxTokens: 128_000, targetTokens: 100_000, outputReserve: 4_096, reservedSlots: slots);

        await Assert.That(budget.TotalReserved).IsEqualTo(4_096 + 2_000 + 3_000);
    }

    [Test]
    public async Task TotalReserved_NoSlots_ReturnsOutputReserve()
    {
        var budget = new ContextBudget(maxTokens: 4_096, targetTokens: 3_000, outputReserve: 1_024);

        await Assert.That(budget.TotalReserved).IsEqualTo(1_024);
    }

    [Test]
    public async Task UnreservedCapacity_Positive()
    {
        var slots = new Dictionary<ContextKind, int>
        {
            [ContextKind.SystemPrompt] = 2_000,
            [ContextKind.Memory] = 3_000,
        };
        var budget = new ContextBudget(maxTokens: 128_000, targetTokens: 100_000, outputReserve: 4_096, reservedSlots: slots);

        await Assert.That(budget.UnreservedCapacity).IsEqualTo(128_000 - 4_096 - 5_000);
    }

    [Test]
    public async Task UnreservedCapacity_Negative_OverCommitted()
    {
        var slots = new Dictionary<ContextKind, int>
        {
            [ContextKind.Message] = 90_000,
        };
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, outputReserve: 20_000, reservedSlots: slots);

        await Assert.That(budget.UnreservedCapacity).IsEqualTo(-10_000);
    }

    [Test]
    public async Task HasCapacity_True_WhenPositive()
    {
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, outputReserve: 1_000);

        await Assert.That(budget.HasCapacity).IsTrue();
    }

    [Test]
    public async Task HasCapacity_False_WhenZero()
    {
        var slots = new Dictionary<ContextKind, int>
        {
            [ContextKind.Message] = 80_000,
        };
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, outputReserve: 20_000, reservedSlots: slots);

        await Assert.That(budget.HasCapacity).IsFalse();
    }

    [Test]
    public async Task HasCapacity_False_WhenNegative()
    {
        var slots = new Dictionary<ContextKind, int>
        {
            [ContextKind.Message] = 90_000,
        };
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, outputReserve: 20_000, reservedSlots: slots);

        await Assert.That(budget.HasCapacity).IsFalse();
    }

    [Test]
    public async Task JsonSerialization_ComputedProperties_NotSerialized()
    {
        var budget = new ContextBudget(maxTokens: 100_000, targetTokens: 80_000, outputReserve: 1_000);
        var json = JsonSerializer.Serialize(budget);

        await Assert.That(json).DoesNotContain("totalReserved");
        await Assert.That(json).DoesNotContain("unreservedCapacity");
        await Assert.That(json).DoesNotContain("hasCapacity");
    }
}
