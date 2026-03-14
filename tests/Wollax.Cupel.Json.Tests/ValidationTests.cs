using System.Text.Json;
using Wollax.Cupel.Json;

namespace Wollax.Cupel.Json.Tests;

public class ValidationTests
{
    [Test]
    public async Task Deserialize_EmptyScorers_ThrowsWithPath()
    {
        var json = """
            {
                "scorers": [],
                "slicerType": "greedy"
            }
            """;

        var action = () => CupelJsonSerializer.Deserialize(json);

        var ex = await Assert.That(action).Throws<JsonException>();
        await Assert.That(ex!.Message).Contains("at least one entry");
    }

    [Test]
    public async Task Deserialize_NegativeWeight_ThrowsWithPath()
    {
        var json = """
            {
                "scorers": [{"type": "recency", "weight": -0.5}]
            }
            """;

        var action = () => CupelJsonSerializer.Deserialize(json);

        var ex = await Assert.That(action).Throws<JsonException>();
        await Assert.That(ex!.Message).Contains("-0.5");
    }

    [Test]
    public async Task Deserialize_ZeroWeight_ThrowsWithPath()
    {
        var json = """
            {
                "scorers": [{"type": "recency", "weight": 0}]
            }
            """;

        var action = () => CupelJsonSerializer.Deserialize(json);

        var ex = await Assert.That(action).Throws<JsonException>();
        await Assert.That(ex!.Message).Contains("weight");
    }

    [Test]
    public async Task Deserialize_KnapsackBucketSizeWithGreedySlicer_ThrowsWithPath()
    {
        var json = """
            {
                "scorers": [{"type": "recency", "weight": 1.0}],
                "slicerType": "greedy",
                "knapsackBucketSize": 100
            }
            """;

        var action = () => CupelJsonSerializer.Deserialize(json);

        var ex = await Assert.That(action).Throws<JsonException>();
        await Assert.That(ex!.Message).Contains("KnapsackBucketSize");
    }

    [Test]
    public async Task Deserialize_NegativeKnapsackBucketSize_ThrowsWithPath()
    {
        var json = """
            {
                "scorers": [{"type": "recency", "weight": 1.0}],
                "slicerType": "knapsack",
                "knapsackBucketSize": -1
            }
            """;

        var action = () => CupelJsonSerializer.Deserialize(json);

        var ex = await Assert.That(action).Throws<JsonException>();
        await Assert.That(ex!.Message).Contains("knapsackBucketSize");
    }

    [Test]
    public async Task Deserialize_TagScorerWithoutTagWeights_ThrowsWithPath()
    {
        var json = """
            {
                "scorers": [{"type": "tag", "weight": 1.0}]
            }
            """;

        var action = () => CupelJsonSerializer.Deserialize(json);

        var ex = await Assert.That(action).Throws<JsonException>();
        await Assert.That(ex!.Message).Contains("TagWeights");
    }

    [Test]
    public async Task Deserialize_QuotaMinExceedsMax_ThrowsWithPath()
    {
        var json = """
            {
                "scorers": [{"type": "recency", "weight": 1.0}],
                "quotas": [{"kind": "message", "minPercent": 80, "maxPercent": 20}]
            }
            """;

        var action = () => CupelJsonSerializer.Deserialize(json);

        var ex = await Assert.That(action).Throws<JsonException>();
        await Assert.That(ex!.Message).Contains("MinPercent");
    }

    [Test]
    public async Task Deserialize_BudgetTargetExceedsMax_ThrowsWithPath()
    {
        var json = """
            {
                "maxTokens": 1000,
                "targetTokens": 2000
            }
            """;

        var action = () => CupelJsonSerializer.DeserializeBudget(json);

        var ex = await Assert.That(action).Throws<JsonException>();
        await Assert.That(ex!.Message).Contains("TargetTokens");
    }
}
