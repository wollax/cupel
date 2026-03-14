# Stage 5: Slice

The Slice stage selects items from the sorted list that fit within the effective token budget. It delegates to a configurable slicer strategy.

## Overview

Slicing is where the token budget is enforced. The pipeline computes an **effective budget** that accounts for tokens already committed (pinned items and output reserve), then delegates to the configured slicer strategy to select which items to include.

The slicer receives items already sorted by score descending (from the [Sort](sort.md) stage), so it can make informed decisions about which items to drop when the budget is tight.

## Effective Budget Computation

Before invoking the slicer, the pipeline computes the adjusted budget:

```text
COMPUTE-EFFECTIVE-BUDGET(budget, pinnedTokens):
    reservedTokens  <- sum(budget.reservedSlots.values)
    effectiveMax    <- max(0, budget.maxTokens - budget.outputReserve - pinnedTokens - reservedTokens)
    effectiveTarget <- max(0, budget.targetTokens - pinnedTokens - reservedTokens)
    effectiveTarget <- min(effectiveTarget, effectiveMax)

    if budget.estimationSafetyMarginPercent > 0:
        multiplier      <- 1.0 - budget.estimationSafetyMarginPercent / 100.0
        effectiveMax    <- floor(effectiveMax * multiplier)
        effectiveTarget <- floor(effectiveTarget * multiplier)
        effectiveTarget <- min(effectiveTarget, effectiveMax)

    return ContextBudget(
        maxTokens:    effectiveMax,
        targetTokens: effectiveTarget
    )
```

Where:
- `pinnedTokens` is the sum of `tokens` for all pinned items (computed during [Classify](classify.md)).
- `reservedTokens` is the sum of all values in `budget.reservedSlots`, reserving token capacity for per-kind guarantees.
- `effectiveMax` is the hard ceiling for scoreable items after reserving space for output, pinned items, and reserved slots.
- `effectiveTarget` is the soft goal for the slicer, clamped to `effectiveMax`.
- The safety margin reduces the effective budget by the configured percentage to account for token estimation error. Both values use `floor` (integer truncation) when converting from the floating-point product.

## Algorithm

```text
SLICE(sorted, budget, pinnedTokens, slicer):
    adjustedBudget <- COMPUTE-EFFECTIVE-BUDGET(budget, pinnedTokens)

    // Delegate to slicer strategy
    slicedItems <- slicer.Slice(sorted, adjustedBudget)

    return slicedItems
```

### Slicer Interface

A slicer implements a single function:

```text
Slice(sorted: list of ScoredItem, budget: ContextBudget) -> list of ContextItem
```

- `sorted` is the score-descending sorted list of ScoredItem pairs.
- `budget` is the effective budget (already adjusted for pinned items and output reserve).
- The return value is the list of selected ContextItem instances (without scores).

See the [Slicers](../slicers.md) chapter for all slicer algorithm specifications.

## Edge Cases

- **Empty sorted list.** The slicer receives an empty list and returns an empty list.
- **Zero effective budget.** If `effectiveMax = 0` or `effectiveTarget = 0`, the slicer should select no items (or only zero-token items, depending on the slicer strategy).
- **All items fit.** If the total tokens of all sorted items is within `effectiveTarget`, the slicer may select all items.
- **Pinned items consume entire budget.** If `pinnedTokens >= targetTokens`, then `effectiveTarget = 0`. The slicer selects no items (or only zero-token items), and the final output consists of pinned items only.
- **Reserved slots exceed remaining budget.** If the sum of `reservedSlots` values exceeds `maxTokens - outputReserve - pinnedTokens`, then `effectiveMax = 0` and the slicer selects no items (or only zero-token items).

## Conformance Notes

- The effective budget computation MUST match the formulas above exactly. In particular, `effectiveTarget` is clamped to `effectiveMax` (not the other way around).
- The slicer receives a fresh ContextBudget with only `maxTokens` and `targetTokens` set. The effects of `outputReserve`, `reservedSlots`, and `estimationSafetyMarginPercent` are incorporated into the `maxTokens` and `targetTokens` values of the adjusted budget; the original fields are not forwarded.
- The slicer output is a list of ContextItem (not ScoredItem). Scores are re-associated by the pipeline after slicing, by matching items back to their scored counterparts using reference identity.
