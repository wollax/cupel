---
estimated_steps: 6
estimated_files: 1
---

# T03: Spec patterns 8–13: Aggregate, Budget, Coverage, Ordering + final audit and regression

**Slice:** S05 — Cupel.Testing Vocabulary Design
**Milestone:** M002

## Description

Write the full per-pattern specs for the remaining 6 patterns (aggregate counts, budget utilization, kind coverage, placement assertions). Then perform a document-wide precision audit to ensure no TBD fields, no undefined terms, and no "high-scoring" language remain. Run the test suites to confirm no regressions from the spec-only changes.

This task completes the vocabulary document and validates it end-to-end. T01 built the skeleton; T02 wrote 7 patterns; this task writes the final 6 and audits the whole document.

## Steps

1. Write the **Aggregate Counts group** (2 patterns):
   - **`HaveAtLeastNExclusions(int n)`** — asserts `Excluded.Count >= n`. N=0 is a valid spelling (always passes unless the pipeline throws). Error message format: `"HaveAtLeastNExclusions({n}) failed: expected at least {n} excluded items, but Excluded had {actual}."` Note: N=0 should be documented as a valid form; there is no separate `HaveNoExclusionsRequired()` pattern — use `HaveAtLeastNExclusions(0)`.
   - **`HaveBudgetUtilizationAbove(double threshold, ContextBudget budget)`** — asserts `sum(included[i].tokens) / budget.MaxTokens >= threshold`. Denominator = `budget.MaxTokens` (the hard token ceiling); this is the value the caller sets at construction time. `TargetTokens` is explicitly NOT the denominator — it is a Slice-stage-internal soft target and is not a public capacity metric. Comparison = exact `>=` with no epsilon (floating-point edge cases are test authoring responsibility per T01 pre-decisions). Edge cases: `budget.MaxTokens == 0` is a pipeline error (ContextBudget validates MaxTokens > 0 at construction); this assertion does not need to handle it. Empty `Included` → utilization = 0.0. Error message format: `"HaveBudgetUtilizationAbove({threshold}) failed: computed utilization was {actual:.6f} (includedTokens={includedTokens}, budget.MaxTokens={maxTokens})."`
2. Write the **Kind Coverage group** (1 pattern) and **Conformance Assertions group** (1 pattern):
   - **`HaveKindCoverageCount(int n)`** — asserts `Included.Select(i => i.Item.Kind).Distinct().Count() >= n`. No ordering dependency. Error message format: `"HaveKindCoverageCount({n}) failed: expected at least {n} distinct ContextKind values in Included, but found {actual}: [{actualKinds}]."`
   - **`ExcludedItemsAreSortedByScoreDescending()`** — conformance assertion. Asserts: for all `0 <= i < Excluded.Count - 1`, `Excluded[i].Score >= Excluded[i+1].Score`. This is a conformance assertion — a correct Cupel pipeline implementation must always satisfy it. Mark with a **Conformance assertion** callout. Caveat: the insertion-order tiebreak for equal scores is guaranteed by the implementation (D019) but is NOT assertable from the report alone — the report does not expose an insertion index. This assertion therefore checks score-descending only, not tiebreak order. Error message format: `"ExcludedItemsAreSortedByScoreDescending failed: item at index {i+1} (score={si1}) is higher than item at index {i} (score={si}). Expected non-increasing scores."`
3. Write the **Ordering group** (2 patterns) with Placer dependency caveat applied consistently:
   - **`PlaceItemAtEdge(Func<IncludedItem, bool> predicate)`** — asserts `predicate(Included[0]) || predicate(Included[Included.Count - 1])`. "Edge" = position 0 (first) OR position `Included.Count − 1` (last). Nothing more. Tie-breaking: if multiple items share the score at the edge position, the assertion passes if the named item occupies one of the two boundary positions — it does not pass merely because the item has the same score as an edge item. **Placer dependency caveat** (in a callout block): "This assertion is only meaningful when the caller knows the Placer's ordering contract. For `UShapedPlacer`, position 0 holds the highest-scored item and position N−1 holds the second-highest. For other Placers, consult the Placer spec. Do not use this assertion against output from an unknown or unspecified Placer." Error message format: `"PlaceItemAtEdge failed: item matching predicate was at index {actual} (not at edge). Edge positions: 0 and {last}. Included had {count} items."` If item is not in Included at all: `"PlaceItemAtEdge failed: no item in Included matched the predicate."`
   - **`PlaceTopNScoredAtEdges(int n)`** — asserts that the `n` items with the highest `score` values in `Included` occupy the `n` outermost positions. Edge position mapping: enumerate positions as 0, `count−1`, 1, `count−2`, … for N items. Step 1: sort `Included` by score descending — these are the top-N items. Step 2: verify that each of the top-N items occupies one of the first `n` edge positions. Tie-score handling: if multiple items share the score at position N (i.e. the score of the N-th top item equals the score of the (N+1)-th item), then any item with that tied score is a valid occupant of the N-th edge position — the assertion does not require a specific item. **Placer dependency caveat** (same language as PlaceItemAtEdge). Error message format: `"PlaceTopNScoredAtEdges({n}) failed: {failCount} of the top-{n} scored items were not at expected edge positions. Top-{n} items (by score): [{topItems}]. Expected edge positions: [{edgePositions}]."`
4. Add a **Notes** section at the end of the vocabulary document covering: (a) D041 prohibition — snapshot assertions are deferred until `SelectionReport` ordering stability for ties is guaranteed; the insertion-order tiebreak for equal scores in `Excluded` makes serialized ordering non-deterministic unless the test controls the full input set and all scores are distinct; (b) `TotalTokensConsidered` is a candidate-set metric (`sum(included.tokens) + sum(excluded.tokens)`) — it measures aggregate candidate volume, not budget utilization; callers should use `HaveBudgetUtilizationAbove` for utilization assertions, not a `TotalTokensConsidered`-based assertion; (c) `SelectionReportAssertionException` should be a dedicated exception type (not `InvalidOperationException`) so test frameworks can report assertion failures distinctly.
5. Perform a document-wide precision audit:
   - `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md` → must be 0
   - `grep -c "high-scoring\|high scoring\|dominant\|important\|recent" spec/src/testing/vocabulary.md` → must be 0 (or only in quoted/example contexts)
   - `grep -c "^### " spec/src/testing/vocabulary.md` → must be ≥ 13
   - `grep -c "Error message format\|**Error message" spec/src/testing/vocabulary.md` → must be ≥ 10
   - Manually review all Ordering patterns for Placer caveat presence
6. Run the test suites to confirm no regressions (spec-only changes should not affect any tests):
   - `cargo test --manifest-path crates/cupel/Cargo.toml`
   - `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`

## Must-Haves

- [ ] `HaveAtLeastNExclusions` fully specified: N=0 valid, error message format
- [ ] `HaveBudgetUtilizationAbove` fully specified: MaxTokens denominator with rationale, exact `>=` comparison, empty-Included edge case, error message format
- [ ] `HaveKindCoverageCount` fully specified: distinct Kind count, error message format
- [ ] `ExcludedItemsAreSortedByScoreDescending` fully specified: score-descending property only, tiebreak caveat (not assertable from report alone), marked as conformance assertion
- [ ] `PlaceItemAtEdge` fully specified: "edge" = position 0 or count−1 (nothing more), Placer dependency caveat, error message for item-found-at-wrong-index and item-not-found cases
- [ ] `PlaceTopNScoredAtEdges` fully specified: edge-position mapping (0, count−1, 1, count−2, …), tie-score handling, Placer dependency caveat, error message format
- [ ] Notes section: D041 snapshot prohibition, `TotalTokensConsidered` is not a utilization metric
- [ ] Document-wide audit: 0 TBD fields, 0 "high-scoring" language, ≥ 13 patterns, ≥ 10 error message format entries
- [ ] `cargo test` passes with no regressions
- [ ] `dotnet test` passes with no regressions

## Verification

```bash
# Pattern count
grep -c "^### " spec/src/testing/vocabulary.md   # → 13

# No TBD
grep -ci "\bTBD\b" spec/src/testing/vocabulary.md   # → 0

# No "high-scoring"
grep -c "high-scoring\|high scoring" spec/src/testing/vocabulary.md   # → 0

# Error message format present (≥ 10)
grep -c "Error message format\|Error message:" spec/src/testing/vocabulary.md   # → ≥ 10

# Placer caveat present for ordering patterns
grep -q "Placer" spec/src/testing/vocabulary.md && echo "PASS"

# SUMMARY.md link
grep -q "testing/vocabulary" spec/src/SUMMARY.md && echo "PASS"

# Test suites
cargo test --manifest-path crates/cupel/Cargo.toml
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj
```

## Observability Impact

- Signals added/changed: None (spec-only)
- How a future agent inspects this: `grep -c "^### " spec/src/testing/vocabulary.md` for pattern count; `grep -ci "\bTBD\b"` for completeness; `cargo test` + `dotnet test` for regressions
- Failure state exposed: The grep audit checks localize exactly which must-have is missing; test suite output identifies any unexpected regression

## Inputs

- `spec/src/testing/vocabulary.md` — document with T01 skeleton + T02 patterns 1-7
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — B01-B05, K01-K04, O01-O06, D01-D03 per-pattern precision analyses; DI-4 (PlaceTopNScoredAtEdges edge-position mapping and tie-score handling)
- `spec/src/diagnostics/selection-report.md` — `Excluded` is score-descending (stable insertion-order tiebreak, D019); this is the invariant `ExcludedItemsAreSortedByScoreDescending` tests
- D019 (insertion-order tiebreak, unobservable from report alone)
- D025 (`available_tokens = effective_target - sum(sliced_item.tokens)`)
- D041 (no snapshots, no FluentAssertions)
- D045 (BudgetUtilization denominator = MaxTokens)

## Expected Output

- `spec/src/testing/vocabulary.md` — complete vocabulary document with 13 fully-specified patterns, Notes section, no TBD fields; ~350–450 lines total
- Clean test runs: `cargo test` and `dotnet test` both pass (no regressions)
