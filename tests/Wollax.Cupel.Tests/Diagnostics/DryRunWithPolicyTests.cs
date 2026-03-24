using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Diagnostics;

public class DryRunWithPolicyTests
{
    /// <summary>
    /// Policy uses ScorerType.Priority; the pipeline uses ReflexiveScorer.
    /// Items have FutureRelevanceHint descending (alpha highest) but Priority ascending
    /// (delta highest Priority int). Budget fits exactly 2 of 4 items (each 100t, budget 200t).
    /// - Pipeline's ReflexiveScorer picks alpha+beta (highest hints 0.9, 0.7).
    /// - Policy's Priority scorer picks delta+gamma (highest Priority ints 4, 3).
    /// </summary>
    [Test]
    public async Task UsesPolicy_Scorer_NotPipelines()
    {
        // Items: FutureRelevanceHint descending, Priority ascending
        // Pipeline (Reflexive) → alpha+beta; Policy (Priority) → delta+gamma
        var items = new List<ContextItem>
        {
            new() { Content = "alpha", Tokens = 100, Kind = ContextKind.Message, FutureRelevanceHint = 0.9, Priority = 1 },
            new() { Content = "beta",  Tokens = 100, Kind = ContextKind.Message, FutureRelevanceHint = 0.7, Priority = 2 },
            new() { Content = "gamma", Tokens = 100, Kind = ContextKind.Message, FutureRelevanceHint = 0.3, Priority = 3 },
            new() { Content = "delta", Tokens = 100, Kind = ContextKind.Message, FutureRelevanceHint = 0.1, Priority = 4 },
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(maxTokens: 400, targetTokens: 200))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var budget = new ContextBudget(maxTokens: 400, targetTokens: 200);

        // Policy uses Priority scorer — highest Priority int wins → delta(4) + gamma(3)
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Priority, weight: 1.0)],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.Chronological);

        // DryRunWithPolicy must use the policy's Priority scorer, not the pipeline's ReflexiveScorer
        var result = pipeline.DryRunWithPolicy(items, budget, policy);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Included.Count).IsEqualTo(2);

        var includedContents = result.Report!.Included.Select(e => e.Item.Content).ToList();
        var excludedContents = result.Report!.Excluded.Select(e => e.Item.Content).ToList();

        // Policy's Priority scorer picks delta(4) and gamma(3)
        await Assert.That(includedContents).Contains("delta");
        await Assert.That(includedContents).Contains("gamma");

        // alpha(1) and beta(2) are excluded by Priority scorer
        await Assert.That(excludedContents).Contains("alpha");
        await Assert.That(excludedContents).Contains("beta");
    }

    /// <summary>
    /// Pipeline has budget 500t; DryRunWithPolicy is called with budget 100t (fits exactly
    /// 1 item of 100t). The policy budget must win over the pipeline's own budget.
    /// </summary>
    [Test]
    public async Task UsesExplicitBudget_NotPipelineBudget()
    {
        var items = new List<ContextItem>
        {
            new() { Content = "item1", Tokens = 100, Kind = ContextKind.Message, FutureRelevanceHint = 0.9 },
            new() { Content = "item2", Tokens = 100, Kind = ContextKind.Message, FutureRelevanceHint = 0.5 },
            new() { Content = "item3", Tokens = 100, Kind = ContextKind.Message, FutureRelevanceHint = 0.1 },
        };

        // Pipeline budget is very generous (500t)
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(maxTokens: 1000, targetTokens: 500))
            .WithScorer(new ReflexiveScorer())
            .Build();

        // Explicit budget fits only 1 item (100t)
        var tightBudget = new ContextBudget(maxTokens: 200, targetTokens: 100);

        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, weight: 1.0)],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.Chronological);

        var result = pipeline.DryRunWithPolicy(items, tightBudget, policy);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Included.Count).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that the slicer from the policy is respected. Both Greedy and Knapsack
    /// policies are exercised without error and each returns a non-null report.
    /// </summary>
    [Test]
    public async Task UsesPolicy_Slicer_Greedy_vs_Knapsack()
    {
        // 4 items of mixed sizes; budget 200t
        // Greedy: fits two 80t items (160t total)
        // Knapsack: also fits two 80t items with bucket-rounded budget
        var items = new List<ContextItem>
        {
            new() { Content = "small1",  Tokens = 80,  Kind = ContextKind.Message, FutureRelevanceHint = 0.9 },
            new() { Content = "small2",  Tokens = 80,  Kind = ContextKind.Message, FutureRelevanceHint = 0.8 },
            new() { Content = "large1",  Tokens = 150, Kind = ContextKind.Message, FutureRelevanceHint = 0.7 },
            new() { Content = "large2",  Tokens = 150, Kind = ContextKind.Message, FutureRelevanceHint = 0.6 },
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(maxTokens: 400, targetTokens: 200))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var budget = new ContextBudget(maxTokens: 400, targetTokens: 200);

        var greedyPolicy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, weight: 1.0)],
            slicerType: SlicerType.Greedy);

        var knapsackPolicy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, weight: 1.0)],
            slicerType: SlicerType.Knapsack);

        // Both should run without exception and return non-null reports
        var greedyResult = pipeline.DryRunWithPolicy(items, budget, greedyPolicy);
        var knapsackResult = pipeline.DryRunWithPolicy(items, budget, knapsackPolicy);

        await Assert.That(greedyResult.Report).IsNotNull();
        await Assert.That(knapsackResult.Report).IsNotNull();
    }

    [Test]
    public async Task ThrowsArgumentNullException_WhenItemsNull()
    {
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(maxTokens: 400, targetTokens: 200))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var budget = new ContextBudget(maxTokens: 400, targetTokens: 200);

        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, weight: 1.0)]);

        var act = () => pipeline.DryRunWithPolicy(null!, budget, policy);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ThrowsArgumentNullException_WhenBudgetNull()
    {
        var items = new List<ContextItem>
        {
            new() { Content = "x", Tokens = 100, Kind = ContextKind.Message },
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(maxTokens: 400, targetTokens: 200))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, weight: 1.0)]);

        var act = () => pipeline.DryRunWithPolicy(items, null!, policy);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ThrowsArgumentNullException_WhenPolicyNull()
    {
        var items = new List<ContextItem>
        {
            new() { Content = "x", Tokens = 100, Kind = ContextKind.Message },
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(maxTokens: 400, targetTokens: 200))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var budget = new ContextBudget(maxTokens: 400, targetTokens: 200);

        var act = () => pipeline.DryRunWithPolicy(items, budget, null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }
}
