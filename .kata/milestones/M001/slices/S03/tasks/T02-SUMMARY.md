---
id: T02
parent: S03
milestone: M001
provides:
  - run_pipeline_diagnostics_test helper in conformance/pipeline.rs — parses [expected.diagnostics.*] TOML sections and asserts SelectionReport fields
  - exclusion_reason_tag helper — maps ExclusionReason variants to canonical string tags
  - inclusion_reason_tag helper — maps InclusionReason variants to canonical string tags
  - diag_negative_tokens test — exercises NegativeTokens exclusion with token field assertion
  - diag_deduplicated test — exercises Deduplicated exclusion with deduplicated_against field assertion
  - diag_pinned_override test — exercises PinnedOverride exclusion with displaced_by field assertion
  - diag_scored_inclusion test — exercises Scored inclusion for both items; excluded list empty
  - diagnostics_budget_exceeded test — exercises BudgetExceeded exclusion with item_tokens/available_tokens assertions
  - Fixed pre-existing clippy type_complexity lints in classify.rs and place.rs (ClassifyResult and PlaceResult type aliases)
key_files:
  - crates/cupel/tests/conformance/pipeline.rs
  - crates/cupel/tests/conformance.rs
  - crates/cupel/src/pipeline/classify.rs
  - crates/cupel/src/pipeline/place.rs
key_decisions:
  - Pre-existing clippy type_complexity errors in classify.rs and place.rs were introduced before T02; fixed with type aliases (ClassifyResult, PlaceResult) as they blocked the required zero-warning clippy gate
  - imports for DiagnosticTraceCollector/ExclusionReason/TraceDetailLevel live in pipeline.rs (direct cupel:: import), not re-exported through conformance.rs, because the helpers use qualified paths and only pipeline.rs needs them
patterns_established:
  - run_pipeline_diagnostics_test pattern: load vector → build pipeline/budget via shared helpers → run_traced → into_report → assert summary, included[], excluded[] in order with epsilon score check + variant-specific field checks
  - exclusion_reason_tag / inclusion_reason_tag: convert non_exhaustive enums to &'static str for vector comparison without serde dependency
observability_surfaces:
  - "cargo test --test conformance -- pipeline::diag --nocapture" runs only the 5 new diagnostics tests; assertion messages print expected vs actual values with field names
  - test name maps to vector filename (e.g. diag_negative_tokens → pipeline/diag-negative-tokens.toml)
duration: 20min
verification_result: passed
completed_at: 2026-03-21T00:00:00Z
blocker_discovered: false
---

# T02: Conformance harness: diagnostics parsing and 5 test cases

**Conformance test harness extended with `run_pipeline_diagnostics_test`; all 5 diagnostics vectors (NegativeTokens, Deduplicated, PinnedOverride, Scored, BudgetExceeded) pass as integration tests against the real `run_traced` implementation.**

## What Happened

Added `exclusion_reason_tag`, `inclusion_reason_tag`, and `run_pipeline_diagnostics_test` to `tests/conformance/pipeline.rs`. The diagnostics helper reuses the existing budget/pipeline/items construction pattern from `run_pipeline_test`, then calls `pipeline.run_traced(&items, &budget, &mut collector)` with a `DiagnosticTraceCollector` at `TraceDetailLevel::Item`, and asserts on every field defined in the vector's `[expected.diagnostics.*]` sections.

Variant-specific field checks (NegativeTokens.tokens, Deduplicated.deduplicated_against, BudgetExceeded.item_tokens/available_tokens, PinnedOverride.displaced_by) are conditional — only asserted when the field is present in the vector, matching the spec's intent.

Also fixed two pre-existing `clippy::type_complexity` lints in `src/pipeline/classify.rs` and `src/pipeline/place.rs` that would have blocked the required zero-warning clippy gate — introduced `ClassifyResult` and `PlaceResult` type aliases.

## Verification

- `cargo test --test conformance -- pipeline` → 10/10 passed (5 original + 5 new), zero warnings
- `cargo test --lib` → 29/29 unit tests passed
- `cargo clippy --all-targets -- -D warnings` → zero warnings/errors
- `cargo doc --no-deps` → zero warnings/errors

## Diagnostics

- `cargo test --test conformance -- pipeline::diag --nocapture` runs only the 5 new diagnostics tests with assertion detail
- Assertion messages include expected vs actual values with field name, e.g. `excluded[0] NegativeTokens.tokens mismatch: expected -5, got X`
- Test name → vector: `diag_negative_tokens` → `pipeline/diag-negative-tokens.toml`, etc.

## Deviations

- Fixed pre-existing clippy type_complexity errors in classify.rs and place.rs that were not part of the task plan but were required to satisfy the zero-warning clippy gate.
- Did not add diagnostics imports to `conformance.rs` (task plan step 1) — the imports are only needed in `pipeline.rs` where they are already imported directly; adding them to the parent module created unused-import warnings.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/tests/conformance/pipeline.rs` — added exclusion_reason_tag, inclusion_reason_tag, run_pipeline_diagnostics_test, and 5 new #[test] functions
- `crates/cupel/tests/conformance.rs` — import unchanged (diagnostics types consumed directly in pipeline.rs)
- `crates/cupel/src/pipeline/classify.rs` — added ClassifyResult type alias; changed classify() return type to use it
- `crates/cupel/src/pipeline/place.rs` — added PlaceResult type alias; changed place_items() return type to use it
