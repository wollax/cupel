using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel;

/// <summary>
/// Asynchronous counterpart of <see cref="ISlicer"/> for streaming/async slicing.
/// Consumes an <see cref="IAsyncEnumerable{T}"/> of scored items without
/// materializing the full collection.
/// </summary>
public interface IAsyncSlicer
{
    /// <summary>
    /// Selects items from a streaming source that fit within the given budget.
    /// </summary>
    /// <param name="scoredItems">Streaming scored items to select from.</param>
    /// <param name="budget">The token budget constraint.</param>
    /// <param name="traceCollector">Trace collector for diagnostic events.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>The selected items.</returns>
    Task<IReadOnlyList<ContextItem>> SliceAsync(
        IAsyncEnumerable<ScoredItem> scoredItems,
        ContextBudget budget,
        ITraceCollector traceCollector,
        CancellationToken cancellationToken = default);
}
