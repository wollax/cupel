using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Slicing;

/// <summary>
/// Online/streaming slicer that processes <see cref="IAsyncEnumerable{T}"/> sources
/// in configurable micro-batches without materializing the full collection.
/// </summary>
/// <remarks>
/// Uses an online greedy strategy: each micro-batch is sorted by score descending,
/// then items are greedily selected until the budget is full. When the budget is
/// exhausted, upstream consumption is cancelled via a linked
/// <see cref="CancellationTokenSource"/>.
/// </remarks>
public sealed class StreamSlice : IAsyncSlicer
{
    private readonly int _batchSize;

    /// <summary>
    /// Gets the micro-batch size used for processing.
    /// Exposed for pipeline scoring batch alignment.
    /// </summary>
    public int BatchSize => _batchSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamSlice"/> class.
    /// </summary>
    /// <param name="batchSize">
    /// The number of items to accumulate before sorting and selecting within a micro-batch.
    /// Must be greater than zero. Defaults to 32.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="batchSize"/> is less than or equal to zero.
    /// </exception>
    public StreamSlice(int batchSize = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);
        _batchSize = batchSize;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContextItem>> SliceAsync(
        IAsyncEnumerable<ScoredItem> scoredItems,
        ContextBudget budget,
        ITraceCollector traceCollector,
        CancellationToken cancellationToken = default)
    {
        if (budget.TargetTokens <= 0)
        {
            return [];
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var selected = new List<ContextItem>();
        var remainingTokens = budget.TargetTokens;
        var batch = new List<ScoredItem>(_batchSize);

        try
        {
            await foreach (var item in scoredItems.WithCancellation(cts.Token).ConfigureAwait(false))
            {
                batch.Add(item);

                if (batch.Count >= _batchSize)
                {
                    ProcessBatch(batch, ref remainingTokens, selected);
                    batch.Clear();

                    if (remainingTokens <= 0)
                    {
                        await cts.CancelAsync().ConfigureAwait(false);
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Budget-full cancellation — we initiated this, swallow it
        }

        // Process final partial batch
        if (batch.Count > 0 && remainingTokens > 0)
        {
            ProcessBatch(batch, ref remainingTokens, selected);
        }

        return selected;
    }

    private static void ProcessBatch(
        List<ScoredItem> batch,
        ref int remainingTokens,
        List<ContextItem> selected)
    {
        // Sort batch by score descending for within-batch prioritization
        batch.Sort(static (a, b) => b.Score.CompareTo(a.Score));

        for (var i = 0; i < batch.Count; i++)
        {
            var item = batch[i].Item;

            if (item.Tokens == 0)
            {
                selected.Add(item);
            }
            else if (item.Tokens <= remainingTokens)
            {
                selected.Add(item);
                remainingTokens -= item.Tokens;
            }
        }
    }
}
