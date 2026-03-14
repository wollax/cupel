# Stage 5: Slice

The Slice stage selects items from the sorted list that fit within the effective token budget. It delegates to a configurable slicer strategy.

## Overview

Slicing is where the token budget is enforced. The pipeline computes an **effective budget** that accounts for tokens already committed (pinned items and output reserve), then delegates to the configured slicer strategy to select which items to include.

The slicer receives items already sorted by score descending (from the [Sort](sort.md) stage), so it can make informed decisions about which items to drop when the budget is tight.

## Effective Budget Computation

Before invoking the slicer, the pipeline computes the adjusted budget:

```text
COMPUTE-EFFECTIVE-BUDGET(budget, pinnedTokens):
    effectiveMax    <- max(0, budget.maxTokens - budget.outputReserve - pinnedTokens)
    effectiveTarget <- max(0, budget.targetTokens - pinnedTokens)
    effectiveTarget <- min(effectiveTarget, effectiveMax)

    return ContextBudget(
        maxTokens:    effectiveMax,
        targetTokens: effectiveTarget
    )
```

Where:
- `pinnedTokens` is the sum of `tokens` for all pinned items (computed during [Classify](classify.md)).
- `effectiveMax` is the hard ceiling for scoreable items after reserving space for output and pinned items.
- `effectiveTarget` is the soft goal for the slicer, clamped to `effectiveMax`.

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

## Conformance Notes

- The effective budget computation MUST match the formulas above exactly. In particular, `effectiveTarget` is clamped to `effectiveMax` (not the other way around).
- The slicer receives a fresh ContextBudget with only `maxTokens` and `targetTokens` set. Other budget fields (`outputReserve`, `reservedSlots`, `estimationSafetyMarginPercent`) are not forwarded to the slicer in the adjusted budget.
- The slicer output is a list of ContextItem (not ScoredItem). Scores are re-associated by the pipeline after slicing, by matching items back to their scored counterparts using reference identity.
