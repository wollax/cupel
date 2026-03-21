# S02: TraceCollector Trait & Implementations — Research

**Date:** 2026-03-17

## Summary

S02 introduces the `TraceCollector` trait plus two concrete implementations (`NullTraceCollector`, `DiagnosticTraceCollector`) and the `TraceDetailLevel` enum. All four types live in a new `crates/cupel/src/diagnostics/trace_collector.rs` submodule and are re-exported from `crate::diagnostics` and `lib.rs`.

The central design decision is how `DiagnosticTraceCollector::into_report()` obtains the item-level data needed to populate `SelectionReport.included` and `SelectionReport.excluded`. The .NET reference solves this with an internal `ReportBuilder` that the pipeline populates via an `is DiagnosticTraceCollector` type check. Rust cannot do this generically. The clean Rust solution is to **extend the `TraceCollector` trait with three additional default no-op methods** (`record_included`, `record_excluded`, `set_candidates`) so S03's `run_traced<C: TraceCollector>` can call them generically, they compile to no-ops for `NullTraceCollector`, and `DiagnosticTraceCollector` overrides them to buffer item data for `into_report()`. The spec defines the minimum 3-method contract; the extra defaulted methods are additive.

All types need `#[non_exhaustive]` + `#[cfg_attr(feature = "serde", ...)]` stubs consistent with S01 patterns. The `SelectionReport` serde gap from S01 (deferred to S04) limits testing: S02 tests must not attempt to serialize `SelectionReport` under `--features serde`.

## Recommendation

**Create `crates/cupel/src/diagnostics/trace_collector.rs`** and define:
1. `TraceDetailLevel` enum (`Stage = 0`, `Item = 1`)
2. `TraceCollector` trait — 3 required methods + 3 defaulted item-recording methods
3. `NullTraceCollector` — ZST with `is_enabled → false`, no-op record methods
4. `DiagnosticTraceCollector` — buffers events + item data, `into_report(self) → SelectionReport`

Add `pub mod trace_collector;` + re-exports in `diagnostics/mod.rs`, and `pub use` in `lib.rs`.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Stable sort for `excluded` by score desc | `sort_by` with a closure comparing `(b_score, a_insertion_idx)` | Already available in std; achieves score-desc with insertion-order tiebreak matching the spec's "stable by insertion order on ties" requirement |
| Module layout for diagnostic types | Established in `diagnostics/mod.rs` (S01) | Introduces a second file under `diagnostics/`, consistent with the existing `pipeline/` module pattern |
| `#[non_exhaustive]` + serde stubs | Established by S01 pattern on all 8 data types | `TraceDetailLevel` gets the same treatment; `NullTraceCollector` and `DiagnosticTraceCollector` are concrete impl types (not data types), so `#[non_exhaustive]` applies to the structs themselves |

## Existing Code and Patterns

- `crates/cupel/src/diagnostics/mod.rs` — all 8 diagnostic data types (`TraceEvent`, `ExclusionReason`, etc.). S02 adds to the `diagnostics` namespace; must import these types from `super::` inside `trace_collector.rs`
- `crates/cupel/src/lib.rs` — re-exports pattern; S02 adds `TraceCollector`, `NullTraceCollector`, `DiagnosticTraceCollector`, `TraceDetailLevel` to the `pub use diagnostics::{…}` block
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — Rust trait maps 1:1; `bool IsEnabled { get; }` → `fn is_enabled(&self) -> bool`; `RecordStageEvent(TraceEvent)` → `fn record_stage_event(&mut self, event: TraceEvent)`; `RecordItemEvent(TraceEvent)` → `fn record_item_event(&mut self, event: TraceEvent)`
- `src/Wollax.Cupel/Diagnostics/NullTraceCollector.cs` — `sealed class` with private constructor and `static readonly Instance`. Rust ZST: `pub struct NullTraceCollector;` with `Default` impl. No singleton needed (ZST is zero-cost to construct), but a `pub const DEFAULT: Self = NullTraceCollector;` would be ergonomic.
- `src/Wollax.Cupel/Diagnostics/DiagnosticTraceCollector.cs` — buffers `List<TraceEvent>` + callback. The `ReportBuilder` is **internal to the pipeline** (`CupelPipeline.cs`), not part of `DiagnosticTraceCollector`. For Rust, item recording goes on `DiagnosticTraceCollector` directly via the extra trait methods.
- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — `.NET` builds the report inside the pipeline. Rust equivalent: the item recording happens via trait methods on `DiagnosticTraceCollector` itself; `into_report()` sorts and constructs the `SelectionReport`.
- `src/Wollax.Cupel/CupelPipeline.cs` — shows how `is DiagnosticTraceCollector` is used to conditionally build `ReportBuilder`. Rust equivalent: no type check needed since `run_traced<C: TraceCollector>` monomorphizes; S03 calls `collector.record_included(…)` generically (NullTraceCollector no-ops it, DiagnosticTraceCollector buffers it).

## Trait Design (critical detail)

The spec defines 3 required methods. Three additional **defaulted** methods are needed for item tracking:

```rust
pub trait TraceCollector {
    // Spec-defined required methods:
    fn is_enabled(&self) -> bool;
    fn record_stage_event(&mut self, event: TraceEvent);
    fn record_item_event(&mut self, event: TraceEvent);

    // Defaulted item-recording methods — no-ops for NullTraceCollector;
    // DiagnosticTraceCollector overrides all three:
    fn record_included(&mut self, _item: ContextItem, _score: f64, _reason: InclusionReason) {}
    fn record_excluded(&mut self, _item: ContextItem, _score: f64, _reason: ExclusionReason) {}
    fn set_candidates(&mut self, _total: usize, _total_tokens: i64) {}
}
```

**Why defaulted methods instead of a second trait or concrete methods only:**
- S03's `run_traced<C: TraceCollector>` needs to call item recording generically without knowing the concrete type. With defaulted trait methods, this works for any `C: TraceCollector`.
- `NullTraceCollector` uses the no-ops and monomorphizes to zero overhead.
- `DiagnosticTraceCollector` overrides all three to buffer data.
- No dynamic dispatch / type-checking needed in S03.
- The spec defines the minimum required contract; the defaulted extensions are additive and Rust-idiomatic.

**Rejected alternative: separate `DiagnosticTraceCollector` concrete methods only** — requires S03 to have two code paths (one for `DiagnosticTraceCollector`, one for `NullTraceCollector`), undermines the generic `run_traced<C>` design, and adds complexity.

## `DiagnosticTraceCollector` internal state

```rust
pub struct DiagnosticTraceCollector {
    events: Vec<TraceEvent>,
    included: Vec<IncludedItem>,
    excluded: Vec<(ExcludedItem, usize)>, // (item, insertion_index) for stable sort
    total_candidates: usize,
    total_tokens_considered: i64,
    detail_level: TraceDetailLevel,
    callback: Option<Box<dyn Fn(&TraceEvent)>>,
}
```

- `excluded` stores a tuple `(ExcludedItem, usize)` to track insertion order for stable sort. `into_report()` sorts by `(score desc, insertion_index asc)` then strips the index.
- `callback: Option<Box<dyn Fn(&TraceEvent)>>` — not `Send` (same constraint as .NET: "not thread-safe"). Called synchronously from `record_stage_event` and `record_item_event` when recording occurs.
- If the callback panics, it unwinds normally (no try-catch needed; observation-side panics are the caller's responsibility — document this in the doc comment).

## `into_report()` sort contract

`SelectionReport.excluded` must be sorted by score descending, stable by insertion order on ties (spec conformance note). Implementation:

```rust
self.excluded.sort_by(|(a, ai), (b, bi)| {
    b.score.total_cmp(&a.score).then_with(|| ai.cmp(bi))
});
let excluded = self.excluded.into_iter().map(|(item, _)| item).collect();
```

Use `total_cmp` (not partial_cmp) for `f64` — handles NaN consistently and avoids panic.

## `NullTraceCollector` zero-cost invariant

Rust monomorphization ensures that when `C = NullTraceCollector`, the compiler inlines `is_enabled()` as `false` and eliminates all event construction branches. The doc comment should state this invariant explicitly. A compile-time test (`assert_eq!(std::mem::size_of::<NullTraceCollector>(), 0)`) verifies ZST status.

## `TraceDetailLevel` spec mapping

| Spec value | Rust variant | Ordinal |
|------------|-------------|---------|
| `Stage` | `Stage` | 0 |
| `Item` | `Item` | 1 |

`record_item_event` in `DiagnosticTraceCollector` gates on `self.detail_level >= TraceDetailLevel::Item`. Implement `PartialOrd`/`Ord` on `TraceDetailLevel` by deriving. Alternatively use a direct match:
```rust
if matches!(self.detail_level, TraceDetailLevel::Item) { ... }
```
Simple match is clearer given only 2 variants and avoids implementing ordering traits.

## Module and file plan

```
crates/cupel/src/diagnostics/
  mod.rs          ← existing (S01 types); add `pub mod trace_collector;` + re-exports
  trace_collector.rs  ← NEW: TraceDetailLevel, TraceCollector, NullTraceCollector, DiagnosticTraceCollector
```

`diagnostics/mod.rs` adds:
```rust
pub mod trace_collector;
pub use trace_collector::{TraceDetailLevel, TraceCollector, NullTraceCollector, DiagnosticTraceCollector};
```

`lib.rs` adds to the `pub use diagnostics::{…}` block:
```rust
TraceCollector, TraceDetailLevel, NullTraceCollector, DiagnosticTraceCollector,
```

## Constraints

- Zero external dependencies — `Box<dyn Fn(&TraceEvent)>` and `Vec` are std; no issue
- MSRV 1.85.0 / Edition 2024 — all constructs used are stable on 1.85
- `#[non_exhaustive]` required on all public structs/enums: `TraceDetailLevel`, `NullTraceCollector`, `DiagnosticTraceCollector` (not the trait itself — traits cannot be `#[non_exhaustive]`)
- `SelectionReport` has no serde derive until S04 — S02 unit tests must not attempt `serde_json::to_string(&report)` under `--features serde`
- `ExclusionReason` serde is also deferred to S04 (D017) — same constraint applies
- `DiagnosticTraceCollector` is not thread-safe by design; document this in the doc comment (matches .NET precedent)
- `cfg_attr(feature = "serde", ...)` stubs: `TraceDetailLevel` and the two concrete struct types get the standard stubs from S01 pattern. The trait `TraceCollector` does NOT get a serde annotation.

## Common Pitfalls

- **Forgetting insertion index for stable sort** — if `excluded` is stored as `Vec<ExcludedItem>` without the index, `into_report()` cannot stably sort by insertion order when scores tie. Store as `Vec<(ExcludedItem, usize)>` and strip the index during `into_report()`.
- **`f64::partial_cmp` vs `total_cmp`** — `f64::partial_cmp` returns `None` for NaN comparisons, causing panics in sort. Always use `total_cmp` when sorting `f64` scores.
- **Consuming vs borrowing `into_report`** — the boundary map specifies `into_report(self) → SelectionReport` (consuming). The dry_run path in S03 creates a local `DiagnosticTraceCollector` and consumes it. The `run_traced` caller creates their own `DiagnosticTraceCollector` and calls `into_report()` on it after the call. Both patterns work with `self`. Do NOT add `&self → SelectionReport` — it would require cloning all item vectors.
- **`NullTraceCollector` ZST** — if any fields are accidentally added to `NullTraceCollector`, it stops being a ZST. The compile-time size assertion catches this.
- **Re-exporting `TraceCollector` from `lib.rs`** — the trait must be re-exported for callers to use `impl TraceCollector` in their own code. Easy to forget since traits are often imported only for method dispatch.
- **Observer callback exception policy** — the .NET implementation silently swallows callback panics. Rust panics unwind normally. Document that a panicking callback aborts the pipeline (this is Rust convention); don't add a `catch_unwind` wrapper, which requires `UnwindSafe` bounds.

## Open Risks

- **`Box<dyn Fn>` for callback vs generic F** — using `Box<dyn Fn(&TraceEvent)>` keeps the `DiagnosticTraceCollector` type simple (no type parameter), making it easy to name and store. A generic `F: Fn(&TraceEvent)` approach would produce different types for different callbacks, complicating naming. Stick with `Box<dyn Fn>`.
- **`record_included` / `record_excluded` in the trait** — while defaulted methods are additive, adding methods to a public trait is technically a breaking change for downstream types that implement the trait manually. However, because the default implementations are no-ops, existing implementations compile without changes. The risk is low for a library at this stage, and the alternative (no generic item recording) requires worse design in S03.
- **`DiagnosticTraceCollector` construction API** — needs at least a `new(detail_level)` and a `with_callback(detail_level, callback)` constructor. Whether to use a builder or separate constructors is a minor ergonomics decision; separate constructors (like .NET) are simpler for two parameters.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust (core language) | No skill needed | n/a — standard library only |

## Sources

- `spec/src/diagnostics/trace-collector.md` — canonical TraceCollector spec: 3 required methods, NullTraceCollector contract (is_enabled false, no alloc), DiagnosticTraceCollector behavior (record_stage always, record_item gated by detail_level), observer callback semantics
- `spec/src/diagnostics/selection-report.md` — excluded sort order (score desc, insertion order stable tiebreak), `into_report()` pseudo-code (`collector.BUILD-REPORT()`), `into_report()` must return complete SelectionReport
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — .NET interface reference (1:1 mapping to Rust trait's 3 required methods)
- `src/Wollax.Cupel/Diagnostics/DiagnosticTraceCollector.cs` — .NET implementation: `List<TraceEvent>` buffer, `Action<TraceEvent>?` callback, `TraceDetailLevel` detail level, `Events` property (read-only list). Rust departs by adding item-recording via trait methods.
- `src/Wollax.Cupel/Diagnostics/NullTraceCollector.cs` — singleton pattern; Rust uses ZST (no singleton needed)
- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — internal pipeline helper in .NET; Rust replaces with extended trait methods on DiagnosticTraceCollector
- `src/Wollax.Cupel/CupelPipeline.cs` — shows full pipeline wiring: `is DiagnosticTraceCollector` check, `ReportBuilder` lifecycle, when `record_included`/`record_excluded` are called at each stage, `totalTokensConsidered` and `totalCandidates` accounting
- `crates/cupel/src/diagnostics/mod.rs` — S01 output; all 8 diagnostic types to import from `super::` in trace_collector.rs
- `crates/cupel/src/pipeline/mod.rs` — current `run()` implementation; S03 will add `run_traced()` using the collector defined in S02
- `.kata/DECISIONS.md` — D001 (per-invocation ownership), D003 (NullTraceCollector ZST), D017 (serde deferred to S04)
