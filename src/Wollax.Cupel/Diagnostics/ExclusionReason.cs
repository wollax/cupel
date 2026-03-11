namespace Wollax.Cupel.Diagnostics;

/// <summary>Reason an item was excluded from the final selection.</summary>
/// <remarks>
/// Referenced by <see cref="SelectionReport"/> in Phase 7, where each excluded item
/// is annotated with its exclusion reason for diagnostic reporting.
/// </remarks>
public enum ExclusionReason
{
    /// <summary>Item scored below the selection threshold.</summary>
    LowScore,

    /// <summary>Item did not fit within the token budget.</summary>
    BudgetExceeded,

    /// <summary>Item was removed during deduplication.</summary>
    Duplicate,

    /// <summary>Item was excluded by a quota constraint.</summary>
    QuotaExceeded
}
