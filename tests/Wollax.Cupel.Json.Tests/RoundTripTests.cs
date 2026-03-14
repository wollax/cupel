using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Json;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Json.Tests;

public class RoundTripTests
{
    [Test]
    public async Task MinimalPolicy_RoundTrips()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Recency, 1.0)]);

        var json = CupelJsonSerializer.Serialize(policy);
        var deserialized = CupelJsonSerializer.Deserialize(json);

        await Assert.That(deserialized.Scorers).Count().IsEqualTo(1);
        await Assert.That(deserialized.Scorers[0].Type).IsEqualTo(ScorerType.Recency);
        await Assert.That(deserialized.Scorers[0].Weight).IsEqualTo(1.0);
        await Assert.That(deserialized.SlicerType).IsEqualTo(SlicerType.Greedy);
        await Assert.That(deserialized.PlacerType).IsEqualTo(PlacerType.Chronological);
        await Assert.That(deserialized.DeduplicationEnabled).IsTrue();
        await Assert.That(deserialized.OverflowStrategy).IsEqualTo(OverflowStrategy.Throw);
        await Assert.That(deserialized.KnapsackBucketSize).IsNull();
        await Assert.That(deserialized.Quotas).IsNull();
        await Assert.That(deserialized.Name).IsNull();
        await Assert.That(deserialized.Description).IsNull();
    }

    [Test]
    public async Task FullPolicy_RoundTrips()
    {
        var policy = new CupelPolicy(
            scorers:
            [
                new ScorerEntry(ScorerType.Recency, 2.0),
                new ScorerEntry(ScorerType.Kind, 1.5,
                    kindWeights: new Dictionary<ContextKind, double>
                    {
                        [ContextKind.Message] = 0.8,
                        [ContextKind.Document] = 1.2
                    }),
                new ScorerEntry(ScorerType.Tag, 1.0,
                    tagWeights: new Dictionary<string, double>
                    {
                        ["important"] = 2.0,
                        ["low"] = 0.5
                    })
            ],
            slicerType: SlicerType.Knapsack,
            placerType: PlacerType.UShaped,
            deduplicationEnabled: false,
            overflowStrategy: OverflowStrategy.Truncate,
            knapsackBucketSize: 50,
            quotas:
            [
                new QuotaEntry(ContextKind.Message, minPercent: 10, maxPercent: 60),
                new QuotaEntry(ContextKind.Document, maxPercent: 40)
            ],
            name: "Full Test Policy",
            description: "A policy with all options configured");

        var json = CupelJsonSerializer.Serialize(policy);
        var deserialized = CupelJsonSerializer.Deserialize(json);

        // Scorers
        await Assert.That(deserialized.Scorers).Count().IsEqualTo(3);
        await Assert.That(deserialized.Scorers[0].Type).IsEqualTo(ScorerType.Recency);
        await Assert.That(deserialized.Scorers[0].Weight).IsEqualTo(2.0);
        await Assert.That(deserialized.Scorers[1].Type).IsEqualTo(ScorerType.Kind);
        await Assert.That(deserialized.Scorers[1].Weight).IsEqualTo(1.5);
        await Assert.That(deserialized.Scorers[1].KindWeights).IsNotNull();
        await Assert.That(deserialized.Scorers[1].KindWeights![ContextKind.Message]).IsEqualTo(0.8);
        await Assert.That(deserialized.Scorers[1].KindWeights![ContextKind.Document]).IsEqualTo(1.2);
        await Assert.That(deserialized.Scorers[2].Type).IsEqualTo(ScorerType.Tag);
        await Assert.That(deserialized.Scorers[2].TagWeights).IsNotNull();
        await Assert.That(deserialized.Scorers[2].TagWeights!["important"]).IsEqualTo(2.0);
        await Assert.That(deserialized.Scorers[2].TagWeights!["low"]).IsEqualTo(0.5);

        // Strategy
        await Assert.That(deserialized.SlicerType).IsEqualTo(SlicerType.Knapsack);
        await Assert.That(deserialized.PlacerType).IsEqualTo(PlacerType.UShaped);
        await Assert.That(deserialized.DeduplicationEnabled).IsFalse();
        await Assert.That(deserialized.OverflowStrategy).IsEqualTo(OverflowStrategy.Truncate);
        await Assert.That(deserialized.KnapsackBucketSize).IsEqualTo(50);

        // Quotas
        await Assert.That(deserialized.Quotas).IsNotNull();
        await Assert.That(deserialized.Quotas!).Count().IsEqualTo(2);
        await Assert.That(deserialized.Quotas![0].Kind).IsEqualTo(ContextKind.Message);
        await Assert.That(deserialized.Quotas![0].MinPercent).IsEqualTo(10.0);
        await Assert.That(deserialized.Quotas![0].MaxPercent).IsEqualTo(60.0);
        await Assert.That(deserialized.Quotas![1].Kind).IsEqualTo(ContextKind.Document);
        await Assert.That(deserialized.Quotas![1].MinPercent).IsNull();
        await Assert.That(deserialized.Quotas![1].MaxPercent).IsEqualTo(40.0);

        // Metadata
        await Assert.That(deserialized.Name).IsEqualTo("Full Test Policy");
        await Assert.That(deserialized.Description).IsEqualTo("A policy with all options configured");
    }

    [Test]
    public async Task PolicyWithQuotas_RoundTrips()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Recency, 1.0)],
            quotas:
            [
                new QuotaEntry(ContextKind.Message, minPercent: 20, maxPercent: 80),
                new QuotaEntry(ContextKind.ToolOutput, minPercent: 5),
                new QuotaEntry(new ContextKind("Custom"), maxPercent: 30)
            ]);

        var json = CupelJsonSerializer.Serialize(policy);
        var deserialized = CupelJsonSerializer.Deserialize(json);

        await Assert.That(deserialized.Quotas).IsNotNull();
        await Assert.That(deserialized.Quotas!).Count().IsEqualTo(3);

        await Assert.That(deserialized.Quotas![0].Kind).IsEqualTo(ContextKind.Message);
        await Assert.That(deserialized.Quotas![0].MinPercent).IsEqualTo(20.0);
        await Assert.That(deserialized.Quotas![0].MaxPercent).IsEqualTo(80.0);

        await Assert.That(deserialized.Quotas![1].Kind).IsEqualTo(ContextKind.ToolOutput);
        await Assert.That(deserialized.Quotas![1].MinPercent).IsEqualTo(5.0);
        await Assert.That(deserialized.Quotas![1].MaxPercent).IsNull();

        await Assert.That(deserialized.Quotas![2].Kind).IsEqualTo(new ContextKind("Custom"));
        await Assert.That(deserialized.Quotas![2].MinPercent).IsNull();
        await Assert.That(deserialized.Quotas![2].MaxPercent).IsEqualTo(30.0);
    }

    [Test]
    public async Task EnumsSerializeAsCamelCaseStrings()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Recency, 1.0)],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.UShaped,
            overflowStrategy: OverflowStrategy.Truncate);

        var json = CupelJsonSerializer.Serialize(policy);

        await Assert.That(json).Contains("\"recency\"");
        await Assert.That(json).Contains("\"greedy\"");
        await Assert.That(json).Contains("\"uShaped\"");
        await Assert.That(json).Contains("\"truncate\"");

        // Ensure PascalCase is NOT present
        await Assert.That(json).DoesNotContain("\"Recency\"");
        await Assert.That(json).DoesNotContain("\"Greedy\"");
        await Assert.That(json).DoesNotContain("\"UShaped\"");
        await Assert.That(json).DoesNotContain("\"Truncate\"");
    }

    [Test]
    public async Task UnknownJsonProperties_AreIgnored()
    {
        var json = """
            {
                "scorers": [{"type": "recency", "weight": 1.0}],
                "slicerType": "greedy",
                "placerType": "chronological",
                "deduplicationEnabled": true,
                "overflowStrategy": "throw",
                "unknownProperty": "should be ignored",
                "anotherUnknown": 42
            }
            """;

        var deserialized = CupelJsonSerializer.Deserialize(json);

        await Assert.That(deserialized.Scorers).Count().IsEqualTo(1);
        await Assert.That(deserialized.Scorers[0].Type).IsEqualTo(ScorerType.Recency);
        await Assert.That(deserialized.SlicerType).IsEqualTo(SlicerType.Greedy);
    }

    [Test]
    public async Task NullOptionalFields_OmittedInJson()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Recency, 1.0)]);

        var json = CupelJsonSerializer.Serialize(policy);

        await Assert.That(json).DoesNotContain("\"quotas\"");
        await Assert.That(json).DoesNotContain("\"knapsackBucketSize\"");
        await Assert.That(json).DoesNotContain("\"name\"");
        await Assert.That(json).DoesNotContain("\"description\"");
        await Assert.That(json).DoesNotContain("\"kindWeights\"");
        await Assert.That(json).DoesNotContain("\"tagWeights\"");
    }

    [Test]
    public async Task ContextBudget_FullConfig_RoundTrips()
    {
        var budget = new ContextBudget(
            maxTokens: 8000,
            targetTokens: 6000,
            outputReserve: 1000,
            reservedSlots: new Dictionary<ContextKind, int>
            {
                [ContextKind.Message] = 100,
                [ContextKind.SystemPrompt] = 200
            },
            estimationSafetyMarginPercent: 10);

        var json = CupelJsonSerializer.Serialize(budget);
        var deserialized = CupelJsonSerializer.DeserializeBudget(json);

        await Assert.That(deserialized).IsEqualTo(budget);

        // Also verify individual properties
        await Assert.That(deserialized.MaxTokens).IsEqualTo(8000);
        await Assert.That(deserialized.TargetTokens).IsEqualTo(6000);
        await Assert.That(deserialized.OutputReserve).IsEqualTo(1000);
        await Assert.That(deserialized.ReservedSlots).Count().IsEqualTo(2);
        await Assert.That(deserialized.ReservedSlots[ContextKind.Message]).IsEqualTo(100);
        await Assert.That(deserialized.ReservedSlots[ContextKind.SystemPrompt]).IsEqualTo(200);
        await Assert.That(deserialized.EstimationSafetyMarginPercent).IsEqualTo(10.0);
    }

    [Test]
    public async Task ContextBudget_EmptyReservedSlots_RoundTrips()
    {
        var budget = new ContextBudget(
            maxTokens: 4000,
            targetTokens: 3000,
            reservedSlots: new Dictionary<ContextKind, int>());

        var json = CupelJsonSerializer.Serialize(budget);
        var deserialized = CupelJsonSerializer.DeserializeBudget(json);

        await Assert.That(deserialized).IsEqualTo(budget);
        await Assert.That(deserialized.ReservedSlots).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ContextBudget_MinimalConfig_RoundTrips()
    {
        var budget = new ContextBudget(maxTokens: 4096, targetTokens: 4096);

        var json = CupelJsonSerializer.Serialize(budget);
        var deserialized = CupelJsonSerializer.DeserializeBudget(json);

        await Assert.That(deserialized).IsEqualTo(budget);
        await Assert.That(deserialized.MaxTokens).IsEqualTo(4096);
        await Assert.That(deserialized.TargetTokens).IsEqualTo(4096);
        await Assert.That(deserialized.OutputReserve).IsEqualTo(0);
        await Assert.That(deserialized.ReservedSlots).Count().IsEqualTo(0);
        await Assert.That(deserialized.EstimationSafetyMarginPercent).IsEqualTo(0.0);
    }
}
