using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel;

/// <summary>
/// Placer that orders items by timestamp ascending, placing the oldest items
/// first and the most recent items last. Items without timestamps sort to the end.
/// </summary>
/// <remarks>
/// Uses stable sort via index tiebreaker to preserve insertion order among
/// items with identical or null timestamps.
/// </remarks>
public sealed class ChronologicalPlacer : IPlacer
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

        // Build (Timestamp, Index) array for stable sort
        var timestamps = new (DateTimeOffset? Timestamp, int Index)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            timestamps[i] = (items[i].Item.Timestamp, i);
        }

        // Sort: null timestamps to end, then ascending by timestamp, stable via index
        Array.Sort(timestamps, static (a, b) =>
        {
            var aHas = a.Timestamp.HasValue;
            var bHas = b.Timestamp.HasValue;

            if (aHas && bHas)
            {
                var tsComparison = a.Timestamp!.Value.CompareTo(b.Timestamp!.Value);
                return tsComparison != 0 ? tsComparison : a.Index.CompareTo(b.Index);
            }

            if (aHas)
            {
                return -1; // a has timestamp, b doesn't -> a comes first
            }

            if (bHas)
            {
                return 1; // b has timestamp, a doesn't -> b comes first
            }

            // Both null -> stable by index
            return a.Index.CompareTo(b.Index);
        });

        // Build result from sorted indices
        var result = new ContextItem[items.Count];
        for (var i = 0; i < timestamps.Length; i++)
        {
            result[i] = items[timestamps[i].Index].Item;
        }

        return result;
    }
}
