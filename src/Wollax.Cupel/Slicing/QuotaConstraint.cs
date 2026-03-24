namespace Wollax.Cupel.Slicing;

/// <summary>
/// A single per-kind quota constraint describing the require and cap thresholds.
/// </summary>
/// <remarks>
/// For <see cref="QuotaConstraintMode.Percentage"/>, <paramref name="Require"/> and
/// <paramref name="Cap"/> are percentages (0–100). For <see cref="QuotaConstraintMode.Count"/>,
/// they are absolute item counts (as doubles for uniformity with the Rust implementation).
/// </remarks>
/// <param name="Kind">The context kind this constraint applies to.</param>
/// <param name="Mode">Whether the constraint is percentage-based or count-based.</param>
/// <param name="Require">Minimum threshold (percentage or count).</param>
/// <param name="Cap">Maximum threshold (percentage or count).</param>
public sealed record QuotaConstraint(
    ContextKind Kind,
    QuotaConstraintMode Mode,
    double Require,
    double Cap);
