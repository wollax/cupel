---
estimated_steps: 6
estimated_files: 2
---

# T02: Implement patterns 8–13 (aggregate/budget/coverage/ordering) with tests

**Slice:** S02 — 13 assertion patterns
**Milestone:** M005

## Description

Implement the remaining 6 spec assertion patterns on `SelectionReportAssertionChain`:
- Pattern 8: `have_at_least_n_exclusions(n: usize)` — at least N items in the excluded list
- Pattern 9: `excluded_items_are_sorted_by_score_descending()` — conformance assertion on sort invariant
- Pattern 10: `have_budget_utilization_above(threshold: f64, budget: &ContextBudget)` — sum(included tokens) / budget.max_tokens() >= threshold; reuse `cupel::analytics::budget_utilization`
- Pattern 11: `have_kind_coverage_count(n: usize)` — at least N distinct ContextKind values in included; reuse `cupel::analytics::kind_diversity`
- Pattern 12: `place_item_at_edge(predicate: impl Fn(&IncludedItem) -> bool)` — item matching predicate is at position 0 or position count−1
- Pattern 13: `place_top_n_scored_at_edges(n: usize)` — top-N scored included items occupy the N outermost edge positions; **uses index-based approach** (collect top-N `(score, index)` pairs; no `HashSet<&IncludedItem>` since `IncludedItem` contains `f64` and cannot implement `Hash`)

This task adds 12 tests to `tests/assertions.rs` (2 per pattern), bringing the total to 26 assertion tests (plus the 1 smoke test = 27 total).

Pattern 13 requires special care for:
- `n = 0` → always passes
- `n > included.count` → panics with count mismatch message
- Tie-score handling: minTopScore = minimum score among top-N items; any item at an edge position with score >= minTopScore is valid

## Steps

1. Add `cupel::analytics` import to `chain.rs`:
   ```rust
   use cupel::analytics;
   use cupel::model::ContextBudget;
   ```

2. Implement patterns 8–12 as `pub fn` methods on `SelectionReportAssertionChain<'a>` returning `&mut Self`, panicking with spec messages on failure:
   - Pattern 8: `have_at_least_n_exclusions(n: usize)` → panic: `"have_at_least_n_exclusions({n}) failed: expected at least {n} excluded items, but Excluded had {actual}."`
   - Pattern 9: `excluded_items_are_sorted_by_score_descending()` — iterate adjacent pairs with `windows(2)` or index loop; panic: `"excluded_items_are_sorted_by_score_descending failed: item at index {i} (score={si:.6}) is higher than item at index {prev} (score={si_prev:.6}). Expected non-increasing scores."`
   - Pattern 10: `have_budget_utilization_above(threshold: f64, budget: &ContextBudget)` — call `analytics::budget_utilization(self.report, budget)`; panic: `"have_budget_utilization_above({threshold}) failed: computed utilization was {actual:.6} (includedTokens={included_tokens}, budget.MaxTokens={max_tokens})."`
   - Pattern 11: `have_kind_coverage_count(n: usize)` — call `analytics::kind_diversity(self.report)`; panic: `"have_kind_coverage_count({n}) failed: expected at least {n} distinct ContextKind values in Included, but found {actual}: [{actual_kinds}]."`
   - Pattern 12: `place_item_at_edge(predicate: impl Fn(&IncludedItem) -> bool)` — two error variants: "no item in Included matched the predicate" and "item matching predicate was at index {idx} (not at edge). Edge positions: 0 and {last}. Included had {count} items."

3. Implement Pattern 13: `place_top_n_scored_at_edges(n: usize)`:
   - `n = 0` → return `self` immediately
   - `n > self.report.included.len()` → panic with: `"place_top_n_scored_at_edges({n}) failed: n={n} exceeds Included count={count}."`
   - Collect `(score, original_index)` pairs: `self.report.included.iter().enumerate().map(|(i, item)| (item.score, i))`
   - Sort by score descending (use `f64::total_cmp` or `partial_cmp().unwrap_or(Equal)`)
   - Take first `n` entries → these are the top-N indices; extract `min_top_score` as the minimum score among them
   - Enumerate edge positions: lo starts at 0, hi starts at `count-1`; add `lo` then `hi` (skip second if `lo == hi`); stop when `n` positions collected
   - Build `edge_set: HashSet<usize>` of the expected edge position indices
   - Verify: for each top-N `(score, original_index)`, check `edge_set.contains(&original_index)`; count failures
   - On failure, format: `"place_top_n_scored_at_edges({n}) failed: {fail_count} of the top-{n} scored items were not at expected edge positions. Top-{n} items (by score): [{top_items}]. Expected edge positions: [{edge_positions}]."` where top_items is formatted as `(kind={kind}, score={score:.6}, idx={idx})`

4. Add 12 tests to `crates/cupel-testing/tests/assertions.rs`. Test structure:
   - Pattern 8: positive = pipeline with excluded items + `have_at_least_n_exclusions(1)`; negative = same report + `have_at_least_n_exclusions(999)` → `#[should_panic(expected = "have_at_least_n_exclusions")]`
   - Pattern 9: positive = real pipeline report (excluded is always sorted by DiagnosticTraceCollector); negative = use an empty excluded list scenario (skipped if guaranteed sorted) — instead construct a case where the excluded list has ≥2 items with different scores (achieved via multiple items where budget forces exclusions at different scores); negative panics if sorting contract is broken (test the assertion itself: build a synthetic scenario or verify the assertion detects a hand-crafted violation via a helper that calls the method on a real report with a known ordering)
   - Pattern 10: positive = pipeline run where items mostly fill budget; negative = `have_budget_utilization_above(0.9999)` on a near-empty report
   - Pattern 11: positive = pipeline with 2+ different kinds; negative = `have_kind_coverage_count(99)`
   - Pattern 12: positive = `UShapedPlacer` report where highest-scored item is at position 0; negative predicate matches an item at a non-edge position
   - Pattern 13 (≥3 edge cases via positive + negative): positive covers n=2 with 4 items where top-2 are at edges (positions 0 and 3); additional positive covers n=0 (always passes); negative covers n > included.count; tie-score covered by the description comment explaining it is exercised via the real pipeline

   **For Pattern 9 negative test:** Because `DiagnosticTraceCollector` always produces a valid sorted report, test the ASSERTION itself (not the pipeline). Create a test that verifies the assertion correctly identifies violations: run a pipeline with 2 excluded items, verify the report is sorted by calling the assertion (positive test). For the negative test, prove the assertion would fail on an out-of-order slice — instead use `have_at_least_n_exclusions` to ensure we have ≥2 excluded items, then verify `excluded_items_are_sorted_by_score_descending` passes (this is the positive test). For the negative, force an obvious scoring difference between two excluded items and verify the assertion passes — it's a conformance check. Note: since real pipelines always produce sorted output, the negative test for Pattern 9 should be documented as a "detection test" that proves the assertion can detect a known invalid state. Use `include_exact_n_items_with_kind` to demonstrate ordering is wrong — or simply mark the negative test as: directly construct a wrapper scenario where we embed a reverse-ordered pair assertion.

   **Pragmatic approach for Pattern 9 negative:** Since we cannot construct a `SelectionReport` directly (it's `#[non_exhaustive]`), for the negative test, write a test that calls `excluded_items_are_sorted_by_score_descending()` on a real report with ≥2 excluded items (which is always valid), demonstrating the positive case. Then prove the method logic is correct by testing with exactly 0 or 1 excluded items (both pass vacuously). For the negative test, use a pipeline that produces a report where we know the score ordering (scorer assigns predictable scores) and confirm the pattern passes; then demonstrate the negative by using `have_at_least_n_exclusions` helper to confirm ≥2 items were excluded. Accept that Pattern 9's negative test can only be verified at the unit level (the assertion logic reads adjacent pairs and panics if out of order), and write a comment in the test noting why a truly failing case cannot be constructed without direct `SelectionReport` construction.

5. Run `cargo test --all-targets` in `crates/cupel-testing/` — all 27 tests (26 assertion tests + 1 smoke) must pass.

6. Run `cargo clippy --all-targets -- -D warnings` in both `crates/cupel-testing/` and `crates/cupel/` — both must be clean.

## Must-Haves

- [ ] All 6 methods return `&mut Self`
- [ ] Pattern 10 uses `cupel::analytics::budget_utilization` (not inline reimplementation)
- [ ] Pattern 11 uses `cupel::analytics::kind_diversity` (not inline reimplementation)
- [ ] Pattern 13 uses index-based top-N approach — `Vec<(f64, usize)>` sorted by score; NO `HashSet<&IncludedItem>` or `HashSet<IncludedItem>` (f64 prevents Hash)
- [ ] Pattern 13 handles `n = 0` (immediate return), `n > count` (panic), and the general case
- [ ] Pattern 13 uses `f64::total_cmp` or equivalent for stable sort (NaN-safe)
- [ ] 12 new tests added to `tests/assertions.rs`, all passing
- [ ] Pattern 13 tests cover ≥3 distinct cases: n=0 passes, n>count panics, n=2 with valid placement passes
- [ ] `cargo test --all-targets` passes (27 tests: 26 assertion + 1 smoke)
- [ ] `cargo clippy --all-targets -- -D warnings` clean in both crates
- [ ] `cargo test --all-targets` in `crates/cupel/` shows no regressions (158 tests pass)

## Verification

- `cd crates/cupel-testing && cargo test --all-targets` → `test result: ok. 27 passed`
- `cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings` → no output (clean)
- `cd crates/cupel && cargo test --all-targets` → `test result: ok. 158 passed` (no regressions)
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings` → clean

## Observability Impact

- Signals added/changed: Pattern 10 and 11 panic messages include computed values (utilization ratio, distinct kind list) enabling post-failure diagnosis without re-running; Pattern 13 panic message includes top-N items with actual indices and expected edge positions
- How a future agent inspects this: `cargo test -- --nocapture` shows full panic messages; test names identify which pattern and whether positive/negative
- Failure state exposed: Each panic message is self-contained — it states the assertion name, what was expected, and the actual state of the report

## Inputs

- `crates/cupel-testing/src/chain.rs` — 7 assertion methods from T01; `dead_code` allow already removed
- `crates/cupel-testing/tests/assertions.rs` — 14 tests from T01 (to be extended with 12 more)
- `crates/cupel/src/analytics.rs` — `budget_utilization(report, budget)` and `kind_diversity(report)` free functions
- `crates/cupel/src/model/context_budget.rs` — `ContextBudget::max_tokens()` returns `i64`
- `spec/src/testing/vocabulary.md` — exact error message templates for patterns 8–13
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — reference implementation for patterns 8–13

## Expected Output

- `crates/cupel-testing/src/chain.rs` — all 13 assertion methods implemented
- `crates/cupel-testing/tests/assertions.rs` — 26 integration tests total (14 from T01 + 12 from T02), all passing
- Both crates: `cargo test --all-targets` passes, `cargo clippy --all-targets -- -D warnings` clean
