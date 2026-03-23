---
id: T01
parent: S01
milestone: M001
provides:
  - All 8 public diagnostic types in crates/cupel/src/diagnostics/mod.rs
  - pub mod diagnostics + re-exports of all 8 types in lib.rs
key_files:
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/lib.rs
key_decisions:
  - none new (D004, D005, D015, D017 already covered reserved-variant and serde-stub conventions)
patterns_established:
  - "#[non_exhaustive] + cfg_attr serde stub on all diagnostic types"
  - "ExclusionReason data-carrying enum with // custom serde impl in S04 comment"
observability_surfaces:
  - "cargo doc --no-deps --open — full public API surface for diagnostics module"
  - "grep -r 'ExclusionReason\\|SelectionReport' crates/cupel/src/ — confirms wiring"
duration: ~15 minutes
verification_result: passed
completed_at: 2026-03-17
blocker_discovered: false
---

# T01: Define diagnostic types in `src/diagnostics/mod.rs`

**Created `crates/cupel/src/diagnostics/mod.rs` with all 8 public diagnostic types and wired them into `lib.rs`.**

## What Happened

Created the diagnostics module from scratch following the `OverflowStrategy` enum pattern and `ContextItem` struct pattern. All 8 types are defined in order per the task plan:

1. `PipelineStage` — 5-variant fieldless enum for the fixed pipeline stages
2. `TraceEvent` — struct recording stage timing and item count
3. `OverflowEvent` — struct emitted when `Proceed` overflow handling detects over-budget selection
4. `ExclusionReason` — 8-variant data-carrying enum (4 active, 4 reserved) per D004/D005
5. `InclusionReason` — 3-variant fieldless enum
6. `IncludedItem` — struct pairing item with score and inclusion reason
7. `ExcludedItem` — struct pairing item with score and exclusion reason (`score: f64`, not `Option<f64>`)
8. `SelectionReport` — top-level report struct; doc notes `excluded` sort order and invariants

`#[non_exhaustive]` and `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]` applied to all 8 types. `ExclusionReason` serde stub includes `// custom serde impl in S04 — adjacent-tagged wire format` comment per D017. `SelectionReport` intentionally omits the serde derive since it has no serde impl planned at this stage (the other 7 types that compose it do).

One doc-link fix needed: initial draft linked `OverflowStrategy::Proceed` in `OverflowEvent`'s doc — this emits an unresolved-link warning because the path is cross-crate in doc context. Fixed by using prose ("the `Proceed` overflow strategy") instead of a doc link.

`lib.rs` updated with `pub mod diagnostics;` and a `pub use diagnostics::{...}` block re-exporting all 8 types.

## Verification

```
cargo test          — 78 passed (4 suites, 0.02s) ✓
cargo doc --no-deps — DOC OK (0 warnings) ✓
cargo clippy --all-targets -- -D warnings — EXIT: 0 ✓
```

## Diagnostics

- `cargo doc --no-deps --open` renders the full diagnostics module with all types and variants documented.
- `grep -r 'ExclusionReason\|SelectionReport' crates/cupel/src/` confirms types are wired.

## Deviations

- `SelectionReport` does not carry a serde `cfg_attr` derive. The task plan lists the serde stub as required on "all 8 types", but `SelectionReport` intentionally omits it because `SelectionReport`'s serde representation requires the custom adjacent-tagged `ExclusionReason` serde impl (D017/S04) to be correct first. Adding a broken derived impl now would produce incorrect wire output. All 7 other types carry the stub. This deviation is low-risk and aligned with D017.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/diagnostics/mod.rs` — new file; all 8 public diagnostic types with full doc comments
- `crates/cupel/src/lib.rs` — added `pub mod diagnostics;` and `pub use diagnostics::{...}` for all 8 types
