---
id: T01
parent: S01
milestone: M008
provides:
  - "`StageTraceSnapshot` struct with 5 fields, `#[non_exhaustive]`, derives `Debug, Clone`"
  - "`TraceCollector::on_pipeline_completed` defaulted no-op with signature `(&mut self, &SelectionReport, &ContextBudget, &[StageTraceSnapshot])`"
  - "`StageTraceSnapshot` exported from `crates/cupel/src/lib.rs` as top-level public type"
  - "`crates/cupel/tests/on_pipeline_completed.rs` with `SpyCollector` and 2 integration tests (1 failing for the right reason, 1 passing)"
  - "Unit test `on_pipeline_completed_default_is_noop` in `trace_collector.rs` confirming default no-op does not panic"
key_files:
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/diagnostics/trace_collector.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/on_pipeline_completed.rs
key_decisions:
  - "`StageTraceSnapshot` defined in `diagnostics/mod.rs` directly (not in `trace_collector.rs`) alongside other diagnostic types; it is `pub` so accessible as `diagnostics::StageTraceSnapshot` without an extra re-export within the module"
  - "`NullTraceCollector::default()` used in integration tests instead of `NullTraceCollector` struct literal because `#[non_exhaustive]` blocks external construction with literal syntax"
  - "`ContextBudget` imported into `trace_collector.rs` from `crate::model` (not separately from `super`) — keeps import block consistent with existing pattern"
patterns_established:
  - "Defaulted no-op trait methods use `_` parameter prefixes — consistent with `record_included`, `record_excluded`, `set_candidates`"
observability_surfaces:
  - "cargo test --all-targets: `on_pipeline_completed_called_once_with_five_snapshots` FAILS with `assertion: on_pipeline_completed must be called exactly once` (spy.called == 0); this is the expected failing state until T02 wires the call"
  - "cargo test on_pipeline_completed_default_is_noop: passes — confirms the default no-op does not panic"
duration: 20m
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T01: Define StageTraceSnapshot, add on_pipeline_completed no-op, export, write failing integration test

**`StageTraceSnapshot` struct and `TraceCollector::on_pipeline_completed` defaulted no-op established; failing integration test written to contract T02's wiring work.**

## What Happened

Defined `StageTraceSnapshot` in `diagnostics/mod.rs` immediately before `SelectionReport`, with `#[non_exhaustive]`, `derive(Debug, Clone)`, and the 5 specified fields (`stage`, `item_count_in`, `item_count_out`, `duration_ms`, `excluded`).

Added `on_pipeline_completed` as a defaulted no-op to the `TraceCollector` trait in `trace_collector.rs`, after `set_candidates`. Added `ContextBudget` to the import from `crate::model` and `StageTraceSnapshot` to the import from `super`. Both `NullTraceCollector` and `DiagnosticTraceCollector` compile without any override — they inherit the no-op.

Exported `StageTraceSnapshot` from `lib.rs` by adding it to the existing `pub use diagnostics::{...}` block.

Created `crates/cupel/tests/on_pipeline_completed.rs` with a `SpyCollector` that records `called: u32` and `Vec<StageTraceSnapshot>`. Two tests: `on_pipeline_completed_called_once_with_five_snapshots` (fails as designed — `spy.called` stays 0 until T02 wires the call) and `on_pipeline_completed_not_called_for_null_collector` (passes — `run_traced` with `NullTraceCollector` does not panic).

Added `on_pipeline_completed_default_is_noop` unit test in `trace_collector.rs` calling the method directly on both `NullTraceCollector` and `DiagnosticTraceCollector` with a real `ContextBudget` and an empty/non-empty snapshot slice — passes.

## Verification

- `cd crates/cupel && cargo build --all-targets` → exit 0 ✓
- `cd crates/cupel && cargo test --all-targets` → 160 existing tests pass, 0 regressions; `on_pipeline_completed_called_once_with_five_snapshots` FAILS with expected assertion (spy.called == 0) ✓
- `cd crates/cupel && cargo test on_pipeline_completed_default_is_noop` → 1 passed ✓
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings` → exit 0 ✓

## Diagnostics

`cargo test --all-targets` output names the failing integration test explicitly:
```
test on_pipeline_completed_called_once_with_five_snapshots ... FAILED
assertion `left == right` failed: on_pipeline_completed must be called exactly once
```
This is the expected failure state. When T02 wires the call, this message will disappear and the test will pass.

## Deviations

- Task plan said "add `StageTraceSnapshot` to the `pub use trace_collector::{...}` re-export block" in `diagnostics/mod.rs` — but `StageTraceSnapshot` lives in `mod.rs` itself, not in `trace_collector`. It is already accessible as `diagnostics::StageTraceSnapshot` by virtue of being `pub` in the module. No extra re-export within `diagnostics/mod.rs` is needed; only `lib.rs` needed updating.
- Task plan said 167 existing tests — actual baseline was 160 (serde feature tests not active by default, count may reflect a different Cargo feature set). Zero regressions either way.

## Known Issues

None. Intentional failing test is documented and expected.

## Files Created/Modified

- `crates/cupel/src/diagnostics/mod.rs` — `StageTraceSnapshot` struct added before `SelectionReport`
- `crates/cupel/src/diagnostics/trace_collector.rs` — `on_pipeline_completed` defaulted no-op added to `TraceCollector` trait; `ContextBudget` and `StageTraceSnapshot` added to imports; `on_pipeline_completed_default_is_noop` unit test added
- `crates/cupel/src/lib.rs` — `StageTraceSnapshot` added to `pub use diagnostics::{...}` block
- `crates/cupel/tests/on_pipeline_completed.rs` (new) — `SpyCollector` + 2 integration tests
