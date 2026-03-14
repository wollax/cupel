using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Policy;

public class CupelPolicyTests
{
    private static IReadOnlyList<ScorerEntry> MinimalScorers() =>
        [new ScorerEntry(ScorerType.Recency, 1.0)];

    [Test]
    public async Task MinimalConstruction_ScorersOnly_Works()
    {
        var policy = new CupelPolicy(MinimalScorers());

        await Assert.That(policy.Scorers.Count).IsEqualTo(1);
        await Assert.That(policy.Scorers[0].Type).IsEqualTo(ScorerType.Recency);
    }

    [Test]
    public async Task FullConstruction_AllParameters_ReturnsCorrectValues()
    {
        var scorers = new List<ScorerEntry>
        {
            new(ScorerType.Recency, 1.0),
            new(ScorerType.Priority, 2.0)
        };
        var quotas = new List<QuotaEntry>
        {
            new(ContextKind.Message, minPercent: 10.0)
        };

        var policy = new CupelPolicy(
            scorers: scorers,
            slicerType: SlicerType.Knapsack,
            placerType: PlacerType.UShaped,
            deduplicationEnabled: false,
            overflowStrategy: OverflowStrategy.Truncate,
            knapsackBucketSize: 50,
            quotas: quotas,
            name: "MyPolicy",
            description: "A test policy");

        await Assert.That(policy.Scorers.Count).IsEqualTo(2);
        await Assert.That(policy.SlicerType).IsEqualTo(SlicerType.Knapsack);
        await Assert.That(policy.PlacerType).IsEqualTo(PlacerType.UShaped);
        await Assert.That(policy.DeduplicationEnabled).IsFalse();
        await Assert.That(policy.OverflowStrategy).IsEqualTo(OverflowStrategy.Truncate);
        await Assert.That(policy.KnapsackBucketSize).IsEqualTo(50);
        await Assert.That(policy.Quotas).IsNotNull();
        await Assert.That(policy.Quotas!.Count).IsEqualTo(1);
        await Assert.That(policy.Name).IsEqualTo("MyPolicy");
        await Assert.That(policy.Description).IsEqualTo("A test policy");
    }

    [Test]
    public async Task Defaults_AreCorrect()
    {
        var policy = new CupelPolicy(MinimalScorers());

        await Assert.That(policy.SlicerType).IsEqualTo(SlicerType.Greedy);
        await Assert.That(policy.PlacerType).IsEqualTo(PlacerType.Chronological);
        await Assert.That(policy.DeduplicationEnabled).IsTrue();
        await Assert.That(policy.OverflowStrategy).IsEqualTo(OverflowStrategy.Throw);
        await Assert.That(policy.KnapsackBucketSize).IsNull();
        await Assert.That(policy.Quotas).IsNull();
        await Assert.That(policy.Name).IsNull();
        await Assert.That(policy.Description).IsNull();
    }

    [Test]
    public async Task Validation_NullScorers_Throws()
    {
        await Assert.That(() => new CupelPolicy(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Validation_EmptyScorers_Throws()
    {
        await Assert.That(() => new CupelPolicy(Array.Empty<ScorerEntry>()))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Validation_KnapsackBucketSizeWithGreedySlicer_Throws()
    {
        await Assert.That(() => new CupelPolicy(
                MinimalScorers(),
                slicerType: SlicerType.Greedy,
                knapsackBucketSize: 100))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Validation_KnapsackBucketSizeWithKnapsackSlicer_Succeeds()
    {
        var policy = new CupelPolicy(
            MinimalScorers(),
            slicerType: SlicerType.Knapsack,
            knapsackBucketSize: 100);

        await Assert.That(policy.KnapsackBucketSize).IsEqualTo(100);
    }

    [Test]
    public async Task Validation_KnapsackBucketSizeZero_Throws()
    {
        await Assert.That(() => new CupelPolicy(
                MinimalScorers(),
                slicerType: SlicerType.Knapsack,
                knapsackBucketSize: 0))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validation_KnapsackBucketSizeNegative_Throws()
    {
        await Assert.That(() => new CupelPolicy(
                MinimalScorers(),
                slicerType: SlicerType.Knapsack,
                knapsackBucketSize: -1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task DefensiveCopy_MutatingOriginalScorersList_DoesNotAffectPolicy()
    {
        var scorers = new List<ScorerEntry> { new(ScorerType.Recency, 1.0) };
        var policy = new CupelPolicy(scorers);

        scorers.Add(new ScorerEntry(ScorerType.Priority, 2.0));

        await Assert.That(policy.Scorers.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DefensiveCopy_MutatingOriginalQuotasList_DoesNotAffectPolicy()
    {
        var quotas = new List<QuotaEntry> { new(ContextKind.Message, minPercent: 10.0) };
        var policy = new CupelPolicy(MinimalScorers(), quotas: quotas);

        quotas.Add(new QuotaEntry(ContextKind.Document, maxPercent: 50.0));

        await Assert.That(policy.Quotas!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task NameAndDescription_AreOptional()
    {
        var policy = new CupelPolicy(MinimalScorers());

        await Assert.That(policy.Name).IsNull();
        await Assert.That(policy.Description).IsNull();
    }

    [Test]
    public async Task NameAndDescription_WhenProvided_ArePreserved()
    {
        var policy = new CupelPolicy(MinimalScorers(), name: "test", description: "desc");

        await Assert.That(policy.Name).IsEqualTo("test");
        await Assert.That(policy.Description).IsEqualTo("desc");
    }
}
