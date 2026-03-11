namespace Wollax.Cupel.Scoring;

/// <summary>
/// Scores items by relative priority ranking among items with a valid priority.
/// Rank is the count of items with a strictly lower priority value.
/// Items with null priorities score 0.0. Tied priorities produce equal scores.
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
