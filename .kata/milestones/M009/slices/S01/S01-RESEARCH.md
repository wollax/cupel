# S01: CountConstrainedKnapsackSlice — Rust implementation — Research

**Date:** 2026-03-25
**Domain:** Rust slicer implementation + conformance test infrastructure
**Confidence:** HIGH

## Summary

`CountConstrainedKnapsackSlice` is a new slicer in `crates/cupel/src/slicer/count_constrained_knapsack.rs` that combines count guarantees (Phase 1 pre-commit) with near-optimal token packing (Phase 2 knapsack). It re-uses `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall`, and `CountCapExceeded` verbatim from M006 — nothing needs to be re-defined. The algorithm is the same 3-phase structure as `CountQuotaSlice` except Phase 2 calls a stored `KnapsackSlice` instance rather than a dynamic `Box<dyn Slicer>`.

The pipeline wiring (`is_count_quota() → true`, `count_cap_map()`) already exists and is exercised by `CountQuotaSlice`. `CountConstrainedKnapsackSlice` plugs in via those same hooks with zero pipeline changes. The conformance test infrastructure also exists and only needs a new `"count_constrained_knapsack"` arm added to `build_slicer_by_type` in `tests/conformance.rs`.

Primary risks: the sub-optimality when required items are token-heavy (documented trade-off), and ensuring the KnapsackSlice sub-budget is constructed with the same pattern as `CountQuotaSlice` to avoid accidentally passing a `target_tokens=0` budget.

## Recommendation

Create `crates/cupel/src/slicer/count_constrained_knapsack.rs`, wire it into `slicer/mod.rs` and `lib.rs`, add 5 TOML conformance vectors (slicing stage), add 5 integration tests in `crates/cupel/tests/count_constrained_knapsack.rs`, and update the conformance test harness. No pipeline changes needed.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Per-kind require/cap entry type | `CountQuotaEntry` in `slicer/count_quota.rs` | Already validated (require ≤ cap, zero-cap guard), already exported from `lib.rs` |
| Scarcity degrade/throw behavior | `ScarcityBehavior` in `slicer/count_quota.rs` | Already exported; same semantics apply |
| Shortfall recording | `CountRequirementShortfall` in `diagnostics/mod.rs` | Already exported; records `kind`, `required_count`, `satisfied_count` |
| Cap exclusion reason | `ExclusionReason::CountCapExceeded` | Already exported; pipeline wires it via `count_cap_map()` |
| Knapsack DP | `KnapsackSlice` in `slicer/knapsack.rs` | Existing, correct, OOM-guarded; wrap an instance — do not duplicate the DP |
| Phase 1 algorithm | Copy the Phase 1 block from `CountQuotaSlice::slice` exactly | Proven correct; same score-descending per-kind commit logic |
| Pipeline cap classification | `pipeline/mod.rs` lines 385–431 (`count_cap_map()` path) | Already wired for `is_count_quota() → true`; no changes needed |
| Monotonicity guard | `Pipeline::find_min_budget_for` checks `is_count_quota()` (line 731) | Auto-applies when `is_count_quota() → true` |
| QuotaPolicy analytics | Implement `QuotaPolicy` same as `CountQuotaSlice` | `quota_utilization` will work automatically |
| Conformance TOML harness | `tests/conformance/slicing.rs` + `run_count_quota_full_test` helper | Add new `"count_constrained_knapsack"` arm to `build_slicer_by_type` in `conformance.rs`; conformance test functions mirror the existing count_quota pattern |

## Existing Code and Patterns

- `crates/cupel/src/slicer/count_quota.rs` (783 lines) — **primary reference**; copy Phase 1 and Phase 3 logic directly; replace Phase 2's `self.inner.slice(&remaining, &sub_budget)?` with `self.knapsack.slice(&remaining, &sub_budget)?` where `self.knapsack: KnapsackSlice` is stored by value (not boxed)
- `crates/cupel/src/slicer/knapsack.rs` — `KnapsackSlice::new(bucket_size)` constructor returns `Result`; `KnapsackSlice::with_default_bucket_size()` is the convenience path (bucket_size=100); stores `bucket_size: i64` by value; `Copy + Clone + Debug`
- `crates/cupel/src/slicer/mod.rs` — declares trait methods; `is_count_quota() → false` default (override to `true`); `is_knapsack() → false` (use default — do NOT override to `true`); `count_cap_map()` default returns empty map (override with per-kind caps from entries)
- `crates/cupel/src/lib.rs` — add `CountConstrainedKnapsackSlice` to the `pub use slicer::{...}` block alongside `CountQuotaSlice`
- `crates/cupel/tests/conformance.rs` — `build_slicer_by_type` needs a new `"count_constrained_knapsack"` arm; parse `bucket_size`, `scarcity_behavior`, `entries` (same shape as `"count_quota"` arm plus `bucket_size`)
- `crates/cupel/tests/conformance/slicing.rs` — add 5 test functions following `count_quota_*` pattern; `run_count_quota_full_test` helper is reusable for shortfall/cap assertions
- `crates/cupel/conformance/required/slicing/` — 5 new TOML files; copy to `spec/conformance/required/slicing/` and `conformance/required/slicing/` (D082 — three locations)

## Constraints

- **No new types** for require/cap entries, scarcity, shortfalls, or cap exclusion (D175) — re-use M006 types exactly
- **`is_count_quota() → true`** (D176) — gates `find_min_budget_for` monotonicity guard
- **`is_knapsack() → false`** (D176) — use inherited default; `CountConstrainedKnapsackSlice` is not a raw knapsack, so `CountQuotaSlice`'s guard won't incorrectly block wrapping
- **`is_knapsack()` guard in constructor is NOT needed** — `CountConstrainedKnapsackSlice` is NOT a `CountQuotaSlice` inner slicer; no constructor guard against knapsack is warranted (it *is* the knapsack wrapper)
- **Sub-budget for Phase 2**: same pattern as `CountQuotaSlice` — `ContextBudget::new(residual, residual, 0, HashMap::new(), 0.0).expect("residual is non-negative")`; use `.max(0)` on the residual before passing
- **`QuotaPolicy` impl** — implement `quota_utilization` compatibility by implementing `QuotaPolicy` exactly like `CountQuotaSlice` (returns `QuotaConstraintMode::Count` constraints)
- **Three-location conformance sync** (D082) — TOML vectors must exist in `spec/conformance/required/slicing/`, `conformance/required/slicing/` (repo root), and `crates/cupel/conformance/required/slicing/`
- **`KnapsackSlice` stored by value, not boxed** — `KnapsackSlice` is `Copy + Clone + Debug`; no need for `Box<dyn Slicer>`; storing by value keeps the struct `Debug + Clone`
- **`CountConstrainedKnapsackSlice` is not `Copy`** (has `Vec<CountQuotaEntry>`) — implement `Debug` manually or derive it
- **Committed-item tracking uses raw pointer set** — `CountQuotaSlice` uses `HashSet<*const ScoredItem>` with pointer identity; safe to copy this pattern; the `unsafe` concern is absent because the pointers are derived from `sorted: &[ScoredItem]` which lives for the duration of the slice call

## Common Pitfalls

- **`is_knapsack() → true` in `CountConstrainedKnapsackSlice`** — do NOT override; the default `false` is correct; overriding to `true` would cause `CountQuotaSlice`'s constructor guard to reject it as an inner slicer, which is correct behavior but does NOT mean the type itself should advertise as a raw knapsack
- **Residual budget of zero** — if Phase 1 commits items whose total token cost equals or exceeds `target_tokens`, `residual_budget_tokens = 0`; the Phase 2 guard `if residual_budget_tokens > 0 && !remaining.is_empty()` must handle this as an early return to an empty vec (same pattern as `CountQuotaSlice`)
- **`ContextBudget::new` validation** — `total_tokens` must be `>= target_tokens`; always pass `(residual, residual, 0, ...)` not `(total, residual, ...)` for the Phase 2 sub-budget to avoid validation failures
- **Duplicate conformance files** — must copy to all three locations (D082); forgetting the repo-root `conformance/required/slicing/` location causes the pre-commit hook to fail
- **`count_constrained_knapsack` TOML key in conformance harness** — the TOML vector must have `test.slicer = "count_constrained_knapsack"` and the harness arm must match that exact string
- **Cap enforcement uses Phase 1 committed counts as starting `selected_count`** — Phase 3 starts from the count of items committed in Phase 1 (`selected_count` built during Phase 1), then increments while filtering Phase 2 output; starting from zero would incorrectly allow cap+1 items through
- **Score-descending sort on partition** — Phase 1 sorts each per-kind partition by score descending; the input `sorted: &[ScoredItem]` is already score-descending but the partition loop re-sorts explicitly in `CountQuotaSlice`; copy that explicit sort to be safe

## Open Risks

- **OOM guard interaction**: `KnapsackSlice` internally enforces the 50M-cell guard (`CupelError::TableTooLarge`). Phase 2 residual has fewer candidates (committed items removed), so cell count is strictly lower than an unconstrained knapsack at the full budget. Risk is low. However, the test `count_constrained_knapsack_residual_budget_oom_guard` should confirm the guard propagates through `Phase 2` correctly.
- **Shortfall reporting at pipeline level**: `CountQuotaSlice` builds shortfalls internally but they are NOT wired into `SelectionReport::count_requirement_shortfalls` via the current pipeline — the `DiagnosticTraceCollector::into_report()` always returns `Vec::new()` for `count_requirement_shortfalls`. The 5 conformance integration tests verify shortfall/cap behavior at the `slicer.slice()` level (counting candidates vs require_count), not at `SelectionReport` level. This is the same pattern as M006/S01. Shortfall reporting at pipeline level is a known gap (D086 equivalent for shortfalls), acceptable for v1.
- **`QuotaPolicy` impl**: Verify `quota_utilization` in `analytics.rs` handles `QuotaConstraintMode::Count` correctly for `CountConstrainedKnapsackSlice` — it already does since `CountQuotaSlice` exercises the same path.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust | (no relevant skill needed) | n/a |

## Sources

- `crates/cupel/src/slicer/count_quota.rs` — 783 lines; direct code reference for Phase 1, Phase 3, and struct layout
- `crates/cupel/src/slicer/knapsack.rs` — direct code reference for `KnapsackSlice` API and OOM guard
- `crates/cupel/src/pipeline/mod.rs` lines 385–431 — count cap map wiring; lines 731 — monotonicity guard
- `crates/cupel/tests/conformance/slicing.rs` — `run_count_quota_full_test` helper and conformance test structure
- `crates/cupel/conformance/required/slicing/count-quota-*.toml` — TOML format for count_quota vectors (5 examples)
- `.kata/DECISIONS.md` D174–D176 — algorithm choice, type re-use, trait method decisions
- `.kata/milestones/M009/M009-CONTEXT.md` — integration points, open questions resolution, scope
