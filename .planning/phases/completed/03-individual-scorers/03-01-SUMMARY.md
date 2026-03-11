# Phase 03 Plan 01: Rank-based and Passthrough Scorers Summary

Rank-based RecencyScorer/PriorityScorer + passthrough ReflexiveScorer with zero-allocation for-loop discipline

## Tasks Completed

### Task 1: RecencyScorer and PriorityScorer (TDD)
- **RED:** 15 failing tests across RecencyScorerTests (8 tests) and PriorityScorerTests (7 tests)
- **GREEN:** Rank-based linear interpolation algorithm — for-loop counts items with lesser value, interpolates rank/(countWithValues-1)
- No LINQ, no foreach, no closures in Score() methods
- Handles null values (returns 0.0), single-item (returns 1.0), tied values (equal scores), and mixed null/valid correctly

### Task 2: ReflexiveScorer (TDD)
- **RED:** 7 failing tests covering null hint, valid passthrough, clamping above/below bounds, boundary values, and allItems independence
- **GREEN:** Simple Math.Clamp passthrough — null returns 0.0, otherwise clamps FutureRelevanceHint to [0.0, 1.0]
- PublicAPI.Unshipped.txt updated with entries for all three scorers

## Deviations

- PublicAPI.Unshipped.txt also received auto-generated entries for KindScorer and TagScorer (from parallel 03-02 execution whose implementation files existed in working tree). These entries are correct and prevent RS0016 build errors.

## Verification

- Solution builds with zero warnings (TreatWarningsAsErrors active)
- 193 tests pass (157 pre-existing + 15 RecencyScorer/PriorityScorer + 7 ReflexiveScorer + 14 KindScorer/TagScorer from parallel 03-02)
- All scorer Score() methods use `for` loops with indexer access only — zero heap allocations

## Commits

| Hash | Message |
|------|---------|
| aa8ce40 | test(03-01): add failing tests for RecencyScorer and PriorityScorer |
| 5a4bb2f | feat(03-01): implement RecencyScorer and PriorityScorer with rank-based linear interpolation |
| bcd1525 | test(03-01): add failing tests for ReflexiveScorer |
| c3a2689 | feat(03-01): implement ReflexiveScorer with FutureRelevanceHint passthrough and clamping |

## Artifacts

| File | Purpose |
|------|---------|
| src/Wollax.Cupel/Scoring/RecencyScorer.cs | Rank-based timestamp scorer implementing IScorer |
| src/Wollax.Cupel/Scoring/PriorityScorer.cs | Rank-based priority scorer implementing IScorer |
| src/Wollax.Cupel/Scoring/ReflexiveScorer.cs | Passthrough scorer for FutureRelevanceHint implementing IScorer |
| tests/Wollax.Cupel.Tests/Scoring/RecencyScorerTests.cs | 8 ordinal and boundary tests for RecencyScorer |
| tests/Wollax.Cupel.Tests/Scoring/PriorityScorerTests.cs | 7 ordinal and boundary tests for PriorityScorer |
| tests/Wollax.Cupel.Tests/Scoring/ReflexiveScorerTests.cs | 7 boundary and clamping tests for ReflexiveScorer |
| src/Wollax.Cupel/PublicAPI.Unshipped.txt | Updated with entries for all three scorers |
