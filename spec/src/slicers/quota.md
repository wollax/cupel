# QuotaSlice

QuotaSlice partitions items by [ContextKind](../data-model/enumerations.md#contextkind), distributes the token budget across kinds using configurable quotas, and delegates per-kind selection to an inner slicer.

## Overview

QuotaSlice is a **decorator slicer** — it wraps another slicer (e.g., [GreedySlice](greedy.md) or [KnapsackSlice](knapsack.md)) and adds budget fairness across context kinds. This ensures that no single kind dominates the context window, and that important kinds receive a guaranteed minimum allocation.

QuotaSlice operates in four phases:
1. Partition items by ContextKind
2. Compute candidate token mass per kind
3. Distribute the budget across kinds (require/cap constraints)
4. Delegate per-kind selection to the inner slicer

## Configuration

QuotaSlice is configured with a **QuotaSet** that defines two constraints per kind:

- **Require(kind, minPercent)** — Guarantees that at least `minPercent`% of the total budget is reserved for this kind. Range: [0.0, 100.0].
- **Cap(kind, maxPercent)** — Limits this kind to at most `maxPercent`% of the total budget. Range: [0.0, 100.0].

### Validation Rules

1. For each kind: `require <= cap`.
2. The sum of all `require` percentages MUST NOT exceed 100%.
3. Kinds without explicit configuration default to `require = 0%` and `cap = 100%`.

## Algorithm

```text
QUOTA-SLICE(scoredItems, budget, quotas, innerSlicer):
    if length(scoredItems) = 0 or budget.targetTokens <= 0:
        return []

    targetTokens <- budget.targetTokens

    // Phase 1: Partition by ContextKind
    partitions <- empty map of ContextKind -> list of ScoredItem
    for i <- 0 to length(scoredItems) - 1:
        kind <- scoredItems[i].item.kind    // case-insensitive
        if kind is not in partitions:
            partitions[kind] <- empty list
        APPEND(partitions[kind], scoredItems[i])

    // Phase 2: Candidate token mass per kind
    candidateTokenMass <- empty map of ContextKind -> integer
    for each (kind, items) in partitions:
        mass <- 0
        for i <- 0 to length(items) - 1:
            mass <- mass + items[i].item.tokens
        candidateTokenMass[kind] <- mass

    // Phase 3: Budget distribution
    kindBudgets <- DISTRIBUTE-BUDGET(partitions, candidateTokenMass,
                                      targetTokens, quotas)

    // Phase 4: Per-kind slicing
    allSelected <- empty list
    for each (kind, items) in partitions:
        kindBudget <- kindBudgets[kind]     // 0 if not present
        if kindBudget <= 0:
            continue
        cap <- floor(quotas.getCap(kind) / 100.0 * targetTokens)
        subBudget <- ContextBudget(maxTokens: cap, targetTokens: kindBudget)
        selected <- innerSlicer.Slice(items, subBudget)
        for i <- 0 to length(selected) - 1:
            APPEND(allSelected, selected[i])

    return allSelected
```

### Budget Distribution

```text
DISTRIBUTE-BUDGET(partitions, candidateTokenMass, targetTokens, quotas):
    // Step 1: Compute require and cap token amounts
    requireTokens <- empty map of ContextKind -> integer
    capTokens     <- empty map of ContextKind -> integer

    for each kind in quotas.configuredKinds:
        requireTokens[kind] <- floor(quotas.getRequire(kind) / 100.0 * targetTokens)
        capTokens[kind]     <- floor(quotas.getCap(kind) / 100.0 * targetTokens)

    // Step 2: Sum required tokens
    totalRequired <- 0
    for each (kind, req) in requireTokens:
        totalRequired <- totalRequired + req

    unassignedBudget <- max(0, targetTokens - totalRequired)

    // Step 3: Compute distribution mass (kinds that can receive beyond their require)
    totalMassForDistribution <- 0
    for each (kind, items) in partitions:
        cap <- capTokens[kind] if kind in capTokens, else targetTokens
        require <- requireTokens[kind] if kind in requireTokens, else 0
        if cap > require:
            totalMassForDistribution <- totalMassForDistribution + candidateTokenMass[kind]

    // Step 4: Distribute per kind
    kindBudgets <- empty map of ContextKind -> integer
    for each (kind, items) in partitions:
        require <- requireTokens[kind] if kind in requireTokens, else 0
        cap <- capTokens[kind] if kind in capTokens, else targetTokens

        proportional <- 0
        if totalMassForDistribution > 0 and cap > require:
            proportional <- floor(unassignedBudget * candidateTokenMass[kind]
                                   / totalMassForDistribution)

        kindBudget <- require + proportional
        if kindBudget > cap:
            kindBudget <- cap

        kindBudgets[kind] <- kindBudget

    return kindBudgets
```

## Budget Distribution Rounding (P6)

All percentage-to-token conversions use **floor truncation** (integer truncation toward zero):

```
requireTokens = floor(requirePercent / 100.0 * targetTokens)
capTokens     = floor(capPercent / 100.0 * targetTokens)
proportional  = floor(unassigned * mass / totalMass)
```

Because of this floor truncation, the **sum of all kind budgets may be less than `targetTokens`**. This is by design — it ensures that no kind receives more tokens than its allocation warrants, at the cost of potentially leaving a small number of tokens unallocated.

### Example

Given `targetTokens = 1000` and two kinds each with `require = 33%`:

```
requireTokens["A"] = floor(0.33 * 1000) = 330
requireTokens["B"] = floor(0.33 * 1000) = 330
totalRequired = 660
// 340 tokens remain for proportional distribution
// Even after distribution, sum of kind budgets may be < 1000
```

## ContextKind Comparison (P4)

All ContextKind comparisons in QuotaSlice — partition key grouping, require/cap lookups, and kind budget assignment — MUST use **case-insensitive** ASCII case folding, consistent with the [ContextKind comparison semantics](../data-model/enumerations.md#contextkind).

## Edge Cases

| Condition | Result |
|---|---|
| Empty `scoredItems` | Empty list |
| `budget.targetTokens <= 0` | Empty list |
| No quotas configured | All kinds get proportional budget (no require/cap constraints) |
| Kind has `require = 100%` | That kind gets the entire budget; all other kinds get 0 |
| Kind has `cap = 0%` | That kind is excluded (budget = 0) |
| Kind has candidates but zero token mass | Gets its require allocation (if any), no proportional share |
| Kind has no candidates | Not present in partitions; its require allocation is unused |

## Complexity

- **Time:** O(*N*) for partitioning + O(*K*) for budget distribution + sum of inner slicer costs per partition, where *K* is the number of distinct kinds.
- **Space:** O(*N*) for partitioned item lists + O(*K*) for budget maps.

## Conformance Notes

- The inner slicer receives a `ContextBudget` with `maxTokens` set to the kind's cap (in tokens) and `targetTokens` set to the kind's computed budget. Other budget fields are not forwarded.
- ContextKind comparison MUST be case-insensitive throughout (partitioning, quota lookups, budget assignment).
- Kinds not present in the quota configuration default to `require = 0%`, `cap = 100%`. They participate in proportional distribution with no floor guarantee and no ceiling (other than the full budget).
- The proportional distribution uses `candidateTokenMass` (sum of token counts for actual candidates in each kind), not the theoretical maximum. This means kinds with fewer/smaller candidates naturally receive less budget.
- The output list order is: items from each kind partition concatenated in partition iteration order. The [Place](../pipeline/place.md) stage handles final ordering.
