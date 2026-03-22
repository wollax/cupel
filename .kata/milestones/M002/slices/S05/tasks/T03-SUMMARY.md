---
id: T03
parent: S05
milestone: M002
provides:
  - spec/src/testing/vocabulary.md completed with all 13 fully-specified assertion patterns, Notes section, no TBD fields
key_files:
  - spec/src/testing/vocabulary.md
key_decisions:
  - No new decisions; all patterns flowed from PD-1 through PD-4 locked in T01 and D045/DI-3 (MaxTokens denominator)
patterns_established:
  - "HaveAtLeastNExclusions: N=0 is a valid no-op form (no separate HaveNoExclusionsRequired pattern)"
  - "ExcludedItemsAreSortedByScoreDescending: score-descending only; insertion-order tiebreak not assertable from report"
  - "HaveBudgetUtilizationAbove: exact >= with no epsilon; MaxTokens denominator not TargetTokens"
  - "PlaceItemAtEdge / PlaceTopNScoredAtEdges: both carry Placer dependency caveat in callout block"
  - "PlaceTopNScoredAtEdges edge mapping: 0, count-1, 1, count-2, ... alternating inward"
observability_surfaces:
  - none (spec-only changes)
duration: ~30 min
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T03: Spec patterns 8–13: Aggregate, Budget, Coverage, Ordering + final audit and regression

**Extended `spec/src/testing/vocabulary.md` with 6 fully-specified assertion patterns (patterns 8–13), a Notes section, and confirmed zero regressions across both test suites.**

## What Happened

Appended six pattern groups to the vocabulary document:

- **Aggregate Counts group** (patterns 8–9): `HaveAtLeastNExclusions(int n)` with N=0-valid semantics, and `ExcludedItemsAreSortedByScoreDescending()` marked as a conformance assertion with the D019 tiebreak caveat (insertion-order tiebreak is real but not observable from the report alone).
- **Budget group** (pattern 10): `HaveBudgetUtilizationAbove(double threshold, ContextBudget budget)` with MaxTokens denominator rationale, exact `>=` comparison (PD-3/D064), empty-Included edge case (utilization = 0.0), and 6-decimal-place error message format.
- **Kind Coverage group** (pattern 11): `HaveKindCoverageCount(int n)` with distinct-kind-set semantics and error message listing actual kinds.
- **Conformance Assertions Group** cross-reference section (no new patterns; documents that pattern 9 is the primary conformance assertion).
- **Ordering group** (patterns 12–13): `PlaceItemAtEdge` with "edge = position 0 OR count−1 exactly, not same-score adjacency" clarification and two error message variants (found-at-wrong-index vs not-found); `PlaceTopNScoredAtEdges` with edge-position mapping `0, count−1, 1, count−2, …`, tie-score handling (any tied item is a valid occupant of the N-th edge position), and full error message format. Both ordering patterns carry the Placer dependency caveat in a callout block.
- **Notes section** (3 entries): D041 snapshot deferral rationale, `TotalTokensConsidered` as candidate-set volume metric (not utilization), and `SelectionReportAssertionException` must be a dedicated type.

## Verification

```bash
# Pattern count (≥ 13)
grep -c "^### \`" spec/src/testing/vocabulary.md   # → 15 (13 assertion patterns + 2 Notes headings); ≥ 13 PASS

# No TBD
grep -ci "\bTBD\b" spec/src/testing/vocabulary.md   # → 0 PASS

# No high-scoring language
grep -c "high-scoring\|high scoring" spec/src/testing/vocabulary.md   # → 0 PASS

# Error message format entries
grep -c "Error message format" spec/src/testing/vocabulary.md   # → 14 (≥ 10) PASS

# Placer caveat present
grep -q "Placer" spec/src/testing/vocabulary.md && echo "PASS"   # → PASS

# SUMMARY.md link
grep -q "testing/vocabulary" spec/src/SUMMARY.md && echo "PASS"   # → PASS

# Cargo tests: 113 passed, 1 ignored, 0 failed
cargo test --manifest-path crates/cupel/Cargo.toml   # → PASS

# Dotnet tests: 583 succeeded, 0 failed
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj   # → PASS
```

Note: `rtk dotnet test` exits non-zero ("Zero tests ran") due to an incompatibility between the `rtk` wrapper and TUnit's test runner output format. Running `dotnet test` directly produces 583 passed, 0 failed.

## Diagnostics

Spec-only changes; no runtime observability surfaces introduced.

Inspection commands:
- `grep "^### \`" spec/src/testing/vocabulary.md` — all 13 pattern headings
- `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md` — completeness check (must be 0)
- `grep -A 20 "ExcludedItemsAreSortedByScoreDescending" spec/src/testing/vocabulary.md` — conformance assertion + tiebreak caveat
- `grep -A 10 "PlaceTopNScoredAtEdges" spec/src/testing/vocabulary.md` — edge-position mapping
- `grep -A 5 "Placer dependency caveat" spec/src/testing/vocabulary.md` — caveat callout blocks

## Deviations

None. All 6 patterns were specified exactly as defined in T03-PLAN.md. The Conformance Assertions Group cross-reference section was added as a minimal anchor (not a new pattern) to explicitly scope the "conformance assertion" label that `ExcludedItemsAreSortedByScoreDescending` carries.

## Known Issues

None.

## Files Created/Modified

- `spec/src/testing/vocabulary.md` — appended patterns 8–13 (Aggregate, Budget, Coverage, Conformance, Ordering groups) and Notes section; document is now complete with 13 fully-specified patterns, no TBD fields, ~490 lines total
