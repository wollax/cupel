# Phase 6: Advanced Slicers & Quota System — Research

**Researched:** 2026-03-13
**Confidence:** HIGH unless noted otherwise

---

## Standard Stack

No external dependencies. Everything is BCL-only:

| Need | Solution | Package |
|------|----------|---------|
| DP table memory | `ArrayPool<int>.Shared` | `System.Buffers` (BCL) |
| Async streaming | `IAsyncEnumerable<T>` | `System.Runtime` (BCL) |
| Cancellation | `CancellationToken` / `CancellationTokenSource` | `System.Threading` (BCL) |
| Enum cancellation | `[EnumeratorCancellation]` attribute | `System.Runtime.CompilerServices` (BCL) |

No NuGet packages needed. .NET 10 includes all of the above in-box.

---

## Architecture Patterns

### 1. KnapsackSlice: 0/1 Knapsack with Bucket Discretization

**Algorithm:** Standard 0/1 knapsack DP with weight scaling.

**Discretization approach:** Divide all token counts and the capacity by the bucket size (default 100). This converts the problem from O(n * W) where W = total tokens (e.g., 100,000) to O(n * W/B) where B = bucket size (e.g., 1,000 buckets). Rounding is ceiling-based for item weights (conservative — items use at least their bucket) and floor-based for capacity (conservative — don't overspend).

```
bucketWeight[i] = (tokens[i] + bucketSize - 1) / bucketSize   // ceiling
bucketCapacity  = targetTokens / bucketSize                     // floor
```

**Confidence:** HIGH — this is standard weight scaling applied to knapsack. Ceiling on weights / floor on capacity guarantees the selected items fit within the actual token budget. The approximation error is bounded: each item's weight is overstated by at most (bucketSize - 1) tokens.

**Item reconstruction:** The 1D rolling-array optimization (single `int[]` of size W+1) does NOT support item reconstruction — it only yields the optimal value. Since we need to know WHICH items to select, we must either:

- **(a) Use a 2D table** — `int[n+1, W/B+1]` for full backtracking. Space: O(n * W/B).
- **(b) Use a 1D DP array + separate boolean tracking** — `bool[n, W/B+1]` recording inclusion decisions per item per capacity level, then backtrack.
- **(c) Use the 1D DP array and re-derive** — after computing dp[], walk items in reverse checking if `dp[w] != dp[w - weight[i]]` to reconstruct.

**Recommendation: Option (c) — re-derive from 1D array.** This uses O(W/B) space for the DP array (rentable from ArrayPool) and O(n) for the result list. Re-derivation is O(n) and trivial. The 1D reverse-iteration pattern:

```csharp
// dp[j] = max total value using capacity j (in buckets)
for (int i = 0; i < n; i++)
    for (int j = capacity; j >= bucketWeight[i]; j--)
        dp[j] = Math.Max(dp[j], values[i] + dp[j - bucketWeight[i]]);

// Reconstruct: walk backward through items
int remaining = capacity;
for (int i = n - 1; i >= 0; i--)
{
    if (remaining >= bucketWeight[i] &&
        dp[remaining] == values[i] + dp[remaining - bucketWeight[i]])
    {
        selected.Add(items[i]);
        remaining -= bucketWeight[i];
    }
}
```

**Confidence:** HIGH — reverse iteration for 0/1 knapsack is textbook. Reconstruction from 1D array by walking items in reverse is well-documented on competitive programming resources (cp-algorithms, Codeforces).

**Value function:** Use `ScoredItem.Score` as the knapsack value. The DP maximizes total score subject to bucket-capacity constraint, which is the mathematically optimal selection.

**Zero-token items:** Weight = 0 buckets. In the DP formulation, zero-weight items with positive value are always included (they improve dp[j] for all j at no cost). The reverse-iteration loop naturally handles this: `j >= 0` for zero-weight items. However, to avoid iterating the full capacity range for zero-weight items, pre-filter them: always include zero-token items, then run DP on the rest. This matches GreedySlice's behavior.

**DP value type:** Use `int` values, not `double`. Multiply scores by a scaling factor (e.g., 10000) and truncate to int. This avoids floating-point accumulation errors in the DP table and keeps the table as `int[]` which is more cache-friendly. The scaling factor should be large enough to preserve ranking fidelity.

**Confidence:** MEDIUM for int-scaling recommendation. Double DP tables work but risk accumulation drift over many additions. Int scaling is standard practice in competitive programming knapsack implementations but adds a conversion step. Either approach is viable; int is the safer choice for correctness.

### 2. QuotaSlice: Decorator with Per-Kind Budget Partitioning

**Pattern:** Decorator wrapping an inner `ISlicer`. QuotaSlice does NOT implement selection itself — it partitions items by Kind, computes per-Kind token budgets from percentage quotas, then delegates to the inner slicer for each partition.

**Budget allocation algorithm:**

1. Compute total available tokens from `budget.TargetTokens`
2. For each Kind with a `Require(minPercent)`: reserve `floor(targetTokens * minPercent / 100)` tokens
3. Sum all required reservations. If sum > targetTokens: this is caught at validation time (sum of Requires <= 100%)
4. Remaining tokens = targetTokens - sum of required reservations
5. For each Kind with a `Cap(maxPercent)`: effective cap = `floor(targetTokens * maxPercent / 100)` - already reserved for that Kind
6. Distribute remaining tokens proportionally to Kinds based on candidate availability, respecting caps
7. Kinds with no Require/Cap get a share of the unassigned pool

**Per-Kind slicer delegation:** For each Kind partition, create a sub-budget with `targetTokens = allocatedTokensForKind` and call `innerSlicer.Slice(partitionItems, subBudget, trace)`. Merge all results.

**Data structures for partitioning:** Use `Dictionary<ContextKind, List<ScoredItem>>` to partition candidates. Since ContextKind uses case-insensitive equality and has a proper GetHashCode, it works as dictionary key.

**Confidence:** HIGH — decorator pattern is well-established. The budget allocation is a straightforward proportional distribution with floor/cap constraints.

**Unassigned budget distribution (Claude's Discretion):** Distribute proportionally to the number of candidate tokens in each uncapped Kind. If a Kind has more candidates, it gets a proportionally larger share of unassigned budget, up to its Cap (if any). Kinds with zero candidates get zero allocation. This is a fair heuristic that avoids wasting budget on Kinds that can't use it.

### 3. StreamSlice: IAsyncSlicer with Online Selection

**New interface:**

```csharp
public interface IAsyncSlicer
{
    Task<IReadOnlyList<ContextItem>> SliceAsync(
        IAsyncEnumerable<ScoredItem> scoredItems,
        ContextBudget budget,
        ITraceCollector traceCollector,
        CancellationToken cancellationToken = default);
}
```

**Namespace:** `Wollax.Cupel` (same as `ISlicer`) — it's a top-level pipeline contract.

**Algorithm:** StreamSlice is fundamentally a greedy online algorithm (not knapsack — you can't do DP on a stream without materializing it). It processes micro-batches:

1. Buffer `batchSize` items from the async enumerable
2. Within each batch, sort by value density (same as GreedySlice)
3. Greedily fill remaining budget
4. When budget is full, signal cancellation upstream via CancellationToken
5. Return accumulated selections

**Cancellation pattern:** The pipeline creates a `CancellationTokenSource` linked to the caller's token. When budget-full condition is met, the pipeline calls `cts.Cancel()`. The `IAsyncEnumerable` producer receives the cancellation through the `[EnumeratorCancellation]` attribute pattern.

```csharp
await foreach (var item in source.WithCancellation(cts.Token))
{
    batch.Add(item);
    if (batch.Count >= batchSize)
    {
        ProcessBatch(batch, ...);
        batch.Clear();
        if (remainingBudget <= 0)
        {
            cts.Cancel();
            break;
        }
    }
}
// Process any remaining partial batch
```

**Confidence:** HIGH — `WithCancellation` + `[EnumeratorCancellation]` is the standard .NET pattern. Verified against Microsoft docs and multiple authoritative sources.

**Default micro-batch size (Claude's Discretion):** 32 items. Rationale: small enough to be responsive (don't buffer too far ahead of budget), large enough to amortize the per-batch overhead. This is configurable, so the default just needs to be reasonable.

### 4. Slicer Composition via Decorator

**Builder API pattern:**

```csharp
// QuotaSlice wraps inner slicer via fluent API
builder.UseGreedySlice().WithQuotas(q => q
    .Require(ContextKind.Document, 20)
    .Cap(ContextKind.Document, 40)
    .Require(ContextKind.Message, 30));

// KnapsackSlice inside QuotaSlice
builder.UseKnapsackSlice(bucketSize: 50).WithQuotas(q => ...);
```

**Implementation:** `WithQuotas` captures the quota configuration and wraps the previously-set slicer in a `QuotaSlice` decorator. Internally:

```csharp
public PipelineBuilder WithQuotas(Action<QuotaBuilder> configure)
{
    var quotaBuilder = new QuotaBuilder();
    configure(quotaBuilder);
    var quotas = quotaBuilder.Build(); // validates here
    _slicer = new QuotaSlice(_slicer ?? new GreedySlice(), quotas);
    return this;
}
```

**Validation at configuration time:** The `QuotaBuilder.Build()` method validates:
- Require <= Cap for same Kind
- Sum of all Requires <= 100%
- Percentages are 0-100 range
- No duplicate Require/Cap for same Kind

Throws `InvalidOperationException` with descriptive message on violation.

### 5. Pipeline Dispatch for IAsyncSlicer

The `CupelPipeline` currently stores `ISlicer _slicer`. To support `IAsyncSlicer`:

- The pipeline checks if the configured slicer implements `IAsyncSlicer`
- For `ExecuteAsync` with `IAsyncEnumerable` source: dispatch to `SliceAsync`
- For `Execute` with materialized `IReadOnlyList`: always use `ISlicer.Slice` (StreamSlice would need to implement ISlicer too, or throw NotSupportedException)

**Recommendation:** StreamSlice implements ONLY `IAsyncSlicer`, not `ISlicer`. Attempting to use StreamSlice with synchronous `Execute()` should fail at build time or with a clear exception. This keeps the interface contract clean.

**Confidence:** HIGH — interface-based dispatch is straightforward.

---

## Don't Hand-Roll

| Problem | Use Instead | Why |
|---------|-------------|-----|
| DP table allocation | `ArrayPool<int>.Shared.Rent()` / `.Return()` | Avoids GC pressure on repeated knapsack calls |
| Cancellation token linking | `CancellationTokenSource.CreateLinkedTokenSource()` | Correct multi-token cancellation behavior |
| Async enumerable cancellation | `WithCancellation()` extension + `[EnumeratorCancellation]` | Compiler-synthesized combined token |
| Dictionary for Kind partitioning | `Dictionary<ContextKind, List<ScoredItem>>` | ContextKind already has correct equality/hash |
| Score-to-int conversion | `(int)(score * SCALE_FACTOR)` with constant | Avoid reinventing fixed-point arithmetic |

---

## Common Pitfalls

### KnapsackSlice

1. **Forward iteration in 1D DP** — MUST iterate capacity in reverse (high to low) for 0/1 knapsack. Forward iteration allows items to be selected multiple times (unbounded knapsack behavior).

2. **Ceiling vs floor rounding** — Item weights must use ceiling division (overestimate), capacity must use floor division (underestimate). Reversing this can cause selected items to exceed actual token budget.

3. **Integer overflow in DP values** — If using int-scaled scores (score * 10000) and summing many items, verify the maximum possible DP value fits in `int`. With scores in [0, 1] and scale 10000, max per item = 10000. With 1000 items, max sum = 10,000,000 — well within `int.MaxValue`.

4. **ArrayPool return discipline** — Always return rented arrays in a `finally` block. Rented arrays are NOT zero-initialized; must `Array.Clear()` or zero manually before use.

5. **Reconstruction correctness** — When reconstructing selected items from the 1D DP array, iterate items in reverse order (last to first). The reconstruction check `dp[w] == values[i] + dp[w - weight[i]]` must use the same value/weight transformations as the forward pass.

6. **Zero-token items in DP** — Zero-weight items cause the inner loop `for j = W down to 0` to iterate the entire array every time. Pre-filter zero-token items (always include them) and exclude from DP to avoid O(n * W/B) work for items that are trivially selected.

### QuotaSlice

7. **Integer truncation in budget allocation** — Percentage-to-tokens conversion loses fractional tokens. Sum of floor-rounded per-Kind budgets may be less than total budget. Track and redistribute the residual tokens.

8. **Empty partitions** — A Kind may have Require(20%) but zero candidate items. Must not crash; emit trace warning and redistribute those tokens to other Kinds.

9. **Sub-budget TargetTokens vs MaxTokens** — When creating sub-budgets for per-Kind slicing, both TargetTokens and MaxTokens should equal the allocated tokens for that Kind. The inner slicer should not distinguish target from max within a quota band.

### StreamSlice

10. **Forgetting to process the final partial batch** — After the `await foreach` loop ends (either by exhaustion or cancellation), any items remaining in the batch buffer must still be processed.

11. **CancellationToken not propagated** — The `WithCancellation` call on the `IAsyncEnumerable` is the ONLY way to propagate cancellation to the producer. Forgetting it means the producer continues generating items after budget-full.

12. **ConfigureAwait(false)** — All `await` calls in library code should use `ConfigureAwait(false)` to avoid capturing synchronization context.

---

## Code Examples

### KnapsackSlice Core Algorithm

```csharp
public IReadOnlyList<ContextItem> Slice(
    IReadOnlyList<ScoredItem> scoredItems,
    ContextBudget budget,
    ITraceCollector traceCollector)
{
    if (scoredItems.Count == 0 || budget.TargetTokens <= 0)
        return [];

    // Pre-filter zero-token items (always included)
    var zeroTokenItems = new List<ContextItem>();
    var dpItems = new List<ScoredItem>();
    for (var i = 0; i < scoredItems.Count; i++)
    {
        if (scoredItems[i].Item.Tokens == 0)
            zeroTokenItems.Add(scoredItems[i].Item);
        else
            dpItems.Add(scoredItems[i]);
    }

    if (dpItems.Count == 0)
        return zeroTokenItems;

    // Discretize weights and capacity
    var n = dpItems.Count;
    var bucketCapacity = budget.TargetTokens / _bucketSize; // floor
    if (bucketCapacity <= 0)
        return zeroTokenItems;

    // Build weight and value arrays
    var weights = new int[n];
    var values = new int[n];
    for (var i = 0; i < n; i++)
    {
        weights[i] = (dpItems[i].Item.Tokens + _bucketSize - 1) / _bucketSize; // ceiling
        values[i] = (int)(dpItems[i].Score * ScaleFactor);
    }

    // Rent DP array from pool
    var dp = ArrayPool<int>.Shared.Rent(bucketCapacity + 1);
    try
    {
        Array.Clear(dp, 0, bucketCapacity + 1);

        // Forward pass: standard 0/1 knapsack, reverse capacity iteration
        for (var i = 0; i < n; i++)
        {
            for (var j = bucketCapacity; j >= weights[i]; j--)
            {
                var candidate = values[i] + dp[j - weights[i]];
                if (candidate > dp[j])
                    dp[j] = candidate;
            }
        }

        // Reconstruct selected items
        var selected = new List<ContextItem>(zeroTokenItems);
        var remaining = bucketCapacity;
        for (var i = n - 1; i >= 0; i--)
        {
            if (remaining >= weights[i] &&
                dp[remaining] == values[i] + dp[remaining - weights[i]])
            {
                selected.Add(dpItems[i].Item);
                remaining -= weights[i];
            }
        }

        return selected;
    }
    finally
    {
        ArrayPool<int>.Shared.Return(dp);
    }
}
```

### QuotaSlice Decorator Pattern

```csharp
public sealed class QuotaSlice : ISlicer
{
    private readonly ISlicer _inner;
    private readonly QuotaSet _quotas;

    public QuotaSlice(ISlicer inner, QuotaSet quotas)
    {
        _inner = inner;
        _quotas = quotas;
    }

    public IReadOnlyList<ContextItem> Slice(
        IReadOnlyList<ScoredItem> scoredItems,
        ContextBudget budget,
        ITraceCollector traceCollector)
    {
        // Partition by Kind
        var partitions = new Dictionary<ContextKind, List<ScoredItem>>();
        for (var i = 0; i < scoredItems.Count; i++)
        {
            var kind = scoredItems[i].Item.Kind;
            if (!partitions.TryGetValue(kind, out var list))
            {
                list = new List<ScoredItem>();
                partitions[kind] = list;
            }
            list.Add(scoredItems[i]);
        }

        // Compute per-Kind budgets from quotas
        var kindBudgets = _quotas.AllocateBudgets(
            budget.TargetTokens, partitions);

        // Delegate to inner slicer per partition
        var result = new List<ContextItem>();
        foreach (var kvp in kindBudgets)
        {
            if (!partitions.TryGetValue(kvp.Key, out var items) || items.Count == 0)
                continue;

            var subBudget = new ContextBudget(
                maxTokens: kvp.Value,
                targetTokens: kvp.Value);

            var subResult = _inner.Slice(items, subBudget, traceCollector);
            for (var i = 0; i < subResult.Count; i++)
                result.Add(subResult[i]);
        }

        return result;
    }
}
```

### StreamSlice Async Pattern

```csharp
public sealed class StreamSlice : IAsyncSlicer
{
    private readonly int _batchSize;

    public StreamSlice(int batchSize = 32) => _batchSize = batchSize;

    public async Task<IReadOnlyList<ContextItem>> SliceAsync(
        IAsyncEnumerable<ScoredItem> scoredItems,
        ContextBudget budget,
        ITraceCollector traceCollector,
        CancellationToken cancellationToken = default)
    {
        var selected = new List<ContextItem>();
        var remainingTokens = budget.TargetTokens;
        var batch = new List<ScoredItem>(_batchSize);

        await foreach (var item in scoredItems
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            batch.Add(item);
            if (batch.Count >= _batchSize)
            {
                ProcessBatch(batch, selected, ref remainingTokens);
                batch.Clear();
                if (remainingTokens <= 0)
                    break;
            }
        }

        // Process final partial batch
        if (batch.Count > 0 && remainingTokens > 0)
            ProcessBatch(batch, selected, ref remainingTokens);

        return selected;
    }

    private static void ProcessBatch(
        List<ScoredItem> batch,
        List<ContextItem> selected,
        ref int remainingTokens)
    {
        // Sort batch by density descending (same as GreedySlice)
        var densities = new (double Density, int Index)[batch.Count];
        for (var i = 0; i < batch.Count; i++)
        {
            var tokens = batch[i].Item.Tokens;
            densities[i] = (tokens == 0 ? double.MaxValue : batch[i].Score / tokens, i);
        }
        Array.Sort(densities, static (a, b) =>
        {
            var cmp = b.Density.CompareTo(a.Density);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        for (var i = 0; i < densities.Length; i++)
        {
            var item = batch[densities[i].Index];
            if (item.Item.Tokens == 0)
            {
                selected.Add(item.Item);
            }
            else if (item.Item.Tokens <= remainingTokens)
            {
                selected.Add(item.Item);
                remainingTokens -= item.Item.Tokens;
            }
        }
    }
}
```

---

## Performance Characteristics

### KnapsackSlice

| Metric | Value | Notes |
|--------|-------|-------|
| Time | O(n * W/B) | n = items, W = target tokens, B = bucket size |
| Space | O(W/B) | Single DP array from ArrayPool |
| Typical dimensions | n=100, W/B=1000 | 100K tokens / 100 bucket = 1000 capacity slots |
| DP table size | ~4 KB | 1000 ints * 4 bytes |
| When to prefer over Greedy | Items have high token variance AND budget is tight | If all items have similar tokens, greedy is near-optimal |

**Guidance threshold (for docs):** KnapsackSlice adds value over GreedySlice when:
- Item token counts vary by >3x (high variance)
- Budget can only fit 30-70% of candidates (tight but not trivial)
- Item count is under ~500 (DP overhead is manageable)

When all items are similar size, greedy density-sort is provably optimal (fractional relaxation = integer solution), and KnapsackSlice wastes cycles producing the same result.

### QuotaSlice

| Metric | Value | Notes |
|--------|-------|-------|
| Time | O(n) + inner slicer per partition | Partitioning is O(n), rest depends on inner slicer |
| Space | O(n) for partition dictionaries | Plus inner slicer space per partition |
| Overhead | Minimal | Dictionary lookups + budget arithmetic |

### StreamSlice

| Metric | Value | Notes |
|--------|-------|-------|
| Time | O(m * b log b) | m = batches processed, b = batch size, due to sort per batch |
| Space | O(b) per batch | Plus accumulated result list |
| Latency | Proportional to batch size | Smaller batches = faster first result |

---

## Knapsack Reconstruction: Why 1D Works

**Critical insight for implementation:** The standard claim that "1D DP can't reconstruct items" is misleading. It's true that during the forward pass, the 1D array overwrites previous state. However, reconstruction IS possible by re-scanning items in reverse after the forward pass completes:

For each item `i` from `n-1` down to `0`:
- If `remaining >= weight[i]` AND `dp[remaining] == value[i] + dp[remaining - weight[i]]`
- Then item `i` was selected. Add it, decrease remaining.

This works because:
1. The final `dp[]` array reflects the optimal solution considering ALL items
2. Walking backward, if removing item `i`'s contribution matches the DP value at the reduced capacity, then item `i` was part of the optimal set
3. After "removing" item `i`, the remaining capacity's DP value still correctly reflects the optimal solution for items `0..i-1`

**Confidence:** HIGH — verified against multiple competitive programming sources. This is a standard technique, just less commonly taught than the 2D backtracking approach.

---

## Open Questions Resolved

| Question | Resolution | Confidence |
|----------|-----------|------------|
| Bucket size default 100? | Reasonable. 100-token buckets for 100K budget = 1000 capacity = 4KB DP table. | HIGH |
| StreamSlice default batch size? | 32 items. Balances responsiveness vs overhead. | MEDIUM |
| DP value type? | Int with scale factor 10000. Avoids float accumulation. | MEDIUM |
| IAsyncSlicer namespace? | `Wollax.Cupel` (top-level, alongside ISlicer) | HIGH |
| Unassigned budget distribution? | Proportional to candidate token mass per Kind | HIGH |
| Score scale factor? | 10000 (preserves 4 decimal places, fits int range easily) | MEDIUM |

---

*Phase: 06-advanced-slicers-quota-system*
*Research completed: 2026-03-13*
