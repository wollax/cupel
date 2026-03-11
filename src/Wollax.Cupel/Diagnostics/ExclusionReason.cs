namespace Wollax.Cupel.Diagnostics;

/// <summary>Reason an item was excluded from the final selection.</summary>
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
