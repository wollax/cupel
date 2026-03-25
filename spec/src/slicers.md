# Slicers

Slicers select a subset of scored items that fits within the token budget. They are invoked during [Stage 5: Slice](pipeline/slice.md).

## Slicer Interface

A slicer implements a single function:

```text
Slice(scoredItems: list of ScoredItem, budget: ContextBudget) -> list of ContextItem
```

- **`scoredItems`** is the list of [ScoredItem](data-model/enumerations.md#scoreditem) pairs, pre-sorted by score descending (from the [Sort](pipeline/sort.md) stage).
- **`budget`** is a [ContextBudget](data-model/context-budget.md) carrying the effective budget (already adjusted for pinned items and output reserve; see [effective budget computation](pipeline/slice.md#effective-budget-computation)).
- The return value is the list of selected [ContextItem](data-model/context-item.md) instances (without scores).

### Contract

1. **Subset selection.** The slicer MUST return a subset of the input items. It MUST NOT create, modify, or duplicate items.
2. **Budget compliance.** The total tokens of selected items SHOULD NOT exceed `budget.targetTokens`. (Some strategies like [KnapsackSlice](slicers/knapsack.md) may slightly under-fill due to discretization.)
3. **Zero-token items.** Items with `tokens = 0` are conventionally always included, as they consume no budget. Individual slicer implementations define this behavior.
4. **Empty input.** If `scoredItems` is empty or `budget.targetTokens <= 0`, the slicer MUST return an empty list.

## Slicer Summary

| Slicer | Algorithm | Complexity | Use Case |
|---|---|---|---|
| [GreedySlice](slicers/greedy.md) | Value-density greedy fill | O(*N* log *N*) | Fast, good-enough selection for most workloads |
| [KnapsackSlice](slicers/knapsack.md) | 0/1 knapsack dynamic programming | O(*N* * *C*) | Optimal selection when budget is tight |
| [QuotaSlice](slicers/quota.md) | Partitioned delegation by kind | O(*N* + per-kind cost) | Budget fairness across context kinds |
| [CountQuotaSlice](slicers/count-quota.md) | Count-based require/cap per kind | O(*N* log *N* + inner cost) | Absolute item-count guarantees per kind |
| [CountConstrainedKnapsackSlice](slicers/count-constrained-knapsack.md) | Count-require/cap + knapsack-optimal selection | O(*N* log *N* + *N* × *C*) | Count guarantees with globally optimal token packing |

Where *N* is the number of scored items and *C* is the discretized budget capacity.

## StreamSlice (Implementation-Optional)

StreamSlice is an asynchronous/streaming slicer that processes items as they arrive rather than requiring the full sorted list upfront. It is documented here for completeness but is **implementation-optional** — the synchronous pipeline defined by this specification does not require streaming support.

Implementations that support asynchronous or streaming pipelines MAY provide a StreamSlice that conforms to the same behavioral contract (subset selection within budget) using a streaming evaluation model. The synchronous conformance test suite does not test StreamSlice.
