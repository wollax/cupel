namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Represents an item that was excluded from the final selection,
/// along with its score, the reason for exclusion, and an optional
/// reference to the item it was deduplicated against.
/// </summary>
public sealed record ExcludedItem
{
    /// <summary>The context item that was excluded.</summary>
    public required ContextItem Item { get; init; }

    /// <summary>The computed score for this item.</summary>
    public required double Score { get; init; }

    /// <summary>The reason this item was excluded from the selection.</summary>
    public required ExclusionReason Reason { get; init; }

    /// <summary>
    /// When <see cref="Reason"/> is <see cref="ExclusionReason.Deduplicated"/>,
    /// the winning item this was deduplicated against. Null for other reasons.
    /// </summary>
    public ContextItem? DeduplicatedAgainst { get; init; }
}
