---
title: "Add ContextItem creation benchmark"
area: benchmarks
priority: low
source: pr-review-phase-1
---

# Benchmark ContextItem creation and with-expressions

The current benchmark only covers empty pipeline iteration. Consider adding benchmarks for:
- ContextItem construction with all properties
- `with` expression copy cost
- ContextBudget construction with validation

**Consideration:** Nice to have for Phase 3+ when scorer performance matters. Not blocking.
