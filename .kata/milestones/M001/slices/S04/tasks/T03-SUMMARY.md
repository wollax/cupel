---
id: T03
parent: S04
milestone: M001
provides:
  - "16 new diagnostics serde integration tests in crates/cupel/tests/serde.rs section 6"
  - "Wire-format assertions confirming BudgetExceeded uses internally-tagged {\"reason\":\"BudgetExceeded\",...} envelope"
  - "Wire-format assertion confirming InclusionReason::Scored produces {\"reason\":\"Scored\"} not bare string"
  - "Round-trips for all 8 ExclusionReason variants and all 3 InclusionReason variants with field equality"
  - "SelectionReport full round-trip test and validation-rejection test (total_candidates mismatch → Err)"
  - "Graceful unknown variant test: {\"reason\":\"FutureVariantFromSpec3\"} → ExclusionReason::_Unknown"
  - "serde_roundtrip.rs Part 4: dry_run two-item pipeline → serialize SelectionReport → pretty-print JSON"
requires:
  - slice: S04/T01
    provides: "ExclusionReason + InclusionReason with #[serde(tag = \"reason\")] and _Unknown forward-compat variant"
  - slice: S04/T02
    provides: "SelectionReport custom Deserialize with total_candidates validation; DiagnosticTraceCollector.excluded usize-stripped serde"
affects: []
key_files:
  - crates/cupel/tests/serde.rs
  - crates/cupel/examples/serde_roundtrip.rs
key_decisions:
  - "TraceCollector trait must be in scope to call record_included/record_excluded/set_candidates on DiagnosticTraceCollector in tests"
  - "ExcludedItem and IncludedItem imports not needed in tests when using DiagnosticTraceCollector API directly"
patterns_established:
  - "Integration tests for serde enums: separate wire-format assertion tests (assert JSON shape) from round-trip tests (assert field equality) — different failure modes, different signal value"
  - "serde_roundtrip.rs example extended with Part N pattern — each Part demonstrates one type family end-to-end"
drill_down_paths:
  - .kata/milestones/M001/slices/S04/tasks/T03-PLAN.md
duration: 20min
verification_result: pass
completed_at: 2026-03-21T00:00:00Z
blocker_discovered: false
---

# T03: Write diagnostics serde integration tests and extend the roundtrip example

**16 new serde integration tests prove the spec-compliant internally-tagged wire format end-to-end; example extended with live pipeline → SelectionReport → JSON demo.**

## What Happened

Added section 6 to `crates/cupel/tests/serde.rs` with 16 tests covering all diagnostic serde contracts established in T01 and T02:

- **Wire-format assertions (2):** Confirm `ExclusionReason::BudgetExceeded` produces `{"reason":"BudgetExceeded",...}` internally-tagged format (not externally-tagged `{"BudgetExceeded":{...}}`), and `InclusionReason::Scored` produces `{"reason":"Scored"}` (not bare `"Scored"` string).
- **ExclusionReason round-trips (8):** All variants including the four reserved ones (`ScoredTooLow`, `QuotaCapExceeded`, `QuotaRequireDisplaced`, `Filtered`) serialize and deserialize with field equality.
- **InclusionReason round-trips (3):** `Scored`, `Pinned`, `ZeroToken` all pass.
- **SelectionReport full round-trip (1):** Built via `DiagnosticTraceCollector`, serialized and deserialized back, all fields match including the `reason` field of the excluded item.
- **Validation rejection (1):** `total_candidates: 99` with `included.len() + excluded.len() = 2` returns `Err` with an error message containing `"total_candidates"`.
- **Graceful unknown (1):** `{"reason":"FutureVariantFromSpec3"}` deserializes to `ExclusionReason::_Unknown` without panic.

One compilation fix was needed: `TraceCollector` trait must be imported in scope to call its methods on `DiagnosticTraceCollector`. Added to the import list.

Extended `crates/cupel/examples/serde_roundtrip.rs` with Part 4: builds a three-item pipeline (one pinned, one regular, one oversized), calls `pipeline.dry_run()`, serializes the `SelectionReport` to pretty JSON, and round-trips it back. The output clearly shows the `{"reason":"BudgetExceeded","item_tokens":2000,"available_tokens":5}` wire format.

## Deviations

- Test filter `-- diag` from the plan does not match the new test names (they use `exclusion_reason_`, `roundtrip_`, etc.) — this is fine, `-- exclusion_reason` or running without filter catches them all. The plan's filter recommendation was a suggestion, not a hard requirement.
- `ExcludedItem` and `IncludedItem` imports were not needed in tests (removed after first compile to eliminate unused-import warning) — DiagnosticTraceCollector's `record_included`/`record_excluded` accept `ContextItem` directly.

## Files Created/Modified

- `crates/cupel/tests/serde.rs` — Added 16 new tests in section 6; added `DiagnosticTraceCollector`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, `TraceCollector`, `TraceDetailLevel` to imports
- `crates/cupel/examples/serde_roundtrip.rs` — Added Part 4 (SelectionReport roundtrip via dry_run); added `Pipeline`, `RecencyScorer`, `GreedySlice`, `ChronologicalPlacer` to imports
