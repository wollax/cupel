namespace Wollax.Cupel.Slicing;

/// <summary>
/// Whether a quota constraint is expressed as a percentage of the token budget
/// or as an absolute item count.
/// </summary>
public enum QuotaConstraintMode
{
    /// <summary>Constraint values are percentages of the token budget (0–100).</summary>
    Percentage = 0,

    /// <summary>Constraint values are absolute item counts (as doubles for uniformity).</summary>
    Count = 1
}
