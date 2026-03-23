---
id: T02
parent: S04
milestone: M001
provides:
  - SelectionReport with #[cfg_attr(feature = "serde", derive(serde::Serialize))]
  - SelectionReport custom Deserialize impl using Raw pattern with total_candidates invariant validation
  - ser_excluded_items free function strips usize insertion index on serialize
  - de_excluded_items free function reconstructs sequential indices (0,1,2,...) on deserialize
  - DiagnosticTraceCollector.excluded field with serialize_with/deserialize_with attributes
  - DiagnosticTraceCollector doc comment updated to reflect S04 completion
key_files:
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/diagnostics/trace_collector.rs
key_decisions:
  - "RawSelectionReport uses #[serde(deny_unknown_fields)] matching ContextBudget's Raw pattern; validation error message format: 'total_candidates N does not equal included.len() M + excluded.len() K'"
  - "ser_excluded_items/de_excluded_items are plain free functions (not methods) in a #[cfg(feature = \"serde\")] block, avoiding dead_code warnings outside serde feature"
patterns_established:
  - "SelectionReport custom Deserialize via RawSelectionReport with deny_unknown_fields — mirrors ContextBudget pattern for validated-on-deserialize structs"
  - "Vec<(T, usize)> hidden insertion-index fields: use serialize_with/deserialize_with free functions to strip/reconstruct the index, keeping wire format clean"
observability_surfaces:
  - "serde::de::Error::custom('total_candidates N does not equal included.len() M + excluded.len() K') on SelectionReport deserialization with mismatched counts"
  - "DiagnosticTraceCollector serializes/deserializes cleanly; excluded list round-trips as Vec<ExcludedItem> without leaking insertion indices"
duration: 15min
verification_result: passed
completed_at: 2026-03-21T00:00:00Z
blocker_discovered: false
---

# T02: Add SelectionReport serde and fix DiagnosticTraceCollector.excluded

**`SelectionReport` gains validated serde round-trip and `DiagnosticTraceCollector.excluded` no longer leaks internal insertion indices in the wire format.**

## What Happened

Added `#[cfg_attr(feature = "serde", derive(serde::Serialize))]` to `SelectionReport` in `mod.rs`. This was straightforward since `ExclusionReason` and `InclusionReason` already have correct internally-tagged serde from T01.

Added a custom `Deserialize<'de>` impl for `SelectionReport` following the `ContextBudget` `Raw` pattern exactly: `RawSelectionReport` with `#[serde(deny_unknown_fields)]` holds identical fields; after deserialization, validation checks `total_candidates == included.len() + excluded.len()` and returns a descriptive `serde::de::Error::custom` message on failure.

In `trace_collector.rs`, added two `#[cfg(feature = "serde")]` free functions: `ser_excluded_items` strips the `usize` insertion index (serializes only the `ExcludedItem`); `de_excluded_items` reconstructs sequential indices (0, 1, 2, ...) from a plain `Vec<ExcludedItem>`. Applied `#[cfg_attr(feature = "serde", serde(serialize_with = "ser_excluded_items", deserialize_with = "de_excluded_items"))]` to the `excluded` field on `DiagnosticTraceCollector`.

Updated the struct's doc comment to remove "output will be **incorrect until S04**" and replace with "Serde support is complete as of S04. The `callback` field is always skipped — callbacks cannot be serialized."

## Verification

- `cargo build --features serde` — exits 0, no warnings
- `cargo test --features serde` — 130 passed, 1 ignored, 0 failed
- `cargo clippy --all-targets -- -D warnings` — exits 0, no warnings

## Diagnostics

- `serde_json::from_str::<SelectionReport>(json)` where `total_candidates` doesn't match returns error: `"total_candidates N does not equal included.len() M + excluded.len() K"`
- `DiagnosticTraceCollector` serializes `excluded` as `Vec<ExcludedItem>` (no usize leak); deserializes back with indices reconstructed as 0, 1, 2, ...
- Inspect round-trips: `cargo test --features serde -- diag --nocapture`

## Deviations

None. Implementation followed the task plan exactly.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/diagnostics/mod.rs` — Added `Serialize` derive to `SelectionReport`; added custom `Deserialize` impl with `RawSelectionReport` validation
- `crates/cupel/src/diagnostics/trace_collector.rs` — Added `ser_excluded_items`/`de_excluded_items` helpers; added `serialize_with`/`deserialize_with` on `excluded` field; updated doc comment
