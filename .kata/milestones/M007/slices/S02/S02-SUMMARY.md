---
id: S02
parent: M007
milestone: M007
provides:
  - crates/cupel/src/pipeline/mod.rs — private run_with_components helper, Policy struct, PolicyBuilder, Pipeline::dry_run_with_policy
  - crates/cupel/src/lib.rs — Policy and PolicyBuilder re-exported
  - crates/cupel/tests/dry_run_with_policy.rs — 5 passing integration tests
requires: []
affects:
  - S03
key_files:
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/dry_run_with_policy.rs
key_decisions:
  - run_with_components takes &dyn Scorer/Slicer/Placer (not Arc) — Arc deref coerces cleanly via policy.scorer.as_ref(); avoids lifetime friction
  - Policy fields are pub(crate) Arc<dyn Trait> (not Box) — allows shared ownership across future policy_sensitivity multi-run calls without cloning
  - PolicyBuilder::new() defaults deduplication=true (matching PipelineBuilder) and OverflowStrategy::Throw (OverflowStrategy::default())
  - KnapsackSlice::new(1) used in slicer_is_respected — with_default_bucket_size() (bucket_size=100) produces capacity=0 when budget target<100, breaking the test
  - '#[allow(clippy::too_many_arguments)] applied to run_with_components — 9-arg private helper; grouping would require a new struct with no semantic benefit
patterns_established:
  - run_with_components pattern — extract hot inner loop into private helper accepting &dyn Trait params; public entry points delegate with either self.* fields or injected policy fields
  - PolicyBuilder mirrors PipelineBuilder exactly, substituting Arc<dyn Trait> for Box<dyn Trait>; same error strings ensure uniform caller experience
observability_surfaces:
  - dry_run_with_policy returns Result<SelectionReport, CupelError> — SelectionReport.included/excluded/total_candidates are the primary inspection surface
  - CupelError variants propagate unchanged through run_with_components (no new error types)
  - cargo test --test dry_run_with_policy — direct check for all 5 behavioral contracts
drill_down_paths:
  - .kata/milestones/M007/slices/S02/tasks/T01-SUMMARY.md
  - .kata/milestones/M007/slices/S02/tasks/T02-SUMMARY.md
  - .kata/milestones/M007/slices/S02/tasks/T03-SUMMARY.md
duration: ~65m (T01: 15m, T02: 45m, T03: 5m)
verification_result: passed
completed_at: 2026-03-24
---

# S02: Rust Policy struct and dry_run_with_policy

**`Policy`, `PolicyBuilder`, and `Pipeline::dry_run_with_policy` are live in the Rust `cupel` crate — all 5 behavioral-contract integration tests pass, 164 tests total, clippy clean.**

## What Happened

**T01 — Red phase:** Created `crates/cupel/tests/dry_run_with_policy.rs` with 5 named `#[test]` functions covering the five behavioral contracts: `scorer_is_respected`, `slicer_is_respected`, `deduplication_false_allows_duplicates`, `deduplication_true_excludes_duplicates`, `overflow_strategy_is_respected`. The file compiled to exactly the expected not-found errors (missing `Policy`, `PolicyBuilder`, `dry_run_with_policy`) and nothing else.

**T02 — Implementation:** Extracted the entire 6-stage body of `run_traced` into a new private method `run_with_components<C: TraceCollector>` accepting injected `scorer: &dyn Scorer`, `slicer: &dyn Slicer`, `placer: &dyn Placer`, `deduplication: bool`, `overflow_strategy: OverflowStrategy`. `run_traced` now delegates to it with `self.*` fields; observable behavior is unchanged. A mid-refactor regression check (all 80 lib + 84 integration tests) confirmed zero regressions before adding new types.

`Policy` was added with `pub(crate)` Arc fields; `PolicyBuilder` mirrors `PipelineBuilder` with Arc instead of Box and identical error strings. `Pipeline::dry_run_with_policy` wires a `DiagnosticTraceCollector::new(TraceDetailLevel::Item)` to `run_with_components` with the policy's fields and returns `collector.into_report()`. `lib.rs` was updated to re-export `Policy` and `PolicyBuilder` alongside `Pipeline` and `PipelineBuilder`.

One test fix was needed: `slicer_is_respected` used `KnapsackSlice::with_default_bucket_size()` (bucket_size=100) against a budget target of 80, yielding capacity=0 and an empty knapsack result. Fixed to `KnapsackSlice::new(1)` for exact per-token capacity — the behavioral contract (Knapsack selects b+c, Greedy selects a) is preserved.

**T03 — Green phase:** All 5 tests passed on first run. No further changes required.

## Verification

```
cargo test --all-targets        → 164 passed, 0 failed (11 suites)
cargo clippy --all-targets -- -D warnings  → 0 warnings, 0 errors
cargo test --test dry_run_with_policy      → 5 passed (all named tests ok)
```

All 5 behavioral contracts confirmed:
- `scorer_is_respected` — PriorityScorer in policy picks C+B; ReflexiveScorer pipeline picks A+B under tight budget
- `slicer_is_respected` — KnapsackSlice(1) in policy selects different item set than GreedySlice pipeline at same budget
- `deduplication_false_allows_duplicates` — both identical-content items appear in `report.included`
- `deduplication_true_excludes_duplicates` — one item appears in `report.excluded` with `ExclusionReason::Deduplicated`
- `overflow_strategy_is_respected` — Throw returns `CupelError::Overflow`; Truncate proceeds without error

## Requirements Advanced

- R056 — S02 delivers the Rust `Policy`, `PolicyBuilder`, and `Pipeline::dry_run_with_policy` components specified for M007/S02; advances R056 toward full validation (pending S03)

## Requirements Validated

- none — R056 validation requires S03 (`policy_sensitivity` free function + spec chapter)

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

- **KnapsackSlice::new(1) vs with_default_bucket_size():** T01 used `with_default_bucket_size()` (bucket_size=100); with budget target=80, capacity=0 → Knapsack returns empty, defeating the test. Fixed in T02 to `KnapsackSlice::new(1)`. The behavioral contract is preserved — only the bucket_size parameter changed.
- **T03 collapsed into T02:** All 5 integration tests passed by the end of T02. T03 required no code changes.

## Known Limitations

- `Policy` cannot be constructed without all three of scorer, slicer, placer — no partial policy with fallback to pipeline components. This is by design (spec requires explicit policy components).
- `PolicyBuilder` error strings ("scorer is required", "slicer is required", "placer is required") are bare strings inside `CupelError::PipelineConfig`; no typed variant distinguishes policy-config errors from pipeline-config errors. Acceptable for now; S03 does not require a typed distinction.
- `policy_sensitivity` free function and `PolicySensitivityReport` types are not yet implemented — those are S03's scope.

## Follow-ups

- S03: `policy_sensitivity(items, budget, &[(label, &Policy)])` free function + `PolicySensitivityReport` + `PolicySensitivityDiffEntry` types + spec chapter at `spec/src/analytics/policy-sensitivity.md` + R056 validation

## Files Created/Modified

- `crates/cupel/src/pipeline/mod.rs` — added `run_with_components` private helper; `run_traced` delegates to it; added `Policy`, `PolicyBuilder`, `Pipeline::dry_run_with_policy`; added `use std::sync::Arc`
- `crates/cupel/src/lib.rs` — added `Policy, PolicyBuilder` to `pub use pipeline::{...}`
- `crates/cupel/tests/dry_run_with_policy.rs` — new file with 5 integration tests; `slicer_is_respected` fixed to use `KnapsackSlice::new(1)`

## Forward Intelligence

### What the next slice should know
- `Policy` fields are `pub(crate)` — S03's `policy_sensitivity` (inside the same crate or as a `pub fn` in `analytics.rs`) can access `policy.scorer.as_ref()`, `policy.slicer.as_ref()`, `policy.placer.as_ref()` directly via `Arc::clone` for multi-run shared ownership
- `run_with_components` is the correct hook for S03's `policy_sensitivity` — clone the Arc fields from each policy and pass `arc.as_ref()` per run; no new refactor needed
- `DiagnosticTraceCollector::new(TraceDetailLevel::Item)` is the right collector for producing `SelectionReport` — same pattern used by `dry_run` and `dry_run_with_policy`

### What's fragile
- `KnapsackSlice::new(bucket_size)` bucket_size semantics — if bucket_size ≥ budget target, capacity=0 and Knapsack returns empty. Tests must use `bucket_size=1` (or smaller than smallest token delta) to get meaningful results. `with_default_bucket_size()` (=100) is only safe when budget target >> 100.

### Authoritative diagnostics
- `cargo test --test dry_run_with_policy` — 5 behavioral contracts; individual test names identify which contract failed
- `cargo test --all-targets` — regression surface; 164 tests across 11 suites

### What assumptions changed
- Original plan assumed `with_default_bucket_size()` would work in `slicer_is_respected` — it does not when budget target < bucket_size (capacity=0). `KnapsackSlice::new(1)` is the correct form for tight-budget tests.
