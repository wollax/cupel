using System.Runtime.CompilerServices;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;
using Wollax.Cupel.Slicing;

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

    // ExecuteStreamAsync tests

    [Test]
    public async Task ExecuteStreamAsync_WithStreamSlice()
    {
        var items = new[]
        {
            CreateItem("a", tokens: 50, futureRelevanceHint: 0.9, kind: ContextKind.Message),
            CreateItem("b", tokens: 50, futureRelevanceHint: 0.7, kind: ContextKind.Message),
            CreateItem("c", tokens: 50, futureRelevanceHint: 0.5, kind: ContextKind.Message)
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .WithAsyncSlicer(new StreamSlice())
            .Build();

        var result = await pipeline.ExecuteStreamAsync(CreateStreamSource(items));

        await Assert.That(result.Items.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(500);
    }

    [Test]
    public async Task ExecuteStreamAsync_StopsOnBudgetFull()
    {
        // Large stream, small budget — should not consume all items
        var items = new ContextItem[100];
        for (var i = 0; i < 100; i++)
            items[i] = CreateItem($"item-{i}", tokens: 50, futureRelevanceHint: 0.8, kind: ContextKind.Message);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithAsyncSlicer(new StreamSlice(batchSize: 4))
            .Build();

        var result = await pipeline.ExecuteStreamAsync(CreateStreamSource(items));

        await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(100);
    }

    [Test]
    public async Task ExecuteStreamAsync_WithoutAsyncSlicer_Throws()
    {
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .Build();

        await Assert.That(async () => await pipeline.ExecuteStreamAsync(
            CreateStreamSource(CreateItem("test", tokens: 10, futureRelevanceHint: 0.5))))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ExecuteStreamAsync_NullSource_Throws()
    {
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .WithAsyncSlicer(new StreamSlice())
            .Build();

        await Assert.That(async () => await pipeline.ExecuteStreamAsync(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ExecuteStreamAsync_Cancellation()
    {
        var items = new[]
        {
            CreateItem("a", tokens: 50, futureRelevanceHint: 0.9, kind: ContextKind.Message)
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .WithAsyncSlicer(new StreamSlice())
            .Build();

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.That(async () => await pipeline.ExecuteStreamAsync(
            CreateStreamSource(items), cancellationToken: cts.Token))
            .Throws<OperationCanceledException>();
    }

    // Pinned+Quota conflict detection tests

    [Test]
    public async Task PinnedItemsExceedingQuotaCap_EmitsTraceWarning()
    {
        // Budget = 1000 target, Cap Message at 30% = 300 tokens
        // Pinned Message items = 500 tokens (exceeds 30% cap)
        var pinnedMessages = new List<ContextItem>();
        for (var i = 0; i < 5; i++)
            pinnedMessages.Add(CreateItem($"pinned-msg-{i}", tokens: 100, pinned: true, kind: ContextKind.Message));

        var docs = new List<ContextItem>();
        for (var i = 0; i < 3; i++)
            docs.Add(CreateItem($"doc-{i}", tokens: 50, futureRelevanceHint: 0.7, kind: ContextKind.Document));

        var items = new List<ContextItem>();
        items.AddRange(pinnedMessages);
        items.AddRange(docs);

        var trace = new DiagnosticTraceCollector(TraceDetailLevel.Item);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(2000, 1000))
            .WithScorer(new ReflexiveScorer())
            .UseGreedySlice()
            .WithQuotas(q => q.Cap(ContextKind.Message, 30))
            .Build();

        var result = pipeline.Execute(items, trace);

        // Pinned items should still be included
        for (var i = 0; i < pinnedMessages.Count; i++)
            await Assert.That(result.Items).Contains(pinnedMessages[i]);

        // Should have a trace event with warning message about pinned exceeding cap
        var warningFound = false;
        for (var i = 0; i < trace.Events.Count; i++)
        {
            if (trace.Events[i].Message is not null && trace.Events[i].Message!.Contains("WARNING"))
            {
                warningFound = true;
                break;
            }
        }
        await Assert.That(warningFound).IsTrue();
    }

    // WithQuotas ordering tests

    [Test]
    public async Task WithQuotas_AfterWithSlicer_WrapsCustomSlicer()
    {
        // KnapsackSlice inside QuotaSlice — both optimizations and quotas should apply
        var messages = new List<ContextItem>();
        for (var i = 0; i < 5; i++)
            messages.Add(CreateItem($"msg-{i}", tokens: 100, futureRelevanceHint: 0.8, kind: ContextKind.Message));

        var docs = new List<ContextItem>();
        for (var i = 0; i < 5; i++)
            docs.Add(CreateItem($"doc-{i}", tokens: 100, futureRelevanceHint: 0.6, kind: ContextKind.Document));

        var items = new List<ContextItem>();
        items.AddRange(messages);
        items.AddRange(docs);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .UseKnapsackSlice()
            .WithQuotas(q => q.Require(ContextKind.Message, 30))
            .Build();

        var result = pipeline.Execute(items);

        // Messages should have at least 30% of 500 = 150 tokens
        var messageTokens = 0;
        for (var i = 0; i < result.Items.Count; i++)
        {
            if (result.Items[i].Kind == ContextKind.Message)
                messageTokens += result.Items[i].Tokens;
        }
        await Assert.That(messageTokens).IsGreaterThanOrEqualTo(100); // At least 1 message (100 tokens)
        await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(500);
    }

    // Report population tests

    [Test]
    public async Task Report_WithDiagnosticTrace_IncludedContainsScoredItems()
    {
        var item1 = CreateItem("a", tokens: 10, futureRelevanceHint: 0.8);
        var item2 = CreateItem("b", tokens: 10, futureRelevanceHint: 0.5);
        var trace = new DiagnosticTraceCollector();

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([item1, item2], trace);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Included.Count).IsEqualTo(2);

        // All scored items should have InclusionReason.Scored
        for (var i = 0; i < result.Report.Included.Count; i++)
            await Assert.That(result.Report.Included[i].Reason).IsEqualTo(InclusionReason.Scored);
    }

    [Test]
    public async Task Report_WithPinnedItems_IncludedContainsPinnedReason()
    {
        var pinned = CreateItem("pinned", tokens: 10, pinned: true);
        var normal = CreateItem("normal", tokens: 10, futureRelevanceHint: 0.5);
        var trace = new DiagnosticTraceCollector();

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([pinned, normal], trace);

        await Assert.That(result.Report).IsNotNull();

        var pinnedIncluded = default(IncludedItem);
        for (var i = 0; i < result.Report!.Included.Count; i++)
        {
            if (ReferenceEquals(result.Report.Included[i].Item, pinned))
            {
                pinnedIncluded = result.Report.Included[i];
                break;
            }
        }
        await Assert.That(pinnedIncluded).IsNotNull();
        await Assert.That(pinnedIncluded!.Reason).IsEqualTo(InclusionReason.Pinned);
        await Assert.That(pinnedIncluded.Score).IsEqualTo(1.0);
    }

    [Test]
    public async Task Report_SlicerExcludesItems_ExcludedContainsBudgetExceeded()
    {
        // Budget fits 2 items of 30 tokens (target=60), 3rd excluded
        var item1 = CreateItem("a", tokens: 30, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 30, futureRelevanceHint: 0.8);
        var item3 = CreateItem("c", tokens: 30, futureRelevanceHint: 0.1);
        var trace = new DiagnosticTraceCollector();

        var pipeline = BuildPipeline(maxTokens: 100, targetTokens: 60);
        var result = pipeline.Execute([item1, item2, item3], trace);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Excluded.Count).IsGreaterThanOrEqualTo(1);

        var budgetExcluded = false;
        for (var i = 0; i < result.Report.Excluded.Count; i++)
        {
            if (result.Report.Excluded[i].Reason == ExclusionReason.BudgetExceeded)
            {
                budgetExcluded = true;
                break;
            }
        }
        await Assert.That(budgetExcluded).IsTrue();
    }

    [Test]
    public async Task Report_DedupExcludesItems_ExcludedContainsDeduplicated()
    {
        var winner = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.9);
        var loser = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.3);
        var trace = new DiagnosticTraceCollector();

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([winner, loser], trace);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Excluded.Count).IsEqualTo(1);
        await Assert.That(result.Report.Excluded[0].Reason).IsEqualTo(ExclusionReason.Deduplicated);
        await Assert.That(result.Report.Excluded[0].DeduplicatedAgainst).IsEqualTo(winner);
    }

    [Test]
    public async Task Report_NegativeTokenItems_ExcludedWithNegativeTokensReason()
    {
        var negative = CreateItem("negative", tokens: -1, futureRelevanceHint: 1.0);
        var valid = CreateItem("valid", tokens: 10, futureRelevanceHint: 0.5);
        var trace = new DiagnosticTraceCollector();

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([negative, valid], trace);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Excluded.Count).IsEqualTo(1);
        await Assert.That(result.Report.Excluded[0].Reason).IsEqualTo(ExclusionReason.NegativeTokens);
        await Assert.That(result.Report.Excluded[0].Item).IsEqualTo(negative);
    }

    [Test]
    public async Task Report_TotalCandidates_EqualsInputCount()
    {
        var items = new[]
        {
            CreateItem("a", tokens: 10, futureRelevanceHint: 0.8),
            CreateItem("b", tokens: -1),
            CreateItem("c", tokens: 10, futureRelevanceHint: 0.5)
        };
        var trace = new DiagnosticTraceCollector();

        var pipeline = BuildPipeline();
        var result = pipeline.Execute(items, trace);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.TotalCandidates).IsEqualTo(3);
    }

    [Test]
    public async Task Report_TotalTokensConsidered_SumOfNonNegativeTokenItems()
    {
        var items = new[]
        {
            CreateItem("a", tokens: 10, futureRelevanceHint: 0.8),
            CreateItem("b", tokens: -5),
            CreateItem("c", tokens: 20, futureRelevanceHint: 0.5)
        };
        var trace = new DiagnosticTraceCollector();

        var pipeline = BuildPipeline();
        var result = pipeline.Execute(items, trace);

        await Assert.That(result.Report).IsNotNull();
        // Only a(10) and c(20) are non-negative = 30
        await Assert.That(result.Report!.TotalTokensConsidered).IsEqualTo(30);
    }

    [Test]
    public async Task Report_ExcludedOrderedByScoreDescending()
    {
        // Force multiple exclusions with different scores
        var item1 = CreateItem("a", tokens: 40, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 40, futureRelevanceHint: 0.5);
        var item3 = CreateItem("c", tokens: 40, futureRelevanceHint: 0.1);
        var trace = new DiagnosticTraceCollector();

        // Budget only fits 1 item (target=40)
        var pipeline = BuildPipeline(maxTokens: 100, targetTokens: 40);
        var result = pipeline.Execute([item1, item2, item3], trace);

        await Assert.That(result.Report).IsNotNull();
        if (result.Report!.Excluded.Count >= 2)
        {
            // Excluded should be sorted by score descending
            for (var i = 0; i < result.Report.Excluded.Count - 1; i++)
            {
                await Assert.That(result.Report.Excluded[i].Score)
                    .IsGreaterThanOrEqualTo(result.Report.Excluded[i + 1].Score);
            }
        }
    }

    [Test]
    public async Task Report_WithoutDiagnosticTrace_ReportIsNull()
    {
        var item = CreateItem("test", tokens: 10, futureRelevanceHint: 0.5);

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([item]);

        await Assert.That(result.Report).IsNull();
    }

    [Test]
    public async Task Report_ZeroTokenItem_IncludedWithZeroTokenReason()
    {
        var zeroToken = CreateItem("zero", tokens: 0, futureRelevanceHint: 0.5);
        var trace = new DiagnosticTraceCollector();

        var pipeline = BuildPipeline();
        var result = pipeline.Execute([zeroToken], trace);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Included.Count).IsEqualTo(1);
        await Assert.That(result.Report.Included[0].Reason).IsEqualTo(InclusionReason.ZeroToken);
    }

    // ReservedSlots budget reduction tests

    [Test]
    public async Task Execute_WithReservedSlots_ReducesEffectiveBudget()
    {
        // Budget: maxTokens=1000, targetTokens=800, reservedSlots: { "message": 200 }
        // 10 items at 100 tokens each, all equal score
        // Without reserved: effectiveTarget = 800, fits 8 items
        // With reserved: effectiveTarget = 800 - 200 = 600, fits 6 items
        var items = new List<ContextItem>();
        for (var i = 0; i < 10; i++)
            items.Add(CreateItem($"item-{i}", tokens: 100, futureRelevanceHint: 0.5));

        var budgetWithReserved = new ContextBudget(
            maxTokens: 1000,
            targetTokens: 800,
            reservedSlots: new Dictionary<ContextKind, int> { [ContextKind.Message] = 200 });

        var pipelineWithReserved = CupelPipeline.CreateBuilder()
            .WithBudget(budgetWithReserved)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var resultWithReserved = pipelineWithReserved.Execute(items);

        // Baseline without reserved slots
        var budgetBaseline = new ContextBudget(maxTokens: 1000, targetTokens: 800);
        var pipelineBaseline = CupelPipeline.CreateBuilder()
            .WithBudget(budgetBaseline)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var resultBaseline = pipelineBaseline.Execute(items);

        // Baseline should select 8 items (800 / 100)
        await Assert.That(resultBaseline.Items.Count).IsEqualTo(8);
        // Reserved should select 6 items (600 / 100)
        await Assert.That(resultWithReserved.Items.Count).IsEqualTo(6);
    }

    [Test]
    public async Task Execute_WithMultipleReservedSlots_SubtractsCombinedTotal()
    {
        // Budget: maxTokens=1000, targetTokens=1000, reservedSlots: { "message": 100, "tool_result": 150 }
        // 10 items at 100 tokens each, all equal score
        // effectiveTarget = 1000 - 250 = 750, fits 7 items
        var items = new List<ContextItem>();
        for (var i = 0; i < 10; i++)
            items.Add(CreateItem($"item-{i}", tokens: 100, futureRelevanceHint: 0.5));

        var budget = new ContextBudget(
            maxTokens: 1000,
            targetTokens: 1000,
            reservedSlots: new Dictionary<ContextKind, int>
            {
                [ContextKind.Message] = 100,
                [new ContextKind("tool_result")] = 150
            });

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(budget)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var result = pipeline.Execute(items);

        await Assert.That(result.Items.Count).IsEqualTo(7);
    }

    [Test]
    public async Task Execute_WithEmptyReservedSlots_NoChange()
    {
        // Default empty reservedSlots should produce same result as no reservedSlots
        var items = new List<ContextItem>();
        for (var i = 0; i < 10; i++)
            items.Add(CreateItem($"item-{i}", tokens: 100, futureRelevanceHint: 0.5));

        var budgetExplicitEmpty = new ContextBudget(
            maxTokens: 1000,
            targetTokens: 800,
            reservedSlots: new Dictionary<ContextKind, int>());

        var budgetDefault = new ContextBudget(maxTokens: 1000, targetTokens: 800);

        var pipelineEmpty = CupelPipeline.CreateBuilder()
            .WithBudget(budgetExplicitEmpty)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var pipelineDefault = CupelPipeline.CreateBuilder()
            .WithBudget(budgetDefault)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var resultEmpty = pipelineEmpty.Execute(items);
        var resultDefault = pipelineDefault.Execute(items);

        await Assert.That(resultEmpty.Items.Count).IsEqualTo(resultDefault.Items.Count);
    }

    // EstimationSafetyMarginPercent budget reduction tests

    [Test]
    public async Task Execute_WithEstimationSafetyMargin_ReducesEffectiveBudget()
    {
        // Budget: maxTokens=1000, targetTokens=1000, estimationSafetyMarginPercent=20
        // 10 items at 100 tokens each, all equal score
        // Without margin: effectiveTarget = 1000, fits 10 items
        // With 20% margin: effectiveTarget = 1000 * 0.8 = 800, fits 8 items
        var items = new List<ContextItem>();
        for (var i = 0; i < 10; i++)
            items.Add(CreateItem($"item-{i}", tokens: 100, futureRelevanceHint: 0.5));

        var budgetWithMargin = new ContextBudget(
            maxTokens: 1000,
            targetTokens: 1000,
            estimationSafetyMarginPercent: 20);

        var pipelineWithMargin = CupelPipeline.CreateBuilder()
            .WithBudget(budgetWithMargin)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var resultWithMargin = pipelineWithMargin.Execute(items);

        // Baseline without margin
        var budgetBaseline = new ContextBudget(maxTokens: 1000, targetTokens: 1000);
        var pipelineBaseline = CupelPipeline.CreateBuilder()
            .WithBudget(budgetBaseline)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var resultBaseline = pipelineBaseline.Execute(items);

        // Baseline should select 10 items
        await Assert.That(resultBaseline.Items.Count).IsEqualTo(10);
        // With 20% margin should select 8 items (800 / 100)
        await Assert.That(resultWithMargin.Items.Count).IsEqualTo(8);
    }

    [Test]
    public async Task Execute_WithReservedSlotsAndSafetyMargin_AppliesInCorrectOrder()
    {
        // Budget: maxTokens=1000, targetTokens=1000, reservedSlots: { "message": 200 }, safetyMargin=25%
        // 10 items at 100 tokens each, all equal score
        // Order: subtract reserved first, then apply margin
        // effectiveTarget = (1000 - 200) * 0.75 = 600, fits 6 items
        var items = new List<ContextItem>();
        for (var i = 0; i < 10; i++)
            items.Add(CreateItem($"item-{i}", tokens: 100, futureRelevanceHint: 0.5));

        var budget = new ContextBudget(
            maxTokens: 1000,
            targetTokens: 1000,
            reservedSlots: new Dictionary<ContextKind, int> { [ContextKind.Message] = 200 },
            estimationSafetyMarginPercent: 25);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(budget)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var result = pipeline.Execute(items);

        await Assert.That(result.Items.Count).IsEqualTo(6);
    }

    [Test]
    public async Task Execute_WithZeroSafetyMargin_NoChange()
    {
        // estimationSafetyMarginPercent=0 should produce same result as omitting it
        var items = new List<ContextItem>();
        for (var i = 0; i < 10; i++)
            items.Add(CreateItem($"item-{i}", tokens: 100, futureRelevanceHint: 0.5));

        var budgetZeroMargin = new ContextBudget(
            maxTokens: 1000,
            targetTokens: 1000,
            estimationSafetyMarginPercent: 0);

        var budgetDefault = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var pipelineZero = CupelPipeline.CreateBuilder()
            .WithBudget(budgetZeroMargin)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var pipelineDefault = CupelPipeline.CreateBuilder()
            .WithBudget(budgetDefault)
            .WithScorer(new ReflexiveScorer())
            .Build();

        var resultZero = pipelineZero.Execute(items);
        var resultDefault = pipelineDefault.Execute(items);

        await Assert.That(resultZero.Items.Count).IsEqualTo(resultDefault.Items.Count);
    }

    // Streaming path ReservedSlots + SafetyMargin tests

    [Test]
    public async Task ExecuteStreamAsync_WithReservedSlots_ReducesEffectiveBudget()
    {
        // Streaming path should also subtract reserved slots
        var items = new ContextItem[10];
        for (var i = 0; i < 10; i++)
            items[i] = CreateItem($"item-{i}", tokens: 100, futureRelevanceHint: 0.5, kind: ContextKind.Message);

        var budget = new ContextBudget(
            maxTokens: 1000,
            targetTokens: 1000,
            reservedSlots: new Dictionary<ContextKind, int> { [ContextKind.Message] = 300 });

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(budget)
            .WithScorer(new ReflexiveScorer())
            .WithAsyncSlicer(new StreamSlice())
            .Build();

        var result = await pipeline.ExecuteStreamAsync(CreateStreamSource(items));

        // effectiveTarget = 1000 - 300 = 700, fits 7 items
        await Assert.That(result.Items.Count).IsEqualTo(7);
    }

    [Test]
    public async Task ExecuteStreamAsync_WithSafetyMargin_ReducesEffectiveBudget()
    {
        // Streaming path should also apply safety margin
        var items = new ContextItem[10];
        for (var i = 0; i < 10; i++)
            items[i] = CreateItem($"item-{i}", tokens: 100, futureRelevanceHint: 0.5, kind: ContextKind.Message);

        var budget = new ContextBudget(
            maxTokens: 1000,
            targetTokens: 1000,
            estimationSafetyMarginPercent: 20);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(budget)
            .WithScorer(new ReflexiveScorer())
            .WithAsyncSlicer(new StreamSlice())
            .Build();

        var result = await pipeline.ExecuteStreamAsync(CreateStreamSource(items));

        // effectiveTarget = 1000 * 0.8 = 800, fits 8 items
        await Assert.That(result.Items.Count).IsEqualTo(8);
    }

    [Test]
    public async Task ExecuteStreamAsync_WithReservedSlotsAndSafetyMargin_AppliesInCorrectOrder()
    {
        // Streaming path: subtract reserved first, then apply margin
        var items = new ContextItem[10];
        for (var i = 0; i < 10; i++)
            items[i] = CreateItem($"item-{i}", tokens: 100, futureRelevanceHint: 0.5, kind: ContextKind.Message);

        var budget = new ContextBudget(
            maxTokens: 1000,
            targetTokens: 1000,
            reservedSlots: new Dictionary<ContextKind, int> { [ContextKind.Message] = 200 },
            estimationSafetyMarginPercent: 25);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(budget)
            .WithScorer(new ReflexiveScorer())
            .WithAsyncSlicer(new StreamSlice())
            .Build();

        var result = await pipeline.ExecuteStreamAsync(CreateStreamSource(items));

        // effectiveTarget = (1000 - 200) * 0.75 = 600, fits 6 items
        await Assert.That(result.Items.Count).IsEqualTo(6);
    }

    // Stream helper

    private static async IAsyncEnumerable<ContextItem> CreateStreamSource(
        params ContextItem[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.CompletedTask;
        }
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
