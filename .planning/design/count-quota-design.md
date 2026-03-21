# Count-Quota Design Decision Record

*Slice: S03 — Count-Based Quota Design | Milestone: M002 | Date: 2026-03-21*
*Status: Settled — zero open questions. All DI rulings from T01 and T02 are final.*

---

## Overview

`CountQuotaSlice` is a **decorator slicer** that enforces absolute item-count requirements and per-kind item caps before delegating to an inner slicer. Where `QuotaSlice` operates in token fractions (guarantee N% of budget), `CountQuotaSlice` operates in item counts (guarantee N items of kind K). The two types are complementary and composable: `CountQuotaSlice(inner: QuotaSlice(inner: GreedySlice()))` applies count requirements first, then percentage constraints on the remaining budget.

`CountQuotaSlice` is a fixed-pipeline slicer: it sits upstream of the inner slicer and pre-commits items that satisfy count requirements before budget distribution occurs. Its relationship to `QuotaSlice`:

- **Separate type** — not an extension or subclass of `QuotaSlice`. Count-quota and percentage-quota are semantically distinct constraint systems.
- **Complementary use case** — percentage constraints control token budget allocation; count constraints control item cardinality. Composing them handles the common case where callers need both ("at least 2 tool calls AND tool calls capped at 30% of budget").
- **Configuration type:** `CountQuotaSet` (holding `CountQuotaEntry` values, distinct from `QuotaEntry`).

---

## 1. Algorithm Architecture

**Decision:** `CountQuotaSlice` is a **separate decorator slicer**. It is not an extension of `QuotaSlice`. (DI-1)

### Two-Phase Algorithm

`CountQuotaSlice` executes in two phases for every `Slice` call:

**Phase 1 — Count-Satisfy:**
For each kind with `require_count > 0`, select the top-`require_count` candidate items ordered by score descending (within-kind score ordering). These items are committed as selected and removed from the candidate pool. Their token cost is accumulated as `pre_allocated_tokens`.

If fewer than `require_count` candidates of a kind exist, select all available. Record the shortfall in `quota_violations` (see Section 4 — scarcity behavior).

**Phase 2 — Budget-Distribute:**
Run `COUNT-DISTRIBUTE-BUDGET` on the remaining (non-committed) candidates with the residual budget (`targetTokens - pre_allocated_tokens`). The inner slicer operates on this residual pool and residual budget. Count caps are enforced during this phase via `CountCapExceeded` exclusions.

### Wrapping Rules

`CountQuotaSlice` wraps any inner slicer **except** `KnapsackSlice` in v1 (see Section 5 for the build-time guard and upgrade path). The canonical inner slicer is `GreedySlice`. Composition with `QuotaSlice` is supported and recommended for combined count + percentage constraints.

---

## 2. Tag Non-Exclusivity Semantics

**Decision:** An item matching multiple `RequireCount` tag constraints counts **toward all matching constraints simultaneously**. Non-exclusive semantics are mandatory; there is no per-entry or per-slicer exclusivity mode. (DI-2)

### Semantics

An item tagged `["critical", "urgent"]` increments the satisfied-count for both `RequireCount("critical")` and `RequireCount("urgent")` by 1 when it is selected in the count-satisfy phase. The selection loop iterates over all tags of an item and increments per-tag counters for each matching `RequireCount` constraint; it does not break after the first match.

### Canonical Worked Example

Setup:

    items  = [ Item(tags:["critical","urgent"], score:0.9, tokens:100),
               Item(tags:["critical"],          score:0.8, tokens:100),
               Item(tags:["urgent"],            score:0.7, tokens:100) ]
    budget = 500 tokens
    policy = RequireCount("critical", 2), RequireCount("urgent", 2)

Execution — Phase 1, score-descending:

    Select Item 1 (score 0.9, tags ["critical","urgent"]):
      critical_satisfied = 1, urgent_satisfied = 1
    Select Item 2 (score 0.8, tags ["critical"]):
      critical_satisfied = 2 — "critical" requirement DONE
    Select Item 3 (score 0.7, tags ["urgent"]):
      urgent_satisfied = 2 — "urgent" requirement DONE
    All count requirements satisfied. pre_allocated_tokens = 300.

Phase 2: residual budget = 200 tokens passed to inner slicer for additional items.

Result: 3 items selected, all distinct. RequireCount("critical", 2) satisfied by Items 1+2. RequireCount("urgent", 2) satisfied by Items 1+3. Correct and expected outcome.

### Caller Guidance

- **Tag overlap is intentional.** An item tagged `["critical", "urgent"]` is genuinely both; count-quota treats it as both. This is correct behavior, not a defect.
- **Multi-tag saturation at n=1.** A single item tagged with all K required tags satisfies all K `RequireCount(1)` constraints simultaneously. If this is not desired, callers should set `require_count > 1` for constraints that should not be satisfiable by a single item, or design tag spaces to avoid total overlap.
- **Exclusive semantics workaround.** The `metadata["cupel:primary_tag"]` convention (caller-assigned, not slicer-enforced) designates which tag a multi-tag item "primarily" belongs to. This is documentation-only for downstream consumers of `SelectionReport` — it is not interpreted by `CountQuotaSlice`.
- **No priority-ordering mechanism.** There is no `CountQuotaEntry.priority` field. Non-exclusive is the only supported semantics. This ruling is irreversible post-ship; changing to exclusive semantics requires a breaking version bump.

---

## 3. Pinned Item Interaction

**Decision:** Count-quota caps apply to **slicer-selected items only**. Pinned items are pre-committed before the slicer runs and are outside the slicer's scope. The `require_count` is decremented by `pinned_count` for each kind before Phase 1 executes. (DI-5)

### Cap Scope

`CapCount(K, n)` limits the number of items of kind K that `CountQuotaSlice` may select via its inner slicer — not the combined total of pinned + slicer-selected items. Pinned items always win regardless of cap configuration.

### Require-Count Decrement Rule

The residual requirement entering Phase 1 is:

    residual_require(K) = max(0, require_count(K) - pinned_count(K))
    selectable_cap(K)   = max(0, cap_count(K) - pinned_count(K))

If `pinned_count(K) >= require_count(K)`, the slicer selects 0 additional items for kind K in the count-satisfy phase — the requirement is already met by pinned items.

If `pinned_count(K) > cap_count(K)`, the slicer selects 0 additional items for kind K in Phase 2. No error or warning is raised — pinned items represent a caller-committed pre-decision that supersedes the cap.

### Worked Example

    Constraints: RequireCount("system", 2), CapCount("system", 3)
    Pinned:      2 items tagged "system"

    Residual require_count entering Phase 1: max(0, 2 - 2) = 0
    Slicer's selectable cap:                 max(0, 3 - 2) = 1

    Phase 1: no items selected (requirement met by pinned items).
    Phase 2: inner slicer may select at most 1 additional system item.

---

## 4. Conflict Detection Rules

**Decision:** Build-time constraint validation is performed at `CountQuotaSlice` construction. Cross-kind conflicts are run-time behavioral conditions. Default `ScarcityBehavior` is `Degrade`. (DI-3, D040)

### Build-Time Checks (Construction-Time Guards)

The following constraint violations are detected at construction time and return an error immediately (Rust: `Err(CupelError::...)`, .NET: throw `ArgumentException`):

1. `require_count(K) > cap_count(K)` for the same kind — a requirement that can never be satisfied by the slicer.
2. `cap_count(K) == 0` with `require_count(K) > 0` for the same kind — a zero cap with a positive requirement is a guaranteed violation.

These are classified as configuration errors, not run-time scarcity: they indicate a logically impossible constraint that no input set can satisfy.

### Run-Time Scarcity: ScarcityBehavior

When the available candidate pool contains fewer items of kind K than `require_count(K)` at run time (consistent with D040 — run-time scarcity is behavioral):

- **`ScarcityBehavior::Degrade` (default):** Include all available candidates for the kind (even if fewer than `require_count`). Populate `quota_violations` with the shortfall. Continue pipeline execution. No exception raised.
- **`ScarcityBehavior::Throw` (per-slicer override):** Raise a run-time error identifying the kind and shortfall. Use for callers who treat count requirements as hard guarantees (e.g., required disclaimer text that must always appear).

Per-slicer configurability is the v1 surface. Per-entry override is deferred pending confirmed demand.

### quota_violations Field Shape

Added to `SelectionReport` as a non-required field with empty-list default:

    quota_violations: Vec<CountQuotaViolation>           (Rust)
    IReadOnlyList<CountQuotaViolation> QuotaViolations   (.NET, non-required, default empty)

    CountQuotaViolation {
        kind:             string   — the tag value that could not be fully satisfied
        required_count:   int      — the RequireCount value configured by the caller
        satisfied_count:  int      — items of this kind actually included
                                     (satisfied_count < required_count on violation)
    }

Empty list means no violations (normal/success case). A non-empty list indicates scarcity degradation. Per D046, `CountRequireUnmet` does NOT appear as an `ExclusionReason` on `ExcludedItem` — scarcity is reported at the `SelectionReport` level, not per-item.

### CountCapExceeded Exclusion Reason

Items excluded because a `CapCount` limit was reached carry:

    ExclusionReason::CountCapExceeded { kind: string, cap: int, count: int }

Where `kind` is the ContextKind tag, `cap` is the configured `cap_count`, and `count` is the running tally of slicer-selected items of that kind at the point of exclusion (i.e., `count == cap`, and including this item would exceed the cap). This IS a per-item reason and belongs on `ExcludedItem.reason`.

---

## 5. KnapsackSlice Compatibility

**Decision:** `CountQuotaSlice` + `KnapsackSlice` is **rejected for v1** with a build-time guard (option 5E, per D052). (DI-4)

### Build-Time Guard Message

At `CountQuotaSlice` construction time, if the inner slicer is a `KnapsackSlice` instance, the construction must fail immediately with:

> `"CountQuotaSlice does not support KnapsackSlice as the inner slicer in this version. Use GreedySlice as the inner slicer. A CountConstrainedKnapsackSlice will be provided in a future release."`

The guard message uses only public API names (per D032): `CountQuotaSlice`, `KnapsackSlice`, `GreedySlice`, `CountConstrainedKnapsackSlice`. No internal type names.

### Upgrade Path

`CountConstrainedKnapsackSlice` — a separate slicer implementing joint count-and-knapsack optimization within a single DP pass — is the target shape for the combined use case. It will be scoped to M003+ if demand is established. Because `CountQuotaSlice` is a separate type (DI-1), adding `CountConstrainedKnapsackSlice` requires no modification to `CountQuotaSlice`.

---

## 6. ExclusionReason + SelectionReport Extensions

**Decision:** Both `SelectionReport` and `ExclusionReason` can be safely extended in Rust and .NET. The backward-compatibility audit (DI-6) confirmed the `quota_violations` field approach and the `CountCapExceeded` variant are non-breaking for all property-access callers. (DI-6)

### Rust Backward Compatibility

- `SelectionReport` is `#[non_exhaustive]`. Adding `pub quota_violations: Vec<CountQuotaViolation>` is non-breaking: callers outside the crate cannot construct `SelectionReport` via struct literal (the compiler enforces this), and field-access callers are unaffected.
- `ExclusionReason` is `#[non_exhaustive]`. Adding `CountCapExceeded` variant is non-breaking: callers outside the crate must already include a wildcard arm in match expressions.
- **Verdict: Rust extension is safe.**

### .NET Backward Compatibility

- `SelectionReport` is `public sealed record`. Adding a new `required` property would be breaking. Adding a **non-required property with a default value** is non-breaking for callers who access the record via property access (`report.QuotaViolations`).
- `QuotaViolations` must be added as a non-required property with a default of an empty immutable list.
- **Positional deconstruction caveat:** Callers using positional deconstruction (`var (events, included, excluded, ...) = report`) would break when fields are added. Positional deconstruction of `SelectionReport` is **explicitly unsupported** and must be documented as such. This applies to all future field additions.
- **Verdict: .NET extension is safe for property-access callers.**

### CountCapExceeded Variant Shape (both languages)

    // Rust
    ExclusionReason::CountCapExceeded { kind: String, cap: usize, count: usize }

    // .NET
    CountCapExceeded(string Kind, int Cap, int Count)

`kind` — the ContextKind tag string. `cap` — the configured cap_count limit. `count` — the number of items of this kind already selected when this item was excluded (equals cap at the point of exclusion).

---

## Pseudocode: COUNT-DISTRIBUTE-BUDGET

The subroutine adapts `DISTRIBUTE-BUDGET` from `spec/src/slicers/quota.md` by prepending a count pre-allocation phase before the proportional token-budget distribution phase. It takes `(partitions, candidateTokenMass, targetTokens, quotas)` where `quotas` carries four fields per kind: `requireTokens`, `capTokens`, `requireCount`, and `capCount`.

```text
COUNT-DISTRIBUTE-BUDGET(partitions, candidateTokenMass, targetTokens, quotas):

    // Phase 1 — Count pre-allocation
    // For each kind with requireCount > 0, select required items by score (descending),
    // remove them from the candidate pool, and accumulate their token cost.

    preAllocated     <- empty map of ContextKind -> list of ScoredItem
    preAllocTokens   <- 0
    selectedCount    <- empty map of ContextKind -> integer   // running count per kind
    remainingCandidates <- deep-copy of partitions            // mutable pool for Phase 2

    for each (kind, items) in partitions:
        reqCount <- quotas.getRequireCount(kind)     // 0 if not configured
        if reqCount <= 0:
            continue

        sorted <- SORT-DESCENDING-BY-SCORE(items)
        committed <- empty list
        i <- 0
        while i < length(sorted) and length(committed) < reqCount:
            APPEND(committed, sorted[i])
            preAllocTokens <- preAllocTokens + sorted[i].item.tokens
            i <- i + 1

        preAllocated[kind] <- committed
        selectedCount[kind] <- length(committed)

        // Remove committed items from the mutable candidate pool
        for each item in committed:
            REMOVE(remainingCandidates[kind], item)

    // Phase 2 — Budget adjustment and proportional distribution
    // Subtract pre-allocated cost from targetTokens; apply DISTRIBUTE-BUDGET
    // logic from quota.md on the remaining candidates with the residual budget.

    residualBudget <- max(0, targetTokens - preAllocTokens)

    // Recompute candidate token mass for remaining candidates only
    residualTokenMass <- empty map of ContextKind -> integer
    for each (kind, items) in remainingCandidates:
        mass <- 0
        for i <- 0 to length(items) - 1:
            mass <- mass + items[i].item.tokens
        residualTokenMass[kind] <- mass

    // Steps 1–4 of DISTRIBUTE-BUDGET from quota.md, applied to residualBudget
    // and residualTokenMass. (requireTokens and capTokens are re-derived from
    // quotas using residualBudget as the base for proportional computation.)

    requireTokens <- empty map of ContextKind -> integer
    capTokens     <- empty map of ContextKind -> integer
    for each kind in quotas.configuredKinds:
        requireTokens[kind] <- floor(quotas.getRequire(kind) / 100.0 * targetTokens)
        capTokens[kind]     <- floor(quotas.getCap(kind) / 100.0 * targetTokens)

    totalRequired <- 0
    for each (kind, req) in requireTokens:
        totalRequired <- totalRequired + req

    unassignedBudget <- max(0, residualBudget - totalRequired)

    totalMassForDistribution <- 0
    for each (kind, items) in remainingCandidates:
        cap     <- capTokens[kind] if kind in capTokens, else targetTokens
        require <- requireTokens[kind] if kind in requireTokens, else 0
        if cap > require:
            totalMassForDistribution <- totalMassForDistribution + residualTokenMass[kind]

    kindBudgets <- empty map of ContextKind -> integer
    for each (kind, items) in remainingCandidates:
        require <- requireTokens[kind] if kind in requireTokens, else 0
        cap     <- capTokens[kind] if kind in capTokens, else targetTokens

        proportional <- 0
        if totalMassForDistribution > 0 and cap > require:
            proportional <- floor(unassignedBudget * residualTokenMass[kind]
                                   / totalMassForDistribution)

        kindBudget <- require + proportional
        if kindBudget > cap:
            kindBudget <- cap

        kindBudgets[kind] <- kindBudget

    // Phase 3 — Count cap enforcement
    // Track how many items of each kind are selected by the inner slicer
    // during the per-kind slicing phase (post-distribution). Once capCount(kind)
    // is reached (accounting for items already committed in Phase 1), additional
    // candidates of that kind are excluded with CountCapExceeded.

    // The inner slicer receives kindBudgets from Phase 2 as its token budget.
    // The caller (COUNT-QUOTA-SLICE outer loop) enforces capCount by:
    //   (a) tracking selectedCount[kind] across Phase 1 committed items, and
    //   (b) for each candidate considered by the inner slicer, checking:
    //         if selectedCount[kind] >= quotas.getCapCount(kind):
    //             exclude candidate with reason CountCapExceeded(kind, capCount, selectedCount)
    //         else:
    //             allow candidate; selectedCount[kind] <- selectedCount[kind] + 1

    return (kindBudgets, preAllocated, selectedCount)
```

**Notes on pseudocode conventions:** Floor truncation is used for all percentage-to-token conversions (consistent with `DISTRIBUTE-BUDGET`). The `selectedCount` map tracks accumulated item counts across both phases for cap enforcement. The inner slicer runs on `remainingCandidates` with `kindBudgets`; the Phase 3 cap check is performed by the wrapping `COUNT-QUOTA-SLICE` loop rather than inside this subroutine, keeping the subroutine pure.

---

## Conformance Vector Outlines

The following scenario sketches describe the minimum conformance coverage for `CountQuotaSlice`. These are prose descriptions; full TOML test vectors are scoped to M003.

**(1) Baseline Count Satisfaction — Greedy inner, require 2 of kind X, 3 candidates available.**
Policy: `RequireCount("tool", 2)`. Candidates: 3 items tagged "tool" with scores 0.9, 0.7, 0.5. Budget: 1000 tokens.
Expected: Phase 1 selects the top-2 items by score (0.9 and 0.7). Phase 2 passes the 3rd item to the inner slicer with the residual budget. `quota_violations` is empty. `ExcludedItem` list does not contain `CountCapExceeded` reasons.

**(2) Count-Cap Exclusion — Cap 1 of kind X, 3 candidates; first wins, 2 get CountCapExceeded.**
Policy: `CapCount("tool", 1)`. Candidates: 3 items tagged "tool" with scores 0.9, 0.7, 0.5. Budget: 1000 tokens.
Expected: Phase 2 inner slicer considers all 3 candidates. The highest-scoring item (0.9) is selected. The remaining 2 candidates are excluded with `ExclusionReason::CountCapExceeded { kind: "tool", cap: 1, count: 1 }`. `quota_violations` is empty (cap exclusion is not a violation).

**(3) Pinned-Count Decrement — 1 pinned of kind X, require 2; slicer selects 1 more.**
Policy: `RequireCount("system", 2)`. Pinned items: 1 item tagged "system". Slicer candidates: 2 additional "system" items.
Expected: Residual `require_count` entering Phase 1 = max(0, 2 - 1) = 1. Phase 1 selects the top-1 candidate "system" item. Combined result: 2 "system" items total (1 pinned + 1 slicer-selected). `quota_violations` is empty.

**(4) Scarcity Degrade — Require 3 of kind X, only 1 candidate; quota_violations populated.**
Policy: `RequireCount("tool", 3)`, `ScarcityBehavior::Degrade` (default). Candidates: 1 item tagged "tool".
Expected: Phase 1 selects the 1 available "tool" item (all that exist). `quota_violations` contains one entry: `CountQuotaViolation { kind: "tool", required_count: 3, satisfied_count: 1 }`. Pipeline continues (no exception). The overall `SelectionReport` includes the available item.

**(5) Tag Non-Exclusivity — Multi-tag item satisfies 2 require constraints simultaneously.**
Policy: `RequireCount("critical", 1)`, `RequireCount("urgent", 1)`. Candidates: 1 item tagged `["critical","urgent"]` (score 0.9), 1 item tagged "critical" (score 0.5), 1 item tagged "urgent" (score 0.4).
Expected: Phase 1, score-descending: the multi-tag item (0.9) is selected first; it increments both `critical_satisfied` and `urgent_satisfied` to 1. Both requirements are satisfied by a single item. Phase 1 does not select the other two items for count-satisfaction (requirements are already met). `quota_violations` is empty.
