---
title: "Phase 5 review: test coverage suggestions"
area: tests
source: PR #16 review
priority: low
---

# Phase 5 Review: Test Coverage Suggestions

Suggestions from PR #16 code review for additional test coverage:

- GreedySlice: test items exactly filling budget (no leftover tokens)
- GreedySlice: test with all zero-token items
- Dedup stage: test interaction with scoring (dedup preserving highest-scored duplicate)
- UShapedPlacer: test with items that have identical scores (verify stable sort)
- ChronologicalPlacer: test all-null timestamps
- CupelPipeline: test single item input (boundary case)
- CupelPipeline: test all items with negative tokens (should return empty)
- ScoredItem: boundary/invalid scores (NaN, infinity, negative)
- NullTraceCollector: verify absence of side-effects rather than vacuous "doesn't throw" tests
- DiagnosticTraceCollector: document callback exception propagation behavior
- TraceEvent inequality: vary Duration and ItemCount (not just Stage)
