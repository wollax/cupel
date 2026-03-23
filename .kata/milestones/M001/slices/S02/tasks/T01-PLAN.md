---
estimated_steps: 6
estimated_files: 3
---

# T01: Implement TraceCollector types and wire into module system

**Slice:** S02 — TraceCollector Trait & Implementations
**Milestone:** M001

## Description

Create `crates/cupel/src/diagnostics/trace_collector.rs` with all four public types — `TraceDetailLevel`, `TraceCollector` trait, `NullTraceCollector`, and `DiagnosticTraceCollector` — then wire them into the diagnostics module and crate-root re-exports. This task delivers the complete S01→S02 boundary contract: all types S03 will consume are in place, documented, and publicly accessible.

No unit tests are written in this task; behavioral verification is T02's responsibility. This task's passing condition is `cargo build` + `cargo doc --no-deps` with zero errors and zero warnings.

## Steps

1. **Create `crates/cupel/src/diagnostics/trace_collector.rs`**. Define `TraceDetailLevel`:
   ```rust
   #[non_exhaustive]
   #[derive(Debug, Clone, Copy, PartialEq, Eq)]
   #[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
   pub enum TraceDetailLevel { Stage, Item }
   ```
   Full doc comment explaining `Stage` captures stage-level events only; `Item` captures both.

2. **Define `TraceCollector` trait** with 3 required methods and 3 defaulted no-op methods. Full doc comment on the trait (not thread-safe; callers check `is_enabled` before constructing payloads) and each method:
   ```rust
   pub trait TraceCollector {
       fn is_enabled(&self) -> bool;
       fn record_stage_event(&mut self, event: TraceEvent);
       fn record_item_event(&mut self, event: TraceEvent);
       fn record_included(&mut self, _item: ContextItem, _score: f64, _reason: InclusionReason) {}
       fn record_excluded(&mut self, _item: ContextItem, _score: f64, _reason: ExclusionReason) {}
       fn set_candidates(&mut self, _total: usize, _total_tokens: i64) {}
   }
   ```
   Doc each defaulted method explaining it is a no-op default that `DiagnosticTraceCollector` overrides; `NullTraceCollector` uses the no-ops via monomorphization.

3. **Define `NullTraceCollector`** ZST:
   ```rust
   #[non_exhaustive]
   #[derive(Debug, Clone, Copy, Default)]
   #[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
   pub struct NullTraceCollector;
   ```
   Doc comment: state that `NullTraceCollector` is a zero-sized type — Rust monomorphization compiles away all event construction branches when `C = NullTraceCollector`; no allocations occur. Implement `TraceCollector`: `is_enabled` returns `false`; `record_stage_event` and `record_item_event` are explicit no-ops (empty bodies). The three defaulted methods are not overridden (the defaults are already no-ops).

4. **Define `DiagnosticTraceCollector`** struct and implementation:
   - Struct fields: `events: Vec<TraceEvent>`, `included: Vec<IncludedItem>`, `excluded: Vec<(ExcludedItem, usize)>` (tracks insertion index for stable sort), `total_candidates: usize`, `total_tokens_considered: i64`, `detail_level: TraceDetailLevel`, `callback: Option<Box<dyn Fn(&TraceEvent)>>`
   - `#[non_exhaustive]` on the struct; serde stub (`#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]` — note: will compile but may produce incorrect output until S04; add a doc comment warning)
   - Doc comment: not thread-safe; one instance per pipeline execution; a panicking callback unwinds normally (no `catch_unwind`)
   - Constructors: `pub fn new(detail_level: TraceDetailLevel) -> Self` and `pub fn with_callback(detail_level: TraceDetailLevel, callback: Box<dyn Fn(&TraceEvent)>) -> Self`
   - Implement `TraceCollector`:
     - `is_enabled` → `true`
     - `record_stage_event`: push to `self.events`, invoke callback
     - `record_item_event`: early-return if `!matches!(self.detail_level, TraceDetailLevel::Item)`, then push + invoke callback
     - `record_included(item, score, reason)`: push `IncludedItem { item, score, reason }` to `self.included`
     - `record_excluded(item, score, reason)`: push `(ExcludedItem { item, score, reason }, self.excluded.len())` to `self.excluded`
     - `set_candidates(total, total_tokens)`: set `self.total_candidates = total`, `self.total_tokens_considered = total_tokens`
   - `pub fn into_report(mut self) -> SelectionReport`: sort `self.excluded` by `|(a, ai), (b, bi)| b.score.total_cmp(&a.score).then_with(|| ai.cmp(bi))`, strip indices, construct and return `SelectionReport { events, included, excluded, total_candidates, total_tokens_considered }`
   - Private `fn invoke_callback(&self, event: &TraceEvent)`: calls `self.callback.as_ref().map(|cb| cb(event))`; callback panics unwind normally (no catch_unwind)

5. **Update `crates/cupel/src/diagnostics/mod.rs`**: add `pub mod trace_collector;` and `pub use trace_collector::{TraceDetailLevel, TraceCollector, NullTraceCollector, DiagnosticTraceCollector};`

6. **Update `crates/cupel/src/lib.rs`**: add `TraceCollector, TraceDetailLevel, NullTraceCollector, DiagnosticTraceCollector,` to the existing `pub use diagnostics::{…}` block.

## Must-Haves

- [ ] `trace_collector.rs` exists with all 4 types defined
- [ ] `TraceCollector` has exactly 3 required + 3 defaulted methods; doc comments on all
- [ ] `NullTraceCollector` is a ZST (`#[non_exhaustive]`, `Default` derive, no fields); doc comment explicitly states ZST zero-cost invariant
- [ ] `DiagnosticTraceCollector` has `excluded: Vec<(ExcludedItem, usize)>` (NOT `Vec<ExcludedItem>`) — required for stable sort
- [ ] `into_report` uses `total_cmp` (not `partial_cmp`) for `f64` sort
- [ ] `into_report` is consuming (`self`, not `&self`)
- [ ] Both constructors exist (`new` and `with_callback`)
- [ ] All 4 names appear in `diagnostics/mod.rs` re-exports
- [ ] All 4 names appear in `lib.rs` `pub use diagnostics::{…}` block
- [ ] `cargo build` passes with zero errors
- [ ] `cargo doc --no-deps 2>&1 | grep -E "warning|error"` outputs nothing

## Verification

- `cargo build 2>&1 | grep "^error"` — empty output
- `cargo doc --no-deps 2>&1 | grep -E "warning|error"` — empty output
- `grep 'TraceCollector\|NullTraceCollector\|DiagnosticTraceCollector\|TraceDetailLevel' crates/cupel/src/lib.rs` — all 4 names present
- `grep 'pub mod trace_collector\|pub use trace_collector' crates/cupel/src/diagnostics/mod.rs` — both lines present

## Observability Impact

- Signals added/changed: None (this task adds library types; no runtime process)
- How a future agent inspects this: `cargo doc --no-deps --open` renders full API for all 4 types; `grep -r 'TraceCollector' crates/cupel/src/` confirms all wiring
- Failure state exposed: `cargo build` errors name the file, line, and type mismatch; `cargo doc` warnings name broken doc-links by file and symbol

## Inputs

- `crates/cupel/src/diagnostics/mod.rs` — imports from `super::` inside `trace_collector.rs`: `TraceEvent`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, `IncludedItem`, `ExcludedItem` (all from S01)
- `crates/cupel/src/lib.rs` — existing `pub use diagnostics::{…}` block to extend
- S02-RESEARCH.md — trait design, struct fields, sort contract, module layout, pitfall list

## Expected Output

- `crates/cupel/src/diagnostics/trace_collector.rs` — new file; all 4 types fully implemented and documented
- `crates/cupel/src/diagnostics/mod.rs` — `pub mod trace_collector` line + 4-name re-export line added
- `crates/cupel/src/lib.rs` — 4 names added to `pub use diagnostics::{…}` block
