# S02: Rust Policy struct and dry_run_with_policy

**Goal:** Introduce `Policy`, `PolicyBuilder`, and `Pipeline::dry_run_with_policy` to the Rust `cupel` crate so callers can build a policy from trait objects and run fork diagnostics without constructing a full `Pipeline`.
**Demo:** A caller builds a `Policy` via `PolicyBuilder`, calls `pipeline.dry_run_with_policy(&items, &budget, &policy)`, and receives a `SelectionReport` driven by the policy's scorer/slicer/placer — verified by integration tests covering scorer respected, slicer respected, deduplication flag, and overflow_strategy.

## Must-Haves

- `Policy` struct with `Arc<dyn Scorer>`, `Arc<dyn Slicer>`, `Arc<dyn Placer>`, `deduplication: bool`, `overflow_strategy: OverflowStrategy`
- `PolicyBuilder` fluent builder mirroring `PipelineBuilder`; `build()` returns `Result<Policy, CupelError>` using same error strings
- `Pipeline::dry_run_with_policy(&self, items, budget, policy)` returning `Result<SelectionReport, CupelError>`
- Private `run_with_components` helper extracted from `run_traced` — both delegate to it; zero change in observable `run_traced` behavior
- `Policy` and `PolicyBuilder` exported from `crates/cupel/src/lib.rs`
- `cargo test --all-targets` passes (no regressions)
- `cargo clippy --all-targets -- -D warnings` clean

## Proof Level

- This slice proves: integration
- Real runtime required: yes (real pipeline execution via `dry_run_with_policy`)
- Human/UAT required: no

## Verification

Integration test file: `crates/cupel/tests/dry_run_with_policy.rs`

Tests to pass:
1. `scorer_is_respected` — policy with `PriorityScorer` selects different items than a `ReflexiveScorer` pipeline (tight budget forces differing exclusions)
2. `slicer_is_respected` — policy with `KnapsackSlice` vs `GreedySlice` pipeline produces different report (items chosen differ by slicer algorithm with carefully chosen token weights)
3. `deduplication_false_allows_duplicates` — policy with `deduplication: false` and two identical-content items; both appear in `report.included`
4. `deduplication_true_excludes_duplicates` — policy with `deduplication: true` and two identical-content items; one appears in `report.excluded` with `ExclusionReason::Deduplicated`
5. `overflow_strategy_is_respected` — policy with `OverflowStrategy::Throw` and an oversized pinned item returns the correct error; policy with `OverflowStrategy::Truncate` does not

Regression check:
```
cargo test --all-targets
cargo clippy --all-targets -- -D warnings
```

## Observability / Diagnostics

- Runtime signals: `dry_run_with_policy` returns `Result<SelectionReport, CupelError>` — errors surface as `CupelError` variants (same as `dry_run`)
- Inspection surfaces: `SelectionReport` fields (`included`, `excluded`, `total_candidates`) are the primary diagnostic surface; tests assert on them directly
- Failure visibility: `CupelError` variants carry context strings; `run_with_components` delegates unchanged error propagation from slice/place stages
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `Pipeline::run_traced` internals (6-stage loop), `DiagnosticTraceCollector`, `OverflowStrategy` (Copy), `CupelError::PipelineConfig`
- New wiring introduced in this slice:
  - `Policy` + `PolicyBuilder` structs in `crates/cupel/src/pipeline/mod.rs`
  - Private `run_with_components(&dyn Scorer, &dyn Slicer, &dyn Placer, bool, OverflowStrategy)` helper in `pipeline/mod.rs`
  - `Pipeline::dry_run_with_policy` method wired to `run_with_components`
  - `pub use pipeline::{Policy, PolicyBuilder, Pipeline, PipelineBuilder}` in `lib.rs`
- What remains before the milestone is truly usable end-to-end: S03 — `policy_sensitivity` free function + `PolicySensitivityReport` types + spec chapter

## Tasks

- [x] **T01: Write failing integration tests for dry_run_with_policy** `est:30m`
  - Why: Establishes the objective stopping condition before any implementation; tests fail until T02 + T03 deliver the implementation
  - Files: `crates/cupel/tests/dry_run_with_policy.rs`
  - Do: Create the test file with 5 tests (scorer_is_respected, slicer_is_respected, deduplication_false_allows_duplicates, deduplication_true_excludes_duplicates, overflow_strategy_is_respected). Import `cupel::{Policy, PolicyBuilder, Pipeline, ...}`. Tests will fail to compile because `Policy`/`PolicyBuilder`/`dry_run_with_policy` do not exist yet — that is correct. Verify the file is well-formed Rust by checking that compilation errors are only "not found" errors, not syntax errors.
  - Verify: `cargo test --test dry_run_with_policy 2>&1 | grep "error\[E" | head -5` — should show only `Policy`, `PolicyBuilder`, or `dry_run_with_policy` not-found errors
  - Done when: Test file exists with 5 named test functions; compilation fails only because the types/methods don't exist yet

- [x] **T02: Extract run_with_components helper and implement Policy + PolicyBuilder** `est:1h`
  - Why: Core implementation step — extracts the 6-stage loop into a shared helper, defines `Policy` and `PolicyBuilder`, and wires `dry_run_with_policy` to the helper
  - Files: `crates/cupel/src/pipeline/mod.rs`, `crates/cupel/src/lib.rs`
  - Do:
    1. In `pipeline/mod.rs`, extract a private `fn run_with_components<C: TraceCollector>(&self, items, budget, scorer: &dyn Scorer, slicer: &dyn Slicer, placer: &dyn Placer, deduplication: bool, overflow_strategy: OverflowStrategy, collector: &mut C)` that contains the current body of `run_traced`. Update `run_traced` to call `run_with_components` passing `self.scorer.as_ref()`, `self.slicer.as_ref()`, `self.placer.as_ref()`, `self.deduplication`, `self.overflow_strategy`.
    2. Immediately run `cargo test --all-targets` to confirm zero regressions from the refactor before adding new types.
    3. Add `pub struct Policy { pub(crate) scorer: Arc<dyn Scorer>, pub(crate) slicer: Arc<dyn Slicer>, pub(crate) placer: Arc<dyn Placer>, pub(crate) deduplication: bool, pub(crate) overflow_strategy: OverflowStrategy }` with a `Debug` impl.
    4. Add `pub struct PolicyBuilder` with `Option<Arc<dyn Scorer>>`, `Option<Arc<dyn Slicer>>`, `Option<Arc<dyn Placer>>`, `bool`, `OverflowStrategy` fields; fluent `scorer`, `slicer`, `placer`, `deduplication`, `overflow_strategy` methods; `build() -> Result<Policy, CupelError>` using `"scorer is required"`, `"slicer is required"`, `"placer is required"` error strings.
    5. Add `pub fn dry_run_with_policy(&self, items: &[ContextItem], budget: &ContextBudget, policy: &Policy) -> Result<SelectionReport, CupelError>` that calls `DiagnosticTraceCollector::new(TraceDetailLevel::Item)`, calls `self.run_with_components(items, budget, policy.scorer.as_ref(), policy.slicer.as_ref(), policy.placer.as_ref(), policy.deduplication, policy.overflow_strategy, &mut collector)`, and returns `collector.into_report()`.
    6. Export `Policy` and `PolicyBuilder` from `lib.rs` alongside `Pipeline` and `PipelineBuilder`.
    7. If clippy fires `type_complexity` on `Arc<dyn Scorer + Send + Sync>`, add type aliases `ArcScorer`, `ArcSlicer`, `ArcPlacer` following D021 pattern.
  - Verify: `cargo build --all-targets` exits 0; `cargo test --all-targets` exits 0 (existing tests all pass); `cargo clippy --all-targets -- -D warnings` exits 0
  - Done when: `Policy`, `PolicyBuilder`, and `Pipeline::dry_run_with_policy` compile; all existing tests pass; clippy clean

- [x] **T03: Make dry_run_with_policy integration tests pass** `est:30m`
  - Why: Proves the implementation is correct — all 5 test cases exercise distinct behavioral contracts
  - Files: `crates/cupel/tests/dry_run_with_policy.rs` (fix/finalize test assertions if needed)
  - Do: Run `cargo test --test dry_run_with_policy` and iterate until all 5 tests pass. Fix any assertion issues (e.g., budget sizing, item token counts) that cause false failures — but do not weaken the behavioral contract being asserted. Confirm the overflow_strategy test exercises both `Throw` and `Truncate` behaviors.
  - Verify: `cargo test --all-targets` exits 0; `cargo clippy --all-targets -- -D warnings` exits 0; all 5 tests in `dry_run_with_policy.rs` pass
  - Done when: `cargo test --all-targets` exits 0; `cargo clippy --all-targets -- -D warnings` exits 0; all 5 named tests in dry_run_with_policy.rs report `ok`

## Files Likely Touched

- `crates/cupel/src/pipeline/mod.rs` — `run_with_components` extraction, `Policy`, `PolicyBuilder`, `dry_run_with_policy`
- `crates/cupel/src/lib.rs` — `pub use pipeline::{Policy, PolicyBuilder, ...}`
- `crates/cupel/tests/dry_run_with_policy.rs` — new integration test file (5 tests)
