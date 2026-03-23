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
}
