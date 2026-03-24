---
id: T02
parent: S02
milestone: M007
provides:
  - crates/cupel/src/pipeline/mod.rs — private run_with_components helper, Policy struct, PolicyBuilder, Pipeline::dry_run_with_policy
  - crates/cupel/src/lib.rs — Policy and PolicyBuilder re-exported
key_files:
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/dry_run_with_policy.rs
key_decisions:
  - run_with_components takes &dyn Scorer/Slicer/Placer (not Arc) — Arc deref coerces cleanly via policy.scorer.as_ref(); avoids lifetime friction
  - Policy fields are pub(crate) Arc<dyn Trait> (not Box) per D149 — allows shared ownership across multiple policies without cloning
  - PolicyBuilder::new() has default deduplication=true matching PipelineBuilder; OverflowStrategy::default() (=Throw)
  - KnapsackSlice::with_default_bucket_size() (bucket_size=100) breaks when target<100 — test fixed to use KnapsackSlice::new(1) for exact-token capacity
  - #[allow(clippy::too_many_arguments)] applied to run_with_components — 9-arg private helper, no better grouping without new types
patterns_established:
  - run_with_components pattern: extract hot inner loop into private helper accepting &dyn Trait params; public methods delegate to it with self.* fields or injected policy fields
  - PolicyBuilder mirrors PipelineBuilder exactly but with Arc<dyn Trait> instead of Box<dyn Trait>
observability_surfaces:
  - dry_run_with_policy returns Result<SelectionReport, CupelError> — same error surface as dry_run
  - SelectionReport fields (included, excluded, total_candidates) are the primary inspection surface
  - CupelError variants propagate unchanged through run_with_components (no new error types added)
duration: ~45m
verification_result: passed
completed_at: 2026-03-24
blocker_discovered: false
---

# T02: Extract run_with_components helper and implement Policy + PolicyBuilder

**Extracted `run_traced` body into private `run_with_components`, then added `Policy`, `PolicyBuilder`, and `Pipeline::dry_run_with_policy` — all 5 integration tests pass, zero regressions, clippy clean.**

## What Happened

**Phase A — Refactor:** Extracted the entire 6-stage body of `run_traced` into a new private method `run_with_components<C: TraceCollector>` that accepts injected `scorer: &dyn Scorer`, `slicer: &dyn Slicer`, `placer: &dyn Placer`, `deduplication: bool`, and `overflow_strategy: OverflowStrategy` parameters. Updated `run_traced` to delegate to this helper passing `self.scorer.as_ref()` etc. Regression check confirmed zero failures across all 80 lib tests + 84 integration tests.

**Phase B — New API:** Added `Policy` struct with `pub(crate)` `Arc<dyn Scorer/Slicer/Placer>` fields plus deduplication and overflow_strategy. Added `PolicyBuilder` mirroring `PipelineBuilder` (Arc instead of Box, same error strings). Added `Pipeline::dry_run_with_policy` that wires a `DiagnosticTraceCollector` to `run_with_components` with the policy's fields.

**Test fix:** The `slicer_is_respected` test used `KnapsackSlice::with_default_bucket_size()` (bucket_size=100) with a budget target of 80 — capacity=0, so Knapsack returned empty. Fixed to use `KnapsackSlice::new(1)` (exact per-token capacity) which correctly demonstrates the GreedySlice vs KnapsackSlice behavioral difference.

**lib.rs:** Extended `pub use pipeline::{Pipeline, PipelineBuilder}` to include `Policy, PolicyBuilder`.

## Verification

```
cargo test --all-targets   → 164 passed, 0 failed (all 11 test suites pass)
cargo clippy --all-targets -- -D warnings   → 0 warnings, 0 errors
```

All 5 integration tests in `dry_run_with_policy.rs` pass:
- `scorer_is_respected` ✓
- `slicer_is_respected` ✓
- `deduplication_false_allows_duplicates` ✓
- `deduplication_true_excludes_duplicates` ✓
- `overflow_strategy_is_respected` ✓

## Diagnostics

- `cargo test --test dry_run_with_policy` runs the 5 behavioral contract tests
- `dry_run_with_policy` returns `Result<SelectionReport, CupelError>` — SelectionReport.included/excluded/total_candidates are the primary inspection surface
- Errors propagate as CupelError variants (PipelineConfig for builder missing fields; Overflow for Throw strategy; SlicerConfig, PinnedExceedsBudget from inner stages)

## Deviations

- **KnapsackSlice::new(1) vs with_default_bucket_size():** T01's test used `with_default_bucket_size()` (bucket_size=100) but budget target=80 means capacity=0 → Knapsack returns empty. Fixed test to use `KnapsackSlice::new(1)` for exact-token capacity. The behavioral contract being tested (Knapsack selects b+c vs Greedy selects a) is preserved — only the bucket_size parameter changed.
- **T03 collapsed into T02:** All 5 integration tests were passing by end of T02 — there was nothing left for T03 to do. T03 can be marked done trivially.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/pipeline/mod.rs` — added `run_with_components` private helper; `run_traced` now delegates to it; added `Policy`, `PolicyBuilder`, `Pipeline::dry_run_with_policy`; added `use std::sync::Arc`
- `crates/cupel/src/lib.rs` — added `Policy, PolicyBuilder` to `pub use pipeline::{...}`
- `crates/cupel/tests/dry_run_with_policy.rs` — fixed `slicer_is_respected` to use `KnapsackSlice::new(1)` instead of `with_default_bucket_size()`; removed unused `Policy` import
