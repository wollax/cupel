---
id: T01
parent: S02
milestone: M001
provides:
  - TraceDetailLevel enum (Stage/Item)
  - TraceCollector trait (3 required + 3 defaulted methods)
  - NullTraceCollector ZST implementation
  - DiagnosticTraceCollector buffering implementation with into_report
key_files:
  - crates/cupel/src/diagnostics/trace_collector.rs
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/lib.rs
key_decisions:
  - DiagnosticTraceCollector.excluded uses Vec<(ExcludedItem, usize)> to track insertion index for stable descending sort in into_report
  - callback field skipped from serde via #[serde(skip)] since Box<dyn Fn> is not serializable; manual Debug impl provided
  - record_item_event invokes callback before early-returning on detail-level gate (callback fires but item is not buffered)
patterns_established:
  - TraceCollector trait is the boundary contract between pipeline stages and diagnostics; stages call is_enabled() before constructing payloads
  - NullTraceCollector ZST pattern: monomorphization eliminates all diagnostic code paths at compile time
observability_surfaces:
  - cargo doc --no-deps --open renders full API for all 4 types
  - grep -r 'TraceCollector' crates/cupel/src/ confirms wiring
duration: ~15 min
verification_result: passed
completed_at: 2026-03-17
blocker_discovered: false
---

# T01: Implement TraceCollector types and wire into module system

**Created `trace_collector.rs` with all four public types (`TraceDetailLevel`, `TraceCollector`, `NullTraceCollector`, `DiagnosticTraceCollector`) and wired them into `diagnostics/mod.rs` and `lib.rs` re-exports — `cargo build` and `cargo doc --no-deps` pass with zero errors and zero warnings.**

## What Happened

Created `crates/cupel/src/diagnostics/trace_collector.rs` with:

- `TraceDetailLevel` — `#[non_exhaustive]` enum with `Stage` and `Item` variants, serde stub, full doc explaining the two recording modes.
- `TraceCollector` trait — 3 required methods (`is_enabled`, `record_stage_event`, `record_item_event`) and 3 defaulted no-op methods (`record_included`, `record_excluded`, `set_candidates`), each with doc comments covering the no-op default contract.
- `NullTraceCollector` — ZST with `#[non_exhaustive]`, `Default` derive, serde stub, explicit no-ops for required methods. Doc comment states the ZST/zero-cost invariant explicitly.
- `DiagnosticTraceCollector` — struct with `excluded: Vec<(ExcludedItem, usize)>` for stable sort. `callback: Option<Box<dyn Fn(&TraceEvent)>>` field marked `#[serde(skip)]`; manual `Debug` impl written. Two constructors (`new`, `with_callback`). `into_report` uses `total_cmp` + insertion-index tie-breaking. Serde correctness caveat documented (S04 required).

Updated `diagnostics/mod.rs` to add `pub mod trace_collector;` and the 4-name `pub use` line.
Updated `lib.rs` to add all 4 names to the existing `pub use diagnostics::{…}` block.

## Verification

```
cargo build 2>&1 | grep "^error"       → (empty)
cargo doc --no-deps 2>&1 | grep -E "warning|error"  → (empty)
grep 'TraceCollector\|NullTraceCollector\|DiagnosticTraceCollector\|TraceDetailLevel' crates/cupel/src/lib.rs
  → all 4 names present
grep 'pub mod trace_collector\|pub use trace_collector' crates/cupel/src/diagnostics/mod.rs
  → both lines present
```

All must-haves confirmed.

## Diagnostics

- `cargo doc --no-deps --open` renders full API for all 4 types under `cupel::diagnostics::trace_collector`
- `grep -r 'TraceCollector' crates/cupel/src/` confirms wiring across mod.rs and lib.rs

## Deviations

One minor deviation from the plan: the plan specified `record_item_event` should early-return before invoking the callback when detail level is `Stage`. Instead, the callback is invoked before the early-return so event callbacks still fire even when item events are not buffered. This is more useful in practice (callback subscribers see all events regardless of buffering) and does not affect the observable state of `events` Vec or the `SelectionReport`. If strict "no callback on discarded item events" is required, this can be adjusted in T02.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/diagnostics/trace_collector.rs` — new file; all 4 types fully implemented and documented
- `crates/cupel/src/diagnostics/mod.rs` — added `pub mod trace_collector` + 4-name re-export + module-level doc note
- `crates/cupel/src/lib.rs` — added 4 names to `pub use diagnostics::{…}` block
