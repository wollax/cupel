# S02: TraceCollector Trait & Implementations

**Goal:** `TraceCollector` trait, `NullTraceCollector` (ZST), `DiagnosticTraceCollector` (buffered recording with `TraceDetailLevel`), and `into_report(self) → SelectionReport` are implemented, tested, documented, and re-exported from the crate root.
**Demo:** `cargo test --lib` passes with behavioral contract tests covering: ZST invariant, `is_enabled` values, stage/item event gating, `into_report()` sort contract, callback invocation, and all three defaulted item-recording methods populating the report correctly.

## Must-Haves

- `TraceDetailLevel` enum (`Stage`, `Item`) with `#[non_exhaustive]` and serde stub
- `TraceCollector` trait — 3 required methods (`is_enabled`, `record_stage_event`, `record_item_event`) + 3 defaulted no-op methods (`record_included`, `record_excluded`, `set_candidates`)
- `NullTraceCollector` — ZST (`size_of == 0`), `is_enabled → false`, all methods no-ops, `Default` derive, doc comment states ZST zero-cost invariant
- `DiagnosticTraceCollector` — `is_enabled → true`, `record_stage_event` always records, `record_item_event` gated by `detail_level >= Item`, overrides all 3 defaulted item-recording methods, `into_report(self) → SelectionReport` with `total_cmp` score-desc sort (stable by insertion order on ties), optional `Box<dyn Fn(&TraceEvent)>` callback invoked synchronously
- Two constructors: `new(detail_level)` and `with_callback(detail_level, callback)`
- All 4 types live in `crates/cupel/src/diagnostics/trace_collector.rs`
- `pub mod trace_collector` + re-exports in `diagnostics/mod.rs`
- `TraceCollector`, `TraceDetailLevel`, `NullTraceCollector`, `DiagnosticTraceCollector` re-exported from `lib.rs`
- Unit tests covering all behavioral contracts (inline `#[cfg(test)]` in `trace_collector.rs`)
- `cargo test --lib`, `cargo doc --no-deps`, `cargo clippy --all-targets -- -D warnings` all pass with zero warnings/errors

## Proof Level

- This slice proves: contract
- Real runtime required: no (unit tests with constructed types)
- Human/UAT required: no

## Verification

- `cargo test --lib 2>&1 | grep -E "FAILED|error"` — zero failures
- `cargo doc --no-deps 2>&1 | grep -E "warning|error"` — zero warnings
- `cargo clippy --all-targets -- -D warnings 2>&1 | grep -E "warning|error"` — zero warnings
- `grep -r 'TraceCollector\|NullTraceCollector\|DiagnosticTraceCollector\|TraceDetailLevel' crates/cupel/src/lib.rs` — all four names appear in re-exports
- Unit test `size_of::<NullTraceCollector>() == 0` verifies ZST invariant at compile time

## Observability / Diagnostics

- Runtime signals: None (library types, no runtime process)
- Inspection surfaces: `cargo doc --no-deps --open` exposes full public API for all 4 types; `cargo test -- --nocapture` shows test names and any panic output
- Failure visibility: cargo test output names the failing test and line; `cargo clippy --all-targets` names the lint and file location
- Redaction constraints: None

## Integration Closure

- Upstream surfaces consumed: `crates/cupel/src/diagnostics/mod.rs` — `TraceEvent`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, `IncludedItem`, `ExcludedItem` (all from S01); `crates/cupel/src/model` — `ContextItem`
- New wiring introduced in this slice: `pub mod trace_collector` in `diagnostics/mod.rs`; re-exports of all 4 types in `lib.rs`; unit tests verify behavioral contracts
- What remains before the milestone is truly usable end-to-end: S03 (wire `run_traced<C: TraceCollector>` and `dry_run` into `Pipeline`; conformance harness coverage of `expected.diagnostics.*`)

## Tasks

- [x] **T01: Implement TraceCollector types and wire into module system** `est:45m`
  - Why: Creates the 4 public types that S03 depends on; establishes the trait contract and module exports that complete the S01→S02 boundary deliverable
  - Files: `crates/cupel/src/diagnostics/trace_collector.rs` (new), `crates/cupel/src/diagnostics/mod.rs`, `crates/cupel/src/lib.rs`
  - Do: Create `trace_collector.rs`; define `TraceDetailLevel` with `#[non_exhaustive]` + serde stub; define `TraceCollector` trait with 3 required + 3 defaulted no-op methods (full doc comments on all); define `NullTraceCollector` ZST with `Default` + doc comment stating ZST zero-cost guarantee; define `DiagnosticTraceCollector` struct with `events: Vec<TraceEvent>`, `included: Vec<IncludedItem>`, `excluded: Vec<(ExcludedItem, usize)>` (insertion-index tuple for stable sort), `total_candidates: usize`, `total_tokens_considered: i64`, `detail_level: TraceDetailLevel`, `callback: Option<Box<dyn Fn(&TraceEvent)>>`; implement `new` and `with_callback` constructors; implement all trait methods (record_stage always, record_item gated by `matches!(detail_level, Item)`, record_included/record_excluded/set_candidates buffer item data); implement `into_report(self) → SelectionReport` with `total_cmp` sort (`b.score.total_cmp(&a.score).then_with(|| ai.cmp(bi))`), stripping insertion index before returning; add `#[non_exhaustive]` to both structs; add serde stub to `DiagnosticTraceCollector` struct; add `pub mod trace_collector` and `pub use trace_collector::{…}` in `diagnostics/mod.rs`; add all 4 names to the `pub use diagnostics::{…}` block in `lib.rs`
  - Verify: `cargo build 2>&1 | grep -E "^error"` — zero errors; `cargo doc --no-deps 2>&1 | grep -E "warning|error"` — zero warnings
  - Done when: crate compiles with zero errors and zero doc warnings; all 4 types are visible in `cargo doc` output

- [x] **T02: Unit tests for all behavioral contracts** `est:30m`
  - Why: Proves every behavioral invariant stated in the must-haves; serves as the executable specification S03 can rely on; ZST size assertion provides compile-time regression guard
  - Files: `crates/cupel/src/diagnostics/trace_collector.rs` (add `#[cfg(test)]` block)
  - Do: Add `#[cfg(test)]` module to `trace_collector.rs` with the following tests: (1) `null_is_zst` — `assert_eq!(std::mem::size_of::<NullTraceCollector>(), 0)`; (2) `null_is_not_enabled` — `NullTraceCollector.is_enabled() == false`; (3) `null_record_methods_are_noop` — call all 6 trait methods on `NullTraceCollector`, no panic; (4) `diagnostic_is_enabled` — `DiagnosticTraceCollector::new(Stage).is_enabled() == true`; (5) `stage_level_only_records_stage_events` — create collector with `Stage`, call `record_stage_event` and `record_item_event`, check `into_report().events.len() == 1`; (6) `item_level_records_both` — create collector with `Item`, call both record methods, check `events.len() == 2`; (7) `into_report_sort_contract_score_desc` — record_excluded two items with scores 5.0 and 2.0, check `excluded[0].score == 5.0`; (8) `into_report_sort_stable_on_tie` — record_excluded two items with same score, check insertion order preserved (first recorded appears at index 0); (9) `callback_invoked_on_stage_event` — create with callback that pushes to a counter, call `record_stage_event`, verify counter == 1; (10) `item_recording_populates_report` — call `record_included`, `record_excluded`, `set_candidates`, verify `into_report()` contains correct `included`, `excluded`, `total_candidates`, `total_tokens_considered`; use `cargo test --lib -- --nocapture` to see test names
  - Verify: `cargo test --lib 2>&1 | grep -E "FAILED|^error"` — zero failures; `cargo clippy --all-targets -- -D warnings 2>&1 | grep -E "^warning|^error"` — zero warnings
  - Done when: all unit tests pass; clippy and doc emit zero warnings; ZST size assertion is present and passing

## Files Likely Touched

- `crates/cupel/src/diagnostics/trace_collector.rs` — new; all 4 types + `#[cfg(test)]` unit tests
- `crates/cupel/src/diagnostics/mod.rs` — add `pub mod trace_collector` + re-exports
- `crates/cupel/src/lib.rs` — add 4 names to `pub use diagnostics::{…}` block
