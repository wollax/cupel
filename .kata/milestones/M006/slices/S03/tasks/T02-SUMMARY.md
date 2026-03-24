---
id: T02
parent: S03
milestone: M006
provides:
  - tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs — integration test proving CountQuotaSlice(QuotaSlice(GreedySlice)) composition via real DryRun()
  - CompositionWithQuotaSlice_CountCapAndPercentageConstraintsBothActive test — asserts count cap holds, CountCapExceeded visible, no shortfalls
key_files:
  - tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs
key_decisions:
  - "No new decisions — test follows established patterns from CountQuotaIntegrationTests.cs and QuotaUtilizationTests.cs"
patterns_established:
  - "CountQuotaCompositionTests.Run() pattern: CupelPipeline.CreateBuilder().WithBudget().WithScorer(ReflexiveScorer).WithSlicer(new CountQuotaSlice(new QuotaSlice(new GreedySlice(), quotaSet), countEntries)).Build().DryRun(items)"
  - "QuotaSet construction via new QuotaBuilder().Require(kind, pct).Cap(kind, pct).Build() — QuotaSet has no public constructor"
observability_surfaces:
  - "dotnet test --solution Cupel.slnx — includes CountQuotaCompositionTests in output with pass/fail per test method and assertion detail on failure"
duration: 5min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T02: .NET composition integration test

**CountQuotaSlice(QuotaSlice(GreedySlice)) composition verified end-to-end via .NET DryRun() with both count and percentage constraints simultaneously active**

## What Happened

Created `CountQuotaCompositionTests.cs` mirroring the T01 Rust test in .NET. The single test `CompositionWithQuotaSlice_CountCapAndPercentageConstraintsBothActive` runs 5 items (3 ToolOutput + 2 Message) through `CupelPipeline.DryRun()` with a `CountQuotaSlice(QuotaSlice(GreedySlice()))` stack. Count entry sets require=1, cap=2 for ToolOutput; quota set sets 10% require, 60% cap.

The test asserts all three observable outcomes: count cap holds (≤2 ToolOutput included), CountCapExceeded appears in excluded list, and require=1 is satisfied (no shortfalls). All assertions pass on first run.

## Verification

- `dotnet test --solution Cupel.slnx 2>&1 | grep -E "CountQuotaComposition|failed"` → `CountQuotaCompositionTests` test project passed; `failed: 0`
- `dotnet build Cupel.slnx 2>&1 | grep -E "error|warning" | wc -l` → 0

## Diagnostics

- `dotnet test --solution Cupel.slnx` output includes `CountQuotaCompositionTests` with test name and pass/fail; assertion failures name the specific count/reason that diverged
- `ExclusionReason.CountCapExceeded` vs `BudgetExceeded` mismatch produces a clear count assertion failure

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs` — new test file (~75 lines) with one passing integration test for CountQuotaSlice(QuotaSlice(GreedySlice)) composition
