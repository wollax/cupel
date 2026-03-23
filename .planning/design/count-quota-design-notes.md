# Count-Quota Design Notes — DI-1 and DI-2

*Slice: S03 — Count-Based Quota Design | Milestone: M002 | Date: 2026-03-21*
*Task: T01 — Debate algorithm architecture and tag non-exclusivity*

---

## DI-1: Algorithm Architecture

### Ruling

Count-quota is a **separate `CountQuotaSlice` decorator** that wraps any inner slicer (e.g., `GreedySlice`); it is not an extension of `QuotaSlice`.

### Rationale

Count-quota (guarantee N items of kind K) and percentage-quota (guarantee a fraction of the token budget) are semantically distinct constraint systems operating in different units with different distribution logic. The two-phase count-quota algorithm — phase 1: commit required items by score; phase 2: distribute remaining budget to the inner slicer — is structurally orthogonal to `DISTRIBUTE-BUDGET`, which works exclusively in token fractions. Merging them into a single type would couple two conceptually independent mechanisms and force every `QuotaEntry` to carry optional count fields even when not needed (the token-estimation approach rejected in 1B is architecturally unsound because token costs per item are not known at policy construction time, making any reservation-by-estimate non-deterministic).

The deciding factor is whether callers commonly need both count AND percentage constraints on the **same kind simultaneously** (e.g., "at least 2 tool calls AND tool calls capped at 30% of budget"). They can, but this is served naturally by composition: `CountQuotaSlice(inner: QuotaSlice(quotas: [Cap("tool", 30%)], inner: GreedySlice()))`. The decorator chain lets count requirements be satisfied in phase 1, then the inner `QuotaSlice` applies percentage caps to the remaining budget allocation. No unified type is needed to support the combined case.

### Caller Guidance

- Callers who need both count floors and percentage caps on the same kind should compose the two slicers: `CountQuotaSlice(inner: QuotaSlice(...))`. The outer decorator handles count requirements; the inner handles percentage constraints.
- `CountQuotaSlice` with `KnapsackSlice` as the inner slicer is explicitly unsupported in v1 (build-time guard per D052). Use `GreedySlice` as the inner slicer for count-quota in v1.
- A `CountQuotaSet`-equivalent (analogous to `QuotaSet`) is the configuration type for `CountQuotaSlice`; it holds `CountQuotaEntry` values, distinct from `QuotaEntry`.

### Implications for Downstream Questions

- DI-4 (KnapsackSlice compatibility): The separate-slicer architecture makes the build-time guard (5E, D052) unambiguous — `CountQuotaSlice` simply does not accept a `KnapsackSlice` inner slicer.
- DI-3 (scarcity behavior): The two-phase algorithm makes scarcity detection straightforward: if phase 1 cannot commit `require_count` items after scoring, the shortfall is known before phase 2 executes. The `ScarcityBehavior` enum applies per entry or per slicer.
- DI-5 (pinned items): With a separate decorator, pinned item accounting is performed before `CountQuotaSlice` runs. Phase 1 starts with `require_count - pinned_count_for_kind` as the residual requirement (per D053, caps apply to slicer-selected items only).

---

## DI-2: Tag Non-Exclusivity

### Ruling

An item whose kind matches multiple count-quota tag constraints **counts toward ALL matching `RequireCount` constraints simultaneously** (non-exclusive semantics); including a single item tagged `["critical", "urgent"]` increments the satisfied-count for both `RequireCount("critical")` and `RequireCount("urgent")` by one.

### Rationale

Non-exclusive semantics are deterministic (no tag-ordering dependency required), semantically correct (an item that genuinely has all matching properties satisfies all matching requirements), and produce the intuitively expected behavior for multi-tag items. The exclusive alternative requires either a formal tag-ordering specification (2B — rejected because `ContextItem` tag storage order is not currently part of the Cupel spec contract) or a priority-ordering mechanism (2C — rejected as premature generalization without a confirmed use case). Non-deterministic exclusive semantics would be a specification defect.

The "saturation" risk — a single item tagged with all 10 required tags satisfying 10 separate `RequireCount(1)` constraints — is acceptable behavior when explicitly documented. If an item genuinely possesses all tagged properties, satisfying all n=1 requirements with that one item is the **correct** output. The risk only materializes when callers over-tag items relative to their policy intent; this is a caller-side tagging design issue, not a semantic defect. For `RequireCount(n > 1)`, a single multi-tagged item can only contribute 1 toward each count, so it cannot singlehandedly satisfy any n≥2 requirement.

### Canonical Worked Example

**Setup:**
```
items    = [ Item(tags:["critical","urgent"], score:0.9, tokens:100),
             Item(tags:["critical"],          score:0.8, tokens:100),
             Item(tags:["urgent"],            score:0.7, tokens:100) ]
budget   = 500 tokens
policy   = RequireCount("critical", 2).RequireCount("urgent", 2)
```

**Execution (two-phase):**
- Phase 1 (count-satisfy, score-descending):
  - Select Item 1 (0.9): critical-count=1, urgent-count=1 (counts toward both)
  - Select Item 2 (0.8): critical-count=2 ✓
  - Select Item 3 (0.7): urgent-count=2 ✓
  - All count requirements satisfied with 3 items, 300 tokens committed.
- Phase 2: remaining budget 200 tokens passed to inner slicer for additional items.

**Result:** `RequireCount("critical", 2)` satisfied by Items 1+2. `RequireCount("urgent", 2)` satisfied by Items 1+3. No additional items needed from phase 2 (budget utilization is 300/500).

This is the correct and expected outcome: 3 items, all distinct, each counted toward exactly the requirements it belongs to.

### Caller Guidance

- **Tag overlap is intentional:** An item tagged `["critical", "urgent"]` is genuinely both critical and urgent; count-quota treats it as both.
- **Multi-tag saturation at n=1:** A single item tagged with all K required tags satisfies all K `RequireCount(1)` constraints. If this is not desired, callers should design tag spaces to avoid total overlap, or set `require_count > 1` for constraints that should not be satisfiable by a single item.
- **To model exclusive semantics (if needed):** Callers can avoid tag overlap in their item tagging scheme, or use the `metadata["cupel:primary_tag"]` convention (documented in spec) as a caller-side workaround to designate which tag a multi-tag item "primarily" belongs to — but this convention is not enforced by the slicer; it is documentation-only for downstream consumers of `SelectionReport`.
- **No priority-ordering mechanism is provided:** There is no `CountQuotaEntry.priority` field. Non-exclusive is the only supported semantics.

### Implications for Downstream Questions

- **DI-2 is irreversible post-ship.** This ruling defines the public semantic contract for count-quota. Changing to exclusive semantics after release requires a breaking version bump. Document prominently in the spec chapter.
- **DI-3 (scarcity):** Non-exclusive semantics make scarcity detection simpler — after phase 1 scoring, the number of items satisfying each constraint is unambiguous. The `TraceEvent::CountQuotaScarcity { kind, required, available }` variant (or `SelectionReport.quota_violations` if backward-compat audit confirms safety) captures the shortfall clearly.
- **Spec "satisfy-by-minimum" note:** The spec chapter for `COUNT-DISTRIBUTE-BUDGET` should include a warning note on multi-tag saturation at `n=1` requirements (as flagged in the S03 plan), pointing to the canonical worked example in this document.
- **Pseudocode:** The `COUNT-DISTRIBUTE-BUDGET` phase 1 inner loop must iterate over all tags of an item and increment the per-tag counter for each matching `RequireCount` constraint, not break after the first match.

---

## DI-6: Backward-Compatibility Audit (Precondition for DI-3)

### Ruling

Both languages support extending `SelectionReport` with new fields without breaking existing callers. The `6D` approach (`quota_violations` field on `SelectionReport`) is safe to adopt.

### Rust Audit

- `SelectionReport` struct at `crates/cupel/src/diagnostics/mod.rs:266` is marked `#[non_exhaustive]`. Adding a new `pub quota_violations: Vec<CountQuotaViolation>` field is **non-breaking**: callers who construct `SelectionReport` via struct literal would already fail to compile (non-exhaustive prevents struct literal construction outside the crate); callers accessing fields by name continue to work unchanged.
- `ExclusionReason` enum is marked `#[non_exhaustive]`. Adding a new `CountCapExceeded` variant is **non-breaking**: callers who `match` on `ExclusionReason` must already include a wildcard arm (the compiler enforces this for non-exhaustive enums outside the crate).
- **Verdict: Rust extension is safe.** Adding struct fields and enum variants is backward-compatible for all external callers.

### .NET Audit

- `SelectionReport` in `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` is defined as `public sealed record SelectionReport` with `required` init-only properties.
- Adding a new `required` property would be **breaking** (existing object initializers omit the new field). Adding a new **optional** property (with a default value or not marked `required`) is **non-breaking** for callers who access the record via property access (`report.QuotaViolations`).
- Callers using **positional deconstruction** (`var (events, included, excluded, ...) = report`) would break when fields are added (positional deconstruct is positional, not name-based). This is a caller-side footgun.
- **Verdict: .NET extension is safe for property-access callers.** The `quota_violations` field should be added as a non-required property with a sensible default (empty list). **Recommendation: document explicitly that positional deconstruction on `SelectionReport` is not supported and will break on field additions.**

---

## DI-3: Scarcity Behavior and SelectionReport Representation

### Ruling

- **Default `ScarcityBehavior`:** `Degrade` — include all available candidates for the kind (even if fewer than `require_count`), populate `quota_violations`, and continue pipeline execution. This is consistent with D040 (run-time scarcity is behavioral, not an exception by default).
- **Diagnostic mechanism:** `SelectionReport.quota_violations` field — a dedicated, prominently placed, easily-accessed list of `CountQuotaViolation` records. This is chosen over `TraceEvent::CountQuotaScarcity` because it provides a structured, first-class signal for callers who need to know if their count requirements were fully satisfied, without parsing event logs.
- **`quota_violations` field shape:**
  ```
  quota_violations: Vec<CountQuotaViolation>  (Rust) / IReadOnlyList<CountQuotaViolation> (.NET)

  CountQuotaViolation {
    kind:             string   — the tag value that could not be fully satisfied
    required_count:   int      — the RequireCount value configured by the caller
    satisfied_count:  int      — the number of items of this kind actually included
                                 (satisfied_count < required_count when a violation occurs)
  }
  ```
  Empty list (not null) when no violations occurred. A `quota_violations` list with entries indicates scarcity; an empty list is the normal/success case.
- **`ScarcityBehavior` configurability:** Per-slicer for v1. Callers who want `Throw` on any violation configure `CountQuotaSlice` with `ScarcityBehavior::Throw`. Per-entry override is deferred to a future version pending confirmed demand.

### Rationale

D040 distinguishes the *class* of condition (run-time, behavioral) but does not mandate the default response. `Degrade` is the safer default: a partial context is usually better than an exception for callers who cannot guarantee that `require_count` items of a given kind will always be available. `quota_violations` on `SelectionReport` (enabled by the DI-6 audit finding) provides a structured, first-class signal — callers who care about violations check the list; callers who don't care ignore it. `TraceEvent` is a lower-level observability mechanism and is less ergonomic as a primary API surface for a semantically important condition.

Per-slicer configurability keeps the API surface minimal for v1 while covering the majority of use cases. The `Throw` option is essential for callers who treat count requirements as hard guarantees (e.g., required disclaimer text that must always appear).

### Implications

- D040 is honored: scarcity is behavioral by default (Degrade), not an exception.
- D046 is honored: `CountRequireUnmet` does NOT appear as an `ExclusionReason` on `ExcludedItem`. Scarcity is reported at the `SelectionReport` level via `quota_violations`, not per-item.
- `ExclusionReason::CountCapExceeded` is a separate variant for items excluded because a `CapCount` limit was reached — this IS a per-item reason and belongs on `ExcludedItem.reason`.

---

## DI-4: KnapsackSlice Compatibility Path

### Ruling

**5E chosen for v1: reject the `CountQuotaSlice` + `KnapsackSlice` combination with a build-time guard.**

### Guard Specification

At `CountQuotaSlice` construction time, if the inner slicer is a `KnapsackSlice` instance, return an error (Rust: `Err(CupelError::...)`, .NET: throw `ArgumentException`):

> `"CountQuotaSlice does not support KnapsackSlice as the inner slicer in this version. Use GreedySlice as the inner slicer. A CountConstrainedKnapsackSlice will be provided in a future release."`

The guard message uses only public API names (per D032): `CountQuotaSlice`, `KnapsackSlice`, `GreedySlice`, `CountConstrainedKnapsackSlice`. No internal type names.

### Upgrade Path

`CountConstrainedKnapsackSlice` — a separate slicer implementing joint count-and-knapsack optimization — is the target shape for the combined use case. It will be scoped to M003+ if demand is established. The separate-slicer pattern (D from DI-1 architecture) means this can be added without modifying `CountQuotaSlice`.

### Rationale

`CountQuotaSlice` wrapping `GreedySlice` covers the overwhelming majority of v1 use cases. Count-constrained knapsack optimization is a significant implementation investment (requires extending the DP algorithm to carry count constraints as hard constraints within the DP state). No confirmed M002 use case requires joint optimization. The `CupelError::TableTooLarge` guard provides the pattern for this class of early-detection error. 5A (pre-processing with accepted sub-optimality) is the next step up if demand materializes before a full 5D implementation is justified.

---

## DI-5: Pinned Item + Overshoot Edge Case

### Ruling

- **Cap scope:** Count-quota caps apply to **slicer-selected items only**. Pinned items are committed before the slicer runs and are outside the slicer's scope. `CapCount(K, n)` limits the number of items of kind K that `CountQuotaSlice` may select via its inner slicer — not the combined total of pinned + slicer-selected items.
- **`pinned_count > cap_count` outcome:** No special treatment. Pinned items of kind K always win (they are committed before the slicer runs). If the caller configures `CapCount("system", 1)` but pins 3 system-prompt items, `CountQuotaSlice` selects 0 additional system items — its budget for kind K is fully consumed by the pinned allocation (the slicer's selectable budget for kind K is `max(0, cap_count - pinned_count) = 0`). No error, no warning, no trace event.
- **`require_count` decrement rule:** Pinned items of kind K decrement `require_count(K)` before the count-satisfy phase (phase 1) executes. If `pinned_count(K) >= require_count(K)`, the slicer selects 0 additional items for kind K in the count-satisfy phase — the requirement is already met by pinned items. The residual requirement entering phase 1 is `max(0, require_count(K) - pinned_count(K))`.

### Worked Example

```
Constraints: RequireCount("system", 2), CapCount("system", 3)
Pinned:      2 items tagged "system"

Residual require_count entering phase 1: max(0, 2 - 2) = 0
Slicer's selectable cap:                 max(0, 3 - 2) = 1

Phase 1: no items needed (requirement met by pinned items).
Phase 2: inner slicer may select at most 1 additional system item.
```

### Rationale

Pinned items represent caller-committed context that is outside the slicer's authority. The slicer operates on the residual problem — the budget and count slots not consumed by pinned items. This is consistent with the broader Cupel model: pinned items are a caller-side pre-commitment, not a slicer decision. "No special treatment for overshoot" is the cleanest option: it avoids adding a `CountCapExceededByPinnedItems` trace event for a degenerate case where the caller has already made a decision (by pinning those items) that supersedes the cap. The cap is an advisory limit on the slicer's choices, not a hard global constraint.

