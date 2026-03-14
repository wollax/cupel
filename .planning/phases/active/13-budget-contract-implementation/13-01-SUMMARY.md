---
phase: 13-budget-contract-implementation
plan: 01
subsystem: pipeline
tags: [budget, reserved-slots, safety-margin, pipeline]
dependency-graph:
  requires: [05-pipeline-assembly-basic-execution, 01-project-scaffold-core-models]
  provides: [ReservedSlots budget reduction, EstimationSafetyMarginPercent budget reduction]
  affects: [14-policy-type-completeness, 15-conformance-hardening]
tech-stack:
  added: []
  patterns: [subtract-then-multiply budget reduction]
key-files:
  created: []
  modified:
    - src/Wollax.Cupel/CupelPipeline.cs
    - tests/Wollax.Cupel.Tests/Pipeline/CupelPipelineTests.cs
decisions:
  - "Order of operations: ReservedSlots subtracted first, then EstimationSafetyMarginPercent applied as multiplicative reduction"
  - "Streaming path uses foreach loop over ReservedSlots (no pinnedTokens in streaming mode)"
  - "Safety margin uses int cast (truncation) for effective budget values, consistent with existing int budget semantics"
metrics:
  duration: ~5 minutes
  completed: 2026-03-14
---

# Phase 13 Plan 01: Budget Contract Wiring Summary

ReservedSlots token sum subtraction and EstimationSafetyMarginPercent multiplicative reduction wired into both sync and streaming pipeline paths, with TDD test coverage proving each behavior independently and combined.

## What Changed

### Sync Path (ExecuteCore)
- Sum all `ReservedSlots` values and subtract from `effectiveMax` and `effectiveTarget` alongside existing `pinnedTokens` and `OutputReserve` deductions
- Apply `EstimationSafetyMarginPercent` as `(1.0 - percent/100.0)` multiplier after reserved slots subtraction
- Order: subtract reserved slots first, then multiply by safety margin

### Streaming Path (ExecuteStreamAsync)
- Sum all `ReservedSlots` values and subtract from `effectiveMax` and `effectiveTarget`
- Apply same safety margin multiplier
- No pinnedTokens deduction (streaming path does not classify pinned items)

## Tasks Completed

| # | Task | Commits |
|---|------|---------|
| 1 | ReservedSlots budget reduction (TDD) | `58eef99` (RED), `77b5fba` (GREEN) |
| 2 | EstimationSafetyMarginPercent + streaming path (TDD) | `67b29d8` (RED), `f071c19` (GREEN) |

## Tests Added (9 new tests)

### ReservedSlots Tests
- `Execute_WithReservedSlots_ReducesEffectiveBudget` — single slot reduces capacity
- `Execute_WithMultipleReservedSlots_SubtractsCombinedTotal` — combined total subtracted
- `Execute_WithEmptyReservedSlots_NoChange` — backward compatibility

### EstimationSafetyMarginPercent Tests
- `Execute_WithEstimationSafetyMargin_ReducesEffectiveBudget` — 20% margin reduces from 10 to 8 items
- `Execute_WithReservedSlotsAndSafetyMargin_AppliesInCorrectOrder` — (1000-200)*0.75=600
- `Execute_WithZeroSafetyMargin_NoChange` — backward compatibility

### Streaming Path Tests
- `ExecuteStreamAsync_WithReservedSlots_ReducesEffectiveBudget`
- `ExecuteStreamAsync_WithSafetyMargin_ReducesEffectiveBudget`
- `ExecuteStreamAsync_WithReservedSlotsAndSafetyMargin_AppliesInCorrectOrder`

## Decisions Made

1. **Order of operations**: Subtract ReservedSlots first, then apply safety margin. This matches the plan specification and ensures reserved slots are a hard carve-out before percentage-based reduction.
2. **Integer truncation**: Safety margin uses `(int)(value * multiplier)` which truncates toward zero, consistent with existing int budget semantics throughout the codebase.
3. **Streaming path**: Uses `foreach` over `_budget.ReservedSlots` (not a for-loop with indexer) since `IReadOnlyDictionary` does not support indexer access. This is acceptable as it's not in the hot scoring path.

## Deviations from Plan

None — plan executed exactly as written.

## Verification

- `dotnet build` succeeds with zero warnings
- `dotnet test` — all 606 tests pass (597 existing + 9 new)
- Test count by project: Wollax.Cupel.Tests (48 pipeline tests of 592 total), Json.Tests, DI.Tests, Tiktoken.Tests all green

## Next Phase Readiness

No blockers. Plan 13-02 can proceed to wire these budget adjustments into policy-level configuration if needed.
