---
id: T02
parent: S02
milestone: M005
provides:
  - 6 assertion methods on SelectionReportAssertionChain (patterns 8–13)
  - 12 integration tests in crates/cupel-testing/tests/assertions.rs (total 27 passing)
key_files:
  - crates/cupel-testing/src/chain.rs
  - crates/cupel-testing/tests/assertions.rs
key_decisions:
  - Pattern 13 uses Vec<(f64, usize)> sorted by f64::total_cmp for NaN-safe stable sort (no HashSet<&IncludedItem>)
  - Pattern 9 negative test is a "vacuous pass" test (0/1 excluded items) with explanatory comment; direct SelectionReport construction is blocked by #[non_exhaustive]
  - Ordering tests (patterns 12/13) use make_priority_pipeline() with PriorityScorer + .priority(n) to produce distinct scores; RecencyScorer gives all items score=0.0 when no timestamps are present
patterns_established:
  - Priority pipeline pattern: Pipeline::builder() + PriorityScorer + GreedySlice + UShapedPlacer; items use .priority(n) for distinct scores
  - Pattern 13 index-based approach: collect (score, original_index) pairs, sort by score descending, enumerate edge positions lo/hi inward, build HashSet<usize> for edge containment check
  - Edge position enumeration: lo starts at 0, hi starts at count-1, add lo then hi (skip if lo==hi), advance lo++ and hi.saturating_sub(1) each iteration
observability_surfaces:
  - Pattern 10 panic: includes computed utilization ratio + includedTokens + budget.MaxTokens
  - Pattern 11 panic: includes actual distinct kind count + comma-separated kind list
  - Pattern 13 panic: includes fail_count, top-N items formatted as (kind=K, score=S, idx=I), expected edge positions
duration: ~30 minutes
verification_result: passed
completed_at: 2026-03-24
blocker_discovered: false
---

# T02: Implement patterns 8–13 (aggregate/budget/coverage/ordering) with tests

**Implemented 6 fluent assertion methods (patterns 8–13) on `SelectionReportAssertionChain` with 12 integration tests; all 27 tests pass, both crates clippy-clean.**

## What Happened

Added 6 methods to `crates/cupel-testing/src/chain.rs`:
- **Pattern 8** `have_at_least_n_exclusions(n)` — count check on `excluded` list
- **Pattern 9** `excluded_items_are_sorted_by_score_descending()` — checks adjacent pairs with index loop
- **Pattern 10** `have_budget_utilization_above(threshold, budget)` — delegates to `analytics::budget_utilization`
- **Pattern 11** `have_kind_coverage_count(n)` — delegates to `analytics::kind_diversity`
- **Pattern 12** `place_item_at_edge(predicate)` — checks first matching item is at index 0 or count-1
- **Pattern 13** `place_top_n_scored_at_edges(n)` — index-based approach with `Vec<(f64, usize)>`; handles n=0, n>count, tie-scores

All methods return `&mut Self`, import `cupel::analytics` and `cupel::model::ContextBudget` at the top of chain.rs.

## Verification

```
cd crates/cupel-testing && cargo test --all-targets  → test result: ok. 27 passed; 0 failed
cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings  → clean
cd crates/cupel && cargo test --all-targets  → 158 tests across all modules, 0 failed
cd crates/cupel && cargo clippy --all-targets -- -D warnings  → clean
```

## Diagnostics

- `cargo test -- --nocapture` shows full panic messages for all assertion failures
- Pattern 10 panic names the computed utilization + token counts; Pattern 11 names the kind list; Pattern 13 names each top-N item with score and original index and the expected edge positions
- Test names: `*_passes` (positive) and `*_panics` (negative with `#[should_panic(expected = "...")]`)

## Deviations

- **Pattern 9 negative test**: Per plan, direct `SelectionReport` construction is impossible (`#[non_exhaustive]`). Wrote a vacuous-pass test on 0 excluded items with an explanatory comment proving the assertion handles empty and single-item lists correctly. Named `excluded_items_are_sorted_by_score_descending_vacuous_pass_on_zero_or_one` to be self-documenting.
- **Ordering tests use `make_priority_pipeline()`**: `RecencyScorer` requires timestamps; items without timestamps score 0.0, making `UShapedPlacer` placement non-deterministic. Added a `make_priority_pipeline()` helper using `PriorityScorer` and `.priority(n)` to produce deterministic distinct scores.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel-testing/src/chain.rs` — Added 6 assertion methods (patterns 8–13) and imports for `cupel::analytics` and `cupel::model::ContextBudget`
- `crates/cupel-testing/tests/assertions.rs` — Added 12 integration tests (2 per pattern 8–13), `make_priority_pipeline()` helper, `PriorityScorer` import
