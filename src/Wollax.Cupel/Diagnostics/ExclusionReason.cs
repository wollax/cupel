namespace Wollax.Cupel.Diagnostics;

/// <summary>Reason an item was excluded from the final selection.</summary>
public enum ExclusionReason
{
    /// <summary>Item did not fit within the token budget.</summary>
    BudgetExceeded,

    /// <summary>Item scored below the selection threshold.</summary>
    ScoredTooLow,

    /// <summary>Item was removed during deduplication in favor of a higher-scoring duplicate.</summary>
    Deduplicated,

    /// <summary>Item was excluded because its kind exceeded the quota cap.</summary>
    QuotaCapExceeded,

    /// <summary>Item was displaced to satisfy a quota require constraint for another kind.</summary>
    QuotaRequireDisplaced,

    /// <summary>Item was excluded because it has a negative token count.</summary>
    NegativeTokens,

    /// <summary>Item was excluded because a pinned item took priority.</summary>
    PinnedOverride,

    /// <summary>Item was excluded by a filter predicate.</summary>
    Filtered
}
