---
estimated_steps: 6
estimated_files: 2
---

# T01: Implement patterns 1–7 (existence/count/reason checks) with tests

**Slice:** S02 — 13 assertion patterns
**Milestone:** M005

## Description

Implement the first 7 spec assertion patterns on `SelectionReportAssertionChain`:
- Pattern 1: `include_item_with_kind(kind: ContextKind)` — at least one included item has the given kind
- Pattern 2: `include_item_matching(predicate: impl Fn(&IncludedItem) -> bool)` — at least one included item satisfies the predicate
- Pattern 3: `include_exact_n_items_with_kind(kind: ContextKind, n: usize)` — exactly N included items have the given kind (n=0 valid)
- Pattern 4: `exclude_item_with_reason` — at least one excluded item carries the given `ExclusionReason` variant
- Pattern 5: `exclude_item_matching_with_reason` — predicate + reason discriminant match on excluded list
- Pattern 6: `have_excluded_item_with_budget_details(predicate, expected_item_tokens: i64, expected_available_tokens: i64)` — Rust-only full form with `BudgetExceeded` token field destructuring
- Pattern 7: `have_no_exclusions_for_kind(kind: ContextKind)` — no excluded item has the given kind

This task also removes `#[allow(dead_code)]` from the `report` field in `chain.rs` (the first assertion method that reads `self.report` makes the allow unnecessary) and creates the test file `tests/assertions.rs` with 14 integration tests.

## Steps

1. Remove `#[allow(dead_code)]` from the `report` field in `crates/cupel-testing/src/chain.rs`.

2. Add the following imports to `chain.rs`:
   ```rust
   use cupel::diagnostics::{ExcludedItem, ExclusionReason, IncludedItem};
   use cupel::model::ContextKind;
   ```

3. Implement patterns 1–7 as `pub fn` methods on `SelectionReportAssertionChain<'a>` returning `&mut Self`. Each method panics with the spec error message on failure. Follow the exact error message templates from `spec/src/testing/vocabulary.md`:
   - Pattern 1: `include_item_with_kind` → panic: `"include_item_with_kind({kind}) failed: Included contained 0 items with Kind={kind}. Included had {count} items with kinds: [{kinds}]."`
   - Pattern 2: `include_item_matching` → panic: `"include_item_matching failed: no item in Included matched the predicate. Included had {count} items."`
   - Pattern 3: `include_exact_n_items_with_kind` → panic: `"include_exact_n_items_with_kind({kind}, {n}) failed: expected {n} items with Kind={kind} in Included, but found {actual}. Included had {count} items total."`
   - Pattern 4: `exclude_item_with_reason` — reason matching via `std::mem::discriminant` or `matches!()` macro for variant matching; panic: `"exclude_item_with_reason({reason:?}) failed: no excluded item had reason {reason:?}. Excluded had {count} items with reasons: [{reasons}]."`
   - Pattern 5: `exclude_item_matching_with_reason` → panic: `"exclude_item_matching_with_reason(reason={reason:?}) failed: predicate matched {predicate_match_count} excluded item(s) but none had reason {reason:?}. Matched items had reasons: [{actual_reasons}]."`
   - Pattern 6: `have_excluded_item_with_budget_details` — destructure `ExclusionReason::BudgetExceeded { item_tokens, available_tokens }` with a match arm; panic: `"have_excluded_item_with_budget_details failed: expected BudgetExceeded with item_tokens={eIT}, available_tokens={eAT}, but found item_tokens={aIT}, available_tokens={aAT}."` (or `"but no matching item had reason BudgetExceeded"` when no BudgetExceeded item found)
   - Pattern 7: `have_no_exclusions_for_kind` → panic: `"have_no_exclusions_for_kind({kind}) failed: found {count} excluded item(s) with Kind={kind}. First: score={score:.4}, reason={reason:?}."`

   **Important for reason matching in patterns 4 and 5:** `ExclusionReason` is a data-carrying enum; use `std::mem::discriminant(&e.reason) == std::mem::discriminant(&reason)` for variant comparison, or pattern-match with `matches!()` where the expected reason is passed by value. Since the caller passes `reason: ExclusionReason` (by value), compare discriminants: `std::mem::discriminant(&e.reason) == std::mem::discriminant(&reason)`.

4. Create `crates/cupel-testing/tests/assertions.rs` with 14 integration tests (2 per pattern). Test construction pattern from smoke.rs: build a mini-pipeline, run via `run_traced`, get the `SelectionReport`. Use `Pipeline::builder()`, `ContextItemBuilder`, `ContextBudget`, `RecencyScorer`, `GreedySlice`, `UShapedPlacer`, `DiagnosticTraceCollector::new(TraceDetailLevel::Item)`. Import `SelectionReportAssertions` from `cupel_testing`. Negative tests use `#[should_panic(expected = "...")]` with a unique substring from the spec error message.

5. Run `cargo test --all-targets` in `crates/cupel-testing/` — all 15 tests (14 new + 1 smoke) must pass.

6. Run `cargo clippy --all-targets -- -D warnings` in both `crates/cupel-testing/` and `crates/cupel/` — both must be clean.

## Must-Haves

- [ ] `#[allow(dead_code)]` removed from `chain.rs` `report` field
- [ ] All 7 methods return `&mut Self` (not `Self` and not `SelectionReportAssertionChain<'a>` with a new lifetime)
- [ ] Pattern 4 and 5 use discriminant comparison (not string matching) for reason variant matching
- [ ] Pattern 6 destructures `ExclusionReason::BudgetExceeded { item_tokens, available_tokens }` directly with a `match` arm
- [ ] `tests/assertions.rs` created with 14 tests — 7 positive (`_passes` suffix) + 7 negative (`_panics` suffix)
- [ ] All negative tests use `#[should_panic(expected = "...")]` with a substring from the spec error message
- [ ] Mini-pipelines in tests produce real `SelectionReport` via `DiagnosticTraceCollector`
- [ ] `cargo test --all-targets` passes (15 tests)
- [ ] `cargo clippy --all-targets -- -D warnings` clean in both crates

## Verification

- `cd crates/cupel-testing && cargo test --all-targets` → `test result: ok. 15 passed`
- `cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings` → no output (clean)
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings` → no regressions

## Observability Impact

- Signals added/changed: Each failing assertion panics with a structured message — `cargo test` output captures the panic message; `#[should_panic]` verifies message content
- How a future agent inspects this: `cargo test -- --nocapture` shows full panic messages; individual test names identify which pattern failed
- Failure state exposed: Panic message names the assertion, states expected vs actual values with field counts and kind lists

## Inputs

- `crates/cupel-testing/src/chain.rs` — `SelectionReportAssertionChain<'a>` struct with `pub(crate)` constructor and `#[allow(dead_code)]` on `report` field
- `crates/cupel-testing/tests/smoke.rs` — test construction pattern: `DiagnosticTraceCollector::new(TraceDetailLevel::Item).into_report()`
- `crates/cupel/src/diagnostics/mod.rs` — `SelectionReport`, `IncludedItem`, `ExcludedItem`, `ExclusionReason` variants
- `spec/src/testing/vocabulary.md` — exact error message templates for all 7 patterns
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — reference implementation for all 7 patterns

## Expected Output

- `crates/cupel-testing/src/chain.rs` — 7 assertion methods implemented; `dead_code` allow removed
- `crates/cupel-testing/tests/assertions.rs` — new file with 14 integration tests (7 positive + 7 negative), all passing
