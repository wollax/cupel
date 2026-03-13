---
phase: "06"
plan: "06-02"
subsystem: "slicing"
tags: ["quota", "decorator", "slicer", "budget-partitioning"]
dependency-graph:
  requires: ["01", "02", "05"]
  provides: ["QuotaSet", "QuotaBuilder", "QuotaSlice"]
  affects: ["06-04", "06-05", "08"]
tech-stack:
  added: []
  patterns: ["decorator pattern for ISlicer", "fluent builder with config-time validation", "proportional budget distribution by token mass"]
key-files:
  created:
    - src/Wollax.Cupel/Slicing/QuotaSet.cs
    - src/Wollax.Cupel/Slicing/QuotaBuilder.cs
    - src/Wollax.Cupel/Slicing/QuotaSlice.cs
    - tests/Wollax.Cupel.Tests/Slicing/QuotaSetTests.cs
    - tests/Wollax.Cupel.Tests/Slicing/QuotaBuilderTests.cs
    - tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs
  modified:
    - src/Wollax.Cupel/PublicAPI.Unshipped.txt
decisions:
  - "QuotaSet uses internal constructor — only QuotaBuilder.Build() can create instances"
  - "QuotaSet.Kinds computed on access from union of _requires and _caps keys"
  - "QuotaSlice proportional distribution uses integer arithmetic with candidate token mass weighting"
  - "Unconfigured kinds receive proportional share of unassigned budget based on candidate token mass"
  - "Cap(kind, 0) effectively excludes that kind from results — budget is zero"
metrics:
  duration: "~20 minutes"
  completed: "2026-03-13"
---

# Phase 6 Plan 2: QuotaSlice Decorator with Require/Cap Constraints Summary

Percentage-based semantic quota enforcement via ISlicer decorator pattern with fluent builder and config-time validation.

## What Was Built

### QuotaSet (immutable config)
- Sealed class with internal constructor (only created via QuotaBuilder)
- `GetRequire(kind)` returns minimum % (default 0), `GetCap(kind)` returns maximum % (default 100)
- `Kinds` property returns all configured kinds (union of Require and Cap entries)

### QuotaBuilder (fluent builder)
- `Require(kind, minPercent)` / `Cap(kind, maxPercent)` with 0-100 range validation
- `Build()` validates: at least one quota configured, Require <= Cap per kind, sum of Requires <= 100%
- Duplicate calls for same kind: last value wins

### QuotaSlice (ISlicer decorator)
- Wraps any inner ISlicer (GreedySlice, KnapsackSlice, etc.)
- Algorithm: partition by kind, compute per-kind budgets (require + proportional unassigned, clamped to cap), delegate to inner slicer per partition, merge results
- Unassigned budget distributed proportionally by candidate token mass across all kinds with room above their require
- Insufficient items for Require: best-effort inclusion + trace warning (not exception)
- Cap(kind, 0) effectively excludes that kind

## Test Coverage

| Test Class | Tests | Status |
|-----------|-------|--------|
| QuotaSetTests | 4 | All pass |
| QuotaBuilderTests | 14 | All pass |
| QuotaSliceTests | 11 | All pass |
| **Total new** | **29** | **All pass** |
| Full suite | 354 | All pass |

## Commits

| Hash | Message |
|------|---------|
| `0af48c9` | feat(06-02): implement QuotaSet and QuotaBuilder with TDD |
| `3c6b57c` | test(06-02): add failing tests for QuotaSlice decorator |
| `8c459bb` | feat(06-02): implement QuotaSlice decorator with quota enforcement |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] PublicAPI entries for 06-01/06-03 types**
- **Found during:** Task 1
- **Issue:** Build failed because IAsyncSlicer and StreamSlice (from 06-01/06-03 plans) were missing PublicAPI entries. These had already been committed by another plan execution but the file lacked entries for my new types.
- **Fix:** Added all missing PublicAPI entries in a single edit.
- **Files modified:** src/Wollax.Cupel/PublicAPI.Unshipped.txt

## Next Phase Readiness

No blockers. QuotaSlice is ready for integration into the pipeline builder (06-04) and policy presets (Phase 8).
