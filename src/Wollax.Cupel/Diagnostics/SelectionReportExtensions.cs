using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Analytics extension methods on <see cref="SelectionReport"/>.
/// All three methods are pure computations — no side effects, no allocation
/// beyond LINQ internals.
/// </summary>
public static class SelectionReportExtensions
{
    /// <summary>
    /// Fraction of the token budget consumed by the included items.
    /// </summary>
    /// <remarks>
    /// Computed as <c>sum(Included[i].Item.Tokens) / budget.MaxTokens</c>.
    /// Can exceed <c>1.0</c> when the pipeline runs under
    /// <c>OverflowStrategy.Proceed</c>. Returns <c>0.0</c> when the report
    /// has no included items. <paramref name="budget"/> validates
    /// <c>MaxTokens &gt; 0</c> at construction, so no division-by-zero guard
    /// is needed here.
    /// </remarks>
    public static double BudgetUtilization(this SelectionReport report, ContextBudget budget) =>
        report.Included.Sum(i => (double)i.Item.Tokens) / budget.MaxTokens;

    /// <summary>
    /// Number of distinct context kinds among the included items.
    /// </summary>
    /// <remarks>
    /// Returns <c>0</c> when <see cref="SelectionReport.Included"/> is empty.
    /// </remarks>
    public static int KindDiversity(this SelectionReport report) =>
        report.Included.Select(i => i.Item.Kind).Distinct().Count();

    /// <summary>
    /// Fraction of included items that carry a timestamp.
    /// </summary>
    /// <remarks>
    /// Returns <c>0.0</c> when <see cref="SelectionReport.Included"/> is empty
    /// (avoids division by zero). A value of <c>1.0</c> means every included
    /// item has a timestamp; <c>0.0</c> means none do.
    /// </remarks>
    public static double TimestampCoverage(this SelectionReport report) =>
        report.Included.Count == 0
            ? 0.0
            : report.Included.Count(i => i.Item.Timestamp.HasValue) / (double)report.Included.Count;

    /// <summary>
    /// Compute per-kind quota utilization from a selection report against a quota policy.
    /// </summary>
    /// <remarks>
    /// Returns one <see cref="KindQuotaUtilization"/> per constraint in the policy,
    /// sorted by kind for determinism.
    /// <para>
    /// For percentage-mode constraints, <c>Actual</c> is
    /// <c>sum(tokens for kind) / targetTokens * 100.0</c>. For count-mode constraints,
    /// <c>Actual</c> is the count of included items of that kind.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<KindQuotaUtilization> QuotaUtilization(
        this SelectionReport report,
        IQuotaPolicy policy,
        ContextBudget budget)
    {
        var constraints = policy.GetConstraints();

        // Pre-aggregate included items by kind: (tokenSum, count).
        var kindStats = new Dictionary<ContextKind, (long TokenSum, int Count)>();
        for (var i = 0; i < report.Included.Count; i++)
        {
            var item = report.Included[i].Item;
            var kind = item.Kind;
            kindStats.TryGetValue(kind, out var stats);
            kindStats[kind] = (stats.TokenSum + item.Tokens, stats.Count + 1);
        }

        var targetTokens = (double)budget.TargetTokens;

        var results = new List<KindQuotaUtilization>(constraints.Count);
        for (var i = 0; i < constraints.Count; i++)
        {
            var c = constraints[i];
            kindStats.TryGetValue(c.Kind, out var stats);

            var actual = c.Mode switch
            {
                QuotaConstraintMode.Percentage => targetTokens == 0.0
                    ? 0.0
                    : stats.TokenSum / targetTokens * 100.0,
                QuotaConstraintMode.Count => stats.Count,
                _ => 0.0
            };

            var utilization = c.Cap == 0.0
                ? 0.0
                : Math.Clamp(actual / c.Cap, 0.0, 1.0);

            results.Add(new KindQuotaUtilization(
                c.Kind, c.Mode, c.Require, c.Cap, actual, utilization));
        }

        results.Sort((a, b) => string.Compare(a.Kind.Value, b.Kind.Value, StringComparison.Ordinal));
        return results;
    }
}
