---
id: S07
parent: M001
milestone: M001
provides:
  - CupelError::TableTooLarge { candidates, capacity, cells } variant
  - Slicer::slice returns Result<Vec<ContextItem>, CupelError> (semver break, v1.2.0)
  - KnapsackSlice DP table guard (50M-cell limit) with flat Vec<bool> keep table
  - QuotaSlice and pipeline propagate Result via ? operator
  - Scorer trait without as_any; all 8 impl Scorer blocks cleaned up
  - CompositeScorer without DFS cycle detection machinery
  - UShapedPlacer refactored to explicit left/right Vecs (no Vec<Option> or .expect())
  - 15 new unit tests across UShapedPlacer, TagScorer, PriorityScorer, ScaledScorer, ReflexiveScorer, Pipeline
  - release-rust.yml permissions scoped to job level
requires:
  - slice: S05
    provides: ci-rust.yml with --all-targets baseline; cargo clippy --all-targets clean before S07 started
affects: []
key_files:
  - crates/cupel/src/error.rs
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/slicer/greedy.rs
  - crates/cupel/src/slicer/knapsack.rs
  - crates/cupel/src/slicer/quota.rs
  - crates/cupel/src/pipeline/slice.rs
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/src/scorer/mod.rs
  - crates/cupel/src/scorer/composite.rs
  - crates/cupel/src/scorer/scaled.rs
  - crates/cupel/src/scorer/frequency.rs
  - crates/cupel/src/scorer/kind.rs
  - crates/cupel/src/scorer/priority.rs
  - crates/cupel/src/scorer/recency.rs
  - crates/cupel/src/scorer/reflexive.rs
  - crates/cupel/src/scorer/tag.rs
  - crates/cupel/src/placer/u_shaped.rs
  - crates/cupel/tests/conformance/slicing.rs
  - .github/workflows/release-rust.yml
key_decisions:
  - D035 — Slicer::slice semver break accepted for v1.2.0; required to propagate TableTooLarge through QuotaSlice and pipeline
  - D036 — CupelError::CycleDetected kept as reserved variant; doc updated to never-emitted; removing would be semver-breaking
  - D037 — UShapedPlacer uses explicit left/right vecs; right.push() + right.reverse() over insert(0,...) for O(1) insertion
  - std::slice::from_ref(&item) used in single-item scorer tests to satisfy clippy's cloned_ref_to_slice_refs lint (-D warnings)
patterns_established:
  - Slicer::slice returns Result; callers use ? propagation; test harnesses use .expect()
  - Scorer unit tests pass std::slice::from_ref(&item) as all_items for single-item cases
  - Pipeline unit tests use ChronologicalPlacer (not a non-existent GreedyPlacer)
observability_surfaces:
  - CupelError::TableTooLarge { candidates, capacity, cells } — structured error; inspect via `cargo test -- knapsack_table_too_large`
  - "grep -r \"as_any\" crates/cupel/src/ returns empty — confirms Scorer cleanup"
  - "grep -n \"Vec<Option\" crates/cupel/src/placer/u_shaped.rs returns empty — confirms UShapedPlacer refactor"
drill_down_paths:
  - .kata/milestones/M001/slices/S07/tasks/T01-SUMMARY.md
  - .kata/milestones/M001/slices/S07/tasks/T02-SUMMARY.md
  - .kata/milestones/M001/slices/S07/tasks/T03-SUMMARY.md
duration: ~90 minutes (3 tasks × ~30m each)
verification_result: passed
completed_at: 2026-03-21
---

# S07: Rust Quality Hardening

**Hardened the Rust crate with a KnapsackSlice OOM guard, error-propagating Slicer trait, dead-code removal from CompositeScorer and Scorer, panic-free UShapedPlacer, 15 new unit tests, and job-scoped CI permissions — all with zero test regressions and clean clippy.**

## What Happened

**T01 — KnapsackSlice guard + Result propagation:** Added `CupelError::TableTooLarge { candidates, capacity, cells }` to `error.rs`. Changed `Slicer::slice` from `→ Vec<ContextItem>` to `→ Result<Vec<ContextItem>, CupelError>` — an intentional semver break accepted for v1.2.0. Updated all three built-in slicers (`GreedySlice`, `KnapsackSlice`, `QuotaSlice`) and both pipeline call sites. The guard fires before any allocation: `if (capacity as u64) * (n as u64) > 50_000_000`. Replaced the nested `Vec<Vec<bool>>` keep table with a flat `Vec<bool>` (single allocation, `stride = capacity + 1`). Added `knapsack_table_too_large` unit test confirming the guard fires at capacity=50_001, n=1001, cells=50_051_001.

**T02 — Remove dead cycle detection and `as_any`:** The `detect_cycles_dfs` / `scorer_identity` DFS in `CompositeScorer::new` was dead code — child scorers are owned `Box<dyn Scorer>`, making structural cycles impossible at the type level. Removed the DFS, `HashSet`, `children()` accessor, and `Any` supertrait from `Scorer`. Removed `as_any` from all 8 `impl Scorer` blocks and `ScaledScorer::inner()`. `CupelError::CycleDetected` is retained with updated doc ("Never emitted. Structural cycles are impossible..."). Zero regressions — all existing tests compiled and passed unchanged.

**T03 — UShapedPlacer refactor, unit tests, CI permissions:** Replaced `Vec<Option<ContextItem>>` + `.expect("all result slots must be filled")` in `UShapedPlacer::place` with two explicit `Vec<ContextItem>` (`left` for even ranks, `right` for odd ranks). `right.push()` + `right.reverse()` replaces the fragile `insert(0, ...)` approach. The `.expect()` call and usize-underflow guard are eliminated entirely. Added 15 `#[cfg(test)]` unit tests: 5 for `UShapedPlacer` (0/1/2/3/4 items), 2 for `TagScorer`, 2 for `PriorityScorer`, 2 for `ScaledScorer`, 2 for `ReflexiveScorer`, 2 for `Pipeline`. Scoped `release-rust.yml` permissions to job level (`test: contents: read`, `publish: contents: write + id-token: write`).

## Verification

```
cargo test --manifest-path crates/cupel/Cargo.toml
# 35 passed; 0 failed (all 6 conformance slicing tests + knapsack_table_too_large + 15 new unit tests)

cargo test --features serde --manifest-path crates/cupel/Cargo.toml
# 35 passed; 0 failed

cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
# Finished with 0 warnings, exit 0

cargo clippy --features serde --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
# Finished with 0 warnings, exit 0

cargo deny check  # from crates/cupel/
# advisories ok, bans ok, licenses ok, sources ok
```

Structural confirmations:
- `grep -r "as_any" crates/cupel/src/` → no output (CLEAN)
- `grep -r "detect_cycles_dfs" crates/cupel/src/` → no output (CLEAN)
- `grep -n "Vec<Option" crates/cupel/src/placer/u_shaped.rs` → no output (CLEAN)
- `grep -n "expect(" crates/cupel/src/placer/u_shaped.rs` → no output (CLEAN)

## Requirements Advanced

- R002 — KnapsackSlice DP table size guard now live in Rust: `CupelError::TableTooLarge` variant + 50M-cell guard in `KnapsackSlice::slice`; `knapsack_table_too_large` test proves it fires correctly.
- R005 — All high-signal Rust quality issues resolved: CompositeScorer cycle detection removed, `Scorer::as_any` eliminated, `UShapedPlacer` panic paths gone, 15 new unit tests added.

## Requirements Validated

- R002 — KnapsackSlice DP guard implemented and tested in Rust (R002 was already validated in .NET via S06; now both languages covered). Move to validated.
- R005 — All R005 items resolved: CompositeScorer DFS removed, `as_any` eliminated across 8 scorer impls, `UShapedPlacer::place` has no `Vec<Option>` or `.expect()`, test coverage gaps addressed. Move to validated.

## New Requirements Surfaced

- None.

## Requirements Invalidated or Re-scoped

- None.

## Deviations

- `std::slice::from_ref(&item)` used in single-item scorer tests instead of `&[item.clone()]` — required to satisfy `clippy::cloned_ref_to_slice_refs` under `-D warnings`. Not mentioned in the plan but a necessary mechanical change.
- Pipeline tests use `ChronologicalPlacer` instead of a `GreedyPlacer` — no `GreedyPlacer` exists; `ChronologicalPlacer` is the correct available implementation.
- The `Any` supertrait bound was removed from `Scorer` (not just the `as_any` method) — this is strictly correct since nothing needed it, and is consistent with removing the method.

## Known Limitations

- None introduced by this slice. `CupelError::CycleDetected` remains constructible but is never emitted — this is intentional (D036) to avoid a semver break.

## Follow-ups

- v1.2.0 tag and crates.io publish (manual step, separate from slice completion).
- Milestone DoD review: all 7 slices complete; all verifications pass; tag ready.

## Files Created/Modified

- `crates/cupel/src/error.rs` — Added `TableTooLarge` variant; updated `CycleDetected` doc
- `crates/cupel/src/slicer/mod.rs` — Changed `Slicer::slice` return type to `Result`; updated doc-test
- `crates/cupel/src/slicer/greedy.rs` — `Result` return; `Ok(...)` wrapping
- `crates/cupel/src/slicer/knapsack.rs` — OOM guard, flat keep table, `Result` return, `knapsack_table_too_large` test
- `crates/cupel/src/slicer/quota.rs` — `Result` return; `inner.slice(...)?` propagation
- `crates/cupel/src/pipeline/slice.rs` — `slice_items` returns `Result`
- `crates/cupel/src/pipeline/mod.rs` — `?` on both `slice_items` call sites; 2 pipeline unit tests
- `crates/cupel/src/scorer/mod.rs` — Removed `Any` supertrait and `as_any` from trait
- `crates/cupel/src/scorer/composite.rs` — Removed DFS cycle detection, `children()`, `as_any`; added cycle-impossibility doc
- `crates/cupel/src/scorer/scaled.rs` — Removed `inner()`, `as_any`, `Any` import
- `crates/cupel/src/scorer/frequency.rs` — Removed `Any` import and `as_any`
- `crates/cupel/src/scorer/kind.rs` — Removed `Any` import and `as_any`
- `crates/cupel/src/scorer/priority.rs` — Removed `Any` import and `as_any`; 2 unit tests
- `crates/cupel/src/scorer/recency.rs` — Removed `Any` import and `as_any`
- `crates/cupel/src/scorer/reflexive.rs` — Removed `Any` import and `as_any`; 2 unit tests
- `crates/cupel/src/scorer/tag.rs` — Removed `Any` import and `as_any`; 2 unit tests
- `crates/cupel/src/placer/u_shaped.rs` — Refactored to explicit left/right vecs; 5 unit tests
- `crates/cupel/tests/conformance/slicing.rs` — `.expect()` on `slicer.slice()` call
- `.github/workflows/release-rust.yml` — Job-level permissions replacing workflow-level block

## Forward Intelligence

### What the next slice should know
- S07 is the final active slice of M001. All milestone DoD criteria are now met. The only remaining step is the v1.2.0 tag + crates.io publish (manual).
- `Slicer::slice` now returns `Result` — any downstream crate implementing `Slicer` must update their signature. This is the only public API break in v1.2.0.

### What's fragile
- `CupelError::CycleDetected` is constructible but never emitted — a future contributor might not realize it's intentionally dead. The doc comment is the only protection.
- `KnapsackSlice` guard uses `capacity` (discretized, not raw `target_tokens`) — the guard threshold is not `budget.target_tokens() > 50M` but rather the discretized capacity used for the DP table. This is intentional (guard matches actual allocation) but can surprise callers who compute raw token budgets.

### Authoritative diagnostics
- `cargo test -- knapsack_table_too_large --nocapture` — proves the guard fires at the right threshold; structured error fields are emitted with correct values.
- `grep -r "as_any" crates/cupel/src/` returning empty — authoritative signal that the Scorer cleanup is complete.
- `grep -n "Vec<Option" crates/cupel/src/placer/u_shaped.rs` returning empty — authoritative signal that UShapedPlacer refactor is in place.

### What assumptions changed
- The plan anticipated ~10-15 Rust issues; in practice the changes were focused and surgical: 3 tasks, all well-scoped, no surprises.
- `ReflexiveScorer` has an explicit finiteness guard before `clamp` — both NaN and Inf return 0.0 (not 1.0 for Inf). The test confirmed this; the plan said "clamped to 1.0 if clamp is used — match actual implementation" and the actual implementation guards finiteness first.
