---
id: T01
parent: S06
milestone: M001
provides:
  - DP table size guard in KnapsackSlice.Slice() — throws InvalidOperationException when (long)candidateCount * (capacity+1) > 50_000_000L
  - Guard uses post-discretization capacity and long arithmetic to prevent int overflow
  - Exception message includes candidates=N, capacity=C, cells=K for diagnosability
  - Test: DpTableGuard_AtExactLimit_Passes (50M cells exactly → no throw)
  - Test: DpTableGuard_OneAboveLimit_Throws (50,005,000 cells → throws)
  - Test: DpTableGuard_ClearlyOverLimit_Throws (10000 items × 5001 capacity → throws)
  - Test: NegativeTokenItems_SilentlyExcluded (verifies existing filter logic has coverage)
requires: []
affects: []
key_files:
  - src/Wollax.Cupel/KnapsackSlice.cs
  - tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs
key_decisions:
  - Guard inserted after discretizedWeights is built (candidateCount is final) and before ArrayPool.Rent calls
  - Uses (long)candidateCount * (capacity + 1) with long cast on candidateCount to avoid int overflow
  - Condition is > 50_000_000L (strictly greater), so exactly 50M cells passes
patterns_established:
  - OOM-bound guard pattern: compute cell count with long arithmetic, throw InvalidOperationException with diagnostic interpolation before allocating
drill_down_paths:
  - .kata/milestones/M001/slices/S06/tasks/T01-PLAN.md
duration: 10min
verification_result: pass
blocker_discovered: false
completed_at: 2026-03-21T00:00:00Z
---

# T01: Add KnapsackSlice DP table size guard

**DP table OOM guard added to KnapsackSlice.Slice(); 4 new tests pass; full suite (653 tests) passes with zero regressions.**

## What Happened

Inserted the R002 guard into `KnapsackSlice.cs` immediately after `discretizedWeights` is built and before the `ArrayPool.Rent` calls. The guard computes `cellCount = (long)candidateCount * (capacity + 1)` and throws `InvalidOperationException` when `cellCount > 50_000_000L`. The exception message includes all three diagnostic fields: `candidates=N`, `capacity=C`, `cells=K`, making it actionable for callers and future agents.

Four tests added to `KnapsackSliceTests.cs`:
- `DpTableGuard_AtExactLimit_Passes` — 5000 items × targetTokens=9999 (bucketSize=1) → 5000×10000 = 50M cells → no throw (condition is `>`, not `>=`)
- `DpTableGuard_OneAboveLimit_Throws` — 5000 items × targetTokens=10000 → 5000×10001 = 50,005,000 cells → `InvalidOperationException`
- `DpTableGuard_ClearlyOverLimit_Throws` — 10000 items × targetTokens=5000 → 10000×5001 = 50,010,000 cells → `InvalidOperationException`
- `NegativeTokenItems_SilentlyExcluded` — verifies existing `tokens > 0` filter in the candidate loop; negative-token items are not included in output, no crash

## Deviations

None. Implementation exactly matches the plan. Guard arithmetic uses `long` cast on `candidateCount` (the first operand) which is sufficient to promote the entire multiplication to `long`.

## Files Created/Modified

- `src/Wollax.Cupel/KnapsackSlice.cs` — guard inserted after discretizedWeights loop, before ArrayPool.Rent
- `tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs` — 4 new tests appended
