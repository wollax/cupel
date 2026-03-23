---
id: S01
parent: M001
milestone: M001
provides:
  - All 8 public diagnostic types in crates/cupel/src/diagnostics/mod.rs (PipelineStage, TraceEvent, OverflowEvent, ExclusionReason, InclusionReason, IncludedItem, ExcludedItem, SelectionReport)
  - pub mod diagnostics + re-exports of all 8 types in lib.rs
  - 4 new diagnostics conformance vectors in spec/conformance/required/pipeline/ and crates/cupel/conformance/required/pipeline/ (5 total including pre-existing diagnostics-budget-exceeded.toml)
requires: []
affects:
  - slice: S02
    provides: TraceEvent, ExclusionReason, InclusionReason, SelectionReport, IncludedItem, ExcludedItem — all consumed by TraceCollector trait and DiagnosticTraceCollector
  - slice: S03
    provides: All diagnostic types + conformance vectors (S03 wires the harness)
  - slice: S04
    provides: All diagnostic types with serde stubs (S04 adds custom impls)
key_files:
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/lib.rs
  - spec/conformance/required/pipeline/diag-negative-tokens.toml
  - spec/conformance/required/pipeline/diag-deduplicated.toml
  - spec/conformance/required/pipeline/diag-pinned-override.toml
  - spec/conformance/required/pipeline/diag-scored-inclusion.toml
  - crates/cupel/conformance/required/pipeline/diag-negative-tokens.toml
  - crates/cupel/conformance/required/pipeline/diag-deduplicated.toml
  - crates/cupel/conformance/required/pipeline/diag-pinned-override.toml
  - crates/cupel/conformance/required/pipeline/diag-scored-inclusion.toml
key_decisions:
  - D004 — data-carrying ExclusionReason variants (referenced, not new)
  - D005 — reserved ExclusionReason variants (referenced, not new)
  - D015 — #[non_exhaustive] on all public types (referenced, not new)
  - D016 — S01 verification is contract-level only (compile + test + doc + clippy + drift)
  - D017 — ExclusionReason serde deferred to S04; stubs with // custom serde impl comment (referenced)
patterns_established:
  - "#[non_exhaustive] + cfg_attr serde stub on all 7 of 8 diagnostic types (SelectionReport intentionally omits serde derive until S04 custom impl is ready)"
  - "ExclusionReason data-carrying enum with // custom serde impl in S04 — adjacent-tagged wire format comment"
  - "diag-*.toml vectors follow diagnostics-budget-exceeded.toml schema: [test], [budget], [config], [[config.scorers]], [[items]], [[expected_output]], [expected.diagnostics.summary], [[expected.diagnostics.included]], [[expected.diagnostics.excluded]]"
  - "PinnedOverride scenario: greedy slicer subtracts pinned tokens from effective_target pre-slice, so PinnedOverride is a Slice-stage exclusion; S03 diagnostics layer maps BudgetExceeded-caused-by-pinned to PinnedOverride"
observability_surfaces:
  - "cargo doc --no-deps --open — full public API surface for diagnostics module"
  - "diff -r spec/conformance/ crates/cupel/conformance/ — drift detection for vector copies"
  - "grep -r 'ExclusionReason\\|SelectionReport' crates/cupel/src/ — confirms types are wired"
  - "cat spec/conformance/required/pipeline/diag-*.toml — readable scenario docs with stage traces"
drill_down_paths:
  - .kata/milestones/M001/slices/S01/tasks/T01-SUMMARY.md
  - .kata/milestones/M001/slices/S01/tasks/T02-SUMMARY.md
duration: ~1 hour (T01 ~15m, T02 short)
verification_result: passed
completed_at: 2026-03-17
---

# S01: Diagnostics Data Types

**All 8 diagnostic types compiled and documented; 5 diagnostics conformance vectors exist in both `spec/` and `crates/` with zero drift.**

## What Happened

**T01** created `crates/cupel/src/diagnostics/mod.rs` from scratch with all 8 public types in definition order: `PipelineStage`, `TraceEvent`, `OverflowEvent`, `ExclusionReason`, `InclusionReason`, `IncludedItem`, `ExcludedItem`, `SelectionReport`. All types carry `#[non_exhaustive]` and full doc comments. Seven of the eight types carry `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]` stubs — `SelectionReport` intentionally omits the serde derive because `ExclusionReason`'s custom adjacent-tagged serde impl (D017, S04) must be correct first. `ExclusionReason` has the `// custom serde impl in S04 — adjacent-tagged wire format` comment. `ExclusionReason` defines all 8 variants (4 active: `BudgetExceeded`, `NegativeTokens`, `Deduplicated`, `PinnedOverride`; 4 reserved: `_Reserved1`–`_Reserved4`). `ExcludedItem.score` is `f64` (not `Option<f64>`) per the task plan. `lib.rs` was updated with `pub mod diagnostics;` and re-exports of all 8 types.

One doc-link fix was required: a `OverflowStrategy::Proceed` cross-crate doc link in `OverflowEvent` emitted an unresolved-link warning — replaced with prose.

**T02** authored 4 new diagnostics conformance vectors after reading `diagnostics-budget-exceeded.toml` and `pinned-items.toml` as schema references. Each vector traces the full 5-stage pipeline and includes `[expected.diagnostics.*]` sections as S03 spec targets:

- `diag-negative-tokens.toml` — item with token_count < 0 excluded at Classify stage (`NegativeTokens`)
- `diag-deduplicated.toml` — duplicate item excluded at Deduplicate stage (`Deduplicated`)
- `diag-pinned-override.toml` — regular item excluded because pinned budget consumption fills effective_target before slicer can include it (`PinnedOverride`); includes detailed S03 implementation note about the Slice-stage trigger
- `diag-scored-inclusion.toml` — two items both included, both with `Scored` inclusion reason

All 4 vectors were copied verbatim to `crates/cupel/conformance/required/pipeline/`. The `diag-pinned-override.toml` scenario required careful pipeline analysis: with the greedy slicer, `compute_effective_budget` subtracts pinned tokens from `effective_target`, so `sliced_tokens ≤ effective_target = target − pinned_tokens`, meaning Place/Truncate overflow is never reached. The regular item is excluded at the Slice stage. The S03 diagnostics layer should detect that the Slice BudgetExceeded was caused by pinned budget consumption and emit `PinnedOverride` accordingly.

## Verification

```
cargo test (all)                     → 78 passed (33 doctests + 28 conformance + 17 unit), 0 failed ✓
cargo doc --no-deps                  → DOC OK (0 warnings) ✓
cargo clippy --all-targets -D warns  → Finished (0 warnings) ✓
diff -rq spec/…/pipeline/ crates/…/pipeline/  → (no output, exit 0) DRIFT OK ✓
ls spec/…/diag*.toml | wc -l        → 5 ✓
```

## Requirements Advanced

- R001 (Rust diagnostics parity) — S01 delivers all diagnostic data types; parity requires S02 (TraceCollector) and S03 (run_traced) to complete
- R006 (Diagnostics serde coverage) — S01 adds serde stubs to 7 of 8 types; S04 adds the remaining custom impl for SelectionReport/ExclusionReason

## Requirements Validated

- None — S01 provides type definitions only; validation requires S03 (runtime wiring + conformance harness coverage of `expected.diagnostics.*`)

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

- **SelectionReport serde stub omitted**: Task plan listed serde stubs as required on "all 8 types". `SelectionReport` intentionally omits the `cfg_attr` derive because adding a broken derived serde impl before `ExclusionReason`'s custom adjacent-tagged impl (D017/S04) would produce incorrect wire output. All other 7 types carry the stub. Risk: low, aligned with D017.

- **diag-pinned-override.toml PinnedOverride trigger**: Pipeline analysis confirmed the scenario cannot trigger Place/Truncate overflow with the greedy slicer. The vector was authored with correct `expected_output` (current runtime behavior: only pinned item in output) and `exclusion_reason = "PinnedOverride"` declared as an S03 diagnostics spec target with a detailed implementation note.

- **diag* glob count vs slice plan**: The slice plan check `ls diag*.toml … | wc -l` expects 5, but the glob `diag*` already matches `diagnostics-budget-exceeded.toml`, so the command counts 5 total (4 new `diag-*.toml` + 1 `diagnostics-budget-exceeded.toml`). Actual distinct files are correct.

## Known Limitations

- `expected.diagnostics.*` sections in the 4 new vectors are spec targets only — not validated by the conformance harness until S03 wires the diagnostics test path
- `SelectionReport` lacks a serde derive; adding it requires the custom `ExclusionReason` serde impl from S04 first

## Follow-ups

- S03: Wire conformance harness to read `[expected.diagnostics.*]` from all 5 `diag*.toml` files; the `diag-pinned-override.toml` S03 implementation note describes the Slice-stage detection required for `PinnedOverride`
- S04: Add serde derive + custom adjacent-tagged impl for `ExclusionReason` and derived impl for `SelectionReport`

## Files Created/Modified

- `crates/cupel/src/diagnostics/mod.rs` — new; all 8 public diagnostic types with full doc comments, #[non_exhaustive], serde stubs
- `crates/cupel/src/lib.rs` — added `pub mod diagnostics;` and `pub use diagnostics::{…}` for all 8 types
- `spec/conformance/required/pipeline/diag-negative-tokens.toml` — NegativeTokens exclusion vector
- `spec/conformance/required/pipeline/diag-deduplicated.toml` — Deduplicated exclusion vector
- `spec/conformance/required/pipeline/diag-pinned-override.toml` — PinnedOverride exclusion vector with S03 impl note
- `spec/conformance/required/pipeline/diag-scored-inclusion.toml` — Scored inclusion vector (both items included)
- `crates/cupel/conformance/required/pipeline/diag-negative-tokens.toml` — vendored copy
- `crates/cupel/conformance/required/pipeline/diag-deduplicated.toml` — vendored copy
- `crates/cupel/conformance/required/pipeline/diag-pinned-override.toml` — vendored copy
- `crates/cupel/conformance/required/pipeline/diag-scored-inclusion.toml` — vendored copy

## Forward Intelligence

### What the next slice should know
- `ExclusionReason` has 8 variants: `BudgetExceeded`, `NegativeTokens`, `Deduplicated`, `PinnedOverride` (active) and `_Reserved1`–`_Reserved4` (reserved, `#[doc(hidden)]`). All reserved variants have `#[doc(hidden)]` — S02 `DiagnosticTraceCollector` must never emit them.
- `IncludedItem.score` is `f64`; `ExcludedItem.score` is also `f64` (not `Option<f64>`). Both fields carry the item's scorer output at time of recording.
- `SelectionReport.excluded` doc comment specifies sort order: first by pipeline stage order, then by item order within each stage. S03 must maintain this sort invariant.
- `TraceEvent` and `OverflowEvent` are structs (not enum variants); `PipelineStage` is the separate enum used as a field in `TraceEvent`.

### What's fragile
- `diag-pinned-override.toml` — `expected_output` contains only the pinned item; the `[expected.diagnostics.excluded]` entry has `exclusion_reason = "PinnedOverride"` as an S03 spec target. If S03 implements PinnedOverride detection differently (e.g. at Place stage), the vector's `exclusion_reason` must be revisited.
- `SelectionReport` serde gap — the type is not serde-serializable until S04. Any S02/S03 test that tries to serialize a `SelectionReport` will fail to compile under `--features serde` until S04 lands.

### Authoritative diagnostics
- `diff -r spec/conformance/ crates/cupel/conformance/` — canonical drift detection; run after any conformance file change
- `cargo doc --no-deps 2>&1 | grep -E "warning|error"` — catches broken doc links early; zero warnings is the required state

### What assumptions changed
- PinnedOverride is a Slice-stage event, not a Place-stage event — the greedy slicer's `compute_effective_budget` subtraction from `effective_target` means Place/Truncate overflow is unreachable when pinned items fit. S03's diagnostics layer must detect this at the Slice stage.
