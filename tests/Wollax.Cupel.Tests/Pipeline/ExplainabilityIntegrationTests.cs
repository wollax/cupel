using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Pipeline;

/// <summary>
/// End-to-end integration tests validating the four Phase 7 success criteria:
/// SC1 — SelectionReport lists included/excluded items with reasons
/// SC2 — DryRun idempotency
/// SC3 — OverflowStrategy behaviors
/// SC4 — Observer callback details
/// </summary>
public class ExplainabilityIntegrationTests
{
    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10,
        double? futureRelevanceHint = null,
        bool pinned = false,
        ContextKind? kind = null) =>
        new()
        {
            Content = content,
            Tokens = tokens,
            FutureRelevanceHint = futureRelevanceHint,
            Pinned = pinned,
            Kind = kind ?? ContextKind.Message
        };

    // SC1 — SelectionReport lists included and excluded items with reasons

    [Test]
    public async Task SC1_Report_IncludedAndExcludedWithReasons()
    {
        // 10 items, budget fits 5 (target=50, each item=10 tokens)
        var items = new ContextItem[10];
        for (var i = 0; i < 10; i++)
            items[i] = CreateItem($"item-{i}", tokens: 10, futureRelevanceHint: (i + 1) / 10.0);

        var trace = new DiagnosticTraceCollector();
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 50))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var result = pipeline.Execute(items, trace);

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Included.Count).IsEqualTo(5);
        await Assert.That(result.Report.Excluded.Count).IsEqualTo(5);

        // All included items should have InclusionReason.Scored
        for (var i = 0; i < result.Report.Included.Count; i++)
            await Assert.That(result.Report.Included[i].Reason).IsEqualTo(InclusionReason.Scored);

        // All excluded items should have ExclusionReason.BudgetExceeded with scores
        for (var i = 0; i < result.Report.Excluded.Count; i++)
        {
            await Assert.That(result.Report.Excluded[i].Reason).IsEqualTo(ExclusionReason.BudgetExceeded);
            await Assert.That(result.Report.Excluded[i].Score).IsGreaterThanOrEqualTo(0.0);
        }

        // Totals
        await Assert.That(result.Report.TotalCandidates).IsEqualTo(10);
        await Assert.That(result.Report.TotalTokensConsidered).IsEqualTo(100); // 10 items x 10 tokens
    }

    [Test]
    public async Task SC1_Report_PinnedDedupNegativeTokens()
    {
        // 2 pinned, 2 duplicates (same content), 1 negative-token, 5 normal
        var pinned1 = CreateItem("pinned-1", tokens: 10, pinned: true);
        var pinned2 = CreateItem("pinned-2", tokens: 10, pinned: true);
        var dup1 = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.9);
        var dup2 = CreateItem("duplicate", tokens: 10, futureRelevanceHint: 0.3);
        var negative = CreateItem("negative", tokens: -5);
        var normal1 = CreateItem("normal-1", tokens: 10, futureRelevanceHint: 0.8);
        var normal2 = CreateItem("normal-2", tokens: 10, futureRelevanceHint: 0.7);
        var normal3 = CreateItem("normal-3", tokens: 10, futureRelevanceHint: 0.6);
        var normal4 = CreateItem("normal-4", tokens: 10, futureRelevanceHint: 0.5);
        var normal5 = CreateItem("normal-5", tokens: 10, futureRelevanceHint: 0.4);

        var trace = new DiagnosticTraceCollector();
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var result = pipeline.Execute(
            [pinned1, pinned2, dup1, dup2, negative, normal1, normal2, normal3, normal4, normal5],
            trace);

        await Assert.That(result.Report).IsNotNull();
        var report = result.Report!;

        // Pinned items should be included with InclusionReason.Pinned
        var pinnedIncluded = 0;
        for (var i = 0; i < report.Included.Count; i++)
        {
            if (report.Included[i].Reason == InclusionReason.Pinned)
                pinnedIncluded++;
        }
        await Assert.That(pinnedIncluded).IsEqualTo(2);

        // Duplicate should be excluded with Deduplicated reason and DeduplicatedAgainst set
        var dedupExcluded = default(ExcludedItem);
        for (var i = 0; i < report.Excluded.Count; i++)
        {
            if (report.Excluded[i].Reason == ExclusionReason.Deduplicated)
            {
                dedupExcluded = report.Excluded[i];
                break;
            }
        }
        await Assert.That(dedupExcluded).IsNotNull();
        await Assert.That(dedupExcluded!.DeduplicatedAgainst).IsNotNull();
        await Assert.That(dedupExcluded.DeduplicatedAgainst!.Content).IsEqualTo("duplicate");

        // Negative-token item should be excluded with NegativeTokens reason
        var negativeExcluded = default(ExcludedItem);
        for (var i = 0; i < report.Excluded.Count; i++)
        {
            if (report.Excluded[i].Reason == ExclusionReason.NegativeTokens)
            {
                negativeExcluded = report.Excluded[i];
                break;
            }
        }
        await Assert.That(negativeExcluded).IsNotNull();
        await Assert.That(negativeExcluded!.Item.Content).IsEqualTo("negative");

        // TotalCandidates includes all 10 input items
        await Assert.That(report.TotalCandidates).IsEqualTo(10);
    }

    // SC2 — DryRun idempotency

    [Test]
    public async Task SC2_DryRun_IsIdempotent()
    {
        var items = new[]
        {
            CreateItem("a", tokens: 30, futureRelevanceHint: 0.9),
            CreateItem("b", tokens: 30, futureRelevanceHint: 0.7),
            CreateItem("c", tokens: 30, futureRelevanceHint: 0.5),
            CreateItem("d", tokens: 30, futureRelevanceHint: 0.3),
            CreateItem("e", tokens: 30, futureRelevanceHint: 0.1)
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 90))
            .WithScorer(new ReflexiveScorer())
            .Build();

        var result1 = pipeline.DryRun(items);
        var result2 = pipeline.DryRun(items);

        // Same items in same order
        await Assert.That(result1.Items.Count).IsEqualTo(result2.Items.Count);
        for (var i = 0; i < result1.Items.Count; i++)
            await Assert.That(result1.Items[i]).IsEqualTo(result2.Items[i]);

        // Reports always non-null from DryRun
        await Assert.That(result1.Report).IsNotNull();
        await Assert.That(result2.Report).IsNotNull();

        // Identical included/excluded lists
        await Assert.That(result1.Report!.Included.Count).IsEqualTo(result2.Report!.Included.Count);
        await Assert.That(result1.Report.Excluded.Count).IsEqualTo(result2.Report.Excluded.Count);
        await Assert.That(result1.Report.TotalCandidates).IsEqualTo(result2.Report.TotalCandidates);
        await Assert.That(result1.Report.TotalTokensConsidered).IsEqualTo(result2.Report.TotalTokensConsidered);

        // Verify same items are included (by reference)
        for (var i = 0; i < result1.Report.Included.Count; i++)
            await Assert.That(result1.Report.Included[i].Item).IsEqualTo(result2.Report.Included[i].Item);
    }

    // SC3 — OverflowStrategy behaviors

    [Test]
    public async Task SC3_Throw_WhenPinnedCauseOverflow()
    {
        // Pinned takes 80 tokens, sliced takes 50, target = 100
        // After merge: 130 > 100 → overflow
        var pinned = CreateItem("pinned", tokens: 80, pinned: true);
        var normal = CreateItem("normal", tokens: 50, futureRelevanceHint: 0.9);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Throw)
            .Build();

        await Assert.That(() => pipeline.Execute([pinned, normal]))
            .Throws<OverflowException>();
    }

    [Test]
    public async Task SC3_Truncate_PinnedOverflow_ReportShowsPinnedOverride()
    {
        // Pinned takes 80, sliced takes 50, target = 100
        var pinned = CreateItem("pinned", tokens: 80, pinned: true);
        var normal1 = CreateItem("normal-1", tokens: 25, futureRelevanceHint: 0.9);
        var normal2 = CreateItem("normal-2", tokens: 25, futureRelevanceHint: 0.5);
        var trace = new DiagnosticTraceCollector();

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Truncate)
            .Build();

        var result = pipeline.Execute([pinned, normal1, normal2], trace);

        // Pinned must be included
        await Assert.That(result.Items).Contains(pinned);
        await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(100);

        await Assert.That(result.Report).IsNotNull();

        // Excluded items should have PinnedOverride reason (because pinned items exist)
        var pinnedOverrideFound = false;
        for (var i = 0; i < result.Report!.Excluded.Count; i++)
        {
            if (result.Report.Excluded[i].Reason == ExclusionReason.PinnedOverride)
            {
                pinnedOverrideFound = true;
                break;
            }
        }
        await Assert.That(pinnedOverrideFound).IsTrue();
    }

    [Test]
    public async Task SC3_Truncate_NoPinnedOverflow_ReportShowsBudgetExceeded()
    {
        // No pinned items; sliced items alone exceed target
        var item1 = CreateItem("a", tokens: 70, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 70, futureRelevanceHint: 0.5);
        var trace = new DiagnosticTraceCollector();

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Truncate)
            .Build();

        var result = pipeline.Execute([item1, item2], trace);

        await Assert.That(result.Report).IsNotNull();

        var budgetExceededFound = false;
        var pinnedOverrideFound = false;
        for (var i = 0; i < result.Report!.Excluded.Count; i++)
        {
            if (result.Report.Excluded[i].Reason == ExclusionReason.BudgetExceeded)
                budgetExceededFound = true;
            if (result.Report.Excluded[i].Reason == ExclusionReason.PinnedOverride)
                pinnedOverrideFound = true;
        }
        await Assert.That(budgetExceededFound).IsTrue();
        await Assert.That(pinnedOverrideFound).IsFalse();
    }

    [Test]
    public async Task SC3_Proceed_CallbackInvoked()
    {
        var item1 = CreateItem("a", tokens: 70, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 70, futureRelevanceHint: 0.8);
        OverflowEvent? capturedEvent = null;

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Proceed, e => capturedEvent = e)
            .Build();

        var result = pipeline.Execute([item1, item2]);

        await Assert.That(capturedEvent).IsNotNull();
        // All items still in result
        await Assert.That(result.Items.Count).IsEqualTo(2);
    }

    // SC4 — Observer callback details

    [Test]
    public async Task SC4_OverflowEvent_TokensOverBudgetIsCorrect()
    {
        // Target = 100. Items total = 140 (70 + 70). Over budget = 40.
        var item1 = CreateItem("a", tokens: 70, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 70, futureRelevanceHint: 0.8);
        OverflowEvent? capturedEvent = null;

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Proceed, e => capturedEvent = e)
            .Build();

        pipeline.Execute([item1, item2]);

        await Assert.That(capturedEvent).IsNotNull();
        await Assert.That(capturedEvent!.TokensOverBudget).IsEqualTo(40); // 140 - 100
    }

    [Test]
    public async Task SC4_OverflowEvent_OverflowingItemsContainsAllMergedItems()
    {
        var item1 = CreateItem("a", tokens: 70, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 70, futureRelevanceHint: 0.8);
        OverflowEvent? capturedEvent = null;

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Proceed, e => capturedEvent = e)
            .Build();

        pipeline.Execute([item1, item2]);

        await Assert.That(capturedEvent).IsNotNull();
        await Assert.That(capturedEvent!.OverflowingItems.Count).IsEqualTo(2);
        await Assert.That(capturedEvent.OverflowingItems).Contains(item1);
        await Assert.That(capturedEvent.OverflowingItems).Contains(item2);
    }

    [Test]
    public async Task SC4_OverflowEvent_BudgetMatchesPipelineBudget()
    {
        var item1 = CreateItem("a", tokens: 70, futureRelevanceHint: 0.9);
        var item2 = CreateItem("b", tokens: 70, futureRelevanceHint: 0.8);
        OverflowEvent? capturedEvent = null;

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new PassAllSlicer())
            .WithOverflowStrategy(OverflowStrategy.Proceed, e => capturedEvent = e)
            .Build();

        pipeline.Execute([item1, item2]);

        await Assert.That(capturedEvent).IsNotNull();
        await Assert.That(capturedEvent!.Budget.MaxTokens).IsEqualTo(200);
        await Assert.That(capturedEvent.Budget.TargetTokens).IsEqualTo(100);
    }

    // Combined scenario: SC1 + SC3 together

    [Test]
    public async Task Combined_RealisticScenario_PinnedDedupBudgetOverflow()
    {
        // Realistic scenario combining pinned, dedup, negative tokens, and truncation
        var pinned = CreateItem("system-prompt", tokens: 30, pinned: true, kind: ContextKind.SystemPrompt);
        var dup1 = CreateItem("repeated-message", tokens: 20, futureRelevanceHint: 0.8);
        var dup2 = CreateItem("repeated-message", tokens: 20, futureRelevanceHint: 0.4);
        var negative = CreateItem("bad-item", tokens: -10);
        var msg1 = CreateItem("msg-1", tokens: 20, futureRelevanceHint: 0.9);
        var msg2 = CreateItem("msg-2", tokens: 20, futureRelevanceHint: 0.7);
        var msg3 = CreateItem("msg-3", tokens: 20, futureRelevanceHint: 0.5);
        var msg4 = CreateItem("msg-4", tokens: 20, futureRelevanceHint: 0.3);

        var trace = new DiagnosticTraceCollector();
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(200, 100))
            .WithScorer(new ReflexiveScorer())
            .WithOverflowStrategy(OverflowStrategy.Truncate)
            .Build();

        var result = pipeline.Execute(
            [pinned, dup1, dup2, negative, msg1, msg2, msg3, msg4],
            trace);

        var report = result.Report!;
        await Assert.That(report).IsNotNull();

        // Total candidates = 8
        await Assert.That(report.TotalCandidates).IsEqualTo(8);

        // Negative item excluded with NegativeTokens
        var hasNegativeExclusion = false;
        for (var i = 0; i < report.Excluded.Count; i++)
        {
            if (report.Excluded[i].Reason == ExclusionReason.NegativeTokens)
            {
                hasNegativeExclusion = true;
                break;
            }
        }
        await Assert.That(hasNegativeExclusion).IsTrue();

        // Duplicate excluded with Deduplicated
        var hasDedupExclusion = false;
        for (var i = 0; i < report.Excluded.Count; i++)
        {
            if (report.Excluded[i].Reason == ExclusionReason.Deduplicated)
            {
                hasDedupExclusion = true;
                break;
            }
        }
        await Assert.That(hasDedupExclusion).IsTrue();

        // Pinned included with Pinned reason
        var hasPinnedInclusion = false;
        for (var i = 0; i < report.Included.Count; i++)
        {
            if (report.Included[i].Reason == InclusionReason.Pinned)
            {
                hasPinnedInclusion = true;
                break;
            }
        }
        await Assert.That(hasPinnedInclusion).IsTrue();

        // Result fits budget
        await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(100);

        // Pinned item is in the result
        await Assert.That(result.Items).Contains(pinned);
    }

    /// <summary>Slicer that passes all items through without filtering.</summary>
    private sealed class PassAllSlicer : ISlicer
    {
        public IReadOnlyList<ContextItem> Slice(
            IReadOnlyList<ScoredItem> scoredItems,
            ContextBudget budget,
            ITraceCollector traceCollector)
        {
            var result = new ContextItem[scoredItems.Count];
            for (var i = 0; i < scoredItems.Count; i++)
                result[i] = scoredItems[i].Item;
            return result;
        }
    }
}
