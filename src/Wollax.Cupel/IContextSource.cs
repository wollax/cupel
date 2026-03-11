namespace Wollax.Cupel;

/// <summary>
/// Provides context items to the pipeline.
/// Implementations supply batch access, streaming access, or both.
/// </summary>
public interface IContextSource
{
    /// <summary>
    /// Returns all context items as a batch.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete list of context items.</returns>
    Task<IReadOnlyList<ContextItem>> GetItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams context items for lazy or incremental consumption.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async sequence of context items.</returns>
    IAsyncEnumerable<ContextItem> GetItemsStreamAsync(CancellationToken cancellationToken = default);
}
