using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Policy;

public class QuotaEntryTests
{
    [Test]
    public async Task ValidConstruction_MinPercentOnly_Works()
    {
        var entry = new QuotaEntry(ContextKind.Message, minPercent: 10.0);

        await Assert.That(entry.Kind).IsEqualTo(ContextKind.Message);
        await Assert.That(entry.MinPercent).IsEqualTo(10.0);
        await Assert.That(entry.MaxPercent).IsNull();
    }

    [Test]
    public async Task ValidConstruction_MaxPercentOnly_Works()
    {
        var entry = new QuotaEntry(ContextKind.Document, maxPercent: 50.0);

        await Assert.That(entry.Kind).IsEqualTo(ContextKind.Document);
        await Assert.That(entry.MinPercent).IsNull();
        await Assert.That(entry.MaxPercent).IsEqualTo(50.0);
    }

    [Test]
    public async Task ValidConstruction_BothMinAndMax_Works()
    {
        var entry = new QuotaEntry(ContextKind.ToolOutput, minPercent: 10.0, maxPercent: 40.0);

        await Assert.That(entry.Kind).IsEqualTo(ContextKind.ToolOutput);
        await Assert.That(entry.MinPercent).IsEqualTo(10.0);
        await Assert.That(entry.MaxPercent).IsEqualTo(40.0);
    }

    [Test]
    public async Task ValidConstruction_BoundaryValues_Work()
    {
        var entry = new QuotaEntry(ContextKind.Memory, minPercent: 0.0, maxPercent: 100.0);

        await Assert.That(entry.MinPercent).IsEqualTo(0.0);
        await Assert.That(entry.MaxPercent).IsEqualTo(100.0);
    }

    [Test]
    public async Task Validation_NeitherSpecified_Throws()
    {
        await Assert.That(() => new QuotaEntry(ContextKind.Message))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Validation_MinPercentNegative_Throws()
    {
        await Assert.That(() => new QuotaEntry(ContextKind.Message, minPercent: -0.01))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_MinPercentOver100_Throws()
    {
        await Assert.That(() => new QuotaEntry(ContextKind.Message, minPercent: 100.01))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_MaxPercentNegative_Throws()
    {
        await Assert.That(() => new QuotaEntry(ContextKind.Message, maxPercent: -0.01))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_MaxPercentOver100_Throws()
    {
        await Assert.That(() => new QuotaEntry(ContextKind.Message, maxPercent: 100.01))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_MinGreaterThanMax_Throws()
    {
        await Assert.That(() => new QuotaEntry(ContextKind.Message, minPercent: 60.0, maxPercent: 40.0))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Validation_MinEqualsMax_IsValid()
    {
        var entry = new QuotaEntry(ContextKind.Message, minPercent: 50.0, maxPercent: 50.0);

        await Assert.That(entry.MinPercent).IsEqualTo(50.0);
        await Assert.That(entry.MaxPercent).IsEqualTo(50.0);
    }
}
