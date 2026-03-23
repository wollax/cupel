# CountQuotaSlice

CountQuotaSlice enforces absolute minimum and maximum **item counts** per [ContextKind](../data-model/enumerations.md#contextkind) using a two-phase COUNT-DISTRIBUTE-BUDGET algorithm, then delegates residual selection to an inner slicer.

## Overview

CountQuotaSlice is a **decorator slicer** — it wraps another slicer (e.g., [GreedySlice](greedy.md)) and adds count-based fairness across context kinds. Where [QuotaSlice](quota.md) distributes a *token budget* as percentages across kinds, CountQuotaSlice enforces *absolute item counts*: "at least 2 tool items, at most 5 tool items."

CountQuotaSlice operates in three phases:

1. **Count-Satisfy (Phase 1):** For each kind with a `requireCount > 0`, commit the top-N candidates (by score descending) to the selection. Their token cost is accumulated as `preAllocatedTokens` and the committed items are removed from the residual candidate pool.
2. **Budget-Distribute (Phase 2):** The inner slicer receives the residual candidate pool and a reduced budget (`targetTokens` reduced by `preAllocatedTokens`).
3. **Cap Enforcement (Phase 3):** Items returned by the inner slicer are filtered against the per-kind `capCount`. Items that would exceed the cap are excluded.

## Configuration

CountQuotaSlice is configured with a list of **CountQuotaEntry** values and a **ScarcityBehavior**:

### CountQuotaEntry

Each entry specifies constraints for one [ContextKind](../data-model/enumerations.md#contextkind):

| Field | Type | Description |
|---|---|---|
| `kind` | ContextKind | The kind this entry constrains |
| `requireCount` | integer ≥ 0 | Minimum number of items of this kind to commit in Phase 1 |
| `capCount` | integer > 0 (or 0 when `requireCount` is also 0) | Maximum number of items of this kind in the final result |

### Validation Rules

1. `requireCount` MUST be ≤ `capCount`.
2. `capCount = 0` with `requireCount > 0` is rejected — a zero cap with a positive requirement can never be satisfied.
3. Kinds without an entry have no require or cap constraint — they participate freely in Phase 2 delegation.

### ScarcityBehavior

Controls what happens when the candidate pool has fewer items of a kind than the configured `requireCount`:

| Value | Behavior |
|---|---|
| `Degrade` (default) | Include all available candidates and record a shortfall. Pipeline execution continues. |
| `Throw` | Return an error / throw an exception immediately. Use when count requirements are hard guarantees (e.g., required disclaimer text). |

## KnapsackSlice Guard

CountQuotaSlice does **not** support [KnapsackSlice](knapsack.md) as the inner slicer. Construction MUST fail with an error if the inner slicer is a KnapsackSlice instance. Use [GreedySlice](greedy.md) as the inner slicer instead. A `CountConstrainedKnapsackSlice` may be provided in a future release.

## Algorithm

```text
COUNT-QUOTA-SLICE(scoredItems, budget, entries, innerSlicer, scarcity):
    if length(scoredItems) = 0 or budget.targetTokens <= 0:
        return []

    // Build policy lookup: kind -> (requireCount, capCount)
    entryByKind <- map from entries

    // --- Phase 1: Count-Satisfy ---

    // Partition candidates by ContextKind
    partitions <- group scoredItems by item.kind (case-insensitive)

    // Sort each partition by score descending
    for each (kind, candidates) in partitions:
        SORT(candidates, by score descending)

    committed     <- empty list
    committedSet  <- empty set (reference equality)
    selectedCount <- empty map of ContextKind -> integer
    preAllocatedTokens <- 0
    shortfalls    <- empty list

    for each entry in entries where entry.requireCount > 0:
        candidates <- partitions[entry.kind] (empty if absent)
        satisfied  <- 0

        for each candidate in candidates while satisfied < entry.requireCount:
            APPEND(committed, candidate.item)
            ADD(committedSet, candidate.item)
            preAllocatedTokens <- preAllocatedTokens + candidate.item.tokens
            satisfied <- satisfied + 1

        selectedCount[entry.kind] <- satisfied

        if satisfied < entry.requireCount:
            if scarcity = Throw:
                ERROR("kind has fewer candidates than requireCount")
            else:
                RECORD-SHORTFALL(entry.kind, entry.requireCount, satisfied)

    // --- Phase 2: Budget-Distribute ---

    residual <- [si in scoredItems where si.item not in committedSet]
    residualTarget <- max(0, budget.targetTokens - preAllocatedTokens)
    residualBudget <- ContextBudget(maxTokens: budget.maxTokens,
                                     targetTokens: min(residualTarget, budget.maxTokens))

    innerSelected <- innerSlicer.Slice(residual, residualBudget)

    // --- Phase 3: Cap Enforcement ---

    result <- committed

    for each item in innerSelected:
        kind <- item.kind
        count <- selectedCount[kind] (default 0)
        cap   <- entryByKind[kind].capCount (uncapped if no entry)

        if cap is defined and count >= cap:
            EXCLUDE(item)    // cap exceeded
        else:
            APPEND(result, item)
            selectedCount[kind] <- count + 1

    return result
```

## Scarcity Reporting

When `ScarcityBehavior.Degrade` is active and a kind's candidate pool cannot satisfy its `requireCount`, a **CountRequirementShortfall** is recorded:

| Field | Type | Description |
|---|---|---|
| `kind` | ContextKind (or string) | The kind that was short |
| `requiredCount` | integer | The configured `requireCount` |
| `satisfiedCount` | integer | How many candidates were actually committed |

In .NET, shortfalls are available via `CountQuotaSlice.LastShortfalls` (populated after each `Slice` call). In Rust, shortfalls are recorded on `SelectionReport.count_requirement_shortfalls` (populated by the pipeline after slicing).

## Monotonicity

CountQuotaSlice does **not** guarantee monotonic item inclusion as the budget changes. Phase 1 commits a fixed set of items regardless of budget, but Phase 2 delegation produces different residual selections at different budgets. Combined with the cap enforcement in Phase 3, the final set can exhibit non-monotonic inclusion.

This means CountQuotaSlice is **incompatible** with [budget simulation](../analytics/budget-simulation.md) methods that assume monotonic inclusion:

- `GetMarginalItems` — does not guard against CountQuotaSlice (only guards against QuotaSlice), because Phase 1 pre-allocation is budget-independent and the inner slicer controls the residual. However, the cap enforcement can cause non-monotonic behavior.
- `FindMinBudgetFor` — MUST throw if the pipeline's slicer is CountQuotaSlice (see [FindMinBudgetFor QuotaSlice + CountQuotaSlice Guard](../analytics/budget-simulation.md#quotaslice--countquotaslice-guard)).

## Edge Cases

| Condition | Result |
|---|---|
| Empty `scoredItems` | Empty list |
| `budget.targetTokens <= 0` | Empty list |
| No entries configured | Phase 1 commits nothing; Phase 2 runs unconstrained |
| `requireCount = capCount` | Phase 1 commits exactly `requireCount` items; Phase 2 cannot add more of that kind |
| `requireCount = 0`, `capCount > 0` | Phase 1 commits nothing for that kind; Phase 2 output is capped |
| Kind has fewer candidates than `requireCount` | Scarcity behavior applies (Degrade or Throw) |
| Phase 1 exhausts the token budget | Phase 2 receives `residualTarget = 0` and selects nothing |
| Inner slicer is KnapsackSlice | Construction fails with error |

## Complexity

- **Time:** O(*N*) for partitioning + O(*K* · *M* log *M*) for per-kind sorting in Phase 1 (where *M* is the largest partition size) + inner slicer cost for Phase 2 + O(*I*) for Phase 3 cap enforcement (where *I* is the inner slicer output size).
- **Space:** O(*N*) for partitioned item lists + O(*K*) for policy maps and count tracking.

## Conformance Notes

- ContextKind comparison MUST be case-insensitive throughout (partitioning and policy lookups), consistent with the [ContextKind comparison semantics](../data-model/enumerations.md#contextkind).
- Phase 1 commits items in score-descending order within each kind. The input is pre-sorted by the pipeline, but implementations MUST sort per-kind partitions explicitly to ensure correctness when multiple kinds interleave.
- The inner slicer in Phase 2 receives a `ContextBudget` with `targetTokens` reduced by Phase 1 pre-allocation. `maxTokens` is passed through unchanged from the original budget.
- Phase 3 cap enforcement applies only to items returned by the inner slicer in Phase 2. Phase 1 committed items are always included (they count toward the cap but are never excluded by it).
- Kinds not present in the entry list have no require or cap constraint. They are not partitioned in Phase 1 and pass through Phase 2 and 3 without cap filtering.
- The KnapsackSlice guard MUST be enforced at construction time, not at slice time.
