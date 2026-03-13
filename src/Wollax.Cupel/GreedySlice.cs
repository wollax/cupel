using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel;

/// <summary>
/// Greedy value-density slicer that fills the budget by selecting items
/// with the highest score-to-token ratio (value density) first.
/// </summary>
/// <remarks>
/// Algorithm runs in O(N log N) due to sorting by density.
/// Zero-token items have infinite density and are always included at zero cost.
/// Fills to <see cref="ContextBudget.TargetTokens"/> (soft goal), not
/// <see cref="ContextBudget.MaxTokens"/> (hard ceiling).
/// </remarks>
public sealed class GreedySlice : ISlicer
{
    /// <inheritdoc />
    public IReadOnlyList<ContextItem> Slice(
        IReadOnlyList<ScoredItem> scoredItems,
        ContextBudget budget,
        ITraceCollector traceCollector)
    {
        if (scoredItems.Count == 0 || budget.TargetTokens <= 0)
        {
            return [];
        }

        // Build density array with original indices for stable sort
        var densities = new (double Density, int Index)[scoredItems.Count];
        for (var i = 0; i < scoredItems.Count; i++)
        {
            var tokens = scoredItems[i].Item.Tokens;
            var density = tokens == 0
                ? double.MaxValue
                : scoredItems[i].Score / tokens;
            densities[i] = (density, i);
        }

        // Sort descending by density, stable via ascending index
        Array.Sort(densities, static (a, b) =>
        {
            var densityComparison = b.Density.CompareTo(a.Density);
            return densityComparison != 0 ? densityComparison : a.Index.CompareTo(b.Index);
        });

        // Greedy fill to TargetTokens
        var remainingTokens = budget.TargetTokens;
        var selected = new List<ContextItem>();

        for (var i = 0; i < densities.Length; i++)
        {
            var originalIndex = densities[i].Index;
            var item = scoredItems[originalIndex].Item;
            var tokens = item.Tokens;

            if (tokens == 0)
            {
                selected.Add(item);
            }
            else if (tokens <= remainingTokens)
            {
                selected.Add(item);
                remainingTokens -= tokens;
            }
        }

        return selected;
    }
}
