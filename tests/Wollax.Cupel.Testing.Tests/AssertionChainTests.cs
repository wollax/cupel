using TUnit.Core;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Testing;

namespace Wollax.Cupel.Testing.Tests;

public class AssertionChainTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static ContextItem MakeItem(string content = "x", int tokens = 10, ContextKind? kind = null)
        => new() { Content = content, Tokens = tokens, Kind = kind ?? ContextKind.Message };

    private static IncludedItem MakeIncluded(ContextItem? item = null, double score = 0.5, InclusionReason reason = InclusionReason.Scored)
        => new() { Item = item ?? MakeItem(), Score = score, Reason = reason };

    private static ExcludedItem MakeExcluded(ContextItem? item = null, double score = 0.3, ExclusionReason reason = ExclusionReason.BudgetExceeded)
        => new() { Item = item ?? MakeItem(), Score = score, Reason = reason };

    private static SelectionReport MakeReport(
        IReadOnlyList<IncludedItem>? included = null,
        IReadOnlyList<ExcludedItem>? excluded = null,
        int totalCandidates = 5,
        int totalTokensConsidered = 100)
        => new()
        {
            Events = [],
            Included = included ?? [],
            Excluded = excluded ?? [],
            TotalCandidates = totalCandidates,
            TotalTokensConsidered = totalTokensConsidered,
        };

    // ── Pattern 1: IncludeItemWithKind ───────────────────────────────────────

    [Test]
    public void Pattern1_Pass_WhenIncludedHasMatchingKind()
    {
        var report = MakeReport(included: [MakeIncluded(MakeItem(kind: ContextKind.Message))]);
        report.Should().IncludeItemWithKind(ContextKind.Message);
    }

    [Test]
    public void Pattern1_Fail_WhenNoMatchingKind()
    {
        var report = MakeReport(included: [MakeIncluded(MakeItem(kind: ContextKind.Document))]);
        try
        {
            report.Should().IncludeItemWithKind(ContextKind.Message);
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("IncludeItemWithKind"))
                throw new Exception($"Wrong message: {ex.Message}");
        }
    }

    // ── Pattern 2: IncludeItemMatching ───────────────────────────────────────

    [Test]
    public void Pattern2_Pass_WhenPredicateMatches()
    {
        var item = MakeItem(content: "hello", tokens: 42);
        var report = MakeReport(included: [MakeIncluded(item)]);
        report.Should().IncludeItemMatching(i => i.Item.Content == "hello");
    }

    [Test]
    public void Pattern2_Fail_WhenNoMatchingItem()
    {
        var report = MakeReport(included: []);
        try
        {
            report.Should().IncludeItemMatching(i => i.Item.Content == "hello");
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("IncludeItemMatching"))
                throw new Exception($"Wrong message: {ex.Message}");
        }
    }

    // ── Pattern 3: IncludeExactlyNItemsWithKind ──────────────────────────────

    [Test]
    public void Pattern3_Pass_WhenExactCountMatches()
    {
        var items = new[]
        {
            MakeIncluded(MakeItem(kind: ContextKind.Message)),
            MakeIncluded(MakeItem(kind: ContextKind.Message)),
        };
        var report = MakeReport(included: items);
        report.Should().IncludeExactlyNItemsWithKind(ContextKind.Message, 2);
    }

    [Test]
    public void Pattern3_Fail_WhenCountDoesNotMatch()
    {
        var items = new[]
        {
            MakeIncluded(MakeItem(kind: ContextKind.Message)),
            MakeIncluded(MakeItem(kind: ContextKind.Message)),
            MakeIncluded(MakeItem(kind: ContextKind.Message)),
        };
        var report = MakeReport(included: items);
        try
        {
            report.Should().IncludeExactlyNItemsWithKind(ContextKind.Message, 2);
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("IncludeExactlyNItemsWithKind"))
                throw new Exception($"Wrong message: {ex.Message}");
        }
    }

    // ── Pattern 4: ExcludeItemWithReason ─────────────────────────────────────

    [Test]
    public void Pattern4_Pass_WhenExcludedHasMatchingReason()
    {
        var report = MakeReport(excluded: [MakeExcluded(reason: ExclusionReason.BudgetExceeded)]);
        report.Should().ExcludeItemWithReason(ExclusionReason.BudgetExceeded);
    }

    [Test]
    public void Pattern4_Fail_WhenNoExcludedWithReason()
    {
        var report = MakeReport(excluded: []);
        try
        {
            report.Should().ExcludeItemWithReason(ExclusionReason.BudgetExceeded);
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("ExcludeItemWithReason"))
                throw new Exception($"Wrong message: {ex.Message}");
        }
    }

    // ── Pattern 5: ExcludeItemMatchingWithReason ─────────────────────────────

    [Test]
    public void Pattern5_Pass_WhenPredicateMatchesAndReasonCorrect()
    {
        var item = MakeItem(content: "big");
        var report = MakeReport(excluded: [MakeExcluded(item, reason: ExclusionReason.BudgetExceeded)]);
        report.Should().ExcludeItemMatchingWithReason(i => i.Content == "big", ExclusionReason.BudgetExceeded);
    }

    [Test]
    public void Pattern5_Fail_WhenPredicateMatchesButWrongReason()
    {
        var item = MakeItem(content: "big");
        var report = MakeReport(excluded: [MakeExcluded(item, reason: ExclusionReason.ScoredTooLow)]);
        try
        {
            report.Should().ExcludeItemMatchingWithReason(i => i.Content == "big", ExclusionReason.BudgetExceeded);
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("ExcludeItemMatchingWithReason"))
                throw new Exception($"Wrong message: {ex.Message}");
        }
    }

    // ── Pattern 6: HaveExcludedItemWithBudgetExceeded ────────────────────────

    [Test]
    public void Pattern6_Pass_WhenItemWithBudgetExceededExists()
    {
        var item = MakeItem(content: "overbudget");
        var report = MakeReport(excluded: [MakeExcluded(item, reason: ExclusionReason.BudgetExceeded)]);
        report.Should().HaveExcludedItemWithBudgetExceeded(i => i.Content == "overbudget");
    }

    [Test]
    public void Pattern6_Fail_WhenNoBudgetExceededExclusion()
    {
        var report = MakeReport(excluded: []);
        try
        {
            report.Should().HaveExcludedItemWithBudgetExceeded(i => true);
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("HaveExcludedItemWithBudgetExceeded"))
                throw new Exception($"Wrong message: {ex.Message}");
        }
    }

    // ── Pattern 7: HaveNoExclusionsForKind ───────────────────────────────────

    [Test]
    public void Pattern7_Pass_WhenNoExclusionForKind()
    {
        var report = MakeReport(excluded: [MakeExcluded(MakeItem(kind: ContextKind.Document))]);
        report.Should().HaveNoExclusionsForKind(ContextKind.Message);
    }

    [Test]
    public void Pattern7_Fail_WhenExclusionExistsForKind()
    {
        var report = MakeReport(excluded: [MakeExcluded(MakeItem(kind: ContextKind.Message))]);
        try
        {
            report.Should().HaveNoExclusionsForKind(ContextKind.Message);
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("HaveNoExclusionsForKind"))
                throw new Exception($"Wrong message: {ex.Message}");
        }
    }

    // ── Pattern 8: HaveAtLeastNExclusions ────────────────────────────────────

    [Test]
    public void Pattern8_Pass_WhenEnoughExclusions()
    {
        var report = MakeReport(excluded: [MakeExcluded(), MakeExcluded()]);
        report.Should().HaveAtLeastNExclusions(2);
    }

    [Test]
    public void Pattern8_Fail_WhenTooFewExclusions()
    {
        var report = MakeReport(excluded: [MakeExcluded()]);
        try
        {
            report.Should().HaveAtLeastNExclusions(2);
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("HaveAtLeastNExclusions(2)"))
                throw new Exception($"Wrong message: {ex.Message}");
            if (!ex.Message.Contains("Excluded had 1"))
                throw new Exception($"Missing count in message: {ex.Message}");
        }
    }

    // ── Pattern 9: ExcludedItemsAreSortedByScoreDescending ───────────────────

    [Test]
    public void Pattern9_Pass_WhenExcludedSortedDescending()
    {
        var report = MakeReport(excluded: [
            MakeExcluded(score: 0.9),
            MakeExcluded(score: 0.7),
            MakeExcluded(score: 0.5),
        ]);
        report.Should().ExcludedItemsAreSortedByScoreDescending();
    }

    [Test]
    public void Pattern9_Fail_WhenExcludedNotSorted()
    {
        var report = MakeReport(excluded: [
            MakeExcluded(score: 0.5),
            MakeExcluded(score: 0.9),
        ]);
        try
        {
            report.Should().ExcludedItemsAreSortedByScoreDescending();
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("ExcludedItemsAreSortedByScoreDescending"))
                throw new Exception($"Wrong message: {ex.Message}");
            if (!ex.Message.Contains("index 1"))
                throw new Exception($"Missing index in message: {ex.Message}");
        }
    }

    // ── Pattern 10: HaveBudgetUtilizationAbove ───────────────────────────────

    [Test]
    public void Pattern10_Pass_WhenUtilizationAboveThreshold()
    {
        // 800 tokens included out of 1000 max = 0.80 utilization
        var item = MakeItem(tokens: 800);
        var report = MakeReport(included: [MakeIncluded(item)]);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 800);
        report.Should().HaveBudgetUtilizationAbove(0.5, budget);
    }

    [Test]
    public void Pattern10_Fail_WhenUtilizationBelowThreshold()
    {
        // 100 tokens included out of 1000 max = 0.10 utilization
        var item = MakeItem(tokens: 100);
        var report = MakeReport(included: [MakeIncluded(item)]);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 500);
        try
        {
            report.Should().HaveBudgetUtilizationAbove(0.5, budget);
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("HaveBudgetUtilizationAbove(0.5)"))
                throw new Exception($"Wrong message: {ex.Message}");
            if (!ex.Message.Contains("includedTokens=100"))
                throw new Exception($"Missing token count: {ex.Message}");
        }
    }

    // ── Pattern 11: HaveKindCoverageCount ────────────────────────────────────

    [Test]
    public void Pattern11_Pass_WhenEnoughDistinctKinds()
    {
        var report = MakeReport(included: [
            MakeIncluded(MakeItem(kind: ContextKind.Message)),
            MakeIncluded(MakeItem(kind: ContextKind.Document)),
        ]);
        report.Should().HaveKindCoverageCount(2);
    }

    [Test]
    public void Pattern11_Fail_WhenTooFewDistinctKinds()
    {
        var report = MakeReport(included: [
            MakeIncluded(MakeItem(kind: ContextKind.Message)),
        ]);
        try
        {
            report.Should().HaveKindCoverageCount(2);
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("HaveKindCoverageCount(2)"))
                throw new Exception($"Wrong message: {ex.Message}");
            if (!ex.Message.Contains("found 1"))
                throw new Exception($"Missing count: {ex.Message}");
        }
    }

    // ── Pattern 12: PlaceItemAtEdge ──────────────────────────────────────────

    [Test]
    public void Pattern12_Pass_WhenMatchingItemAtEdge()
    {
        var special = MakeItem(content: "edge");
        var report = MakeReport(included: [
            MakeIncluded(special),
            MakeIncluded(MakeItem(content: "middle")),
            MakeIncluded(MakeItem(content: "other")),
        ]);
        report.Should().PlaceItemAtEdge(i => i.Item.Content == "edge");
    }

    [Test]
    public void Pattern12_Fail_WhenMatchingItemNotAtEdge()
    {
        var special = MakeItem(content: "middle");
        var report = MakeReport(included: [
            MakeIncluded(MakeItem(content: "first")),
            MakeIncluded(special),
            MakeIncluded(MakeItem(content: "last")),
        ]);
        try
        {
            report.Should().PlaceItemAtEdge(i => i.Item.Content == "middle");
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("PlaceItemAtEdge"))
                throw new Exception($"Wrong message: {ex.Message}");
            if (!ex.Message.Contains("index 1"))
                throw new Exception($"Missing index: {ex.Message}");
        }
    }

    // ── Pattern 13: PlaceTopNScoredAtEdges ───────────────────────────────────

    [Test]
    public void Pattern13_Pass_WhenTopNAtEdges()
    {
        // Top 2: scores 0.95 and 0.90 — place at indices 0 and 2 (edges)
        var report = MakeReport(included: [
            MakeIncluded(score: 0.95),
            MakeIncluded(score: 0.50),
            MakeIncluded(score: 0.90),
        ]);
        report.Should().PlaceTopNScoredAtEdges(2);
    }

    [Test]
    public void Pattern13_Fail_WhenTopNNotAtEdges()
    {
        // Top 2: scores 0.95 and 0.90 — but placed at indices 1 and 2, not edges 0 and 2
        var report = MakeReport(included: [
            MakeIncluded(score: 0.50),
            MakeIncluded(score: 0.95),
            MakeIncluded(score: 0.90),
        ]);
        try
        {
            report.Should().PlaceTopNScoredAtEdges(2);
            throw new Exception("Expected SelectionReportAssertionException");
        }
        catch (SelectionReportAssertionException ex)
        {
            if (!ex.Message.Contains("PlaceTopNScoredAtEdges(2)"))
                throw new Exception($"Wrong message: {ex.Message}");
        }
    }
}
