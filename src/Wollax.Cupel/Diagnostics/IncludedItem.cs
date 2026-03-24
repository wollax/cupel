namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Represents an item that was included in the final selection,
/// along with its score and the reason for inclusion.
/// </summary>
public sealed record IncludedItem : IEquatable<IncludedItem>
{
    /// <summary>The context item that was included.</summary>
    public required ContextItem Item { get; init; }

    /// <summary>The computed score for this item.</summary>
    public required double Score { get; init; }

    /// <summary>The reason this item was included in the selection.</summary>
    public required InclusionReason Reason { get; init; }

    /// <inheritdoc />
    public bool Equals(IncludedItem? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Item == other.Item
            && Score == other.Score
            && Reason == other.Reason;
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Item, Score, Reason);
}
