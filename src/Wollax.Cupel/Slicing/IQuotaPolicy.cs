namespace Wollax.Cupel.Slicing;

/// <summary>
/// A slicer that exposes per-kind quota constraints.
/// </summary>
/// <remarks>
/// Implemented by <see cref="QuotaSlice"/> (percentage-based) and
/// <see cref="CountQuotaSlice"/> (count-based). The returned constraints are
/// consumed by analytics functions such as
/// <see cref="Diagnostics.SelectionReportExtensions.QuotaUtilization"/>
/// to compute how fully each kind's quota is used.
/// </remarks>
public interface IQuotaPolicy
{
    /// <summary>
    /// Returns all per-kind constraints configured on this slicer.
    /// </summary>
    IReadOnlyList<QuotaConstraint> GetConstraints();
}
