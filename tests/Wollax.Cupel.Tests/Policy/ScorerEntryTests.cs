using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Wollax.Cupel.Tests.Policy;

public class ScorerEntryTests
{
    [Test]
    [Arguments(ScorerType.Recency)]
    [Arguments(ScorerType.Priority)]
    [Arguments(ScorerType.Kind)]
    [Arguments(ScorerType.Frequency)]
    [Arguments(ScorerType.Reflexive)]
    public async Task ValidConstruction_EachScorerType_Works(ScorerType type)
    {
        var entry = new ScorerEntry(type, 1.0);

        await Assert.That(entry.Type).IsEqualTo(type);
        await Assert.That(entry.Weight).IsEqualTo(1.0);
        await Assert.That(entry.KindWeights).IsNull();
        await Assert.That(entry.TagWeights).IsNull();
    }

    [Test]
    public async Task ValidConstruction_TagTypeWithTagWeights_Works()
    {
        var tagWeights = new Dictionary<string, double> { ["important"] = 2.0, ["debug"] = 0.5 };
        var entry = new ScorerEntry(ScorerType.Tag, 1.5, tagWeights: tagWeights);

        await Assert.That(entry.Type).IsEqualTo(ScorerType.Tag);
        await Assert.That(entry.Weight).IsEqualTo(1.5);
        await Assert.That(entry.TagWeights).IsNotNull();
        await Assert.That(entry.TagWeights!["important"]).IsEqualTo(2.0);
        await Assert.That(entry.TagWeights!["debug"]).IsEqualTo(0.5);
    }

    [Test]
    public async Task ValidConstruction_KindTypeWithKindWeights_Works()
    {
        var kindWeights = new Dictionary<ContextKind, double>
        {
            [ContextKind.Message] = 1.0,
            [ContextKind.Document] = 0.8
        };
        var entry = new ScorerEntry(ScorerType.Kind, 2.0, kindWeights: kindWeights);

        await Assert.That(entry.Type).IsEqualTo(ScorerType.Kind);
        await Assert.That(entry.KindWeights).IsNotNull();
        await Assert.That(entry.KindWeights![ContextKind.Message]).IsEqualTo(1.0);
    }

    [Test]
    public async Task ValidConstruction_KindTypeWithoutKindWeights_UsesDefaults()
    {
        var entry = new ScorerEntry(ScorerType.Kind, 1.0);

        await Assert.That(entry.Type).IsEqualTo(ScorerType.Kind);
        await Assert.That(entry.KindWeights).IsNull();
    }

    [Test]
    public async Task Validation_ZeroWeight_Throws()
    {
        await Assert.That(() => new ScorerEntry(ScorerType.Recency, 0.0))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_NegativeWeight_Throws()
    {
        await Assert.That(() => new ScorerEntry(ScorerType.Recency, -1.0))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_NaNWeight_Throws()
    {
        await Assert.That(() => new ScorerEntry(ScorerType.Recency, double.NaN))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_PositiveInfinityWeight_Throws()
    {
        await Assert.That(() => new ScorerEntry(ScorerType.Recency, double.PositiveInfinity))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_NegativeInfinityWeight_Throws()
    {
        await Assert.That(() => new ScorerEntry(ScorerType.Recency, double.NegativeInfinity))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_TagTypeWithoutTagWeights_Throws()
    {
        await Assert.That(() => new ScorerEntry(ScorerType.Tag, 1.0))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task DefensiveCopy_MutatingOriginalDictionary_DoesNotAffectEntry()
    {
        var tagWeights = new Dictionary<string, double> { ["important"] = 2.0 };
        var entry = new ScorerEntry(ScorerType.Tag, 1.0, tagWeights: tagWeights);

        tagWeights["important"] = 999.0;
        tagWeights["sneaky"] = 1.0;

        await Assert.That(entry.TagWeights!["important"]).IsEqualTo(2.0);
        await Assert.That(entry.TagWeights!.ContainsKey("sneaky")).IsFalse();
    }

    [Test]
    public async Task DefensiveCopy_MutatingOriginalKindWeights_DoesNotAffectEntry()
    {
        var kindWeights = new Dictionary<ContextKind, double> { [ContextKind.Message] = 1.0 };
        var entry = new ScorerEntry(ScorerType.Kind, 1.0, kindWeights: kindWeights);

        kindWeights[ContextKind.Message] = 999.0;

        await Assert.That(entry.KindWeights![ContextKind.Message]).IsEqualTo(1.0);
    }

    // Scaled type tests

    [Test]
    public async Task ValidConstruction_ScaledTypeWithInnerScorer_Works()
    {
        var inner = new ScorerEntry(ScorerType.Recency, 1.0);
        var entry = new ScorerEntry(ScorerType.Scaled, 2.0, innerScorer: inner);

        await Assert.That(entry.Type).IsEqualTo(ScorerType.Scaled);
        await Assert.That(entry.Weight).IsEqualTo(2.0);
        await Assert.That(entry.InnerScorer).IsNotNull();
        await Assert.That(entry.InnerScorer!.Type).IsEqualTo(ScorerType.Recency);
    }

    [Test]
    public async Task Validation_ScaledTypeWithoutInnerScorer_Throws()
    {
        await Assert.That(() => new ScorerEntry(ScorerType.Scaled, 1.0))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Validation_NonScaledTypeWithInnerScorer_Throws()
    {
        var inner = new ScorerEntry(ScorerType.Recency, 1.0);

        await Assert.That(() => new ScorerEntry(ScorerType.Recency, 1.0, innerScorer: inner))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task ValidConstruction_NestedScaledScorers_Works()
    {
        var leaf = new ScorerEntry(ScorerType.Recency, 1.0);
        var middle = new ScorerEntry(ScorerType.Scaled, 1.0, innerScorer: leaf);
        var outer = new ScorerEntry(ScorerType.Scaled, 1.0, innerScorer: middle);

        await Assert.That(outer.Type).IsEqualTo(ScorerType.Scaled);
        await Assert.That(outer.InnerScorer).IsNotNull();
        await Assert.That(outer.InnerScorer!.Type).IsEqualTo(ScorerType.Scaled);
        await Assert.That(outer.InnerScorer!.InnerScorer).IsNotNull();
        await Assert.That(outer.InnerScorer!.InnerScorer!.Type).IsEqualTo(ScorerType.Recency);
    }

    [Test]
    public async Task ValidConstruction_NonScaledTypes_InnerScorerDefaultsToNull()
    {
        var entry = new ScorerEntry(ScorerType.Recency, 1.0);

        await Assert.That(entry.InnerScorer).IsNull();
    }
}
