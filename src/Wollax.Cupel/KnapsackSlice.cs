using System.Buffers;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel;

/// <summary>
/// Optimal 0/1 knapsack slicer with configurable bucket discretization.
/// Finds the mathematically optimal item subset for a given token budget,
/// trading precision for performance via bucket size granularity.
/// </summary>
/// <remarks>
/// <para>
/// Algorithm: standard 0/1 knapsack dynamic programming with 1D array
/// (reverse iteration). Item weights use ceiling discretization and capacity
/// uses floor discretization to guarantee feasibility — selected items always
/// fit within the original (non-discretized) budget.
/// </para>
/// <para>
/// Scores are int-scaled (×10000) for DP table correctness. Floating-point
/// scores in a DP table accumulate rounding errors that produce wrong results.
/// </para>
/// <para>
/// The DP array is rented from <see cref="ArrayPool{T}.Shared"/> and returned
/// in a <c>finally</c> block to avoid GC pressure.
/// </para>
/// </remarks>
public sealed class KnapsackSlice : ISlicer
{
    private readonly int _bucketSize;

    /// <summary>
    /// Initializes a new instance of <see cref="KnapsackSlice"/>.
    /// </summary>
    /// <param name="bucketSize">
    /// Discretization bucket size in tokens. Larger values trade precision for
    /// performance (smaller DP table). Default is 100.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="bucketSize"/> is less than or equal to zero.
    /// </exception>
    public KnapsackSlice(int bucketSize = 100)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bucketSize, 0);
        _bucketSize = bucketSize;
    }

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

        // Pre-filter zero-token items (always included — zero weight = free)
        var zeroTokenItems = new List<ContextItem>();
        var candidateCount = 0;

        // First pass: count non-zero candidates
        for (var i = 0; i < scoredItems.Count; i++)
        {
            if (scoredItems[i].Item.Tokens == 0)
            {
                zeroTokenItems.Add(scoredItems[i].Item);
            }
            else if (scoredItems[i].Item.Tokens > 0)
            {
                candidateCount++;
            }
        }

        if (candidateCount == 0)
        {
            return zeroTokenItems;
        }

        // Build parallel arrays for candidates
        var weights = new int[candidateCount];
        var values = new int[candidateCount];
        var items = new ContextItem[candidateCount];
        var idx = 0;

        for (var i = 0; i < scoredItems.Count; i++)
        {
            var tokens = scoredItems[i].Item.Tokens;
            if (tokens > 0)
            {
                weights[idx] = tokens;
                values[idx] = (int)(scoredItems[i].Score * 10000);
                items[idx] = scoredItems[i].Item;
                idx++;
            }
        }

        // Discretize capacity (floor division — conservative)
        var capacity = budget.TargetTokens / _bucketSize;

        if (capacity == 0)
        {
            // Budget too small for any candidate at this bucket size
            return zeroTokenItems;
        }

        // Discretize weights (ceiling division — overestimates item cost for feasibility)
        var discretizedWeights = new int[candidateCount];
        for (var i = 0; i < candidateCount; i++)
        {
            discretizedWeights[i] = (weights[i] + _bucketSize - 1) / _bucketSize;
        }

        // Rent DP array from pool
        var dpArray = ArrayPool<int>.Shared.Rent(capacity + 1);
        // 2D boolean keep table for correct reconstruction from 1D DP array
        var keep = new bool[candidateCount][];
        for (var i = 0; i < candidateCount; i++)
        {
            keep[i] = new bool[capacity + 1];
        }

        try
        {
            // Zero the rented portion (pool may return dirty arrays)
            Array.Clear(dpArray, 0, capacity + 1);

            // Fill DP table — 1D, REVERSE iteration (0/1 knapsack, not unbounded)
            for (var i = 0; i < candidateCount; i++)
            {
                var dw = discretizedWeights[i];
                var dv = values[i];
                for (var w = capacity; w >= dw; w--)
                {
                    var withItem = dpArray[w - dw] + dv;
                    if (withItem > dpArray[w])
                    {
                        dpArray[w] = withItem;
                        keep[i][w] = true;
                    }
                }
            }

            // Reconstruct solution using keep table
            var selected = new List<ContextItem>();
            var remainingCapacity = capacity;

            for (var i = candidateCount - 1; i >= 0; i--)
            {
                if (keep[i][remainingCapacity])
                {
                    selected.Add(items[i]);
                    remainingCapacity -= discretizedWeights[i];
                }
            }

            // Combine zero-token items + selected candidates
            var result = new List<ContextItem>(zeroTokenItems.Count + selected.Count);
            for (var i = 0; i < zeroTokenItems.Count; i++)
            {
                result.Add(zeroTokenItems[i]);
            }
            for (var i = 0; i < selected.Count; i++)
            {
                result.Add(selected[i]);
            }

            // Emit summary trace event
            if (traceCollector.IsEnabled)
            {
                traceCollector.RecordItemEvent(new TraceEvent
                {
                    Stage = PipelineStage.Slice,
                    Duration = TimeSpan.Zero,
                    ItemCount = result.Count
                });
            }

            return result;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(dpArray, clearArray: true);
        }
    }
}
