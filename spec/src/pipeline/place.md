# Stage 6: Place

The Place stage merges pinned items with sliced items, detects and handles token budget overflow, and delegates to a configurable placer strategy for final ordering.

## Overview

Placing is the final pipeline stage. It has three responsibilities:

1. **Merge** pinned items (from [Classify](classify.md)) with sliced items (from [Slice](slice.md)) into a single scored item list. Pinned items are assigned a score of 1.0.
2. **Detect and handle overflow** — if the merged total exceeds `targetTokens`, apply the configured [OverflowStrategy](../data-model/enumerations.md#overflowstrategy).
3. **Order** the merged items by delegating to the configured placer strategy.

## Algorithm

```text
PLACE(pinned, slicedScored, budget, overflowStrategy, placer):
    // Step 1: Merge pinned items with sliced scored items
    merged <- new array of length(pinned) + length(slicedScored)
    for i <- 0 to length(pinned) - 1:
        merged[i] <- ScoredItem(pinned[i], 1.0)
    for i <- 0 to length(slicedScored) - 1:
        merged[length(pinned) + i] <- slicedScored[i]

    // Step 2: Overflow detection
    mergedTokens <- 0
    for i <- 0 to length(merged) - 1:
        mergedTokens <- mergedTokens + merged[i].item.tokens

    if mergedTokens > budget.targetTokens:
        merged <- HANDLE-OVERFLOW(merged, budget.targetTokens, overflowStrategy)

    // Step 3: Delegate to placer for final ordering
    placed <- placer.Place(merged)

    return placed
```

### Overflow Handling

```text
HANDLE-OVERFLOW(merged, targetTokens, strategy):
    if strategy = Throw:
        ERROR("Selected items require {mergedTokens} tokens,
               exceeding target budget of {targetTokens}")

    if strategy = Truncate:
        // Keep items from front (highest-scored), skip from back
        // Pinned items are never removed
        kept <- empty list
        currentTokens <- 0
        for i <- 0 to length(merged) - 1:
            if merged[i].item.pinned:
                APPEND(kept, merged[i])
                currentTokens <- currentTokens + merged[i].item.tokens
            else if currentTokens + merged[i].item.tokens <= targetTokens:
                APPEND(kept, merged[i])
                currentTokens <- currentTokens + merged[i].item.tokens
            // else: excluded (BudgetExceeded or PinnedOverride)
        return kept

    if strategy = Proceed:
        // Accept over-budget selection, notify observer
        NOTIFY-OVERFLOW(mergedTokens - targetTokens, merged)
        return merged
```

### Placer Interface

A placer implements a single function:

```text
Place(merged: list of ScoredItem) -> list of ContextItem
```

- `merged` is the list of ScoredItem pairs (pinned items with score 1.0, sliced items with their computed scores).
- The return value is the final ordered list of ContextItem instances.

See the [Placers](../placers.md) chapter for all placer algorithm specifications.

## Pinned Item Score

Pinned items are assigned a score of **1.0** when merged into the scored item list. This score value represents the highest conventional relevance — pinned items are always-included by definition. The 1.0 score affects placer behavior (e.g., [UShapedPlacer](../placers/u-shaped.md) places highest-scored items at the edges).

## Edge Cases

- **No pinned items.** The merged list contains only sliced items. Overflow detection and placement proceed normally.
- **No sliced items.** The merged list contains only pinned items (all with score 1.0). This occurs when the scoreable list was empty or the entire effective budget was zero.
- **Truncation cannot reach target.** If pinned items alone exceed `targetTokens`, truncation removes all non-pinned items but the total still exceeds the target. This is a best-effort situation — pinned items are never removed.
- **Empty merged list.** Both pinned and sliced lists are empty. The placer receives an empty list and returns an empty list.

## Conformance Notes

- Pinned items MUST appear before sliced items in the `merged` array (indices `[0, length(pinned))` for pinned, `[length(pinned), end)` for sliced).
- Overflow detection compares against `budget.targetTokens` (the original target, not the effective target used for slicing).
- The `Truncate` strategy iterates the merged array in order (pinned first, then sliced items in score-descending order). Pinned items are always kept; non-pinned items are kept while they fit within `targetTokens`.
- The placer receives the post-overflow merged list (which may be shorter than the pre-overflow list if `Truncate` was applied).
