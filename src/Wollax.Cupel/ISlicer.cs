using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel;

/// <summary>
/// Selects items from scored candidates within a token budget.
/// Slicers decide which items to keep — they do not re-score or reorder.
/// </summary>
/// <remarks>
/// The pipeline sorts scored items by score descending before calling
/// <see cref="Slice"/>. Implementations may rely on this ordering.
/// </remarks>
public interface ISlicer
{
    /// <summary>
    /// Selects items that fit within the given budget.
    /// Candidates must be provided sorted by score descending.
    /// </summary>
    /// <param name="scoredItems">Items with their scores, sorted by score descending.</param>
    /// <param name="budget">The token budget constraint.</param>
    /// <param name="traceCollector">Trace collector for diagnostic events.</param>
    /// <returns>The selected items.</returns>
    IReadOnlyList<ContextItem> Slice(
        IReadOnlyList<ScoredItem> scoredItems,
        ContextBudget budget,
        ITraceCollector traceCollector);
}
