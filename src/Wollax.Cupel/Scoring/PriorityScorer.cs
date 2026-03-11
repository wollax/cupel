namespace Wollax.Cupel.Scoring;

/// <summary>
/// Scores items by relative priority ranking.
/// Highest priority gets 1.0, lowest gets 0.0, linearly interpolated by rank.
/// Items with null priorities score 0.0.
/// </summary>
public sealed class PriorityScorer : IScorer
{
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        if (!item.Priority.HasValue)
            return 0.0;

        var itemPriority = item.Priority.Value;
        var countWithPriority = 0;
        var rank = 0;

        for (var i = 0; i < allItems.Count; i++)
        {
            var other = allItems[i];
            if (!other.Priority.HasValue)
                continue;

            countWithPriority++;

            if (other.Priority.Value < itemPriority)
                rank++;
        }

        if (countWithPriority <= 1)
            return 1.0;

        return rank / (double)(countWithPriority - 1);
    }
}
