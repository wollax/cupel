# Phase 07 Plan 01: Explainability & Overflow Data Types Summary

Established all explainability and overflow data types via TDD, evolving the ExclusionReason enum from 4 to 8 values, adding InclusionReason/OverflowStrategy enums, IncludedItem/ExcludedItem/OverflowEvent sealed records, evolving SelectionReport with per-item diagnostics, and adding the internal ReportBuilder accumulator.

## Tasks Completed

| # | Task | Commit |
|---|------|--------|
| 1 | Enums and value types (InclusionReason, ExclusionReason, OverflowStrategy, OverflowEvent) | `14a0cab` |
| 2 | IncludedItem, ExcludedItem records, evolved SelectionReport, and internal ReportBuilder | `a25141a` |

## Decisions Made

- ExclusionReason values reordered: BudgetExceeded=0 (was 1), ScoredTooLow replaces LowScore, Deduplicated replaces Duplicate, QuotaCapExceeded replaces QuotaExceeded, plus 4 new values
- SelectionReport uses `required` for all new properties (Included, Excluded, TotalCandidates, TotalTokensConsidered) to match the existing Events pattern
- ReportBuilder uses `(ExcludedItem, int Index)` tuple array with `Array.Sort` and static comparison delegate for stable descending sort (same pattern as GreedySlice, UShapedPlacer)
- ReportBuilder.Build() uses `.ToArray()` for defensive copy of accumulated items
- InternalsVisibleTo added to csproj for test project access to internal ReportBuilder
- CupelPipeline SelectionReport construction sites updated with placeholder empty collections (Plan 02 will wire ReportBuilder)

## Deviations from Plan

- Fixed ContextResultTests.cs (3 SelectionReport construction sites) which were not listed in the plan but broke due to the new required properties on SelectionReport
- Plan called TotalCandidates and TotalTokensConsidered as "non-required" per RESEARCH.md context, but implemented as `required` to match the SelectionReport pattern and avoid ambiguous default-0 semantics

## Files Created

- `src/Wollax.Cupel/Diagnostics/InclusionReason.cs`
- `src/Wollax.Cupel/Diagnostics/OverflowStrategy.cs`
- `src/Wollax.Cupel/Diagnostics/OverflowEvent.cs`
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs`
- `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs`
- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs`
- `tests/Wollax.Cupel.Tests/Diagnostics/OverflowEventTests.cs`
- `tests/Wollax.Cupel.Tests/Diagnostics/IncludedItemTests.cs`
- `tests/Wollax.Cupel.Tests/Diagnostics/ExcludedItemTests.cs`
- `tests/Wollax.Cupel.Tests/Diagnostics/SelectionReportTests.cs`
- `tests/Wollax.Cupel.Tests/Diagnostics/ReportBuilderTests.cs`

## Files Modified

- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — replaced 4-value enum with 8-value enum
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — evolved with Included, Excluded, TotalCandidates, TotalTokensConsidered
- `src/Wollax.Cupel/Wollax.Cupel.csproj` — added InternalsVisibleTo
- `src/Wollax.Cupel/CupelPipeline.cs` — updated 2 SelectionReport construction sites with placeholder properties
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — updated with new/changed API surface
- `tests/Wollax.Cupel.Tests/Diagnostics/TraceEventTests.cs` — updated ExclusionReason assertions for 8-value enum
- `tests/Wollax.Cupel.Tests/Models/ContextResultTests.cs` — updated 3 SelectionReport construction sites
