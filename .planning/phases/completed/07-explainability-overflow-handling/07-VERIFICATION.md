# Phase 7 Verification Report

**Status: `passed`**

**Date:** 2026-03-13
**Verifier:** Automated phase verification

---

## Summary

All must-have artifacts exist, all implementation truths hold, the build is clean (zero warnings), and all 440 tests pass.

---

## Build & Test Results

| Check | Result |
|---|---|
| `rtk dotnet build` | ok (build succeeded, zero warnings) |
| `rtk dotnet test --project tests/Wollax.Cupel.Tests/` | 440 passed, 0 failed, 0 skipped |

---

## Must-Have Checklist

### Enums

- [x] **InclusionReason** enum has values `Scored`, `Pinned`, `ZeroToken`
  - Source: `src/Wollax.Cupel/Diagnostics/InclusionReason.cs`
  - All three values confirmed present.

- [x] **ExclusionReason** enum has 8 values replacing the existing 4-value stub
  - Source: `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs`
  - Values: `BudgetExceeded`, `ScoredTooLow`, `Deduplicated`, `QuotaCapExceeded`, `QuotaRequireDisplaced`, `NegativeTokens`, `PinnedOverride`, `Filtered` (8 values).

- [x] **OverflowStrategy** enum has `Throw`, `Truncate`, `Proceed`
  - Source: `src/Wollax.Cupel/Diagnostics/OverflowStrategy.cs`

### Record types

- [x] **IncludedItem** is a `sealed record` with `Item`, `Score`, `Reason`
  - Source: `src/Wollax.Cupel/Diagnostics/IncludedItem.cs`

- [x] **ExcludedItem** is a `sealed record` with `Item`, `Score`, `Reason`, optional `DeduplicatedAgainst`
  - Source: `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs`
  - `DeduplicatedAgainst` is `ContextItem?` (nullable).

- [x] **OverflowEvent** is a `sealed record` with `TokensOverBudget`, `OverflowingItems`, `Budget`
  - Source: `src/Wollax.Cupel/Diagnostics/OverflowEvent.cs`

### SelectionReport

- [x] **SelectionReport** has `Included`, `Excluded`, `TotalCandidates`, `TotalTokensConsidered`, `Events`
  - Source: `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`
  - All five required properties present.

### Internal infrastructure

- [x] **ReportBuilder** is `internal sealed class` and accumulates items through pipeline stages
  - Source: `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs`
  - Has `AddIncluded`, `AddExcluded`, `SetTotalCandidates`, `SetTotalTokensConsidered`, `Build`.

- [x] **ReportBuilder instantiated only when `traceCollector` is `DiagnosticTraceCollector`**
  - Confirmed in `CupelPipeline.cs` line 90: `ReportBuilder? reportBuilder = trace is DiagnosticTraceCollector ? new ReportBuilder() : null;`

### Pipeline behavior

- [x] **`ExecuteCore()` extracted from `Execute()` to share logic with `DryRun()`**
  - `Execute()` calls `ExecuteCore(items, trace, isDryRun: false)`.
  - `DryRun()` calls `ExecuteCore(items, trace, isDryRun: true)` with a fresh `DiagnosticTraceCollector`.

- [x] **`DryRun()` forces `DiagnosticTraceCollector` internally and always populates `Report`**
  - `DryRun()` creates `new DiagnosticTraceCollector()` regardless of caller-provided trace.
  - `Report` is always populated from the internal collector.

- [x] **`DryRun()` is idempotent**
  - No mutable state modified. Verified by `DryRunTests.DryRun_IsIdempotent` and `ExplainabilityIntegrationTests.SC2_DryRun_IsIdempotent`.

- [x] **Diff-based exclusion tracking — no `ISlicer` changes**
  - `ISlicer` interface unchanged (signature unchanged from prior phases).
  - Exclusion tracking done by diff between `sorted` items and `slicedSet` in `CupelPipeline.cs`.

- [x] **OverflowStrategy applies post-merge (pinned + sliced), pre-place**
  - Overflow check in `CupelPipeline.cs` after `merged` array is built (line 333), before `_placer.Place(merged, trace)` (line 392).

- [x] **Default overflow strategy is `Throw`**
  - `PipelineBuilder._overflowStrategy` defaults to `OverflowStrategy.Throw`.
  - `CupelPipeline` constructor default parameter is also `OverflowStrategy.Throw`.

- [x] **`OverflowStrategy.Throw` raises `OverflowException` on budget overflow**
  - Confirmed in `ExecuteCore()` switch case. Tested by `OverflowStrategyTests.Throw_WhenOverflow_ThrowsOverflowException`.

- [x] **`OverflowStrategy.Truncate` truncates excess items**
  - Items removed from end of merged list (lowest scored first, pinned items skipped). Tested by `OverflowStrategyTests.Truncate_RemovesLowestScoredToFitBudget`.

- [x] **`OverflowStrategy.Proceed` continues with optional observer callback invoked**
  - Invokes `_overflowObserver?.Invoke(...)`. Tested by `OverflowStrategyTests.Proceed_InvokesCallback` and `Proceed_WithoutCallback_NoException`.

- [x] **Observer callback receives overflow details (`TokensOverBudget`, `OverflowingItems`, `Budget`)**
  - All three fields populated in the `OverflowEvent` record. Verified by `SC4_*` integration tests.

- [x] **Pinned items alone exceeding `MaxTokens` remains a hard `InvalidOperationException`**
  - `pinnedTokens > availableForPinned` throws `InvalidOperationException` before the overflow strategy switch. Tested by `OverflowStrategyTests.PinnedAloneExceedingMaxTokens_ThrowsInvalidOperationException`.

### Public API surface

- [x] **`PublicAPI.Unshipped.txt` contains entries for all new public types and members**
  - File at `src/Wollax.Cupel/PublicAPI.Unshipped.txt` contains entries for:
    - `ExclusionReason` (8 values)
    - `InclusionReason` (3 values)
    - `OverflowStrategy` (3 values)
    - `OverflowEvent` (all properties)
    - `IncludedItem` (all properties)
    - `ExcludedItem` (all properties)
    - `SelectionReport` (all properties)
    - `CupelPipeline.DryRun`
    - `PipelineBuilder.WithOverflowStrategy`
    - `ContextResult.Report`

### Tests

- [x] **Integration tests verify end-to-end explainability across realistic scenarios**
  - `tests/Wollax.Cupel.Tests/Pipeline/ExplainabilityIntegrationTests.cs` — covers all 4 success criteria (SC1–SC4) plus a combined realistic scenario.
  - `tests/Wollax.Cupel.Tests/Pipeline/DryRunTests.cs` — 6 tests covering DryRun behavior.
  - `tests/Wollax.Cupel.Tests/Pipeline/OverflowStrategyTests.cs` — 8 tests covering all overflow strategies.
  - `tests/Wollax.Cupel.Tests/Diagnostics/` — unit tests for `IncludedItem`, `ExcludedItem`, `SelectionReport`, `OverflowEvent`, `ReportBuilder`.

---

## Gaps Found

None. All must-haves are implemented and verified.

---

## Notes

- The `isDryRun` parameter is threaded into `ExecuteCore` but is not used in the current implementation body (the distinction is handled by `DryRun` always passing a fresh `DiagnosticTraceCollector`). This is correct and intentional per the plan.
- The Truncate path uses a `List<ScoredItem>` with `RemoveAt` which is O(n²) in the worst case, but this is a correctness-only concern and not a correctness gap.
- `ExclusionReason.ScoredTooLow` and `ExclusionReason.Filtered` are defined in the enum and registered in `PublicAPI.Unshipped.txt` but are not emitted by the current pipeline implementation. They are reserved for future use by custom slicers/filters and do not represent a gap.
