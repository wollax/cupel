---
id: S01
parent: M008
milestone: M008
provides:
  - "`StageTraceSnapshot` struct (`stage`, `item_count_in`, `item_count_out`, `duration_ms`, `excluded`) — `#[non_exhaustive]`, derives `Debug, Clone` — in `crates/cupel/src/diagnostics/mod.rs`"
  - "`TraceCollector::on_pipeline_completed(&mut self, report: &SelectionReport, budget: &ContextBudget, stage_snapshots: &[StageTraceSnapshot])` defaulted no-op on trait — non-breaking for all existing implementors"
  - "`StageTraceSnapshot` exported from `crates/cupel/src/lib.rs` as a top-level public type"
  - "5-stage snapshot collection wired into `run_with_components`: Classify, Score, Deduplicate, Slice, Place — each inside its `if collector.is_enabled()` block"
  - "`on_pipeline_completed` called at end of `run_with_components` with synthetic `SelectionReport` and 5 `StageTraceSnapshot`s — gated on `!stage_snapshots.is_empty()`"
  - "`NullTraceCollector` path: zero-cost — `is_enabled()` false → snapshots never built → hook never called"
  - "Integration test `crates/cupel/tests/on_pipeline_completed.rs` with `SpyCollector` proving 5 snapshots, correct stage ordering, item counts, and zero-call guarantee for `NullTraceCollector`"
requires:
  - slice: none
    provides: "first slice; builds on existing `TraceCollector` trait and `run_with_components` machinery"
affects:
  - S02
key_files:
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/diagnostics/trace_collector.rs
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/on_pipeline_completed.rs
key_decisions:
  - "D164 — `on_pipeline_completed` added as a defaulted no-op; Rust Span trait is not dyn-compatible so structured end-of-run handoff is the only viable OTel pattern"
  - "D165 — `StageTraceSnapshot.excluded` is stage-scoped (only items attributable to that stage); OTel collector builds events from snapshot data, never re-scans the full report"
  - "D166 — S01 verification is integration-level: failing-first `SpyCollector` test written in T01, made to pass in T02; `cargo test --all-targets` + clippy are the gate"
  - "D167 — Synthetic `SelectionReport` for `on_pipeline_completed` uses union of snapshot excluded items via `flat_map`; `events` and `count_requirement_shortfalls` are empty vecs"
  - "`StageTraceSnapshot` defined in `diagnostics/mod.rs` directly (alongside other diagnostic types), not in `trace_collector.rs` — already accessible as `diagnostics::StageTraceSnapshot` without extra re-export within the module"
  - "`scored_len` captured before `deduplicate()` consumes `scored` so it is available as `item_count_in` for the Dedup snapshot"
  - "Duration captured to stage-local (`classify_ms`, `score_ms`, etc.) so the same value feeds both `TraceEvent` and `StageTraceSnapshot` without calling `elapsed()` twice"
patterns_established:
  - "Defaulted no-op trait methods use `_` parameter prefixes — consistent with `record_included`, `record_excluded`, `set_candidates`"
  - "All stage snapshot pushes inside their respective `if collector.is_enabled()` blocks — consistent with existing `TraceEvent` emission pattern"
  - "`on_pipeline_completed` call gated on `!stage_snapshots.is_empty()` (equivalent to `is_enabled()` having been true) — keeps disabled path zero-allocation"
observability_surfaces:
  - "`cargo test on_pipeline_completed` — two integration tests by name; `on_pipeline_completed_called_once_with_five_snapshots` and `on_pipeline_completed_not_called_for_null_collector`"
  - "`grep -c 'stage_snapshots.push' crates/cupel/src/pipeline/mod.rs` → must show 5 (one per stage)"
drill_down_paths:
  - .kata/milestones/M008/slices/S01/tasks/T01-SUMMARY.md
  - .kata/milestones/M008/slices/S01/tasks/T02-SUMMARY.md
duration: 45m
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
---

# S01: Add on_pipeline_completed hook to core cupel TraceCollector

**`TraceCollector::on_pipeline_completed` defaulted hook and `StageTraceSnapshot` wired into all 5 pipeline stages; 170 tests pass, clippy clean.**

## What Happened

**T01** established the compile-level contract. `StageTraceSnapshot` (5 fields, `#[non_exhaustive]`, `Debug, Clone`) was defined in `diagnostics/mod.rs` and exported from `lib.rs`. `TraceCollector::on_pipeline_completed` was added as a defaulted no-op — both `NullTraceCollector` and `DiagnosticTraceCollector` compile without overriding it. A `SpyCollector` integration test was written in `crates/cupel/tests/on_pipeline_completed.rs` that asserts the hook is called exactly once with 5 snapshots; this test was intentionally failing (spy.called == 0) until T02 wired the call. A unit test `on_pipeline_completed_default_is_noop` confirmed the default doesn't panic.

**T02** implemented the wiring. `crates/cupel/src/pipeline/mod.rs` was the only file changed. Snapshot collection was wired into all 5 stage blocks inside their existing `if collector.is_enabled()` guards. Stage durations are captured to locals (`classify_ms`, etc.) to avoid double-calling `elapsed()`. `scored_len` is captured before `deduplicate()` consumes `scored`. Excluded items for Dedup and Slice stages are built as `Vec<ExcludedItem>` once and reused for both `record_excluded` and the snapshot field. After the Place stage, a synthetic `SelectionReport` is built from snapshot data and `collector.on_pipeline_completed(...)` is called. The previously failing integration test now passes.

## Verification

- `cd crates/cupel && cargo test --all-targets` → 170 passed, 0 failed, 0 regressions ✓
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings` → exit 0, 0 warnings ✓
- `on_pipeline_completed_called_once_with_five_snapshots` — passes: hook called exactly once, 5 snapshots in Classify/Score/Deduplicate/Slice/Place order, item counts correct ✓
- `on_pipeline_completed_not_called_for_null_collector` — passes: `run_traced` with `NullTraceCollector` does not call the override ✓
- `on_pipeline_completed_default_is_noop` — passes: calling the default method on both `NullTraceCollector` and `DiagnosticTraceCollector` does not panic ✓
- `grep -c 'stage_snapshots.push' crates/cupel/src/pipeline/mod.rs` → 5 ✓

## Requirements Advanced

- R058 — `on_pipeline_completed` hook (the prerequisite for `CupelOtelTraceCollector`) now exists in the core crate; `StageTraceSnapshot` provides the structured completion data S02 needs to build OTel spans without re-scanning the report

## Requirements Validated

- none — R058 validation requires S02 (full OTel crate) and S03 (packaging + spec + explicit validation)

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

- Task plan said "add `StageTraceSnapshot` to the `pub use trace_collector::{...}` re-export in `diagnostics/mod.rs`" — but `StageTraceSnapshot` lives in `mod.rs` itself (not in `trace_collector`), so it is already accessible as `diagnostics::StageTraceSnapshot` without an extra re-export. Only `lib.rs` needed updating.
- Task plan estimated 167 existing tests as the baseline; actual count was 160 (serde feature tests not active by default). Slice completed at 170. Zero regressions either way.

## Known Limitations

- `on_pipeline_completed` carries a synthetic `SelectionReport` (union of snapshot excluded items; no raw events, no `count_requirement_shortfalls`). The OTel collector builds from snapshot data, not from the report's event log. This is by design (D167) but means the synthetic report is not a full-fidelity replica of what `DiagnosticTraceCollector::into_report()` would return.
- `StageTraceSnapshot` is `#[non_exhaustive]` — external implementors cannot construct it with a struct literal; must use a functional update or wait for a builder. This is intentional for additive evolution.

## Follow-ups

- S02: Implement `CupelOtelTraceCollector` that overrides `on_pipeline_completed` to emit real OTel spans using `StageTraceSnapshot` data.

## Files Created/Modified

- `crates/cupel/src/diagnostics/mod.rs` — `StageTraceSnapshot` struct added before `SelectionReport`
- `crates/cupel/src/diagnostics/trace_collector.rs` — `on_pipeline_completed` defaulted no-op added to trait; `ContextBudget` and `StageTraceSnapshot` added to imports; `on_pipeline_completed_default_is_noop` unit test added
- `crates/cupel/src/lib.rs` — `StageTraceSnapshot` added to `pub use diagnostics::{...}` block
- `crates/cupel/src/pipeline/mod.rs` — snapshot collection wired into all 5 stage blocks + `on_pipeline_completed` call at end of `run_with_components`
- `crates/cupel/tests/on_pipeline_completed.rs` (new) — `SpyCollector` + 2 integration tests

## Forward Intelligence

### What the next slice should know

- `on_pipeline_completed` receives a **synthetic** `SelectionReport` — not the same as the one `DiagnosticTraceCollector::into_report()` would return. The OTel collector must rely on `stage_snapshots` for span data, not on the report's `events` field (it's always empty in the synthetic version).
- `StageTraceSnapshot.excluded` is stage-scoped: Classify → NegativeTokens; Score → empty; Deduplicate → Deduplicated; Slice → BudgetExceeded/CountCapExceeded/PinnedOverride; Place → BudgetExceeded (truncated items). The OTel collector can emit exclusion events directly from snapshot.excluded without knowing stage-to-reason mapping.
- The `PipelineStage` enum has 5 variants; the snapshot vec is always in Classify/Score/Deduplicate/Slice/Place order when the collector is enabled.
- `StageTraceSnapshot` is `#[non_exhaustive]` — construct it with `StageTraceSnapshot { stage, item_count_in, item_count_out, duration_ms, excluded }` inside the crate; external code (cupel-otel) receives it by reference and reads fields.

### What's fragile

- The `on_pipeline_completed` call is gated on `!stage_snapshots.is_empty()`. If someone adds an early-return path in `run_with_components` before all 5 stages complete, the vec might have fewer than 5 entries or be empty. The integration test catches this, but the gate condition is implicit.

### Authoritative diagnostics

- `cargo test on_pipeline_completed` — two test names; any regression here immediately names the broken assertion with field-level detail (expected vs actual stage, item count, or called flag).

### What assumptions changed

- Assumed 167 existing tests at baseline; actual was 160. Not significant — the gate is "0 regressions" not a fixed count.
