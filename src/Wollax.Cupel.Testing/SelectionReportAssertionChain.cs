using System.Linq;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Testing;

/// <summary>
/// Fluent assertion chain for <see cref="SelectionReport"/>.
/// Each method asserts a condition and returns <c>this</c> for chaining.
/// On failure, throws <see cref="SelectionReportAssertionException"/> with a structured message.
/// </summary>
public sealed class SelectionReportAssertionChain
{
    private readonly SelectionReport _report;

    internal SelectionReportAssertionChain(SelectionReport report)
    {
        _report = report;
    }

    // ── Pattern 1: Inclusion ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that at least one included item has the specified kind.
    /// </summary>
    public SelectionReportAssertionChain IncludeItemWithKind(ContextKind kind)
    {
        if (!_report.Included.Any(i => i.Item.Kind == kind))
        {
            var kinds = string.Join(", ", _report.Included.Select(i => i.Item.Kind.ToString()).Distinct());
            throw new SelectionReportAssertionException(
                $"IncludeItemWithKind({kind}) failed: Included contained 0 items with Kind={kind}. " +
                $"Included had {_report.Included.Count} items with kinds: [{kinds}].");
        }
        return this;
    }

    // ── Pattern 2: Inclusion ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that at least one included item matches the predicate.
    /// </summary>
    public SelectionReportAssertionChain IncludeItemMatching(Func<IncludedItem, bool> predicate)
    {
        if (!_report.Included.Any(predicate))
        {
            throw new SelectionReportAssertionException(
                $"IncludeItemMatching failed: no item in Included matched the predicate. " +
                $"Included had {_report.Included.Count} items.");
        }
        return this;
    }

    // ── Pattern 3: Inclusion ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that exactly <paramref name="n"/> included items have the specified kind.
    /// </summary>
    public SelectionReportAssertionChain IncludeExactlyNItemsWithKind(ContextKind kind, int n)
    {
        var actual = _report.Included.Count(i => i.Item.Kind == kind);
        if (actual != n)
        {
            throw new SelectionReportAssertionException(
                $"IncludeExactlyNItemsWithKind({kind}, {n}) failed: expected {n} items with Kind={kind} in Included, " +
                $"but found {actual}. Included had {_report.Included.Count} items total.");
        }
        return this;
    }

    // ── Pattern 4: Exclusion ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that at least one excluded item has the specified reason.
    /// </summary>
    public SelectionReportAssertionChain ExcludeItemWithReason(ExclusionReason reason)
    {
        if (!_report.Excluded.Any(e => e.Reason == reason))
        {
            var reasons = string.Join(", ", _report.Excluded.Select(e => e.Reason.ToString()).Distinct());
            throw new SelectionReportAssertionException(
                $"ExcludeItemWithReason({reason}) failed: no excluded item had reason {reason}. " +
                $"Excluded had {_report.Excluded.Count} items with reasons: [{reasons}].");
        }
        return this;
    }

    // ── Pattern 5: Exclusion ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that at least one excluded item matches the predicate and has the specified reason.
    /// </summary>
    public SelectionReportAssertionChain ExcludeItemMatchingWithReason(Func<ContextItem, bool> predicate, ExclusionReason reason)
    {
        var predicateMatches = _report.Excluded.Where(e => predicate(e.Item)).ToList();
        if (!predicateMatches.Any(e => e.Reason == reason))
        {
            var reasons = string.Join(", ", predicateMatches.Select(e => e.Reason.ToString()).Distinct());
            throw new SelectionReportAssertionException(
                $"ExcludeItemMatchingWithReason(reason={reason}) failed: predicate matched {predicateMatches.Count} " +
                $"excluded item(s) but none had reason {reason}. Matched items had reasons: [{reasons}].");
        }
        return this;
    }

    // ── Pattern 6: Exclusion ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that at least one excluded item matching the predicate was excluded due to <see cref="ExclusionReason.BudgetExceeded"/>.
    /// </summary>
    /// <remarks>
    /// .NET degenerate form: the flat <see cref="ExclusionReason"/> enum has no token-detail fields,
    /// so this method accepts only a predicate to identify the item.
    /// </remarks>
    public SelectionReportAssertionChain HaveExcludedItemWithBudgetExceeded(Func<ContextItem, bool> predicate)
    {
        if (!_report.Excluded.Any(e => predicate(e.Item) && e.Reason == ExclusionReason.BudgetExceeded))
        {
            throw new SelectionReportAssertionException(
                "HaveExcludedItemWithBudgetExceeded failed: no excluded item matching the predicate had reason BudgetExceeded.");
        }
        return this;
    }

    // ── Pattern 7: Exclusion ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that no excluded item has the specified kind.
    /// </summary>
    public SelectionReportAssertionChain HaveNoExclusionsForKind(ContextKind kind)
    {
        var matching = _report.Excluded.Where(e => e.Item.Kind == kind).ToList();
        if (matching.Any())
        {
            throw new SelectionReportAssertionException(
                $"HaveNoExclusionsForKind({kind}) failed: found {matching.Count} excluded item(s) with Kind={kind}. " +
                $"First: score={matching[0].Score:F4}, reason={matching[0].Reason}.");
        }
        return this;
    }

    // ── Pattern 8: Aggregate ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the report has at least <paramref name="n"/> excluded items.
    /// </summary>
    public SelectionReportAssertionChain HaveAtLeastNExclusions(int n)
    {
        if (_report.Excluded.Count < n)
        {
            throw new SelectionReportAssertionException(
                $"HaveAtLeastNExclusions({n}) failed: expected at least {n} excluded items, but Excluded had {_report.Excluded.Count}.");
        }
        return this;
    }

    // ── Pattern 9: Ordering ─────────────────────────────────────────────────

    /// <summary>
    /// Asserts that excluded items are sorted by score in non-increasing (descending) order.
    /// </summary>
    public SelectionReportAssertionChain ExcludedItemsAreSortedByScoreDescending()
    {
        for (int i = 0; i < _report.Excluded.Count - 1; i++)
        {
            if (_report.Excluded[i].Score < _report.Excluded[i + 1].Score)
            {
                throw new SelectionReportAssertionException(
                    $"ExcludedItemsAreSortedByScoreDescending failed: item at index {i + 1} (score={_report.Excluded[i + 1].Score:F6}) " +
                    $"is higher than item at index {i} (score={_report.Excluded[i].Score:F6}). Expected non-increasing scores.");
            }
        }
        return this;
    }

    // ── Pattern 10: Budget ──────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the budget utilization (included tokens / budget max tokens) exceeds <paramref name="threshold"/>.
    /// </summary>
    public SelectionReportAssertionChain HaveBudgetUtilizationAbove(double threshold, ContextBudget budget)
    {
        var includedTokens = _report.Included.Sum(i => (long)i.Item.Tokens);
        var actual = includedTokens / (double)budget.MaxTokens;
        if (actual < threshold)
        {
            throw new SelectionReportAssertionException(
                $"HaveBudgetUtilizationAbove({threshold}) failed: computed utilization was {actual:F6} " +
                $"(includedTokens={includedTokens}, budget.MaxTokens={budget.MaxTokens}).");
        }
        return this;
    }

    // ── Pattern 11: Coverage ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that at least <paramref name="n"/> distinct <see cref="ContextKind"/> values appear in included items.
    /// </summary>
    public SelectionReportAssertionChain HaveKindCoverageCount(int n)
    {
        var distinctKinds = _report.Included.Select(i => i.Item.Kind).Distinct().ToList();
        if (distinctKinds.Count < n)
        {
            throw new SelectionReportAssertionException(
                $"HaveKindCoverageCount({n}) failed: expected at least {n} distinct ContextKind values in Included, " +
                $"but found {distinctKinds.Count}: [{string.Join(", ", distinctKinds)}].");
        }
        return this;
    }

    // ── Pattern 12: Ordering ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the item matching <paramref name="predicate"/> is placed at the first or last position in Included.
    /// </summary>
    public SelectionReportAssertionChain PlaceItemAtEdge(Func<IncludedItem, bool> predicate)
    {
        var included = _report.Included;
        if (included.Count == 0 || !included.Any(predicate))
        {
            throw new SelectionReportAssertionException(
                "PlaceItemAtEdge failed: no item in Included matched the predicate.");
        }
        var idx = included.Select((item, i) => (item, i)).First(t => predicate(t.item)).i;
        var last = included.Count - 1;
        if (idx != 0 && idx != last)
        {
            throw new SelectionReportAssertionException(
                $"PlaceItemAtEdge failed: item matching predicate was at index {idx} (not at edge). " +
                $"Edge positions: 0 and {last}. Included had {included.Count} items.");
        }
        return this;
    }

    // ── Pattern 13: Ordering ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the top-<paramref name="n"/> scored items are placed at edge positions (alternating from both ends).
    /// </summary>
    public SelectionReportAssertionChain PlaceTopNScoredAtEdges(int n)
    {
        if (n == 0) return this;
        if (n > _report.Included.Count)
        {
            throw new SelectionReportAssertionException(
                $"PlaceTopNScoredAtEdges({n}) failed: n={n} exceeds Included count={_report.Included.Count}.");
        }

        var topNItems = _report.Included.OrderByDescending(i => i.Score).Take(n).ToHashSet();
        var minTopScore = topNItems.Min(i => i.Score);

        var edgePositions = new List<int>();
        for (int lo = 0, hi = _report.Included.Count - 1; edgePositions.Count < n; lo++, hi--)
        {
            edgePositions.Add(lo);
            if (lo != hi && edgePositions.Count < n)
                edgePositions.Add(hi);
        }

        var edgeSet = new HashSet<int>(edgePositions);
        int failCount = 0;
        for (int i = 0; i < _report.Included.Count; i++)
        {
            var item = _report.Included[i];
            if (item.Score >= minTopScore && topNItems.Contains(item) && !edgeSet.Contains(i))
                failCount++;
        }

        if (failCount > 0)
        {
            var topItems = string.Join(", ", topNItems.Select(i => $"score={i.Score:F6}"));
            var edgePos = string.Join(", ", edgePositions);
            throw new SelectionReportAssertionException(
                $"PlaceTopNScoredAtEdges({n}) failed: {failCount} top-scored item(s) were not at edge positions. " +
                $"Top items: [{topItems}]. Edge positions: [{edgePos}].");
        }
        return this;
    }
}
