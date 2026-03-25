# CountConstrainedKnapsackSlice

CountConstrainedKnapsackSlice enforces absolute minimum and maximum **item counts** per [ContextKind](../data-model/enumerations.md#contextkind) while using knapsack-optimal selection for the residual budget, implementing a 3-phase COUNT-KNAPSACK-CAP algorithm.

## Overview

CountConstrainedKnapsackSlice is a **decorator slicer** — it wraps a [KnapsackSlice](knapsack.md) directly and adds count-based fairness across context kinds. Unlike [CountQuotaSlice](count-quota.md), which delegates to a configurable inner slicer and guards against KnapsackSlice at construction time, CountConstrainedKnapsackSlice **hardwires KnapsackSlice** as the Phase 2 engine — it is the knapsack wrapper itself.

CountConstrainedKnapsackSlice operates in three phases:

1. **Count-Satisfy (Phase 1):** Identical to CountQuotaSlice Phase 1. For each kind with a `requireCount > 0`, commit the top-N candidates (by score descending) to the selection. Their token cost is accumulated as `preAllocatedTokens` and the committed items are removed from the residual candidate pool.
2. **Knapsack-Distribute (Phase 2):** The residual candidate pool is passed to the stored `KnapsackSlice` with a reduced budget (`targetTokens` reduced by `preAllocatedTokens`). The Phase 2 output is then **re-sorted by score descending** before Phase 3 proceeds (D180 — required because KnapsackSlice output order is not guaranteed to be score-descending).
3. **Cap Enforcement (Phase 3):** Items returned by Phase 2 (after re-sort) are filtered against the per-kind `capCount`. Items that would exceed the cap are excluded. The `selectedCount` map is **seeded from Phase 1 committed counts** (D181 — not initialized to zero), ensuring cap tracking is correct across both phases.

## Configuration

CountConstrainedKnapsackSlice is configured with a list of **CountQuotaEntry** values and a **ScarcityBehavior**:

### CountQuotaEntry

Each entry specifies constraints for one [ContextKind](../data-model/enumerations.md#contextkind):

| Field | Type | Description |
|---|---|---|
| `kind` | ContextKind | The kind this entry constrains |
| `requireCount` | integer ≥ 0 | Minimum number of items of this kind to commit in Phase 1 |
| `capCount` | integer > 0 (or 0 when `requireCount` is also 0) | Maximum number of items of this kind in the final result |

### ScarcityBehavior

Controls what happens when the candidate pool has fewer items of a kind than the configured `requireCount`:

| Value | Behavior |
|---|---|
| `Degrade` (default) | Include all available candidates and record a shortfall. Pipeline execution continues. |
| `Throw` | Return an error / throw an exception immediately. Use when count requirements are hard guarantees (e.g., required disclaimer text). |

### Construction Parameters

**Rust:** `CountConstrainedKnapsackSlice::new(Vec<CountQuotaEntry>, KnapsackSlice, ScarcityBehavior)`

**\.NET:** `CountConstrainedKnapsackSlice(IReadOnlyList<CountQuotaEntry>, KnapsackSlice, ScarcityBehavior = ScarcityBehavior.Degrade)`

### Validation Rules

1. `requireCount` MUST be ≤ `capCount`.
2. `capCount = 0` with `requireCount > 0` is rejected — a zero cap with a positive requirement can never be satisfied.
3. Kinds without an entry have no require or cap constraint — they participate freely in Phase 2 delegation.

## Algorithm

```text
COUNT-KNAPSACK-CAP(scoredItems, budget, entries, knapsack, scarcity):
    if length(scoredItems) = 0 or budget.targetTokens <= 0:
        return []

    // Build policy lookup: kind -> (requireCount, capCount)
    entryByKind <- map from entries

    // --- Phase 1: Count-Satisfy (identical to CountQuotaSlice Phase 1) ---

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

        selectedCount[entry.kind] <- satisfied    // D181: seed Phase 3 starting counts

        if satisfied < entry.requireCount:
            if scarcity = Throw:
                ERROR("CountConstrainedKnapsackSlice: candidate pool for kind '<kind>' has <satisfied> items but RequireCount is <requireCount>.")
            else:
                RECORD-SHORTFALL(entry.kind, entry.requireCount, satisfied)

    // --- Phase 2: Knapsack-Distribute ---

    residual <- [si in scoredItems where si.item not in committedSet]
    residualTarget <- max(0, budget.targetTokens - preAllocatedTokens)
    residualBudget <- ContextBudget(maxTokens: budget.maxTokens,
                                     targetTokens: min(residualTarget, budget.maxTokens))

    innerSelected <- KnapsackSlice(residual, residualBudget)

    // D180: Re-sort Phase 2 output by score descending before Phase 3 cap loop.
    // KnapsackSlice output order is not guaranteed; cap enforcement must see
    // highest-scoring items first so cap budget is spent on the best items.
    innerSelected <- SORT(innerSelected, by score descending)

    // --- Phase 3: Cap Enforcement ---

    result <- committed

    // selectedCount is already seeded from Phase 1 (D181 — not reset to zero)
    for each item in innerSelected:
        kind  <- item.kind
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

The exact `ScarcityBehavior.Throw` error message is:

> `"CountConstrainedKnapsackSlice: candidate pool for kind '<kind>' has <satisfied> items but RequireCount is <requireCount>."`

In .NET, shortfalls are available via `CountConstrainedKnapsackSlice.LastShortfalls` (populated after each `Slice` call). In Rust, shortfalls are recorded on `SelectionReport.count_requirement_shortfalls` (populated by the pipeline after slicing).

## Monotonicity

CountConstrainedKnapsackSlice returns `is_count_quota() → true`. This signals that the slicer is **incompatible** with budget simulation methods that require monotonic item inclusion.

- `FindMinBudgetFor` MUST guard against CountConstrainedKnapsackSlice. See the guard specification at [FindMinBudgetFor QuotaSlice + CountQuotaSlice Guard](../analytics/budget-simulation.md#quotaslice--countquotaslice-guard).

Note: Unlike CountQuotaSlice, there is **no KnapsackSlice guard at construction time**. CountConstrainedKnapsackSlice is the knapsack wrapper itself — it hardwires KnapsackSlice internally and does not accept it as a configurable inner slicer.

## Trade-offs

### Pre-processing Sub-optimality (D174)

Phase 1 commits required items before the knapsack runs, consuming budget. If required items are token-heavy, the residual budget available to Phase 2 may be significantly reduced — potentially to zero. In the worst case, globally optimal selection is impossible because the knapsack cannot see the full candidate pool with the full budget. This is the inherent trade-off of the pre-processing approach (Path 5A): it avoids a full constrained knapsack DP (which would be NP-hard to extend with per-kind minimums) at the cost of potential sub-optimality when Phase 1 pre-allocation is large relative to the total budget.

### Cap Waste

KnapsackSlice may select items in Phase 2 that Phase 3 then excludes due to cap enforcement. Token budget was "spent" on those items in the knapsack DP computation — they counted toward capacity — but they do not appear in the final result. In scenarios with a low cap and many candidates of a capped kind, Phase 2 may select several items that are subsequently excluded by Phase 3, reducing effective throughput.

## Edge Cases

| Condition | Result |
|---|---|
| Empty `scoredItems` | Empty list |
| `budget.targetTokens <= 0` | Empty list |
| No entries configured | Phase 1 commits nothing; Phase 2 runs unconstrained knapsack |
| `requireCount = capCount` | Phase 1 commits exactly `requireCount` items; Phase 3 cannot add more of that kind |
| `requireCount = 0`, `capCount > 0` | Phase 1 commits nothing for that kind; Phase 2 output is capped |
| Kind has fewer candidates than `requireCount` | Scarcity behavior applies (Degrade or Throw) |
| Phase 1 exhausts budget | Phase 2 receives `residualTarget = 0` and selects nothing |
| No KnapsackSlice guard | N/A — CountConstrainedKnapsackSlice hardwires KnapsackSlice internally; no guard is needed at construction |
| KnapsackSlice OOM guard | `CupelError::TableTooLarge` propagates from KnapsackSlice when the DP table exceeds 50 M cells |

## Complexity

- **Time:** O(*N*) for partitioning + O(*K* · *M* log *M*) for per-kind sorting in Phase 1 (where *M* is the largest partition size) + O(*N* × *C*) for Phase 2 KnapsackSlice DP (where *C* = residual capacity / bucket_size) + O(*I* log *I*) for Phase 2 re-sort + O(*I*) for Phase 3 cap enforcement (where *I* is the KnapsackSlice output size).
- **Space:** O(*N*) for partitioned item lists + O(*K*) for policy maps and count tracking + O(*N* × *C*) for the knapsack DP table.

## Conformance Notes

- ContextKind comparison MUST be case-insensitive throughout (partitioning and policy lookups), consistent with the [ContextKind comparison semantics](../data-model/enumerations.md#contextkind).
- **Phase 2 re-sort is normative (D180):** Implementations MUST sort Phase 2 output by score descending before entering the Phase 3 cap loop. KnapsackSlice output order is undefined; without re-sorting, cap enforcement may exclude higher-scoring items and include lower-scoring ones.
- **Phase 3 MUST initialize `selectedCount` from Phase 1 committed counts (D181):** Implementations MUST NOT reset `selectedCount` to zero before Phase 3. The Phase 1 committed counts are the correct starting state for cap tracking across both phases.
- Phase 3 cap enforcement applies only to items returned by Phase 2. Phase 1 committed items are always included (they count toward the cap but are never excluded by it).
- Kinds not present in the entry list have no require or cap constraint. They are not partitioned in Phase 1 and pass through Phase 2 and Phase 3 without cap filtering.

## Conformance Vector Outlines

### 1. Baseline — Require 2 of kind, knapsack selects residual

**File:** `count-constrained-knapsack-baseline.toml`

- Budget: 1000 tokens. 3 items, each 100 tokens.
- Policy: `require_count=2, cap_count=4` for kind `"tool"`. `bucket_size=100`.
- Phase 1: commits `tool-a` (0.9) and `tool-b` (0.7). `preAllocatedTokens=200`. `selectedCount["tool"]=2`.
- Phase 2: `KnapsackSlice` receives residual `[msg-x (0.5, 100t)]` with budget 800. Selects `msg-x`.
- Phase 3: `selectedCount["tool"]=2`, cap=4. All Phase 2 items pass (kind `"msg"` is uncapped).
- **Expected:** all 3 items selected. 0 shortfalls. 0 cap exclusions.

### 2. Cap Exclusion — cap=2, 4 candidates, 2 excluded by cap after Phase 2 re-sort

**File:** `count-constrained-knapsack-cap-exclusion.toml`

- Budget: 600 tokens. 4 tool items, each 100 tokens.
- Policy: `require_count=1, cap_count=2` for kind `"tool"`. `bucket_size=100`.
- Phase 1: commits `tool-a` (0.9). `preAllocatedTokens=100`. `selectedCount["tool"]=1`.
- Phase 2: `KnapsackSlice` receives `[tool-b (0.8), tool-c (0.7), tool-d (0.6)]` with budget 500. All fit — selects all 3. Re-sort: `[tool-b, tool-c, tool-d]` (already score-descending).
- Phase 3: `selectedCount["tool"]=1`, cap=2. `tool-b` → count=2, pass. `tool-c` → count=2 ≥ cap=2, excluded. `tool-d` → excluded.
- **Expected:** `[tool-a, tool-b]`. 0 shortfalls. 2 cap exclusions.

### 3. Scarcity Degrade — require_count=3, only 1 candidate, shortfall recorded

**File:** `count-constrained-knapsack-scarcity-degrade.toml`

- Budget: 500 tokens. 1 item: `tool-a` (0.9, 100t).
- Policy: `require_count=3, cap_count=5` for kind `"tool"`. `ScarcityBehavior=Degrade`.
- Phase 1: commits `tool-a`. `satisfied=1 < requireCount=3` → shortfall recorded `{kind:"tool", requiredCount:3, satisfiedCount:1}`. `selectedCount["tool"]=1`.
- Phase 2: residual empty. `KnapsackSlice` selects nothing.
- Phase 3: no Phase 2 items.
- **Expected:** `[tool-a]`. 1 shortfall. 0 cap exclusions.

### 4. Tag Non-exclusivity — two kinds with independent require constraints

**File:** `count-constrained-knapsack-tag-nonexclusive.toml`

- Budget: 1000 tokens. 3 items: `item-tool` (tool, 0.9, 100t), `item-memory` (memory, 0.8, 100t), `item-extra` (tool, 0.5, 100t).
- Policy: `require_count=1, cap_count=4` for `"tool"` and `"memory"` independently. `bucket_size=100`.
- Phase 1: commits `item-tool` (satisfies tool), `item-memory` (satisfies memory). `preAllocatedTokens=200`. `selectedCount={"tool":1, "memory":1}`.
- Phase 2: `KnapsackSlice` receives `[item-extra (0.5, 100t)]` with budget 800. Selects `item-extra`.
- Phase 3: `item-extra` is kind `"tool"`, count=1 < cap=4 → pass. `selectedCount["tool"]=2`.
- **Expected:** all 3 items selected. 0 shortfalls. 0 cap exclusions.

### 5. Require-and-Cap — require=2, cap=2, knapsack picks best msg items from residual

**File:** `count-constrained-knapsack-require-and-cap.toml`

- Budget: 1000 tokens. `bucket_size=1` (exact knapsack). 5 items: `tool-a` (tool, 0.9, 100t), `tool-b` (tool, 0.7, 100t), `msg-s` (msg, 0.8, 50t), `msg-m` (msg, 0.6, 150t), `msg-l` (msg, 0.4, 200t).
- Policy: `require_count=2, cap_count=2` for `"tool"`. No constraint on `"msg"`.
- Phase 1: commits `tool-a` and `tool-b`. `preAllocatedTokens=200`. `selectedCount["tool"]=2`.
- Phase 2: `KnapsackSlice` receives `[msg-s, msg-m, msg-l]` with budget 800. All three fit (50+150+200=400 < 800). Selects all 3.
- Phase 3: `selectedCount["tool"]=2`, cap=2. Phase 2 items are kind `"msg"` (uncapped) → all pass.
- **Expected:** all 5 items selected. 0 shortfalls. 0 cap exclusions.
