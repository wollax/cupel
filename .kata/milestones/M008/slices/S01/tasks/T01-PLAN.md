---
estimated_steps: 5
estimated_files: 4
---

# T01: Define StageTraceSnapshot, add on_pipeline_completed no-op, export, write failing integration test

**Slice:** S01 — Add on_pipeline_completed hook to core cupel TraceCollector
**Milestone:** M008

## Description

Establish the compile-level contract for the `on_pipeline_completed` hook:
1. Define `StageTraceSnapshot` struct in `diagnostics/mod.rs`.
2. Add the `on_pipeline_completed` defaulted no-op to the `TraceCollector` trait.
3. Export `StageTraceSnapshot` from `diagnostics/mod.rs` and `lib.rs`.
4. Write the integration test `tests/on_pipeline_completed.rs` with a `SpyCollector` — this test will initially **fail** (hook not called) and T02 will make it pass.

This task produces no behavior change in `run_with_components`. The crate must compile clean and the existing 167 tests must continue to pass.

## Steps

1. In `crates/cupel/src/diagnostics/mod.rs`, define `StageTraceSnapshot` struct immediately before the `SelectionReport` definition:
   ```rust
   #[non_exhaustive]
   #[derive(Debug, Clone)]
   pub struct StageTraceSnapshot {
       pub stage: PipelineStage,
       pub item_count_in: usize,
       pub item_count_out: usize,
       pub duration_ms: f64,
       /// Excluded items attributable to this stage only.
       pub excluded: Vec<ExcludedItem>,
   }
   ```
   Add a doc comment matching the spec: "A snapshot of one pipeline stage's execution, passed to `TraceCollector::on_pipeline_completed` after the run completes."

2. In `crates/cupel/src/diagnostics/trace_collector.rs`, add `on_pipeline_completed` as a defaulted no-op to the `TraceCollector` trait, after `set_candidates`:
   ```rust
   /// Called once at the end of a pipeline run with structured completion data.
   ///
   /// **No-op default.** OTel-bridge implementations override this to emit
   /// `cupel.pipeline` and `cupel.stage.*` spans from the structured snapshot data.
   /// [`NullTraceCollector`] and [`DiagnosticTraceCollector`] rely on this no-op.
   fn on_pipeline_completed(
       &mut self,
       _report: &SelectionReport,
       _budget: &ContextBudget,
       _stage_snapshots: &[StageTraceSnapshot],
   ) {
   }
   ```
   Add the necessary imports: `use super::StageTraceSnapshot;` (it's in the parent module). Also add `ContextBudget` to the existing imports from `crate::model`.

3. In `crates/cupel/src/diagnostics/mod.rs`, add `StageTraceSnapshot` to the `pub use trace_collector::{...}` re-export block at the bottom of the file.

4. In `crates/cupel/src/lib.rs`, add `StageTraceSnapshot` to the `pub use diagnostics::{...}` block.

5. Create `crates/cupel/tests/on_pipeline_completed.rs` with:
   - A `SpyCollector` struct that stores a `called: u32` counter and a `Vec<StageTraceSnapshot>` of snapshots received
   - Implement `TraceCollector` for `SpyCollector`: `is_enabled` returns true; `record_stage_event`, `record_item_event` are no-ops; `on_pipeline_completed` increments `called` and clones `stage_snapshots` into the vec
   - Test `on_pipeline_completed_called_once_with_five_snapshots`: build a minimal `Pipeline` (ReflexiveScorer, GreedySlice, ChronologicalPlacer), create 3 items, call `pipeline.run_traced(&items, &budget, &mut spy)`, assert `spy.called == 1` and `spy.snapshots.len() == 5` and `spy.snapshots[0].stage == PipelineStage::Classify`
   - Test `on_pipeline_completed_not_called_for_null_collector`: call `pipeline.run_traced(&items, &budget, &mut NullTraceCollector)` — should not panic (no-op default confirmed)
   - Add a unit test `on_pipeline_completed_default_is_noop` in the existing `tests` module in `trace_collector.rs`: call `NullTraceCollector.on_pipeline_completed(...)` directly with a dummy report and assert no panic

   **This integration test will fail** at "spy.called == 1" because T02 has not wired the call yet. That is expected and correct.

## Must-Haves

- [ ] `StageTraceSnapshot` struct defined with all 5 fields, `#[non_exhaustive]`, derives `Debug, Clone`
- [ ] `on_pipeline_completed` defaulted no-op on `TraceCollector` trait with correct signature (`&mut self, &SelectionReport, &ContextBudget, &[StageTraceSnapshot]`)
- [ ] `StageTraceSnapshot` exported from `crates/cupel/src/lib.rs` as a top-level public type
- [ ] `crates/cupel/tests/on_pipeline_completed.rs` exists with `SpyCollector` and two tests
- [ ] `cargo build --all-targets` exits 0
- [ ] All 167 existing tests still pass (`cargo test --all-targets` shows 0 failed among existing tests)
- [ ] The new integration test `on_pipeline_completed_called_once_with_five_snapshots` fails with the expected assertion (hook not yet wired)

## Verification

- `cd crates/cupel && cargo build --all-targets` — must exit 0 (compile check)
- `cd crates/cupel && cargo test --all-targets 2>&1 | grep -E "FAILED|passed|failed"` — existing 167 tests pass; only the new integration test fails
- `cd crates/cupel && cargo test on_pipeline_completed_default_is_noop` — passes
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings` — exits 0

## Observability Impact

- Signals added/changed: `StageTraceSnapshot` type and `on_pipeline_completed` trait method become part of the public API surface — future agents can verify their presence with `cargo doc --no-deps` or `grep`
- How a future agent inspects this: `cargo test --all-targets` output names each failing test; the failing integration test message will say `assertion failed: spy.called == 1` with actual 0
- Failure state exposed: compile errors are immediate and name the missing import or type; runtime test failure is specific about which assertion failed

## Inputs

- `crates/cupel/src/diagnostics/mod.rs` — location for `StageTraceSnapshot`, alongside `ExcludedItem` and `SelectionReport`
- `crates/cupel/src/diagnostics/trace_collector.rs` — `TraceCollector` trait, pattern for defaulted no-op methods (`record_included`, `record_excluded`, `set_candidates`)
- `crates/cupel/src/lib.rs` — existing `pub use diagnostics::{...}` block to extend
- Research doc S01-RESEARCH.md — exact struct fields, method signature, `#[non_exhaustive]` requirement, `&mut self` rationale (D164), doc comment text

## Expected Output

- `crates/cupel/src/diagnostics/mod.rs` — `StageTraceSnapshot` struct added; `StageTraceSnapshot` added to the `pub use` at bottom
- `crates/cupel/src/diagnostics/trace_collector.rs` — `on_pipeline_completed` defaulted no-op on `TraceCollector` trait; unit test `on_pipeline_completed_default_is_noop` added
- `crates/cupel/src/lib.rs` — `StageTraceSnapshot` in the `pub use diagnostics::{...}` block
- `crates/cupel/tests/on_pipeline_completed.rs` (new) — `SpyCollector` + 2 integration tests (1 failing for the right reason, 1 passing)
