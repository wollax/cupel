---
phase: "04"
plan: "04-01"
title: "CompositeScorer with weighted average and cycle detection"
subsystem: scoring
tags: [composite, weighted-average, cycle-detection, DFS]
dependency-graph:
  requires: [phase-03]
  provides: [CompositeScorer]
  affects: [phase-04-02, phase-04-03, phase-05]
tech-stack:
  added: []
  patterns: [weighted-average-normalization, DFS-cycle-detection, parallel-arrays]
key-files:
  created:
    - src/Wollax.Cupel/Scoring/CompositeScorer.cs
  modified:
    - tests/Wollax.Cupel.Tests/Scoring/CompositeScorerTests.cs
    - src/Wollax.Cupel/PublicAPI.Unshipped.txt
decisions:
  - id: "04-01-D1"
    decision: "Floating-point tolerance (1e-14) for relative weight equivalence tests"
    rationale: "Different weight sums produce sub-ULP differences in normalized weights"
metrics:
  duration: "~8 minutes"
  completed: "2026-03-13"
---

# Phase 4 Plan 1: CompositeScorer with Weighted Average and Cycle Detection Summary

CompositeScorer: sealed IScorer combining multiple child scorers via pre-normalized weighted average with DFS cycle detection using ReferenceEqualityComparer at construction time.

## What Was Built

### CompositeScorer (`src/Wollax.Cupel/Scoring/CompositeScorer.cs`)

- **Sealed class** implementing `IScorer` with weighted average aggregation
- **Constructor** accepts `IReadOnlyList<(IScorer Scorer, double Weight)>` entries
- **Weight normalization**: relative weights pre-normalized at construction (`w[i] / totalWeight`)
- **Validation**: null entries, empty list, null scorer, zero/negative/infinite/NaN weights
- **Cycle detection**: DFS with `ReferenceEqualityComparer.Instance` traverses CompositeScorer children and ScaledScorer wrappers
- **Score()**: zero-allocation for-loop multiply-accumulate over parallel `IScorer[]` and `double[]` arrays

### Tests (`tests/Wollax.Cupel.Tests/Scoring/CompositeScorerTests.cs`)

18 tests covering:
- 7 constructor validation tests (null, empty, null scorer, zero/negative/infinite/NaN weight)
- 5 weighted average scoring tests (single scorer, equal weights, weight dominance, relative weights, three scorers)
- 2 nesting tests (valid scores, ordinal relationships)
- 4 cycle detection tests (deep nesting, diamond DAG, same instance reused, different instances of same type)

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| 04-01-D1 | Use `Within(1e-14)` tolerance for relative weight equivalence test | Different total weights (3.0 vs 0.9) produce sub-ULP differences during normalization division |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ScaledScorer traversal in cycle detection**

- **Found during:** Task 1 implementation
- **Issue:** ScaledScorer.cs and ScaledScorerTests.cs already existed from a prior partial execution of Plan 04-02, blocking the test project build
- **Fix:** Activated ScaledScorer cycle detection traversal immediately (instead of leaving as commented-out placeholder) since ScaledScorer already existed on the branch
- **Files modified:** `src/Wollax.Cupel/Scoring/CompositeScorer.cs`

## Test Results

- **CompositeScorer tests:** 18/18 passing
- **Full test suite:** 230/230 passing
- **Build:** Zero warnings

## Next Phase Readiness

CompositeScorer is ready for use by:
- **Plan 04-02 (ScaledScorer):** Already implemented on this branch; cycle detection already traverses ScaledScorer.Inner
- **Plan 04-03 (Stable sort/integration):** CompositeScorer produces scalar scores ready for pipeline sorting
- **Phase 5 (Pipeline):** CompositeScorer can serve as the pipeline's scorer
