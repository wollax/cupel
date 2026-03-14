#pragma warning disable CUPEL001, CUPEL002, CUPEL003, CUPEL004, CUPEL005, CUPEL006, CUPEL007

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Policy;

public class CupelPresetsTests
{
    [Test]
    public async Task Chat_ReturnsValidPolicy()
    {
        var policy = CupelPresets.Chat();

        await Assert.That(policy).IsNotNull();
        await Assert.That(policy.Name).IsEqualTo("Chat");
        await Assert.That(policy.Scorers.Count).IsEqualTo(2);
        await Assert.That(policy.SlicerType).IsEqualTo(SlicerType.Greedy);
        await Assert.That(policy.PlacerType).IsEqualTo(PlacerType.Chronological);
        await Assert.That(policy.DeduplicationEnabled).IsTrue();
        await Assert.That(policy.OverflowStrategy).IsEqualTo(OverflowStrategy.Throw);
        await Assert.That(policy.KnapsackBucketSize).IsNull();
    }

    [Test]
    public async Task Chat_HasCorrectScorerTypesAndWeights()
    {
        var policy = CupelPresets.Chat();

        var recency = policy.Scorers.Single(s => s.Type == ScorerType.Recency);
        var kind = policy.Scorers.Single(s => s.Type == ScorerType.Kind);

        await Assert.That(recency.Weight).IsEqualTo(3);
        await Assert.That(kind.Weight).IsEqualTo(1);
        await Assert.That(recency.Weight).IsGreaterThan(kind.Weight);
    }

    [Test]
    public async Task CodeReview_ReturnsValidPolicy()
    {
        var policy = CupelPresets.CodeReview();

        await Assert.That(policy).IsNotNull();
        await Assert.That(policy.Name).IsEqualTo("CodeReview");
        await Assert.That(policy.Scorers.Count).IsEqualTo(3);
        await Assert.That(policy.SlicerType).IsEqualTo(SlicerType.Greedy);
        await Assert.That(policy.PlacerType).IsEqualTo(PlacerType.Chronological);
        await Assert.That(policy.DeduplicationEnabled).IsTrue();
        await Assert.That(policy.OverflowStrategy).IsEqualTo(OverflowStrategy.Throw);
    }

    [Test]
    public async Task CodeReview_HasCorrectScorerTypesAndWeights()
    {
        var policy = CupelPresets.CodeReview();

        var kind = policy.Scorers.Single(s => s.Type == ScorerType.Kind);
        var priority = policy.Scorers.Single(s => s.Type == ScorerType.Priority);
        var recency = policy.Scorers.Single(s => s.Type == ScorerType.Recency);

        await Assert.That(kind.Weight).IsEqualTo(2);
        await Assert.That(priority.Weight).IsEqualTo(2);
        await Assert.That(recency.Weight).IsEqualTo(1);
        await Assert.That(kind.Weight).IsGreaterThan(recency.Weight);
        await Assert.That(priority.Weight).IsGreaterThan(recency.Weight);
    }

    [Test]
    public async Task Rag_ReturnsValidPolicy()
    {
        var policy = CupelPresets.Rag();

        await Assert.That(policy).IsNotNull();
        await Assert.That(policy.Name).IsEqualTo("Rag");
        await Assert.That(policy.Scorers.Count).IsEqualTo(2);
        await Assert.That(policy.SlicerType).IsEqualTo(SlicerType.Greedy);
        await Assert.That(policy.PlacerType).IsEqualTo(PlacerType.UShaped);
        await Assert.That(policy.DeduplicationEnabled).IsTrue();
        await Assert.That(policy.OverflowStrategy).IsEqualTo(OverflowStrategy.Throw);
    }

    [Test]
    public async Task Rag_HasCorrectScorerTypesAndWeights()
    {
        var policy = CupelPresets.Rag();

        var reflexive = policy.Scorers.Single(s => s.Type == ScorerType.Reflexive);
        var kind = policy.Scorers.Single(s => s.Type == ScorerType.Kind);

        await Assert.That(reflexive.Weight).IsEqualTo(3);
        await Assert.That(kind.Weight).IsEqualTo(1);
        await Assert.That(reflexive.Weight).IsGreaterThan(kind.Weight);
    }

    [Test]
    public async Task DocumentQa_ReturnsValidPolicy()
    {
        var policy = CupelPresets.DocumentQa();

        await Assert.That(policy).IsNotNull();
        await Assert.That(policy.Name).IsEqualTo("DocumentQa");
        await Assert.That(policy.Scorers.Count).IsEqualTo(3);
        await Assert.That(policy.SlicerType).IsEqualTo(SlicerType.Knapsack);
        await Assert.That(policy.PlacerType).IsEqualTo(PlacerType.UShaped);
        await Assert.That(policy.DeduplicationEnabled).IsTrue();
        await Assert.That(policy.OverflowStrategy).IsEqualTo(OverflowStrategy.Throw);
        await Assert.That(policy.KnapsackBucketSize).IsEqualTo(100);
    }

    [Test]
    public async Task DocumentQa_HasCorrectScorerTypesAndWeights()
    {
        var policy = CupelPresets.DocumentQa();

        var kind = policy.Scorers.Single(s => s.Type == ScorerType.Kind);
        var reflexive = policy.Scorers.Single(s => s.Type == ScorerType.Reflexive);
        var priority = policy.Scorers.Single(s => s.Type == ScorerType.Priority);

        await Assert.That(kind.Weight).IsEqualTo(2);
        await Assert.That(reflexive.Weight).IsEqualTo(2);
        await Assert.That(priority.Weight).IsEqualTo(1);
        await Assert.That(kind.Weight).IsGreaterThan(priority.Weight);
        await Assert.That(reflexive.Weight).IsGreaterThan(priority.Weight);
    }

    [Test]
    public async Task ToolUse_ReturnsValidPolicy()
    {
        var policy = CupelPresets.ToolUse();

        await Assert.That(policy).IsNotNull();
        await Assert.That(policy.Name).IsEqualTo("ToolUse");
        await Assert.That(policy.Scorers.Count).IsEqualTo(3);
        await Assert.That(policy.SlicerType).IsEqualTo(SlicerType.Greedy);
        await Assert.That(policy.PlacerType).IsEqualTo(PlacerType.Chronological);
        await Assert.That(policy.DeduplicationEnabled).IsTrue();
        await Assert.That(policy.OverflowStrategy).IsEqualTo(OverflowStrategy.Throw);
    }

    [Test]
    public async Task ToolUse_HasCorrectScorerTypesAndWeights()
    {
        var policy = CupelPresets.ToolUse();

        var kind = policy.Scorers.Single(s => s.Type == ScorerType.Kind);
        var recency = policy.Scorers.Single(s => s.Type == ScorerType.Recency);
        var priority = policy.Scorers.Single(s => s.Type == ScorerType.Priority);

        await Assert.That(kind.Weight).IsEqualTo(2);
        await Assert.That(recency.Weight).IsEqualTo(2);
        await Assert.That(priority.Weight).IsEqualTo(1);
        await Assert.That(kind.Weight).IsGreaterThan(priority.Weight);
        await Assert.That(recency.Weight).IsGreaterThan(priority.Weight);
    }

    [Test]
    public async Task LongRunning_ReturnsValidPolicy()
    {
        var policy = CupelPresets.LongRunning();

        await Assert.That(policy).IsNotNull();
        await Assert.That(policy.Name).IsEqualTo("LongRunning");
        await Assert.That(policy.Scorers.Count).IsEqualTo(3);
        await Assert.That(policy.SlicerType).IsEqualTo(SlicerType.Greedy);
        await Assert.That(policy.PlacerType).IsEqualTo(PlacerType.Chronological);
        await Assert.That(policy.DeduplicationEnabled).IsTrue();
        await Assert.That(policy.OverflowStrategy).IsEqualTo(OverflowStrategy.Throw);
    }

    [Test]
    public async Task LongRunning_HasCorrectScorerTypesAndWeights()
    {
        var policy = CupelPresets.LongRunning();

        var recency = policy.Scorers.Single(s => s.Type == ScorerType.Recency);
        var frequency = policy.Scorers.Single(s => s.Type == ScorerType.Frequency);
        var kind = policy.Scorers.Single(s => s.Type == ScorerType.Kind);

        await Assert.That(recency.Weight).IsEqualTo(3);
        await Assert.That(frequency.Weight).IsEqualTo(1);
        await Assert.That(kind.Weight).IsEqualTo(1);
        await Assert.That(recency.Weight).IsGreaterThan(frequency.Weight);
        await Assert.That(recency.Weight).IsGreaterThan(kind.Weight);
    }

    [Test]
    public async Task Debugging_ReturnsValidPolicy()
    {
        var policy = CupelPresets.Debugging();

        await Assert.That(policy).IsNotNull();
        await Assert.That(policy.Name).IsEqualTo("Debugging");
        await Assert.That(policy.Scorers.Count).IsEqualTo(3);
        await Assert.That(policy.SlicerType).IsEqualTo(SlicerType.Greedy);
        await Assert.That(policy.PlacerType).IsEqualTo(PlacerType.Chronological);
        await Assert.That(policy.DeduplicationEnabled).IsTrue();
        await Assert.That(policy.OverflowStrategy).IsEqualTo(OverflowStrategy.Throw);
    }

    [Test]
    public async Task Debugging_HasCorrectScorerTypesAndWeights()
    {
        var policy = CupelPresets.Debugging();

        var priority = policy.Scorers.Single(s => s.Type == ScorerType.Priority);
        var kind = policy.Scorers.Single(s => s.Type == ScorerType.Kind);
        var recency = policy.Scorers.Single(s => s.Type == ScorerType.Recency);

        await Assert.That(priority.Weight).IsEqualTo(3);
        await Assert.That(kind.Weight).IsEqualTo(2);
        await Assert.That(recency.Weight).IsEqualTo(1);
        await Assert.That(priority.Weight).IsGreaterThan(kind.Weight);
        await Assert.That(kind.Weight).IsGreaterThan(recency.Weight);
    }
}
