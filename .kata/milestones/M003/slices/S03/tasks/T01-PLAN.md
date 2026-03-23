---
estimated_steps: 8
estimated_files: 5
---

# T01: Implement CountQuotaSlice in Rust

**Slice:** S03 — CountQuotaSlice — Rust + .NET Implementation
**Milestone:** M003

## Description

Implements the Rust side of `CountQuotaSlice` in full: data model types (`CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall`), the `is_knapsack` trait extension on `Slicer`, two new `ExclusionReason` variants, a new `count_requirement_shortfalls` field on `SelectionReport` (with serde compatibility), and the `CountQuotaSlice` struct implementing `Slicer` via the two-phase COUNT-DISTRIBUTE-BUDGET algorithm from `.planning/design/count-quota-design.md`.

The pseudocode is fully locked. No design work is needed. Focus is entirely on correct translation of the algorithm to Rust and safe backward-compatible extension of the existing diagnostic types.

## Steps

1. **Add `fn is_knapsack(&self) -> bool { false }` default method to `Slicer` trait** in `crates/cupel/src/slicer/mod.rs`; override it in `KnapsackSlice` with `fn is_knapsack(&self) -> bool { true }` in `knapsack.rs`. This enables the construction-time guard in `CountQuotaSlice::new` without any `Any`/downcast machinery.

2. **Add `CountCapExceeded` and `CountRequireCandidatesExhausted` to `ExclusionReason`** in `crates/cupel/src/diagnostics/mod.rs`. `CountCapExceeded { kind: String, cap: usize, count: usize }` — emitted when inner slicer tries to select beyond the configured cap. `CountRequireCandidatesExhausted { kind: String }` — emitted (reserved) when Phase 1 exhausts all candidates for a kind before satisfying require_count (currently informational; the shortfall is the primary mechanism). Both are struct variants compatible with `#[serde(tag = "reason")]`.

3. **Add `count_requirement_shortfalls` field to `SelectionReport`** in `mod.rs`: add `pub count_requirement_shortfalls: Vec<CountRequirementShortfall>` with `#[cfg_attr(feature = "serde", serde(default))]` to the struct; add the same field with `#[serde(default)]` to `RawSelectionReport` inside the custom `Deserialize` impl; propagate the field in the `Ok(SelectionReport { ... })` construction.

4. **Create `crates/cupel/src/slicer/count_quota.rs`** with: `CountRequirementShortfall { kind: String, required_count: usize, satisfied_count: usize }` (Debug, Clone); `CountQuotaEntry { kind: ContextKind, require_count: usize, cap_count: usize }` with `CountQuotaEntry::new` validating `require_count <= cap_count` (error if not) and `cap_count == 0 && require_count > 0` (error); `ScarcityBehavior { Degrade, Throw }` enum (Debug, Clone, Copy, Default=Degrade); `CountQuotaSlice { entries: Vec<CountQuotaEntry>, inner: Box<dyn Slicer>, scarcity: ScarcityBehavior }` with `CountQuotaSlice::new` that checks `inner.is_knapsack()` and returns `CupelError::SlicerConfig` with the exact message from the design doc if true.

5. **Implement `Slicer::slice` for `CountQuotaSlice`** following the pseudocode exactly:
   - Phase 1: for each entry with require_count > 0, collect items of matching kind sorted by score desc; commit top-N (or all if fewer); accumulate pre_allocated_tokens; remove committed items from remaining pool; record shortfalls if satisfied < required (Degrade: record in shortfalls vec; Throw: return `CupelError::SlicerConfig`).
   - Phase 2: build residual budget (`target_tokens - pre_allocated_tokens`); call `self.inner.slice(&remaining, &residual_budget)`; apply count-cap enforcement: for each item the inner slicer selected, if `selected_count[kind] >= cap_count[kind]`, it would have been excluded — BUT since `inner.slice` already ran, the cap enforcement must happen differently. **Implementation note**: cap enforcement must wrap the inner slicer call with a post-filter or pass the cap constraint via a capping wrapper. Simplest correct approach: after inner returns its result, iterate the inner result and re-exclude items that exceed the cap, recording them as `CountCapExceeded` exclusions. However, the slicer trait signature returns `Vec<ContextItem>` with no trace collector — cap-exceeded items cannot be emitted as `ExclusionReason` via the normal trace path. **Resolution**: `CountQuotaSlice::slice` maintains its own `excluded_cap_items: Vec<(ContextItem, ExclusionReason)>` — but `Slicer::slice` returns only `Vec<ContextItem>`. The cap exclusion reasons are therefore not observable at the slicer level; they are an S03 internal concern. The conformance test for cap exclusion verifies via `dry_run` / `SelectionReport` at the pipeline level, OR the conformance test uses a dedicated unit test that wraps the slicer in a `DiagnosticTraceCollector` pipeline. For v1, cap enforcement at the slicer level simply filters out items exceeding the cap — the exclusion reason is a pipeline-level concern. The unit tests verify selected item sets; the ConformanceVectors in T02 will verify via direct unit tests that check excluded counts, not via the slicing conformance harness.
   - Return: Phase 1 committed items + Phase 2 inner result (cap-capped).

6. **Re-export from `slicer/mod.rs`**: add `mod count_quota;` and `pub use count_quota::{CountQuotaEntry, CountQuotaSlice, CountRequirementShortfall, ScarcityBehavior};`

7. **Re-export from `lib.rs`**: extend `pub use slicer::` to include the 4 new types.

8. **Run `cargo test --all-targets`** and fix any compile errors or test failures. Run `cargo doc --no-deps` and ensure 0 new warnings.

## Must-Haves

- [ ] `is_knapsack()` default method on `Slicer` trait returns `false`; `KnapsackSlice` overrides to `true`
- [ ] `ExclusionReason::CountCapExceeded { kind: String, cap: usize, count: usize }` variant exists and serializes correctly under `#[serde(tag = "reason")]`
- [ ] `ExclusionReason::CountRequireCandidatesExhausted { kind: String }` variant exists
- [ ] `SelectionReport.count_requirement_shortfalls: Vec<CountRequirementShortfall>` field present; `RawSelectionReport` has it with `#[serde(default)]` so old payloads deserialize without error
- [ ] `CountQuotaSlice::new` returns `Err(CupelError::SlicerConfig(...))` when inner is KnapsackSlice
- [ ] `CountQuotaSlice::new` returns `Err` when any entry has `require_count > cap_count` or `cap_count == 0 && require_count > 0`
- [ ] Phase 1 selects top-N items per kind by score descending; records shortfalls in `count_requirement_shortfalls` under Degrade
- [ ] Phase 2 delegates residual candidates to inner slicer with `target_tokens - pre_allocated_tokens` as target
- [ ] Cap enforcement: items of a kind exceeding `cap_count` are excluded from final result
- [ ] All new types exported from `cupel` crate root
- [ ] `cargo test --all-targets` exits 0

## Verification

- `cargo test --all-targets 2>&1 | tail -3` — must show all tests passed
- `cargo doc --no-deps 2>&1 | grep -c warning` — must be 0 new warnings
- `grep -c "is_knapsack" crates/cupel/src/slicer/mod.rs` — must be ≥ 1
- `grep -c "CountCapExceeded\|CountRequireCandidatesExhausted" crates/cupel/src/diagnostics/mod.rs` — must be ≥ 2
- `grep "count_requirement_shortfalls" crates/cupel/src/diagnostics/mod.rs` — must show field on SelectionReport AND in RawSelectionReport
- `cargo test -p cupel --features serde -- serde` — serde round-trip tests still pass (confirms no regression to SelectionReport deserialization)

## Observability Impact

- Signals added/changed: `CountCapExceeded` and `CountRequireCandidatesExhausted` variants on `ExclusionReason` (new signals emitted during slicing); `count_requirement_shortfalls` on `SelectionReport` (new field surfacing unmet minimums)
- How a future agent inspects this: `cargo test -- count_quota --nocapture` runs relevant tests; `report.count_requirement_shortfalls` inspection shows which kinds fell short; `report.excluded` filtered by `CountCapExceeded` reason shows cap-exceeded items
- Failure state exposed: construction-time `CupelError::SlicerConfig` names the constraint violation; `count_requirement_shortfalls` entries name the kind, required count, and actual count

## Inputs

- `crates/cupel/src/slicer/quota.rs` — DISTRIBUTE-BUDGET reference implementation; Phase 2 of CountQuotaSlice replicates the proportional distribution logic on residual candidates
- `crates/cupel/src/slicer/greedy.rs` — score-descending iteration pattern for Phase 1
- `crates/cupel/src/diagnostics/mod.rs` — `ExclusionReason` enum structure; `SelectionReport` + `RawSelectionReport` serde pattern
- `.planning/design/count-quota-design.md` — locked pseudocode, all design decisions DI-1 through DI-6, D040, D046, D052–D057
- S01-SUMMARY.md Forward Intelligence — PublicAPI.Unshipped.txt RS0016 pattern; Rust construction error pattern

## Expected Output

- `crates/cupel/src/slicer/count_quota.rs` — new; ~200 lines implementing CountQuotaEntry, ScarcityBehavior, CountRequirementShortfall, CountQuotaSlice + Slicer impl
- `crates/cupel/src/slicer/mod.rs` — `is_knapsack` default method added; `mod count_quota; pub use count_quota::...` added
- `crates/cupel/src/slicer/knapsack.rs` — `is_knapsack()` override returning `true`
- `crates/cupel/src/diagnostics/mod.rs` — 2 new `ExclusionReason` variants; `count_requirement_shortfalls` field on `SelectionReport` and `RawSelectionReport`
- `crates/cupel/src/lib.rs` — 4 new types re-exported
- `cargo test --all-targets` exits 0
