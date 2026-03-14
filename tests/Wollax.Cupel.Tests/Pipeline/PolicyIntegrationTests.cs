#pragma warning disable CUPEL001, CUPEL002, CUPEL003, CUPEL004, CUPEL005, CUPEL006, CUPEL007

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Pipeline;

/// <summary>
/// End-to-end integration tests proving policy-built pipelines produce correct results.
/// Validates that WithPolicy() translates policy configuration into working pipelines
/// equivalent to manual builder configuration.
/// </summary>
public class PolicyIntegrationTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10,
        ContextKind? kind = null,
        DateTimeOffset? timestamp = null,
        int? priority = null,
        double? futureRelevanceHint = null,
        IReadOnlyList<string>? tags = null) =>
        new()
        {
            Content = content,
            Tokens = tokens,
            Kind = kind ?? ContextKind.Message,
            Timestamp = timestamp,
            Priority = priority,
            FutureRelevanceHint = futureRelevanceHint,
            Tags = tags ?? []
        };

    // 1. Policy-built pipeline produces results

    [Test]
    public async Task PolicyBuiltPipeline_ProducesResults()
    {
        var policy = new CupelPolicy(
            scorers: [
                new ScorerEntry(ScorerType.Recency, 2),
                new ScorerEntry(ScorerType.Kind, 1),
            ],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.Chronological);

        var items = new ContextItem[5];
        for (var i = 0; i < 5; i++)
        {
            items[i] = CreateItem(
                content: $"item-{i}",
                tokens: 50,
                timestamp: BaseTime.AddMinutes(i),
                kind: ContextKind.Message);
        }

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 300))
            .WithPolicy(policy)
            .Build();

        var result = pipeline.Execute(items);

        await Assert.That(result.Items.Count).IsGreaterThan(0);
        await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(300);
    }

    // 2. Policy with UShaped placer

    [Test]
    public async Task PolicyWithUShapedPlacer_HighScoredItemsAtEnds()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, 1)],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.UShaped);

        var items = new[]
        {
            CreateItem("low-1", tokens: 10, futureRelevanceHint: 0.1),
            CreateItem("high-1", tokens: 10, futureRelevanceHint: 0.9),
            CreateItem("mid", tokens: 10, futureRelevanceHint: 0.5),
            CreateItem("high-2", tokens: 10, futureRelevanceHint: 0.8),
            CreateItem("low-2", tokens: 10, futureRelevanceHint: 0.2),
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithPolicy(policy)
            .Build();

        var result = pipeline.Execute(items);

        await Assert.That(result.Items.Count).IsEqualTo(5);

        // U-shaped: highest-scored items at start and end
        var first = result.Items[0];
        var last = result.Items[result.Items.Count - 1];

        // First and last should be among the highest scored
        await Assert.That(first.FutureRelevanceHint!.Value).IsGreaterThanOrEqualTo(0.8);
        await Assert.That(last.FutureRelevanceHint!.Value).IsGreaterThanOrEqualTo(0.8);
    }

    // 3. Policy with Knapsack slicer

    [Test]
    public async Task PolicyWithKnapsackSlicer_FitsWithinBudget()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Priority, 1)],
            slicerType: SlicerType.Knapsack,
            placerType: PlacerType.Chronological,
            knapsackBucketSize: 50);

        var items = new[]
        {
            CreateItem("small-high", tokens: 50, priority: 10),
            CreateItem("large-low", tokens: 200, priority: 1),
            CreateItem("medium-medium", tokens: 100, priority: 5),
            CreateItem("small-medium", tokens: 50, priority: 6),
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(500, 200))
            .WithPolicy(policy)
            .Build();

        var result = pipeline.Execute(items);

        await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(200);
        await Assert.That(result.Items.Count).IsGreaterThan(0);
    }

    // 4. Policy-built matches manual-built

    [Test]
    public async Task PolicyBuilt_MatchesManualBuilt()
    {
        // Create equivalent configurations via policy and manual builder
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Recency, 1)],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.Chronological);

        var budget = new ContextBudget(1000, 300);

        var items = new ContextItem[8];
        for (var i = 0; i < 8; i++)
        {
            items[i] = CreateItem(
                content: $"item-{i}",
                tokens: 50,
                timestamp: BaseTime.AddMinutes(i));
        }

        // Policy-built pipeline
        var policyPipeline = CupelPipeline.CreateBuilder()
            .WithBudget(budget)
            .WithPolicy(policy)
            .Build();

        // Manual-built pipeline (equivalent configuration)
        var manualPipeline = CupelPipeline.CreateBuilder()
            .WithBudget(budget)
            .AddScorer(new RecencyScorer(), 1)
            .UseGreedySlice()
            .WithPlacer(new ChronologicalPlacer())
            .WithDeduplication(true)
            .WithOverflowStrategy(OverflowStrategy.Throw)
            .Build();

        var policyResult = policyPipeline.Execute(items);
        var manualResult = manualPipeline.Execute(items);

        await Assert.That(policyResult.Items.Count).IsEqualTo(manualResult.Items.Count);
        await Assert.That(policyResult.TotalTokens).IsEqualTo(manualResult.TotalTokens);

        // Same items in same order
        for (var i = 0; i < policyResult.Items.Count; i++)
        {
            await Assert.That(policyResult.Items[i].Content).IsEqualTo(manualResult.Items[i].Content);
        }
    }

    // 5. Each preset builds a working pipeline

    [Test]
    public async Task Preset_Chat_BuildsWorkingPipeline()
    {
        await VerifyPresetWorks(CupelPresets.Chat());
    }

    [Test]
    public async Task Preset_CodeReview_BuildsWorkingPipeline()
    {
        await VerifyPresetWorks(CupelPresets.CodeReview());
    }

    [Test]
    public async Task Preset_Rag_BuildsWorkingPipeline()
    {
        await VerifyPresetWorks(CupelPresets.Rag());
    }

    [Test]
    public async Task Preset_DocumentQa_BuildsWorkingPipeline()
    {
        await VerifyPresetWorks(CupelPresets.DocumentQa());
    }

    [Test]
    public async Task Preset_ToolUse_BuildsWorkingPipeline()
    {
        await VerifyPresetWorks(CupelPresets.ToolUse());
    }

    [Test]
    public async Task Preset_LongRunning_BuildsWorkingPipeline()
    {
        await VerifyPresetWorks(CupelPresets.LongRunning());
    }

    [Test]
    public async Task Preset_Debugging_BuildsWorkingPipeline()
    {
        await VerifyPresetWorks(CupelPresets.Debugging());
    }

    // 6. Policy with quotas integration

    [Test]
    public async Task PolicyWithQuotas_EnforcesMinPercent()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, 1)],
            quotas: [new QuotaEntry(ContextKind.Message, minPercent: 40)]);

        var items = new List<ContextItem>();
        // 5 message items
        for (var i = 0; i < 5; i++)
            items.Add(CreateItem($"msg-{i}", tokens: 50, kind: ContextKind.Message, futureRelevanceHint: 0.3 + i * 0.05));
        // 5 document items with higher relevance (would normally dominate)
        for (var i = 0; i < 5; i++)
            items.Add(CreateItem($"doc-{i}", tokens: 50, kind: ContextKind.Document, futureRelevanceHint: 0.8 + i * 0.02));

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithPolicy(policy)
            .Build();

        var result = pipeline.Execute(items);

        // Calculate message token percentage
        var messageTokens = 0;
        for (var i = 0; i < result.Items.Count; i++)
        {
            if (result.Items[i].Kind == ContextKind.Message)
                messageTokens += result.Items[i].Tokens;
        }

        // 40% of 500 target = 200 tokens minimum for messages
        await Assert.That(messageTokens).IsGreaterThanOrEqualTo(200);
    }

    private static async Task VerifyPresetWorks(CupelPolicy policy)
    {
        var items = CreateRealisticItemSet();

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(2000, 500))
            .WithPolicy(policy)
            .Build();

        var result = pipeline.Execute(items);

        await Assert.That(result.Items.Count).IsGreaterThan(0);
        await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(500);
    }

    private static ContextItem[] CreateRealisticItemSet()
    {
        return
        [
            CreateItem("system-prompt", tokens: 50, kind: ContextKind.SystemPrompt,
                timestamp: BaseTime, priority: 10, futureRelevanceHint: 1.0,
                tags: ["system"]),
            CreateItem("memory-1", tokens: 30, kind: ContextKind.Memory,
                timestamp: BaseTime.AddMinutes(1), priority: 7, futureRelevanceHint: 0.8,
                tags: ["context", "memory"]),
            CreateItem("user-msg-1", tokens: 40, kind: ContextKind.Message,
                timestamp: BaseTime.AddMinutes(2), priority: 5, futureRelevanceHint: 0.6,
                tags: ["user"]),
            CreateItem("tool-output-1", tokens: 60, kind: ContextKind.ToolOutput,
                timestamp: BaseTime.AddMinutes(3), priority: 6, futureRelevanceHint: 0.7,
                tags: ["tool", "search"]),
            CreateItem("doc-1", tokens: 80, kind: ContextKind.Document,
                timestamp: BaseTime.AddMinutes(4), priority: 3, futureRelevanceHint: 0.5,
                tags: ["reference"]),
            CreateItem("user-msg-2", tokens: 40, kind: ContextKind.Message,
                timestamp: BaseTime.AddMinutes(5), priority: 5, futureRelevanceHint: 0.7,
                tags: ["user"]),
            CreateItem("assistant-msg-1", tokens: 50, kind: ContextKind.Message,
                timestamp: BaseTime.AddMinutes(6), priority: 4, futureRelevanceHint: 0.6,
                tags: ["assistant"]),
            CreateItem("tool-output-2", tokens: 70, kind: ContextKind.ToolOutput,
                timestamp: BaseTime.AddMinutes(7), priority: 8, futureRelevanceHint: 0.9,
                tags: ["tool", "code"]),
            CreateItem("doc-2", tokens: 90, kind: ContextKind.Document,
                timestamp: BaseTime.AddMinutes(8), priority: 2, futureRelevanceHint: 0.4,
                tags: ["reference", "api"]),
            CreateItem("user-msg-3", tokens: 40, kind: ContextKind.Message,
                timestamp: BaseTime.AddMinutes(9), priority: 5, futureRelevanceHint: 0.8,
                tags: ["user", "recent"]),
        ];
    }
}
