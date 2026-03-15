---
title: "Phase 7 review suggestions — tests"
source: PR review (Phase 7)
priority: low
area: tests
---

# Phase 7 Test Review Suggestions

Suggestions from the Phase 7 PR review that were not fixed immediately.

## Test Improvements

1. **Duplicated `CreateItem` helpers** — `DryRunTests`, `OverflowStrategyTests`, `ExplainabilityIntegrationTests`, `CupelPipelineTests` all have slightly different `CreateItem` factories. Extract to shared test utility.
2. **Duplicated `PassAllSlicer`** — Identical inner class in `ExplainabilityIntegrationTests` and `OverflowStrategyTests`. Extract to shared helper.
3. **`IsSealed` reflection tests** — `IncludedItemTests` and `ExcludedItemTests` test `typeof(T).IsSealed` which is guaranteed by the `sealed` keyword. Remove or consolidate.
4. **`ExcludedItem` equality with differing `DeduplicatedAgainst`** — Not tested that two items with same fields but different `DeduplicatedAgainst` are not equal.
5. **`SelectionReport` value equality** — No test for structural equality on the report record itself.
6. **`ExecuteAsync` with tracing** — No test verifying report is populated on the async path.
7. **`ExecuteStreamAsync` report plumbing** — No test verifying streaming path produces empty Included/Excluded with correct defaults.
8. **`EndToEnd_WithTracing` assertions too weak** — Only asserts `Events.Count > 0`, should verify all 5 stage events present.
9. **`OverflowStrategyTests.DefaultStrategy_IsThrow`** — Contains dead pipeline instance that's never asserted on.
10. **`DryRunTests.DryRun_MatchesExecuteResult`** — Does not assert `TotalTokens` match between Execute and DryRun.
