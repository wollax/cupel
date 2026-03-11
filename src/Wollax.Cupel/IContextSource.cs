using System.Runtime.CompilerServices;

namespace Wollax.Cupel;

/// <summary>
/// Provides context items to the pipeline.
/// Implementations override whichever access pattern is natural — batch or streaming.
/// </summary>
/// <remarks>
/// Default interface methods bridge the two access patterns: the default
/// <see cref="GetItemsStreamAsync"/> wraps the batch result, and the default
/// <see cref="GetItemsAsync"/> materializes the stream. Implementors need only
/// override the method that matches their data source.
/// </remarks>
public interface IContextSource
{
    /// <summary>
    /// Returns all context items as a batch.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete list of context items.</returns>
    async Task<IReadOnlyList<ContextItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<ContextItem>();
        await foreach (var item in GetItemsStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(item);
        }
        return items;
    }

    /// <summary>
    /// Streams context items for lazy or incremental consumption.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async sequence of context items.</returns>
    async IAsyncEnumerable<ContextItem> GetItemsStreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var items = await GetItemsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in items)
        {
            yield return item;
        }
    }
}
