---
created: 2026-03-13T00:00
title: Phase 4 PR review suggestions
area: testing
provenance: github:wollax/cupel#15
files:
  - tests/Wollax.Cupel.Tests/Scoring/CompositeScorerTests.cs
  - tests/Wollax.Cupel.Tests/Scoring/ScaledScorerTests.cs
  - benchmarks/Wollax.Cupel.Benchmarks/ScorerBenchmark.cs
  - src/Wollax.Cupel/Scoring/CompositeScorer.cs
---

## Problem

Six minor suggestions from Phase 4 PR review that were not addressed during the fix pass:

1. **Missing test for null scorer in non-first position** — CompositeScorer constructor validation only tests null at index 0, not at later indices
2. **Duplicated CreateItem helper** — identical helper exists in both CompositeScorerTests and ScaledScorerTests, should be extracted to shared fixture
3. **Benchmark naming inconsistency** — individual scorer benchmarks use plain names (Recency, Priority) while composite ones use qualified names (CompositeScorer_Score)
4. **DFS algorithm comment** — DetectCyclesCore could use a brief inline comment explaining visited vs inPath two-set invariant (partially addressed but could be expanded)
5. **Stale benchmark doc comment** — says "six scorers" but now covers eight
6. **Cycle detection traversal limitation** — only covers CompositeScorer and ScaledScorer children; future IScorer wrappers would be invisible to DFS — document this

## Solution

TBD — batch these as a cleanup task, likely in a future phase or as a quick task.
