using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Per-kind utilization of a quota constraint, computed from a
/// <see cref="SelectionReport"/> against an <see cref="IQuotaPolicy"/>.
/// </summary>
/// <remarks>
/// For <see cref="QuotaConstraintMode.Percentage"/> mode, <paramref name="Actual"/> is the
/// percentage of <c>TargetTokens</c> consumed by items of this kind. For
/// <see cref="QuotaConstraintMode.Count"/> mode, <paramref name="Actual"/> is the number
/// of included items of this kind.
/// <para>
/// <paramref name="Utilization"/> is <c>Actual / Cap</c>, clamped to <c>[0.0, 1.0]</c>.
/// When <c>Cap</c> is zero, <c>Utilization</c> is <c>0.0</c>.
/// </para>
/// </remarks>
/// <param name="Kind">The context kind this utilization applies to.</param>
/// <param name="Mode">Whether the constraint is percentage-based or count-based.</param>
/// <param name="Require">Minimum threshold from the policy constraint.</param>
/// <param name="Cap">Maximum threshold from the policy constraint.</param>
/// <param name="Actual">Actual value achieved: percentage of TargetTokens for percentage mode, item count for count mode.</param>
/// <param name="Utilization"><c>Actual / Cap</c>, clamped to <c>[0.0, 1.0]</c>. <c>0.0</c> when <c>Cap</c> is zero.</param>
public sealed record KindQuotaUtilization(
    ContextKind Kind,
    QuotaConstraintMode Mode,
    double Require,
    double Cap,
    double Actual,
    double Utilization);
