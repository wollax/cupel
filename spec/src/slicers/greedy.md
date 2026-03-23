# GreedySlice

GreedySlice selects items by value density (score per token), greedily filling the budget from highest-density items first.

## Overview

GreedySlice is the simplest and fastest slicer strategy. It computes a "value density" for each item — the ratio of relevance score to token cost — then iterates items in descending density order, including each item if it fits within the remaining budget. Zero-token items have infinite density and are always included.

This is the fractional knapsack heuristic applied to a 0/1 selection problem. It does not guarantee optimal selection (the [KnapsackSlice](knapsack.md) provides that), but it is fast and produces good results in practice.

## Algorithm

```text
GREEDY-SLICE(scoredItems, budget):
    if length(scoredItems) = 0 or budget.targetTokens <= 0:
        return []

    // Step 1: Compute value densities
    densities <- new array of (density: float64, index: integer) of length(scoredItems)
    for i <- 0 to length(scoredItems) - 1:
        tokens <- scoredItems[i].item.tokens
        if tokens = 0:
            densities[i] <- (MAX_FLOAT, i)
        else:
            densities[i] <- (scoredItems[i].score / tokens, i)

    // Step 2: Sort by density descending, tiebreak by index ascending
    STABLE-SORT(densities, by density descending, then by index ascending)

    // Step 3: Greedy fill
    remaining <- budget.targetTokens
    selected <- empty list

    for i <- 0 to length(densities) - 1:
        originalIndex <- densities[i].index
        item <- scoredItems[originalIndex].item
        tokens <- item.tokens

        if tokens = 0:
            APPEND(selected, item)
        else if tokens <= remaining:
            APPEND(selected, item)
            remaining <- remaining - tokens

    return selected
```

## Value Density

Value density measures how much relevance score an item provides per token consumed:

```
density = score / tokens
```

- Items with `tokens = 0` have density `MAX_FLOAT` (the largest representable finite double), ensuring they sort first and are always included. Among zero-token items, all share the same density `MAX_FLOAT`. The sort tiebreak is index only — score values are irrelevant for zero-token items.
- Items with high scores and low token counts are preferred over items with moderate scores and high token counts.

## Sort Stability

The sort MUST be **stable** or use a composite sort key that includes the original index as a tiebreaker. When two items have the same density, the item with the lower original index (earlier in the input list) is preferred.

Since the input is pre-sorted by score descending (from the [Sort](../pipeline/sort.md) stage), this tiebreaking preserves score order among equal-density items.

## Edge Cases

| Condition | Result |
|---|---|
| Empty `scoredItems` | Empty list |
| `budget.targetTokens <= 0` | Empty list |
| All items are zero-token | All items selected |
| Single item fits within budget | That item is selected |
| Single item exceeds budget | Empty list (only zero-token items, if any) |
| All items exceed budget individually | Only zero-token items are selected |
| All items fit within budget | All items selected |

## Complexity

- **Time:** O(*N* log *N*) — dominated by the sort. The greedy fill pass is O(*N*).
- **Space:** O(*N*) for the density array.

## Conformance Notes

- The density for zero-token items MUST be treated as the maximum possible value, ensuring they are always sorted first and always included.
- Among zero-token items, tiebreaking is by original index only; score values MUST NOT affect the relative order of zero-token items.
- The sort MUST be stable with index-based tiebreaking. Given items A (density 0.5, index 0) and B (density 0.5, index 3), A must precede B.
- Items that do not fit (`tokens > remaining`) are skipped, not deferred. The algorithm makes a single pass through the sorted densities — it does not backtrack or attempt smaller items after skipping a large one.
- The output list order is determined by the greedy iteration order (density-descending), **not** the original input order. The [Place](../pipeline/place.md) stage handles final ordering.
