# Phase 3: Individual Scorers — UAT

**Date:** 2026-03-11
**Phase:** 03-individual-scorers

## Tests

| # | Test | Expected | Status |
|---|------|----------|--------|
| 1 | RecencyScorer: newer item scores higher than older | Most recent → 1.0, oldest → 0.0 | pass |
| 2 | PriorityScorer: higher priority scores higher | Priority=10 > Priority=1 | pass |
| 3 | ReflexiveScorer: clamps out-of-range hints | 1.5 → 1.0, -0.3 → 0.0 | pass |
| 4 | KindScorer: default weights produce correct ordinal | SystemPrompt > Memory > ToolOutput > Document > Message | pass |
| 5 | TagScorer: matched weights normalized correctly | All tags match → 1.0, partial → proportional | pass |
| 6 | FrequencyScorer: tag co-occurrence produces 0-1 score | More shared peers → higher score | pass |
| 7 | Zero-allocation benchmark compiles | ScorerBenchmark.cs builds with MemoryDiagnoser | pass |
| 8 | Full test suite passes | 202 tests, zero failures | pass |

## Result

**8/8 passed** — All individual scorers verified.
