# KnapsackSlice

KnapsackSlice selects items using 0/1 knapsack dynamic programming, finding the combination of items that maximizes total score within the token budget.

## Overview

KnapsackSlice provides optimal or near-optimal item selection by solving the classic 0/1 knapsack problem. Items are the "objects" with token counts as weights and relevance scores as values. The algorithm finds the subset with maximum total value that fits within the budget.

To make the DP feasible for large budgets, KnapsackSlice **discretizes** both item weights and budget capacity using a configurable bucket size. This trades precision for performance — the result is near-optimal rather than exactly optimal.

## Algorithm

```text
KNAPSACK-SLICE(scoredItems, budget, bucketSize):
    if length(scoredItems) = 0 or budget.targetTokens <= 0:
        return []

    // Step 1: Pre-filter zero-token items (always included)
    zeroTokenItems <- empty list
    candidates <- empty list
    for i <- 0 to length(scoredItems) - 1:
        if scoredItems[i].item.tokens = 0:
            APPEND(zeroTokenItems, scoredItems[i].item)
        else if scoredItems[i].item.tokens > 0:
            APPEND(candidates, scoredItems[i])

    if length(candidates) = 0:
        return zeroTokenItems

    n <- length(candidates)

    // Step 2: Build parallel arrays
    weights <- new array of integer[n]      // original token counts
    values  <- new array of integer[n]      // scaled integer scores
    items   <- new array of ContextItem[n]

    for i <- 0 to n - 1:
        weights[i] <- candidates[i].item.tokens
        values[i]  <- max(0, floor(candidates[i].score * 10000))
        items[i]   <- candidates[i].item

    // Step 3: Discretize capacity and weights
    capacity <- floor(budget.targetTokens / bucketSize)
    if capacity = 0:
        return zeroTokenItems

    discretizedWeights <- new array of integer[n]
    for i <- 0 to n - 1:
        discretizedWeights[i] <- ceil(weights[i] / bucketSize)

    // Step 4: DP with 1D value array + 2D keep table
    dp   <- new array of integer[capacity + 1], all zeros
    keep <- new 2D array of boolean[n][capacity + 1], all false

    for i <- 0 to n - 1:
        dw <- discretizedWeights[i]
        dv <- values[i]
        for w <- capacity downto dw:
            withItem <- dp[w - dw] + dv
            if withItem > dp[w]:
                dp[w] <- withItem
                keep[i][w] <- true

    // Step 5: Reconstruct solution
    selected <- empty list
    remainingCapacity <- capacity
    for i <- n - 1 downto 0:
        if keep[i][remainingCapacity]:
            APPEND(selected, items[i])
            remainingCapacity <- remainingCapacity - discretizedWeights[i]

    // Step 6: Combine zero-token items with selected items
    result <- empty list
    for i <- 0 to length(zeroTokenItems) - 1:
        APPEND(result, zeroTokenItems[i])
    for i <- 0 to length(selected) - 1:
        APPEND(result, selected[i])

    return result
```

## Discretization

### Score Scaling

Scores (float64 in [0.0, 1.0]) are converted to integers for the DP table:

```
integerValue = max(0, floor(score * 10000))
```

> **Implementation note:** For non-negative scores (which all Cupel scores are, since scorers return values in [0.0, 1.0]), `floor` and truncation-toward-zero (C-style integer truncation) produce identical results. Either operation is conformant.

This provides 4 decimal digits of score precision. Items with very small positive scores (< 0.0001) are treated as value 0.

### Weight Discretization

Item token counts and budget capacity are discretized using the bucket size:

- **Item weights** use **ceiling division**: `ceil(tokens / bucketSize)`. This ensures the DP never selects items that would exceed the original budget.
- **Budget capacity** uses **floor division**: `floor(targetTokens / bucketSize)`. This ensures the discretized capacity does not exceed the original budget.

The asymmetry (ceiling for weights, floor for capacity) is deliberate — it guarantees that any solution feasible in the discretized problem is also feasible in the original problem. The tradeoff is that the discretized problem may under-fill the budget.

### Bucket Size

The `bucketSize` parameter (default: 100) controls the granularity of discretization:

- **Smaller bucket size** = more precise but larger DP table (more memory and time).
- **Larger bucket size** = faster but coarser approximation.
- `bucketSize` MUST be a positive integer (> 0).

## Edge Cases

| Condition | Result |
|---|---|
| Empty `scoredItems` | Empty list |
| `budget.targetTokens <= 0` | Empty list |
| All items are zero-token | All items selected |
| `capacity = 0` after discretization | Only zero-token items |
| All items exceed discretized capacity | Only zero-token items |
| Single candidate fits | That candidate plus zero-token items |

## Complexity

- **Time:** O(*N* * *C*) where *N* is the number of non-zero-token candidates and *C* = `floor(targetTokens / bucketSize)`.
- **Space:** O(*N* * *C*) for the keep table. The 1D DP array uses O(*C*) additional space.

## Precision Caveat (P5)

The score scaling factor (10000) and bucket size are **implementation-defined** parameters. This specification defines them as defaults for the reference implementation, but conforming implementations MAY use different values provided:

1. The behavioral contract holds: the slicer returns a subset of items that fits within the budget.
2. The selection is optimal or near-optimal for the discretization granularity used.
3. Conformance tests use score differences of at least 0.01 (100 integer units at scale factor 10000) to avoid precision-dependent divergence.

Implementations that change the score scaling factor or bucket size SHOULD document the deviation.

## Conformance Notes

- Zero-token items MUST be pre-filtered and always included in the result. They do not participate in the DP.
- Items with negative token counts are not expected in the input (they are excluded during [classification](../pipeline/classify.md)), but if present, they SHOULD be excluded from the DP candidates.
- The DP uses reverse iteration on the weight dimension (`w <- capacity downto dw`) to implement the standard 1D space optimization for 0/1 knapsack.
- Solution reconstruction iterates candidates in reverse order (`i <- n-1 downto 0`), which is the standard backtracking direction for 0/1 knapsack with a keep table.
- The output list order is: zero-token items first, then DP-selected items in reconstruction order. The [Place](../pipeline/place.md) stage handles final ordering.
