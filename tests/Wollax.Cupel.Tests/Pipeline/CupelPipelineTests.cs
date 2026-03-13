using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Pipeline;

public class CupelPipelineTests
{
    // Helpers

    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10,
        double? futureRelevanceHint = null,
        bool pinned = false,
        DateTimeOffset? timestamp = null,
        ContextKind? kind = null,
        int? priority = null) =>
        new()
        {
            Content = content,
            Tokens = tokens,
            FutureRelevanceHint = futureRelevanceHint,
            Pinned = pinned,
            Timestamp = timestamp,
            Kind = kind ?? ContextKind.Message,
            Priority = priority
        };

    private static CupelPipeline BuildPipeline(
        IScorer? scorer = null,
        ISlicer? slicer = null,
        IPlacer? placer = null,
        int maxTokens = 1000,
        int targetTokens = 500,
        int outputReserve = 0,
        bool deduplication = true)
    {
        var builder = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(maxTokens, targetTokens, outputReserve))
            .WithScorer(scorer ?? new ReflexiveScorer());

        if (slicer is not null)
            builder.WithSlicer(slicer);
        if (placer is not null)
            builder.WithPlacer(placer);
        if (!deduplication)
            builder.WithDeduplication(false);

        return builder.Build();
    }

    // Default behavior tests

    [Test]
    public async Task Build_DefaultSlicer_IsGreedySlice()
    {
        // GreedySlice selects by value density. Item with high score + low tokens wins.
        var highDensity = CreateItem("high-density", tokens: 10, futureRelevanceHint: 1.0);
        var lowDensity = CreateItem("low-density", tokens: 100, futureRelevanceHint: 0.5);

        var pipeline = BuildPipeline(targetTokens: 50);
        var result = pipeline.Execute([highDensity, lowDensity]);

        // Only high-density item should fit (budget 50, high-density=10 tokens)
        await Assert.That(result.Items).Contains(highDensity);
    }

    [Test]
    public async Task Build_DefaultPlacer_IsChronologicalPlacer()
    {
        var older = CreateItem("older", tokens: 10, futureRelevanceHint: 0.5,
            timestamp: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = CreateItem("newer", tokens: 10, futureRelevanceHint: 0.5,
            timestamp: new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([newer, older]);

        // ChronologicalPlacer orders by timestamp ascending
        await Assert.That(result.Items[0]).IsEqualTo(older);
        await Assert.That(result.Items[1]).IsEqualTo(newer);
    }

    [Test]
    public async Task Build_DefaultDedup_IsEnabled()
    {
        var item1 = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.8);
        var item2 = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.5);

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([item1, item2]);

        await Assert.That(result.Items.Count).IsEqualTo(1);
    }

    // Stage ordering test

    [Test]
    public async Task StageOrder_ClassifyScoreDedupSlicePlace()
    {
        var stageLog = new List<string>();

        var scorerUsed = false;
        var scorer = new DelegateScorer((item, allItems) =>
        {
            if (!scorerUsed)
            {
                stageLog.Add("Score");
                scorerUsed = true;
            }
            // Verify scorer does NOT receive pinned items
            for (var i = 0; i < allItems.Count; i++)
            {
                if (allItems[i].Pinned)
                    throw new InvalidOperationException("Pinned item passed to scorer!");
            }
            return 0.5;
        });

        var slicer = new DelegateSlicer((items, budget, trace) =>
        {
            stageLog.Add("Slice");
            var result = new ContextItem[items.Count];
            for (var i = 0; i < items.Count; i++)
                result[i] = items[i].Item;
            return result;
        });

        var placer = new DelegatePlacer((items, trace) =>
        {
            stageLog.Add("Place");
            var result = new ContextItem[items.Count];
            for (var i = 0; i < items.Count; i++)
                result[i] = items[i].Item;
            return result;
        });

        var pipeline = BuildPipeline(scorer: scorer, slicer: slicer, placer: placer);
        var pinned = CreateItem("pinned", tokens: 5, pinned: true);
        var normal = CreateItem("normal", tokens: 10, futureRelevanceHint: 0.5);

        pipeline.Execute([pinned, normal]);

        // Classify runs before Score (proved by scorer not receiving pinned)
        // Score, (Dedup is internal), Slice, Place
        await Assert.That(stageLog.Count).IsEqualTo(3);
        await Assert.That(stageLog[0]).IsEqualTo("Score");
        await Assert.That(stageLog[1]).IsEqualTo("Slice");
        await Assert.That(stageLog[2]).IsEqualTo("Place");
    }

    // Pinned item tests

    [Test]
    public async Task PinnedItems_BypassScoring()
    {
        var scoredItems = new List<string>();
        var scorer = new DelegateScorer((item, allItems) =>
        {
            scoredItems.Add(item.Content);
            return 0.5;
        });

        var pinned = CreateItem("pinned-item", tokens: 10, pinned: true);
        var normal = CreateItem("normal-item", tokens: 10);

        var pipeline = BuildPipeline(scorer: scorer);
        var result = pipeline.Execute([pinned, normal]);

        await Assert.That(scoredItems.Contains("pinned-item")).IsFalse();
        await Assert.That(result.Items).Contains(pinned);
    }

    [Test]
    public async Task PinnedItems_NotPassedToScorer()
    {
        IReadOnlyList<ContextItem>? capturedAllItems = null;
        var scorer = new DelegateScorer((item, allItems) =>
        {
            capturedAllItems = allItems;
            return 0.5;
        });

        var pinned = CreateItem("pinned", tokens: 10, pinned: true);
        var normal = CreateItem("normal", tokens: 10);

        var pipeline = BuildPipeline(scorer: scorer);
        pipeline.Execute([pinned, normal]);

        await Assert.That(capturedAllItems).IsNotNull();
        await Assert.That(capturedAllItems!.Count).IsEqualTo(1);
        await Assert.That(capturedAllItems![0].Content).IsEqualTo("normal");
    }

    [Test]
    public async Task PinnedItems_EffectiveScore1_0()
    {
        // With UShapedPlacer, highest-scored items go to edges.
        // Pinned item has score 1.0, so it should be at position 0 (first edge).
        var pinned = CreateItem("pinned", tokens: 10, pinned: true);
        var normal1 = CreateItem("normal1", tokens: 10, futureRelevanceHint: 0.3);
        var normal2 = CreateItem("normal2", tokens: 10, futureRelevanceHint: 0.2);

        var pipeline = BuildPipeline(placer: new UShapedPlacer());
        var result = pipeline.Execute([pinned, normal1, normal2]);

        // Pinned at score 1.0 should be placed at an edge (position 0)
        await Assert.That(result.Items[0]).IsEqualTo(pinned);
    }

    [Test]
    public async Task PinnedItems_ConsumeBudget()
    {
        // Budget TargetTokens=100. Pinned item with 60 tokens.
        // Remaining scoreable items should only fill 40 tokens.
        var pinned = CreateItem("pinned", tokens: 60, pinned: true);
        var item1 = CreateItem("item1", tokens: 30, futureRelevanceHint: 0.9);
        var item2 = CreateItem("item2", tokens: 30, futureRelevanceHint: 0.8);

        var pipeline = BuildPipeline(maxTokens: 100, targetTokens: 100);
        var result = pipeline.Execute([pinned, item1, item2]);

        // Pinned takes 60. Only 40 remains. item1 (30) fits, item2 (30) doesn't.
        await Assert.That(result.Items).Contains(pinned);
        await Assert.That(result.Items).Contains(item1);
        await Assert.That(result.Items.Contains(item2)).IsFalse();
    }

    [Test]
    public async Task PinnedOverflow_ThrowsInvalidOperationException()
    {
        // Budget MaxTokens=100, OutputReserve=20. Available=80. Pinned=90.
        var pinned = CreateItem("pinned", tokens: 90, pinned: true);

        var pipeline = BuildPipeline(maxTokens: 100, targetTokens: 80, outputReserve: 20);

        await Assert.That(() => pipeline.Execute([pinned]))
            .Throws<InvalidOperationException>();
    }

    // Deduplication tests

    [Test]
    public async Task DuplicateContent_KeepsHighestScored()
    {
        var highScored = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.9);
        var lowScored = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.3);

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([lowScored, highScored]);

        await Assert.That(result.Items.Count).IsEqualTo(1);
        // The surviving item should be the one with higher score
        // ReflexiveScorer uses FutureRelevanceHint, so highScored wins
        await Assert.That(result.Items[0]).IsEqualTo(highScored);
    }

    [Test]
    public async Task DuplicateContent_DifferentKind_StillDeduped()
    {
        var item1 = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.8,
            kind: ContextKind.Message);
        var item2 = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.5,
            kind: ContextKind.Document);

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([item1, item2]);

        await Assert.That(result.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Dedup_Disabled_KeepsBoth()
    {
        var item1 = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.8);
        var item2 = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.5);

        var pipeline = BuildPipeline(deduplication: false);
        var result = pipeline.Execute([item1, item2]);

        await Assert.That(result.Items.Count).IsEqualTo(2);
    }

    // Null / empty input tests

    [Test]
    public async Task Execute_NullItems_ThrowsArgumentNull()
    {
        var pipeline = BuildPipeline();

        await Assert.That(() => pipeline.Execute(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Execute_EmptyItems_ReturnsEmpty()
    {
        var pipeline = BuildPipeline();
        var result = pipeline.Execute([]);

        await Assert.That(result.Items.Count).IsEqualTo(0);
        await Assert.That(result.TotalTokens).IsEqualTo(0);
    }

    [Test]
    public async Task PinnedItems_CapturedScoreIs1_0()
    {
        // Capture the ScoredItem[] passed to placer to verify pinned score directly
        var capturedScores = new List<(string Content, double Score)>();
        var placer = new DelegatePlacer((items, trace) =>
        {
            for (var i = 0; i < items.Count; i++)
                capturedScores.Add((items[i].Item.Content, items[i].Score));
            var result = new ContextItem[items.Count];
            for (var i = 0; i < items.Count; i++)
                result[i] = items[i].Item;
            return result;
        });

        var pinned = CreateItem("pinned", tokens: 10, pinned: true);
        var normal = CreateItem("normal", tokens: 10, futureRelevanceHint: 0.5);

        var pipeline = BuildPipeline(placer: placer);
        pipeline.Execute([pinned, normal]);

        var pinnedEntry = capturedScores.Find(x => x.Content == "pinned");
        await Assert.That(pinnedEntry.Score).IsEqualTo(1.0);
    }

    [Test]
    public async Task AllPinnedItems_ExecutesSuccessfully()
    {
        var pinned1 = CreateItem("p1", tokens: 10, pinned: true);
        var pinned2 = CreateItem("p2", tokens: 10, pinned: true);

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([pinned1, pinned2]);

        await Assert.That(result.Items.Count).IsEqualTo(2);
    }

    // Classify validation tests

    [Test]
    public async Task NegativeTokens_ItemSkipped()
    {
        var negative = CreateItem("negative", tokens: -1, futureRelevanceHint: 1.0);
        var valid = CreateItem("valid", tokens: 10, futureRelevanceHint: 0.5);

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([negative, valid]);

        await Assert.That(result.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0]).IsEqualTo(valid);
    }

    [Test]
    public async Task ZeroTokens_ItemIncluded()
    {
        var zeroToken = CreateItem("zero", tokens: 0, futureRelevanceHint: 0.5);

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([zeroToken]);

        await Assert.That(result.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0]).IsEqualTo(zeroToken);
    }

    // End-to-end integration tests

    [Test]
    public async Task EndToEnd_SimpleSelection()
    {
        var items = new List<ContextItem>();
        for (var i = 0; i < 10; i++)
        {
            items.Add(CreateItem($"item-{i}", tokens: 20, futureRelevanceHint: i / 10.0));
        }

        // Budget fits about 5 items (targetTokens=100, each item=20)
        var pipeline = BuildPipeline(maxTokens: 200, targetTokens: 100);
        var result = pipeline.Execute(items);

        await Assert.That(result.Items.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(100);
    }

    [Test]
    public async Task EndToEnd_WithTracing()
    {
        var item = CreateItem("test", tokens: 10, futureRelevanceHint: 0.5);
        var trace = new DiagnosticTraceCollector();

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([item], trace);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Events.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task EndToEnd_WithoutTracing()
    {
        var item = CreateItem("test", tokens: 10, futureRelevanceHint: 0.5);

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([item]);

        await Assert.That(result.Report).IsNull();
    }

    // Async tests

    [Test]
    public async Task ExecuteAsync_MaterializesSource()
    {
        var items = new List<ContextItem>
        {
            CreateItem("async-1", tokens: 10, futureRelevanceHint: 0.8),
            CreateItem("async-2", tokens: 10, futureRelevanceHint: 0.5)
        };
        var source = new ListContextSource(items);

        var pipeline = BuildPipeline();
        var result = await pipeline.ExecuteAsync(source);

        await Assert.That(result.Items.Count).IsEqualTo(2);
    }

    // Test helpers

    private sealed class DelegateScorer(Func<ContextItem, IReadOnlyList<ContextItem>, double> scoreFunc)
        : IScorer
    {
        public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems) =>
            scoreFunc(item, allItems);
    }

    private sealed class DelegateSlicer(
        Func<IReadOnlyList<ScoredItem>, ContextBudget, ITraceCollector, IReadOnlyList<ContextItem>> sliceFunc)
        : ISlicer
    {
        public IReadOnlyList<ContextItem> Slice(
            IReadOnlyList<ScoredItem> scoredItems,
            ContextBudget budget,
            ITraceCollector traceCollector) =>
            sliceFunc(scoredItems, budget, traceCollector);
    }

    private sealed class DelegatePlacer(
        Func<IReadOnlyList<ScoredItem>, ITraceCollector, IReadOnlyList<ContextItem>> placeFunc)
        : IPlacer
    {
        public IReadOnlyList<ContextItem> Place(
            IReadOnlyList<ScoredItem> items,
            ITraceCollector traceCollector) =>
            placeFunc(items, traceCollector);
    }

    private sealed class ListContextSource(IReadOnlyList<ContextItem> items) : IContextSource
    {
        public Task<IReadOnlyList<ContextItem>> GetItemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(items);
    }
}
