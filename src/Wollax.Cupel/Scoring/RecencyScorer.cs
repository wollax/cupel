namespace Wollax.Cupel.Scoring;

/// <summary>
/// Scores items by relative timestamp ranking.
/// Most recent item gets 1.0, oldest gets 0.0, linearly interpolated by rank.
/// Items with null timestamps score 0.0.
/// </summary>
public sealed class RecencyScorer : IScorer
{
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        if (!item.Timestamp.HasValue)
            return 0.0;

        var itemTimestamp = item.Timestamp.Value;
        var countWithTimestamp = 0;
        var rank = 0;

        for (var i = 0; i < allItems.Count; i++)
        {
            var other = allItems[i];
            if (!other.Timestamp.HasValue)
                continue;

            countWithTimestamp++;

            if (other.Timestamp.Value < itemTimestamp)
                rank++;
        }

        if (countWithTimestamp <= 1)
            return 1.0;

        return rank / (double)(countWithTimestamp - 1);
    }
}
