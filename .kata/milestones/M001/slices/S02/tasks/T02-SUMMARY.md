---
id: T02
parent: S02
milestone: M001
provides:
  - 12 unit tests covering all behavioral contracts for TraceDetailLevel, NullTraceCollector, and DiagnosticTraceCollector
key_files:
  - crates/cupel/src/diagnostics/trace_collector.rs
key_decisions:
  - Added TraceEventCallback type alias (Box<dyn Fn(&TraceEvent)>) to satisfy clippy::type_complexity — this was a pre-existing issue in T01 code surfaced only when --all-targets clippy was run
patterns_established:
  - make_item / make_event test helpers reduce boilerplate and keep test bodies focused on the contract being verified
  - Arc<AtomicU32> pattern used for callback counting in tests (avoids RefCell borrow complexity and has no Send requirement issues since Arc<AtomicU32>: Send)
observability_surfaces:
  - cargo test --lib -- trace_collector scopes to these 12 tests specifically; --nocapture prints assertion details on failure
duration: ~15min
verification_result: passed
completed_at: 2026-03-17
blocker_discovered: false
---

# T02: Unit tests for all behavioral contracts

**Added 12 unit tests covering every behavioral invariant of the four TraceCollector types; all pass with zero clippy warnings and zero doc warnings.**

## What Happened

Appended a `#[cfg(test)]` module to `trace_collector.rs` with two private helpers (`make_item`, `make_event`) and 12 tests:

- **NullTraceCollector (3 tests):** ZST size assertion, `is_enabled()` returns false, all 6 trait methods callable without panic.
- **DiagnosticTraceCollector — is_enabled (1 test):** Both `Stage` and `Item` level collectors return `true`.
- **Stage-level gating (2 tests):** `record_item_event` is silently discarded when `detail_level == Stage`; both methods are recorded when `detail_level == Item`.
- **Callback invocation (2 tests):** Callback fires after `record_stage_event`; callback is NOT fired when an item event is filtered by stage-level gating.
- **into_report sort contract (2 tests):** Score-descending order verified; stable insertion-order tie-breaking verified.
- **Item-recording (2 tests):** `record_included`, `record_excluded`, `set_candidates` all correctly populate `SelectionReport` fields; stage events are preserved in insertion order.

Also fixed a pre-existing `clippy::type_complexity` warning in the T01 production code: `Option<Box<dyn Fn(&TraceEvent)>>` replaced with a named `TraceEventCallback` type alias. The `with_callback` constructor signature was updated to use the alias; behavior is identical.

## Verification

```
cargo test --lib                              → 29 passed, 0 failed
cargo clippy --all-targets -- -D warnings    → 0 warnings, 0 errors (EXIT 0)
cargo doc --no-deps                          → 0 warnings, 0 errors (EXIT 0)
```

12 new trace_collector tests + 17 pre-existing model/budget/kind tests.

## Diagnostics

- `cargo test --lib -- diagnostics::trace_collector` — runs only the 12 new tests
- `cargo test --lib -- --nocapture` — prints each test name and any panic message with file/line on failure
- Sort-contract failures appear as: `assertion failed: report.excluded[0].score == 5.0`

## Deviations

- Fixed pre-existing `clippy::type_complexity` lint in T01 production code (was not caught during T01 because clippy was run without `--all-targets`). Added `TraceEventCallback` type alias; no behavioral change.
- Test count is 12, not ≥10 as the plan required; the "78 existing from S01" in the plan was an overestimate — S01 tests are in .NET (the plan was mixing Rust and .NET counts). Actual Rust pre-existing count was 17.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/diagnostics/trace_collector.rs` — appended 12-test `#[cfg(test)]` module; added `TraceEventCallback` type alias; updated `DiagnosticTraceCollector::callback` field and `with_callback` signature to use alias
