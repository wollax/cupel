namespace Wollax.Cupel.Scoring;

/// <summary>
/// Scores items by the proportion of other items in the candidate set that share at least one tag.
/// Uses case-insensitive tag comparison and <see cref="object.ReferenceEquals"/> for self-exclusion.
/// </summary>
/// <remarks>
/// Self-exclusion uses reference identity, not value equality.
/// If the scored item was copied (e.g., via <c>with { }</c> or deserialization),
/// the copy will not be excluded and may count as a peer match.
/// Callers must ensure the exact same object reference appears in <c>allItems</c>.
/// </remarks>
public sealed class FrequencyScorer : IScorer
{
    /// <inheritdoc />
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        if (item.Tags.Count == 0 || allItems.Count <= 1)
            return 0.0;

        var matchingItems = 0;

        for (var i = 0; i < allItems.Count; i++)
        {
            var other = allItems[i];

            if (ReferenceEquals(item, other))
                continue;

            if (other.Tags.Count == 0)
                continue;

            if (SharesAnyTag(item.Tags, other.Tags))
                matchingItems++;
        }

        return matchingItems / (double)(allItems.Count - 1);
    }

    private static bool SharesAnyTag(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        for (var i = 0; i < a.Count; i++)
        {
            for (var j = 0; j < b.Count; j++)
            {
                if (string.Equals(a[i], b[j], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
