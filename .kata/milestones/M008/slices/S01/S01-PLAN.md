# S01: Add on_pipeline_completed hook to core cupel TraceCollector

**Goal:** Add a defaulted `on_pipeline_completed` method to the `TraceCollector` trait, define `StageTraceSnapshot`, wire snapshot collection into `run_with_components`, and export `StageTraceSnapshot` from `lib.rs` — enabling the OTel bridge crate (S02) to build spans from structured completion data without re-scanning the report.
**Demo:** `cargo test --all-targets` in `crates/cupel/` passes all 167+ tests, including a new integration test that runs a real pipeline via `run_traced` with a custom `TraceCollector` override and asserts that `on_pipeline_completed` is called once with exactly 5 stage snapshots carrying correct `stage`, `item_count_in`, `item_count_out`, `duration_ms`, and `excluded` fields.

## Must-Haves

- `StageTraceSnapshot` struct defined in `crates/cupel/src/diagnostics/mod.rs` with fields: `stage: PipelineStage`, `item_count_in: usize`, `item_count_out: usize`, `duration_ms: f64`, `excluded: Vec<ExcludedItem>`. Marked `#[non_exhaustive]`, derives `Debug, Clone`.
- `TraceCollector::on_pipeline_completed(&mut self, report: &SelectionReport, budget: &ContextBudget, stage_snapshots: &[StageTraceSnapshot])` exists as a defaulted no-op. `NullTraceCollector` and `DiagnosticTraceCollector` compile without overriding it.
- `Pipeline::run_with_components` calls `collector.on_pipeline_completed(...)` at the end, gated on `collector.is_enabled()`, with a synthetic `SelectionReport` and correctly populated `Vec<StageTraceSnapshot>` (5 snapshots: Classify, Score, Deduplicate, Slice, Place — in order).
- Each snapshot's `excluded` field contains only items attributable to that stage (Classify → NegativeTokens, Score → none, Deduplicate → Deduplicated, Slice → BudgetExceeded/CountCapExceeded/PinnedOverride, Place → BudgetExceeded truncated).
- `StageTraceSnapshot` is re-exported from `crates/cupel/src/lib.rs`.
- `NullTraceCollector` incurs zero cost: `is_enabled()` returns false, so all snapshot-building code is monomorphized away.
- `cargo test --all-targets` passes 167+ tests (0 regressions).
- `cargo clippy --all-targets -- -D warnings` clean.

## Proof Level

- This slice proves: integration (a real `run_traced` call exercises the new wiring end-to-end with a custom `TraceCollector` override)
- Real runtime required: yes (cargo test)
- Human/UAT required: no

## Verification

- `cd crates/cupel && cargo test --all-targets` — must show 167+ passed, 0 failed
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings` — must exit 0 clean
- `crates/cupel/tests/on_pipeline_completed.rs` — new integration test file; asserts:
  - `on_pipeline_completed` is called exactly once after `pipeline.run_traced()`
  - receives exactly 5 snapshots in order: Classify, Score, Deduplicate, Slice, Place
  - each snapshot's `stage` field matches the expected `PipelineStage` variant
  - snapshot `item_count_in` and `item_count_out` are correct for the test scenario
  - `NullTraceCollector` path: `run_traced` with `NullTraceCollector` does not call the override (no-op confirmed by zero-cost ZST)
- `crates/cupel/src/diagnostics/trace_collector.rs` — new unit test: `on_pipeline_completed_default_is_noop` calls the defaulted method directly on a `NullTraceCollector` and `DiagnosticTraceCollector` and asserts no panic

## Observability / Diagnostics

- Runtime signals: none (library crate, no runtime process)
- Inspection surfaces: `cargo test --all-targets` output — test failures name the exact assertion; snapshot field mismatches produce clear panic messages naming expected vs actual
- Failure visibility: if `on_pipeline_completed` is not called, the integration test's `called` flag assertion fails with "expected called == 1, got 0"; if snapshot count is wrong, "expected 5 snapshots, got N"
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `TraceCollector` trait (`crates/cupel/src/diagnostics/trace_collector.rs`), `run_with_components` in `crates/cupel/src/pipeline/mod.rs`, `diagnostics/mod.rs` type definitions
- New wiring introduced in this slice: `on_pipeline_completed` call at end of `run_with_components`; `StageTraceSnapshot` populated in each stage block
- What remains before the milestone is truly usable end-to-end: S02 — `CupelOtelTraceCollector` that overrides `on_pipeline_completed` to emit real OTel spans; S03 — packaging + spec + R058 validation

## Tasks

- [x] **T01: Define `StageTraceSnapshot`, add `on_pipeline_completed` no-op to trait, export, write failing integration test** `est:45m`
  - Why: establishes the compile-level contract and a failing integration test that T02 makes pass
  - Files: `crates/cupel/src/diagnostics/mod.rs`, `crates/cupel/src/diagnostics/trace_collector.rs`, `crates/cupel/src/lib.rs`, `crates/cupel/tests/on_pipeline_completed.rs`
  - Do: (1) Define `StageTraceSnapshot` in `diagnostics/mod.rs` alongside `ExcludedItem`; mark `#[non_exhaustive]`, derive `Debug, Clone`; (2) Add `on_pipeline_completed` defaulted no-op to `TraceCollector` trait with `_` parameter names; (3) Add `StageTraceSnapshot` to the `pub use trace_collector::{...}` re-export in `diagnostics/mod.rs` and to `pub use diagnostics::{...}` in `lib.rs`; (4) Write `tests/on_pipeline_completed.rs` with a custom `SpyCollector` that implements `TraceCollector` and records the snapshots passed to `on_pipeline_completed`; assert that running a pipeline via `run_traced` calls it with 5 snapshots — this **fails** until T02 wires the call
  - Verify: `cargo build --all-targets` succeeds (no compile errors); `cargo test --all-targets` shows T01 unit tests pass; integration test fails with "`called == 0`" (expected failure until T02)
  - Done when: crate compiles clean; new unit test `on_pipeline_completed_default_is_noop` passes; integration test exists and fails for the right reason (hook not yet called)

- [x] **T02: Wire snapshot collection into `run_with_components`, build synthetic report, call `on_pipeline_completed`** `est:1h`
  - Why: the implementation heart of the slice — populates snapshot data in each stage block, builds the synthetic `SelectionReport`, and calls the hook; makes the T01 integration test pass
  - Files: `crates/cupel/src/pipeline/mod.rs`
  - Do: (1) Capture `total_tokens_considered` as a local when `collector.is_enabled()` (extract from the `set_candidates` call); (2) In each existing `if collector.is_enabled()` stage block, build a `StageTraceSnapshot` for that stage and push it onto a `Vec<StageTraceSnapshot>` (initialized when enabled); (3) Classify snapshot: `item_count_in=items.len()`, `item_count_out=pinned.len()+scoreable.len()`, `excluded` cloned from `neg_items`; (4) Score snapshot: `item_count_in=scoreable.len()`, `item_count_out=scored.len()`, `excluded=vec![]`; (5) Deduplicate snapshot: `item_count_in=scored.len()`, `item_count_out=deduped.len()`, `excluded` cloned from `ded_excluded`; (6) Slice snapshot: `item_count_in=sorted.len()`, `item_count_out=sliced.len()`, `excluded` collected in the existing per-item loop (clone each ExcludedItem as it is recorded); (7) Place snapshot: `item_count_in=pinned.len()+sliced.len()`, `item_count_out=result.len()`, `excluded` from truncated items; (8) After Place, build a synthetic `SelectionReport` from snapshot data + result items + score_lookup; (9) Call `collector.on_pipeline_completed(&report, budget, &snapshots)`
  - Verify: `cargo test --all-targets` — all 167 prior tests + the new integration test in `on_pipeline_completed.rs` pass; `cargo clippy --all-targets -- -D warnings` exits 0
  - Done when: `cargo test --all-targets` shows 169+ passed 0 failed; clippy clean; integration test asserts correct snapshot count, stage ordering, and item counts

## Files Likely Touched

- `crates/cupel/src/diagnostics/mod.rs`
- `crates/cupel/src/diagnostics/trace_collector.rs`
- `crates/cupel/src/pipeline/mod.rs`
- `crates/cupel/src/lib.rs`
- `crates/cupel/tests/on_pipeline_completed.rs` (new)
