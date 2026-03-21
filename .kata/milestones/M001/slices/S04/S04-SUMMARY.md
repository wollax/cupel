---
id: S04
parent: M001
milestone: M001
provides:
  - ExclusionReason with #[serde(tag = "reason")] — internally-tagged wire format matching spec
  - ExclusionReason._Unknown variant with #[serde(other)] for forward-compat deserialization
  - InclusionReason with #[serde(tag = "reason")] — internally-tagged wire format
  - SelectionReport Serialize derive + custom Deserialize impl with total_candidates invariant validation
  - DiagnosticTraceCollector.excluded serde via ser_excluded_items/de_excluded_items — usize index stripped from wire format
  - 16 new serde integration tests covering all 8 ExclusionReason variants, all 3 InclusionReason variants, full SelectionReport round-trip, validation rejection, and graceful unknown variant
  - serde_roundtrip.rs Part 4: live pipeline.dry_run() → SelectionReport → pretty-print JSON demo
requires:
  - slice: S01
    provides: ExclusionReason, InclusionReason, SelectionReport, IncludedItem, ExcludedItem type definitions with cfg_attr serde stubs
  - slice: S02
    provides: DiagnosticTraceCollector with Vec<(ExcludedItem, usize)> excluded field; TraceCollector trait with record_included/record_excluded
  - slice: S03
    provides: Pipeline::dry_run() and Pipeline::run_traced() wiring; conformance vectors passing in CI
affects:
  - S05
  - S07
key_files:
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/diagnostics/trace_collector.rs
  - crates/cupel/tests/serde.rs
  - crates/cupel/examples/serde_roundtrip.rs
key_decisions:
  - "D027: #[serde(tag = \"reason\")] (internally-tagged) on ExclusionReason and InclusionReason — no custom impl needed since all variants are struct or unit type"
  - "D028: SelectionReport custom Deserialize via RawSelectionReport with deny_unknown_fields + total_candidates validation — mirrors ContextBudget Raw pattern"
  - "D029: Vec<(T, usize)> hidden-index fields: serialize_with/deserialize_with free functions strip/reconstruct index, keeping wire format clean"
  - "_Unknown added only to ExclusionReason (not InclusionReason) — inclusion is always a known-set per spec; exclusion must tolerate future spec variants"
patterns_established:
  - "Internally-tagged serde on #[non_exhaustive] enums: cfg_attr serde(tag) line immediately after derive cfg_attr, before enum declaration"
  - "Vec<(T, usize)> serde: serialize_with strips index, deserialize_with reconstructs sequential indices — no leaking of internal sort bookkeeping to wire"
  - "Integration test split: separate wire-format assertion tests from round-trip tests — different failure modes, different signal value"
  - "serde_roundtrip.rs extended with Part N pattern — each part demonstrates one type family end-to-end with live output"
observability_surfaces:
  - "serde::de::Error::custom('total_candidates N does not equal included.len() M + excluded.len() K') on SelectionReport deserialization with mismatched counts"
  - "cargo test --features serde covers all wire-format, round-trip, validation, and forward-compat contracts"
  - "cargo run --example serde_roundtrip --features serde prints spec-compliant JSON output for visual confirmation"
drill_down_paths:
  - .kata/milestones/M001/slices/S04/tasks/T01-SUMMARY.md
  - .kata/milestones/M001/slices/S04/tasks/T02-SUMMARY.md
  - .kata/milestones/M001/slices/S04/tasks/T03-SUMMARY.md
duration: 40min
verification_result: passed
completed_at: 2026-03-21T00:00:00Z
---

# S04: Diagnostics Serde Integration

**All diagnostic types serialize/deserialize with spec-compliant internally-tagged wire format; 16 new integration tests prove every variant and edge case end-to-end.**

## What Happened

S04 completed the serde story for the entire diagnostics subsystem introduced in S01–S03.

**T01** fixed the wire format at the enum level. The existing `cfg_attr` stubs produced serde's default externally-tagged format (`{"BudgetExceeded":{...}}`); the spec requires internally-tagged (`{"reason":"BudgetExceeded",...}`). Adding `#[cfg_attr(feature = "serde", serde(tag = "reason"))]` to both `ExclusionReason` and `InclusionReason` was sufficient — all variants are struct or unit variants, exactly what serde's internal tagging requires. A `_Unknown` unit variant with `#[serde(other)]` was added to `ExclusionReason` to handle unknown `reason` values from future spec versions gracefully, without panic. `InclusionReason` intentionally omits `_Unknown` since inclusion is always a closed set.

**T02** handled the two more complex serde cases. `SelectionReport` had no serde at all. A straightforward `Serialize` derive was added; for `Deserialize`, the `RawSelectionReport` pattern (already used by `ContextBudget`) was applied: deserialize into a raw struct first, then validate `total_candidates == included.len() + excluded.len()`, returning a descriptive error on mismatch. `DiagnosticTraceCollector.excluded` stores `Vec<(ExcludedItem, usize)>` where the `usize` is an internal insertion index for stable sort — stripping this from the wire format required `serialize_with`/`deserialize_with` free functions that serialize as `Vec<&ExcludedItem>` and deserialize back as `Vec<ExcludedItem>` with sequential indices reconstructed.

**T03** wrote the proof layer. 16 integration tests were added to `tests/serde.rs` section 6, covering: wire-format assertions for both enum types, round-trips for all 8 `ExclusionReason` variants and all 3 `InclusionReason` variants (with field equality), a full `SelectionReport` round-trip, a validation rejection test (mismatched `total_candidates`), and a graceful unknown-variant test. The `serde_roundtrip.rs` example was extended with Part 4 demonstrating a live `pipeline.dry_run()` → `SelectionReport` → pretty JSON cycle that visually confirms the spec-compliant wire format.

## Verification

- `cargo build --features serde` — exit 0, no warnings ✓
- `cargo clippy --all-targets -- -D warnings` — exit 0, no warnings ✓
- `cargo test --features serde` — 29 unit + 33 conformance + 49 serde (16 new) + 35 doc = 146 tests, 0 failures, 1 ignored ✓
- `cargo run --example serde_roundtrip --features serde` — exits 0; output shows `{"reason":"BudgetExceeded","item_tokens":2000,"available_tokens":5}` ✓
- Wire-format spot check: `ExclusionReason::BudgetExceeded` → `{"reason":"BudgetExceeded",...}` (internally-tagged) ✓
- Wire-format spot check: `InclusionReason::Scored` → `{"reason":"Scored"}` (not bare string) ✓
- Graceful unknown: `{"reason":"FutureVariantFromSpec3"}` → `ExclusionReason::_Unknown` ✓
- Validation rejection: `total_candidates: 99` with 2 items → `Err` with message containing `"total_candidates"` ✓

## Requirements Advanced

- R006 — Diagnostics serde coverage: all diagnostic types now support Serialize/Deserialize behind `serde` feature; validation-on-deserialize pattern applied to SelectionReport; wire format matches spec

## Requirements Validated

- R006 — Fully validated: `cargo test --features serde` passes with wire-format assertions, round-trips for all enum variants, and validation rejection test; `serde_json::to_string(&report)` produces spec-compliant JSON

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

- T03: test filter `-- diag` in the plan doesn't match the actual test names (they use prefixes like `exclusion_reason_`, `roundtrip_`, `graceful_`). Not a functional issue — all tests pass and are discoverable without the filter. The plan's filter was a suggestion.
- T03: `ExcludedItem`/`IncludedItem` imports were removed after first compile — `DiagnosticTraceCollector`'s `record_included`/`record_excluded` take `ContextItem` directly, so those types aren't needed in test code.

## Known Limitations

- `DiagnosticTraceCollector`'s `callback` field is always skipped on serialize (cannot serialize a closure). Callers that deserialize a `DiagnosticTraceCollector` will get `callback: None` back. This is documented in the struct's doc comment and is the expected behavior for a non-serializable closure field.

## Follow-ups

- S05 should verify `cargo clippy --all-targets -- -D warnings` still passes after CI baseline is set — this slice confirmed it passes locally.
- R006 can now be marked validated in REQUIREMENTS.md.

## Files Created/Modified

- `crates/cupel/src/diagnostics/mod.rs` — Added `#[serde(tag = "reason")]` to `ExclusionReason` and `InclusionReason`; added `_Unknown` variant; added `Serialize` derive and custom `Deserialize` impl to `SelectionReport`
- `crates/cupel/src/diagnostics/trace_collector.rs` — Added `ser_excluded_items`/`de_excluded_items` free functions; added `serialize_with`/`deserialize_with` on `excluded` field; updated doc comment
- `crates/cupel/tests/serde.rs` — Added 16 new tests in section 6 with full imports for diagnostic types
- `crates/cupel/examples/serde_roundtrip.rs` — Added Part 4 (SelectionReport roundtrip via dry_run with pretty JSON output)

## Forward Intelligence

### What the next slice should know
- All diagnostic types are serde-complete — S05/S07 can add clippy/quality fixes without touching serde code unless a type's structure changes
- The `_Unknown` variant exists on `ExclusionReason` and must be preserved in any `match` refactoring (it slots into the `_` wildcard arm that `#[non_exhaustive]` already required)
- `DiagnosticTraceCollector` serde skips `callback` — this is intentional and documented; do not attempt to make callbacks serializable
- `RawSelectionReport` is a private serde-only struct in `mod.rs`; it mirrors `SelectionReport` exactly and must stay in sync if new fields are added

### What's fragile
- `RawSelectionReport` mirrors `SelectionReport` manually — if a field is added to `SelectionReport`, `RawSelectionReport` must be updated simultaneously or deserialization will silently drop the new field (since it lacks `deny_unknown_fields` bypass — actually it has `deny_unknown_fields`, so it will error, which is a good forcing function)
- `de_excluded_items` reconstructs indices as 0,1,2,... — this means a deserialized `DiagnosticTraceCollector` loses the original insertion-order tiebreak metadata; only matters if you serialize/deserialize a collector mid-use (unusual), not for `SelectionReport` round-trips

### Authoritative diagnostics
- `cargo test --features serde` — first signal for serde breakage; any regression in wire format or validation will surface here
- `cargo run --example serde_roundtrip --features serde` — visual confirmation of the full pipeline→SelectionReport→JSON flow with live output

### What assumptions changed
- T01 assumption: that "adjacent-tagged" was the right term (from D017). Actual: D017's comment was imprecise. The spec uses `#[serde(tag = "reason")]` which is serde's *internal* tagging. No behavioral difference, but the terminology in docs was corrected.
