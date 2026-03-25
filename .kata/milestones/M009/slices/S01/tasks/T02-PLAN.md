---
estimated_steps: 7
estimated_files: 4
---

# T02: Implement CountConstrainedKnapsackSlice and wire into slicer module

**Slice:** S01 — CountConstrainedKnapsackSlice — Rust implementation
**Milestone:** M009

## Description

Create `crates/cupel/src/slicer/count_constrained_knapsack.rs` implementing the full 3-phase algorithm, then wire the new type into `slicer/mod.rs`, `lib.rs`, and the conformance harness `conformance.rs`. This task makes T01's 5 failing tests pass.

The implementation closely mirrors `CountQuotaSlice` for Phases 1 and 3. The critical difference is Phase 2: instead of calling `self.inner.slice()` on a `Box<dyn Slicer>`, it calls `self.knapsack.slice()` on a stored-by-value `KnapsackSlice` (which is `Copy + Clone`).

Key design decisions to honor:
- **D174**: Pre-processing path (5A) — no full constrained-DP
- **D175**: Re-use `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall` — no new types
- **D176**: `is_count_quota() → true`; `is_knapsack()` default stays `false`
- **No constructor guard** against `is_knapsack()`: `CountConstrainedKnapsackSlice` IS the knapsack wrapper; the guard in `CountQuotaSlice::new` explicitly does not apply here
- **`KnapsackSlice` stored by value** (not `Box<dyn Slicer>`): it's `Copy`; `CountConstrainedKnapsackSlice` itself cannot be `Copy` (has `Vec<CountQuotaEntry>`) but can be `Debug + Clone`
- **`committed_ids` pointer set**: safe to copy from `CountQuotaSlice`; pointers derive from `sorted` which lives for the duration of the call
- **Sub-budget for Phase 2**: `ContextBudget::new(residual, residual, 0, HashMap::new(), 0.0).expect(...)` with `.max(0)` on residual
- **Phase 3 starts with Phase 1 counts**: `selected_count` built during Phase 1 is the starting state for cap enforcement in Phase 3 — starting from zero would incorrectly allow cap+1 items

## Steps

1. Create `crates/cupel/src/slicer/count_constrained_knapsack.rs`: module doc comment referencing D174; imports from `count_quota.rs` (`CountQuotaEntry`, `ScarcityBehavior`), `crate::slicer::{KnapsackSlice, QuotaConstraint, QuotaConstraintMode, QuotaPolicy, Slicer}`, etc.; define `pub struct CountConstrainedKnapsackSlice { entries: Vec<CountQuotaEntry>, knapsack: KnapsackSlice, scarcity: ScarcityBehavior }` — derive `Clone`; implement manual `Debug` matching `CountQuotaSlice`'s pattern (or derive if all fields implement `Debug`)

2. Implement `CountConstrainedKnapsackSlice::new(entries: Vec<CountQuotaEntry>, knapsack: KnapsackSlice, scarcity: ScarcityBehavior) -> Result<Self, CupelError>`: no `is_knapsack()` guard; just `Ok(Self { entries, knapsack, scarcity })`; add `entries()` and `scarcity()` accessor methods; add `build_policy_maps()` private method identical to `CountQuotaSlice`

3. Implement `Slicer for CountConstrainedKnapsackSlice::slice()`: copy Phase 1 block verbatim from `CountQuotaSlice::slice()` (lines ~274–360) — partitions, score-descending sort, committed loop, `selected_count`, `pre_alloc_tokens`, `committed_ids`, `shortfalls`; copy Phase 3 cap enforcement verbatim; Phase 2: `self.knapsack.slice(&remaining, &sub_budget)?` (not `self.inner`)

4. Override `is_count_quota()` to return `true`; inherit `is_knapsack()` default (false); override `count_cap_map()` to return per-kind caps from entries (copy from `CountQuotaSlice`)

5. Implement `QuotaPolicy for CountConstrainedKnapsackSlice`: `quota_constraints()` returns `QuotaConstraintMode::Count` constraints — copy the impl from `CountQuotaSlice::quota_constraints()` exactly

6. Wire into module: add `pub mod count_constrained_knapsack;` and `pub use count_constrained_knapsack::CountConstrainedKnapsackSlice;` in `slicer/mod.rs`; add `CountConstrainedKnapsackSlice` to the `pub use slicer::{...}` block in `lib.rs` next to `CountQuotaSlice`

7. Add `"count_constrained_knapsack"` arm to `build_slicer_by_type` in `crates/cupel/tests/conformance.rs`: parse `bucket_size` (default 100) from config; parse `scarcity_behavior` (default "degrade"); parse `entries` array (identical shape to `count_quota` arm); construct `KnapsackSlice::new(bucket_size).unwrap()` then `CountConstrainedKnapsackSlice::new(entries, knapsack, scarcity).unwrap()`; wrap in `Box::new(...)`; add `CountConstrainedKnapsackSlice` to the `use cupel::{...}` import at the top of `conformance.rs`

## Must-Haves

- [ ] `crates/cupel/src/slicer/count_constrained_knapsack.rs` exists with full implementation
- [ ] `CountConstrainedKnapsackSlice::new()` accepts `Vec<CountQuotaEntry>`, `KnapsackSlice`, `ScarcityBehavior`; returns `Result<Self, CupelError>`
- [ ] `Slicer::slice()` implements 3-phase algorithm; Phase 2 calls `self.knapsack.slice()`
- [ ] `is_count_quota()` returns `true`
- [ ] `count_cap_map()` returns per-kind caps
- [ ] `QuotaPolicy` implemented
- [ ] `CountConstrainedKnapsackSlice` importable from `cupel` crate root
- [ ] `"count_constrained_knapsack"` arm exists in `build_slicer_by_type`
- [ ] All 5 tests in `count_constrained_knapsack.rs` pass
- [ ] `cargo test --all-targets` exits 0
- [ ] `cargo clippy --all-targets -- -D warnings` exits 0

## Verification

- `rtk cargo test --all-targets` — all tests pass including the 5 new ones
- `cargo clippy --all-targets -- -D warnings` — zero warnings
- `grep "CountConstrainedKnapsackSlice" crates/cupel/src/lib.rs` — confirms re-export
- `cargo test count_constrained_knapsack 2>&1 | grep -E "test .+ ok|FAILED"` — all 5 ok

## Observability Impact

- Signals added/changed: `CupelError::SlicerConfig` path not triggered by construction (no guard), but Phase 2 propagates `CupelError::TableTooLarge` from KnapsackSlice; `CupelError::SlicerConfig` from scarcity throw is propagated
- How a future agent inspects this: `cargo test count_constrained_knapsack -- --nocapture` for detailed output; test names match TOML vector names
- Failure state exposed: If Phase 2 OOM guard fires, `CupelError::TableTooLarge { candidates, capacity, cells }` is returned with all three fields needed to diagnose the issue

## Inputs

- `crates/cupel/src/slicer/count_quota.rs` — Phase 1 and Phase 3 code blocks to copy; struct layout reference; `QuotaPolicy` impl to mirror
- `crates/cupel/src/slicer/knapsack.rs` — `KnapsackSlice::new(bucket_size)` API; confirms `Copy + Clone + Debug`
- `crates/cupel/tests/conformance.rs` lines 354–398 — `"count_quota"` arm to mirror for the new `"count_constrained_knapsack"` arm
- T01 TOML vectors — already written; the new conformance arm must correctly parse them

## Expected Output

- `crates/cupel/src/slicer/count_constrained_knapsack.rs` — ~200 lines; full 3-phase Slicer impl + QuotaPolicy impl
- Updated `crates/cupel/src/slicer/mod.rs` — mod declaration + pub use
- Updated `crates/cupel/src/lib.rs` — CountConstrainedKnapsackSlice in pub use block
- Updated `crates/cupel/tests/conformance.rs` — new arm + import
- `cargo test --all-targets` green across all crates
