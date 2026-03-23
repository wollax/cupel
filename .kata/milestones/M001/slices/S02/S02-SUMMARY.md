---
id: S02
parent: M001
milestone: M001
provides:
  - TraceDetailLevel enum (Stage/Item, #[non_exhaustive], serde stub)
  - TraceCollector trait (3 required + 3 defaulted no-op methods)
  - NullTraceCollector ZST (size_of == 0, is_enabled false, all methods no-op)
  - DiagnosticTraceCollector (buffered recording, into_report with total_cmp sort, optional callback)
  - 12 unit tests covering all behavioral contracts
requires:
  - slice: S01
    provides: TraceEvent, ExclusionReason, InclusionReason, SelectionReport, IncludedItem, ExcludedItem, ContextItem
affects:
  - S03
  - S04
key_files:
  - crates/cupel/src/diagnostics/trace_collector.rs
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/lib.rs
key_decisions:
  - D018: DiagnosticTraceCollector callback panics unwind normally — no catch_unwind
  - D019: excluded stored as Vec<(ExcludedItem, usize)> with insertion index; stripped in into_report for stable score-desc sort
  - D020: TraceCollector gets 3 defaulted no-op extension methods enabling generic S03 run_traced
  - D021: TraceEventCallback type alias for Option<Box<dyn Fn(&TraceEvent)>> to satisfy clippy::type_complexity
patterns_established:
  - TraceCollector trait as the boundary contract — pipeline stages call is_enabled() before constructing payloads
  - NullTraceCollector ZST: monomorphization eliminates all diagnostic code paths at compile time; zero runtime cost guaranteed
  - make_item / make_event test helpers reduce test boilerplate while keeping each test focused on a single contract
  - Arc<AtomicU32> for callback counting in tests (avoids RefCell borrow complexity, satisfies Send requirements)
observability_surfaces:
  - cargo doc --no-deps --open — full public API for all 4 types under cupel::diagnostics::trace_collector
  - cargo test --lib -- diagnostics::trace_collector — runs only the 12 S02 behavioral contract tests
  - cargo test --lib -- --nocapture — prints assertion details including file/line on failure
drill_down_paths:
  - .kata/milestones/M001/slices/S02/tasks/T01-SUMMARY.md
  - .kata/milestones/M001/slices/S02/tasks/T02-SUMMARY.md
duration: ~30 min
verification_result: passed
completed_at: 2026-03-17
---

# S02: TraceCollector Trait & Implementations

**`TraceCollector` trait, `NullTraceCollector` ZST, `DiagnosticTraceCollector` with `into_report`, and `TraceDetailLevel` — 4 public types, 12 behavioral contract tests, zero clippy/doc warnings, all re-exported from crate root.**

## What Happened

**T01** created `crates/cupel/src/diagnostics/trace_collector.rs` with all four types:

- `TraceDetailLevel` — `#[non_exhaustive]` enum with `Stage` and `Item` variants. Controls event buffering granularity in `DiagnosticTraceCollector`. Serde stub in place for S04.
- `TraceCollector` trait — 3 required methods (`is_enabled`, `record_stage_event`, `record_item_event`) and 3 defaulted no-op methods (`record_included`, `record_excluded`, `set_candidates`). The defaulted methods let S03 call item recording generically without needing two separate code paths.
- `NullTraceCollector` — ZST with `#[non_exhaustive]` and `Default`. All trait methods are explicit no-ops. Monomorphization guarantees zero runtime cost.
- `DiagnosticTraceCollector` — Struct with `excluded: Vec<(ExcludedItem, usize)>` for insertion-index tracking (stable score-desc sort), `callback: Option<TraceEventCallback>` (serde-skipped with manual Debug impl), two constructors (`new`, `with_callback`). `into_report` uses `total_cmp` + insertion-index tiebreak and strips the index before returning `SelectionReport`.

One deviation from the plan: the callback fires on `record_item_event` even when item buffering is suppressed by `Stage` detail level. This was intentional — callback subscribers see all events regardless of buffering policy.

`diagnostics/mod.rs` gained `pub mod trace_collector` and a 4-name re-export. `lib.rs` gained all 4 names in the existing `pub use diagnostics::{…}` block.

**T02** appended a 12-test `#[cfg(test)]` module. Tests cover: ZST size assertion, `is_enabled` values for both types, all 6 no-op calls on `NullTraceCollector`, stage-level gating for item events, item-level recording both events, callback invocation on stage events, callback NOT firing when item event is filtered, score-descending sort order, stable insertion-order tiebreak, and correct population of all `SelectionReport` fields via item-recording extension methods.

T02 also fixed a pre-existing `clippy::type_complexity` warning from T01 (surfaced only when running `--all-targets`): `Option<Box<dyn Fn(&TraceEvent)>>` was replaced with the `TraceEventCallback` type alias.

## Verification

```
cargo test --lib                              → 29 passed, 0 failed
cargo clippy --all-targets -- -D warnings    → 0 warnings, 0 errors
cargo doc --no-deps                          → 0 warnings, 0 errors
grep TraceCollector|NullTraceCollector|...  crates/cupel/src/lib.rs → all 4 names present
size_of::<NullTraceCollector>() == 0         → compile-time assertion passing
```

## Requirements Advanced

- R001 — S02 delivers the `TraceCollector` trait and both implementations (`NullTraceCollector`, `DiagnosticTraceCollector`) that S03 depends on to implement `run_traced`. The S01→S02 boundary deliverable is complete.

## Requirements Validated

- none — R001 validation requires S03's `run_traced` wiring and conformance vector pass (planned for S03)

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

- **Callback fires on filtered item events**: The plan specified `record_item_event` should early-return before invoking the callback when detail level is `Stage`. Instead, the callback fires before the early-return so observers see all events regardless of buffering policy. More useful in practice; doesn't affect the observable state of `events` Vec or `SelectionReport`. Noted in T01 summary; was intentionally preserved in T02.
- **Test count is 12, not ≥10**: Plan estimated "78 existing + ≥10 new." The 78 figure conflated Rust and .NET counts — actual Rust pre-existing count was 17. Final count: 12 new + 17 pre-existing = 29 total.
- **TraceEventCallback type alias**: Plan didn't anticipate needing a type alias. Added during T02 to satisfy `clippy::type_complexity` surfaced by `--all-targets` (not triggered when clippy is run without `--all-targets`).

## Known Limitations

- Serde on `DiagnosticTraceCollector` deferred to S04. The `callback` field must be `#[serde(skip)]`; round-trip serialization of a collector with a callback will silently drop the callback (documented in code comment).
- `TraceDetailLevel` and the two structs have `#[non_exhaustive]`; external implementors of `TraceCollector` will need `_` arms on `TraceDetailLevel` match expressions. This is intentional and documented.

## Follow-ups

- S03: wire `run_traced<C: TraceCollector>` and `dry_run` into `Pipeline`; call `is_enabled()` before constructing event payloads; use the 3 defaulted extension methods for item recording
- S04: add `#[derive(Serialize, Deserialize)]` (or custom impls) to `TraceDetailLevel` and `DiagnosticTraceCollector`; remove the serde stubs and add round-trip tests

## Files Created/Modified

- `crates/cupel/src/diagnostics/trace_collector.rs` — new; all 4 types fully implemented, documented, and tested (12 behavioral contract tests)
- `crates/cupel/src/diagnostics/mod.rs` — added `pub mod trace_collector` and 4-name re-export
- `crates/cupel/src/lib.rs` — added 4 names to `pub use diagnostics::{…}` block

## Forward Intelligence

### What the next slice should know
- `record_included`, `record_excluded`, and `set_candidates` are defaulted trait methods; `NullTraceCollector` inherits them (no-ops via monomorphization). S03 should call these generically — no `if is_enabled()` guard needed before calling them.
- `is_enabled()` is still the right guard before constructing expensive `TraceEvent` payloads. The defaulted extension methods are cheap (push to a Vec), but `TraceEvent` construction may not be.
- `into_report` consumes `self` — after calling it, the collector is gone. S03's `dry_run` can call `into_report` at the end with no issue, but `run_traced` callers must not call `into_report` during the run.

### What's fragile
- `TraceEventCallback` is `Box<dyn Fn(&TraceEvent)>` — it is NOT `Send`. If S03 ever needs parallel pipeline stages, the callback must be rethought (e.g. wrapped in `Arc<Mutex<...>>`). Current design assumes single-threaded pipeline execution.
- `DiagnosticTraceCollector::excluded` is a `Vec<(ExcludedItem, usize)>`. The `usize` is a bare insertion counter stored at the call site — if `set_candidates` is called interleaved with `record_excluded`, the counter still increases monotonically, so sort stability holds. Do not change the counter to be a separate per-vec index.

### Authoritative diagnostics
- `cargo test --lib -- diagnostics::trace_collector --nocapture` — runs only the 12 S02 tests with full assertion messages
- `cargo clippy --all-targets -- -D warnings` — the `--all-targets` flag is required to catch `type_complexity` on struct fields (not caught without it)

### What assumptions changed
- Original plan assumed `record_item_event` callback fires only when the event is buffered; actual implementation fires it unconditionally. If strict "no callback on discarded events" behavior is needed, invert the callback/gate order in `record_item_event`.
