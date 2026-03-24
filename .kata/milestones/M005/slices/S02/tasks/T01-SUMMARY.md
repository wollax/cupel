---
id: T01
parent: S02
milestone: M005
provides:
  - 7 assertion methods on SelectionReportAssertionChain (patterns 1–7)
  - 14 integration tests in crates/cupel-testing/tests/assertions.rs
key_files:
  - crates/cupel-testing/src/chain.rs
  - crates/cupel-testing/tests/assertions.rs
key_decisions:
  - Pattern 4/5 use std::mem::discriminant for variant comparison (not string matching)
  - Pattern 6 uses a match arm to destructure BudgetExceeded fields directly
  - include_item_matching closure argument simplified to bare `predicate` (clippy redundant_closure fix)
patterns_established:
  - Mini-pipeline test pattern: Pipeline::builder() + RecencyScorer + GreedySlice + UShapedPlacer + DiagnosticTraceCollector → SelectionReport
  - Negative tests use #[should_panic(expected = "...")] with a unique substring from the spec error message prefix
observability_surfaces:
  - cargo test -- --nocapture shows full panic messages when debugging negative test failures
  - Test names include pattern name (e.g. include_item_with_kind_passes / _panics)
duration: ~15 minutes
verification_result: passed
completed_at: 2026-03-24
blocker_discovered: false
---

# T01: Implement patterns 1–7 (existence/count/reason checks) with tests

**Implemented 7 fluent assertion methods on `SelectionReportAssertionChain` with 14 integration tests, all passing; clippy clean in both crates.**

## What Happened

Removed `#[allow(dead_code)]` from the `report` field in `chain.rs` (first assertion methods make it live). Added imports for `ExcludedItem`, `ExclusionReason`, `IncludedItem`, and `ContextKind`. Implemented patterns 1–7 as `pub fn` methods returning `&mut Self`, each panicking with the spec error message on failure:

- **Pattern 1** `include_item_with_kind`: checks `included.iter().any(|i| i.item.kind() == &kind)`; collects distinct actual kinds into error message.
- **Pattern 2** `include_item_matching`: generic predicate `Fn(&IncludedItem) -> bool`; reports total count on failure.
- **Pattern 3** `include_exact_n_items_with_kind`: counts items matching kind; reports expected vs actual vs total.
- **Pattern 4** `exclude_item_with_reason`: discriminant comparison `std::mem::discriminant(&e.reason) == std::mem::discriminant(&reason)`; reports distinct actual reasons.
- **Pattern 5** `exclude_item_matching_with_reason`: splits into predicate-match then discriminant-match; reports predicate-match count + actual reasons of matched items.
- **Pattern 6** `have_excluded_item_with_budget_details`: `matches!` macro to find BudgetExceeded items, then `if let` destructure to compare token fields; two failure messages (wrong values vs no matching item).
- **Pattern 7** `have_no_exclusions_for_kind`: collects all matching excluded items; reports count + first item's score/reason.

Created `tests/assertions.rs` with 14 integration tests (7 positive `_passes` + 7 negative `_panics`). Each test builds a mini-pipeline with real items and a real `SelectionReport` via `DiagnosticTraceCollector`. The `have_excluded_item_with_budget_details_passes` test extracts the actual token values from the report first to stay robust across budget changes.

Fixed one clippy warning: `|i| predicate(i)` → bare `predicate` in `include_item_matching`.

## Verification

```
cd crates/cupel-testing && cargo test --all-targets
# → test result: ok. 15 passed (14 new + 1 smoke)

cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings
# → clean (no output)

cd crates/cupel && cargo clippy --all-targets -- -D warnings
# → clean (no regressions)
```

## Diagnostics

- `cargo test -- --nocapture` shows full panic messages for all assertion failures
- Test names map directly to pattern names: `include_item_with_kind_passes`, `include_item_with_kind_panics`, etc.
- Each panic message names the assertion, states expected vs actual values with field counts and kind/reason lists

## Deviations

None. The `include_item_matching` predicate type is `Fn(&IncludedItem) -> bool` rather than `Fn(&ContextItem) -> bool` — this matches the spec (PD-1: predicates over `IncludedItem` are strictly more powerful). The C# reference uses `Func<IncludedItem, bool>` too.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel-testing/src/chain.rs` — removed dead_code allow; added 7 assertion methods (patterns 1–7)
- `crates/cupel-testing/tests/assertions.rs` — new file with 14 integration tests (7 positive + 7 negative)
