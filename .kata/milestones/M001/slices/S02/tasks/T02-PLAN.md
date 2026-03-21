---
estimated_steps: 4
estimated_files: 1
---

# T02: Unit tests for all behavioral contracts

**Slice:** S02 — TraceCollector Trait & Implementations
**Milestone:** M001

## Description

Add a `#[cfg(test)]` block to `trace_collector.rs` covering all behavioral invariants of the four types: ZST size, `is_enabled` values, event gating by `TraceDetailLevel`, `into_report()` sort contract (score-desc, stable by insertion order on ties), callback invocation, and item-recording methods populating the `SelectionReport` correctly. These tests are the slice's acceptance criteria — the slice is not done until they all pass with zero clippy warnings.

## Steps

1. **Append `#[cfg(test)]` module** to the bottom of `trace_collector.rs`. Add `use super::*;` and any needed imports (`use crate::model::ContextItem;`, etc.). Helper: write a private `make_item(content: &str, tokens: i64) -> ContextItem` using `ContextItemBuilder::new(content, tokens).build().unwrap()` to reduce test boilerplate.

2. **Write NullTraceCollector tests**:
   - `null_is_zst`: `assert_eq!(std::mem::size_of::<NullTraceCollector>(), 0);`
   - `null_is_not_enabled`: `assert!(!NullTraceCollector.is_enabled());`
   - `null_record_methods_are_noop`: call all 6 trait methods on a `NullTraceCollector` instance — none should panic (no assertions needed; not-panicking is the contract)

3. **Write DiagnosticTraceCollector event recording tests**:
   - `diagnostic_is_enabled`: `assert!(DiagnosticTraceCollector::new(TraceDetailLevel::Stage).is_enabled());`
   - `stage_level_only_records_stage_events`: create `Stage` collector; call `record_stage_event` (supply a `TraceEvent` with `stage: PipelineStage::Classify, duration_ms: 1.0, item_count: 0, message: None`); call `record_item_event` with same; call `into_report()` and assert `report.events.len() == 1`
   - `item_level_records_both`: create `Item` collector; call both record methods; assert `into_report().events.len() == 2`
   - `callback_invoked_on_stage_event`: use a `std::cell::Cell<u32>` inside a closure (wrap in `Rc` for shared ownership) — or use a simpler approach with `std::sync::atomic::AtomicU32` in an `Arc`; create `with_callback` collector; call `record_stage_event`; assert counter == 1. Alternatively: write to a `Vec` captured by the closure using `RefCell` — pick whatever compiles cleanly without `Send` requirement on the closure.
   - `callback_not_invoked_when_item_event_filtered`: create `Stage`-level collector with a counting callback; call `record_item_event`; assert counter == 0

4. **Write into_report and item-recording tests**:
   - `into_report_sort_score_desc`: call `record_excluded` with items at scores 2.0 and 5.0 (in that insertion order); assert `into_report().excluded[0].score == 5.0`
   - `into_report_sort_stable_on_tie`: call `record_excluded` twice with the same score (e.g. 3.0); assert `into_report().excluded[0].item.content() == <first-recorded item content>` — verifies insertion-order stability
   - `item_recording_populates_report_fields`: create `Stage` collector; call `record_included(item_a, 4.0, InclusionReason::Scored)`, `record_excluded(item_b, 1.0, ExclusionReason::BudgetExceeded { item_tokens: 100, available_tokens: 50 })`, `set_candidates(2, 60)`; call `into_report()` and assert `report.included.len() == 1`, `report.excluded.len() == 1`, `report.total_candidates == 2`, `report.total_tokens_considered == 60`
   - `into_report_events_in_insertion_order`: emit stage events for `Classify` then `Score`; assert `report.events[0].stage == PipelineStage::Classify && report.events[1].stage == PipelineStage::Score`

   Run `cargo test --lib -- --nocapture` to see all test names pass.
   Run `cargo clippy --all-targets -- -D warnings` to confirm zero warnings.

## Must-Haves

- [ ] `size_of::<NullTraceCollector>() == 0` compile-time assertion is present as a test
- [ ] `NullTraceCollector::is_enabled()` test verifies `false`
- [ ] `DiagnosticTraceCollector::is_enabled()` test verifies `true`
- [ ] Stage-level gating test: `record_item_event` is ignored when `detail_level == Stage`
- [ ] `into_report()` sort test: score-descending order verified with two items at different scores
- [ ] `into_report()` stable sort test: insertion order preserved when scores are equal
- [ ] Item-recording test: `record_included`, `record_excluded`, `set_candidates` all correctly populate `SelectionReport` fields
- [ ] `cargo test --lib` passes with zero failures
- [ ] `cargo clippy --all-targets -- -D warnings` passes with zero warnings
- [ ] `cargo doc --no-deps 2>&1 | grep -E "warning|error"` outputs nothing

## Verification

- `cargo test --lib 2>&1 | grep -E "FAILED|^error"` — empty output
- `cargo clippy --all-targets -- -D warnings 2>&1 | grep -E "^warning|^error"` — empty output
- `cargo doc --no-deps 2>&1 | grep -E "warning|error"` — empty output
- Test count: `cargo test --lib 2>&1 | grep "test result"` should show ≥ 10 tests passed (adding to existing 78 from S01)

## Observability Impact

- Signals added/changed: None (test-only code; no change to production code paths)
- How a future agent inspects this: `cargo test --lib -- --nocapture` prints each test name; a failing test names the assertion and line; `cargo test --lib trace_collector` scopes to these tests specifically
- Failure state exposed: test panic message + line number; sort contract violations surface as assertion failures on `excluded[0].score` or `excluded[0].item.content()`

## Inputs

- `crates/cupel/src/diagnostics/trace_collector.rs` — T01 output; all 4 types to test
- `crates/cupel/src/model` — `ContextItemBuilder` for constructing test items
- S02-RESEARCH.md — behavioral contracts, ZST invariant, sort contract, `total_cmp` requirement

## Expected Output

- `crates/cupel/src/diagnostics/trace_collector.rs` — `#[cfg(test)]` block appended with ≥ 10 unit tests
- `cargo test --lib` — all tests pass (≥ 10 new + ≥ 78 existing)
- `cargo clippy --all-targets -- -D warnings` — zero warnings
- `cargo doc --no-deps` — zero warnings
