---
estimated_steps: 5
estimated_files: 2
---

# T03: Write diagnostics serde integration tests and extend the roundtrip example

**Slice:** S04 — Diagnostics Serde Integration
**Milestone:** M001

## Description

With T01 and T02 complete, all diagnostic types have correct serde. This task proves it with integration tests in `crates/cupel/tests/serde.rs` and extends `crates/cupel/examples/serde_roundtrip.rs` with a `SelectionReport` demonstration.

The tests add a section 6 to the existing serde test file. Existing sections (1–5) cover the non-diagnostic types. Section 6 must cover:
- **Wire-format assertions** — JSON must use the `reason` discriminator, not variant wrappers
- **Round-trips** — all 8 `ExclusionReason` variants and all 3 `InclusionReason` variants
- **Full `SelectionReport` round-trip** — at least one included and one excluded item
- **Validation rejection** — `total_candidates` mismatch must be rejected
- **Graceful unknown** — `_Unknown` variant deserializes without panic

These tests make R006 machine-verifiable and catch any future regression if serde attributes are accidentally removed.

## Steps

1. Add imports to `crates/cupel/tests/serde.rs`: `use cupel::{ExclusionReason, InclusionReason, SelectionReport, IncludedItem, ExcludedItem, ContextItemBuilder, TraceEvent, PipelineStage};` (only those not already imported). All tests go inside the existing `#![cfg(feature = "serde")]` file — no additional cfg gates needed.

2. Add section `// 6. Diagnostics serde tests` and write **wire-format assertion tests**:
   - `exclusion_reason_budget_exceeded_wire_format` — `serde_json::to_string(&ExclusionReason::BudgetExceeded { item_tokens: 100, available_tokens: 50 })` → JSON string contains `"reason"` key (not a `"BudgetExceeded"` outer key); assert `json.contains(r#""reason":"BudgetExceeded""#)` and `json.contains("item_tokens")`.
   - `inclusion_reason_scored_wire_format` — `serde_json::to_string(&InclusionReason::Scored)` → exact match `r#"{"reason":"Scored"}"#` (not `"Scored"` bare string).

3. Add **round-trip tests for all 8 ExclusionReason variants**. Each test serializes the variant to JSON and deserializes back, then asserts field equality:
   - `roundtrip_exclusion_budget_exceeded`
   - `roundtrip_exclusion_negative_tokens`
   - `roundtrip_exclusion_deduplicated`
   - `roundtrip_exclusion_pinned_override`
   - `roundtrip_exclusion_scored_too_low` (reserved variant)
   - `roundtrip_exclusion_quota_cap_exceeded` (reserved variant)
   - `roundtrip_exclusion_quota_require_displaced` (reserved variant)
   - `roundtrip_exclusion_filtered` (reserved variant)
   Add **round-trip tests for all 3 InclusionReason variants**: `roundtrip_inclusion_scored`, `roundtrip_inclusion_pinned`, `roundtrip_inclusion_zero_token`. Use `PartialEq` derives already present on both enums.

4. Add **SelectionReport and DiagnosticTraceCollector tests**:
   - `roundtrip_selection_report_full` — Build a `SelectionReport` manually (use struct construction inside the crate tests via `tests/serde.rs` — or build it from a `DiagnosticTraceCollector::into_report()` call with one included item and one excluded item). Serialize to JSON, deserialize back, assert `total_candidates`, `included.len()`, `excluded.len()`, and the `reason` field of the excluded item. Use `ContextItemBuilder::new("content", tokens).build().unwrap()` for items.
   - `reject_selection_report_total_candidates_mismatch` — Manually construct JSON where `total_candidates: 99` but `included` and `excluded` have 1 item each (total = 2). Assert `serde_json::from_str::<SelectionReport>` returns `Err`.
   - `exclusion_reason_unknown_variant_graceful` — `serde_json::from_str::<ExclusionReason>(r#"{"reason":"FutureVariantFromSpec3"}"#).unwrap()` → `ExclusionReason::_Unknown` (use a match or `matches!` macro).

5. Extend `crates/cupel/examples/serde_roundtrip.rs`: Add Part 4 that builds a two-item pipeline, calls `pipeline.dry_run(&items, &budget)`, serializes the returned `SelectionReport` to pretty JSON with `serde_json::to_string_pretty`, and prints it. This demonstrates the end-to-end diagnostic → serialize → inspect workflow. Use the existing `ContextItemBuilder`, `ContextBudget`, and `Pipeline` types; import `cupel::Pipeline` and `cupel::{TraceDetailLevel, DiagnosticTraceCollector}` as needed.

   After all code changes, run:
   - `cargo test --features serde -- diag` (all new diagnostics section tests pass)
   - `cargo test --features serde` (all tests pass, no regressions)
   - `cargo clippy --all-targets -- -D warnings` (zero warnings)
   - `cargo run --example serde_roundtrip --features serde` (exits 0, prints SelectionReport JSON)

## Must-Haves

- [ ] Wire-format assertion confirms `ExclusionReason::BudgetExceeded` produces `{ "reason": "BudgetExceeded", ... }` not `{ "BudgetExceeded": { ... } }`
- [ ] Wire-format assertion confirms `InclusionReason::Scored` produces `{"reason":"Scored"}` not `"Scored"` (bare string)
- [ ] All 8 `ExclusionReason` variants pass round-trip tests with field equality
- [ ] All 3 `InclusionReason` variants pass round-trip tests
- [ ] `SelectionReport` round-trip test passes with correct included/excluded/total_candidates
- [ ] `total_candidates` mismatch is rejected with `Err`
- [ ] `{"reason":"FutureVariantFromSpec3"}` deserializes to `ExclusionReason::_Unknown` without panic
- [ ] `cargo test --features serde` exits 0 with zero failures
- [ ] `cargo clippy --all-targets -- -D warnings` exits 0 with zero warnings
- [ ] `cargo run --example serde_roundtrip --features serde` exits 0

## Verification

- `cargo test --features serde -- diag` — all diagnostics serde tests pass
- `cargo test --features serde` — all tests pass (count: ≥29 existing + new diagnostics tests)
- `cargo clippy --all-targets -- -D warnings` — exits 0, zero warnings
- `cargo run --example serde_roundtrip --features serde` — exits 0, prints pretty JSON with `"reason"` discriminators
- Spot-check in test output: wire-format assertion for `BudgetExceeded` confirms the spec-compliant envelope format

## Observability Impact

- Signals added/changed: new integration tests are the primary observability for R006; any future regression in serde tag attributes will be caught by the wire-format assertion tests
- How a future agent inspects this: `cargo test --features serde -- diag --nocapture` shows per-assertion details; `cargo run --example serde_roundtrip --features serde` provides a human-readable demo
- Failure state exposed: test failure messages show exact JSON produced vs. expected wire format; `--nocapture` reveals the full serialized output on failure

## Inputs

- `crates/cupel/tests/serde.rs` — existing sections 1–5; add section 6 here
- `crates/cupel/examples/serde_roundtrip.rs` — existing Parts 1–3 to extend
- T01-SUMMARY.md — confirms `ExclusionReason` + `InclusionReason` serde is correct
- T02-SUMMARY.md — confirms `SelectionReport` serde + `DiagnosticTraceCollector.excluded` fix is correct
- `spec/src/diagnostics/exclusion-reasons.md` — authoritative wire format for assertion values
- `spec/src/diagnostics/selection-report.md` — full `SelectionReport` JSON example

## Expected Output

- `crates/cupel/tests/serde.rs` — section 6 with ≥15 new tests: 2 wire-format, 8 ExclusionReason round-trips, 3 InclusionReason round-trips, 1 SelectionReport full round-trip, 1 validation rejection, 1 graceful unknown
- `crates/cupel/examples/serde_roundtrip.rs` — Part 4 block demonstrating `SelectionReport` serialize/pretty-print
