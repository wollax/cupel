---
phase: 25
name: Conformance Infrastructure & Drift Guard
started: 2026-03-15
status: passed
tests: 8
passed: 8
failed: 0
---

# Phase 25 UAT: Conformance Infrastructure & Drift Guard

## Tests

| # | Test | Status | Notes |
|---|------|--------|-------|
| 1 | knapsack-basic.toml: no abandoned scenario or scratchpad text | ✓ | Clean — starts with redesigned big/small-a/small-b items |
| 2 | composite-weighted.toml: no "Wait —" scratchpad text | ✓ | Priority values intact, scratchpad removed |
| 3 | pinned-items.toml: density-sort step present in greedy section | ✓ | Density values and sort order shown before fill trace |
| 4 | u-shaped vectors: result[N] notation, no left[N]/right[N] | ✓ | Both files use result[N] exclusively |
| 5 | All 3 conformance directory copies byte-identical | ✓ | diff -rq confirms zero differences |
| 6 | CI drift guard: spec/** in path triggers, drift guard step present | ✓ | Loop-based, asymmetric guard, shell: bash |
| 7 | format.md: Diagnostics Vectors schema section complete | ✓ | 11 fields, ordering/optionality/compatibility notes, example |
| 8 | diagnostics-budget-exceeded.toml: exists in all copies, valid schema | ✓ | 3 copies, all diagnostics sub-tables, corrected TBD comment |
