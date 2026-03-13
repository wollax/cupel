using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel;

/// <summary>
/// Placer that distributes items in a U-shaped pattern across the context window,
/// placing the highest-scored items at the start and end positions where LLM
/// attention is strongest (primacy and recency bias).
/// </summary>
/// <remarks>
/// Items are sorted by score descending. The highest-scored item is placed at
/// the start (left edge), the second-highest at the end (right edge), the third
/// at position 1, and so on — alternating inward from both edges. Lower-scored
/// items end up in the middle where attention is weakest.
/// </remarks>
public sealed class UShapedPlacer : IPlacer
{
    /// <inheritdoc />
    public IReadOnlyList<ContextItem> Place(
        IReadOnlyList<ScoredItem> items,
        ITraceCollector traceCollector)
    {
        if (items.Count <= 1)
        {
            if (items.Count == 0)
            {
                return [];
            }

            return [items[0].Item];
        }

        // Build (Score, Index) array for stable sort
        var scored = new (double Score, int Index)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            scored[i] = (items[i].Score, i);
        }

        // Sort descending by score, stable via ascending index
        Array.Sort(scored, static (a, b) =>
        {
            var scoreComparison = b.Score.CompareTo(a.Score);
            return scoreComparison != 0 ? scoreComparison : a.Index.CompareTo(b.Index);
        });

        // Alternating placement: even sorted indices go left, odd go right
        var result = new ContextItem[items.Count];
        var left = 0;
        var right = items.Count - 1;

        for (var i = 0; i < scored.Length; i++)
        {
            var originalIndex = scored[i].Index;
            var item = items[originalIndex].Item;

            if (i % 2 == 0)
            {
                result[left] = item;
                left++;
            }
            else
            {
                result[right] = item;
                right--;
            }
        }

        return result;
    }
}
