---
id: T02
parent: S05
milestone: M003
provides:
  - Structured completion hook on ITraceCollector with default no-op for backward compatibility
  - StageTraceSnapshot model with in/out counts and timing per stage
  - Report population for any enabled ITraceCollector (not only DiagnosticTraceCollector)
  - Stage snapshots accumulated in ExecuteCore and handed off via OnPipelineCompleted
key_files:
  - src/Wollax.Cupel/Diagnostics/StageTraceSnapshot.cs
  - src/Wollax.Cupel/Diagnostics/ITraceCollector.cs
  - src/Wollax.Cupel/CupelPipeline.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
key_decisions:
  - ReportBuilder created for any enabled collector, not just DiagnosticTraceCollector; events array falls back to empty for non-diagnostic collectors
  - OnPipelineCompleted is a default interface method (no-op) to avoid breaking existing implementations
  - StageTraceSnapshot uses readonly record struct matching TraceEvent's allocation discipline
patterns_established:
  - Default interface method pattern for additive ITraceCollector hooks
  - Stage snapshot accumulation alongside existing TraceEvent recording
observability_surfaces:
  - OnPipelineCompleted callback receives SelectionReport, ContextBudget, and IReadOnlyList<StageTraceSnapshot> for any enabled collector
  - ContextResult.Report is now populated for any enabled ITraceCollector, inspectable by downstream bridges
duration: 15m
verification_result: passed
completed_at: 2026-03-23
blocker_discovered: false
---

# T02: Add structured completion handoff in core diagnostics

**Added structured completion hook and report population for all enabled ITraceCollector implementations, enabling the OTel bridge to receive stage snapshots, budget data, and the final SelectionReport without parsing trace messages.**

## What Happened

1. Created `StageTraceSnapshot` readonly record struct capturing stage name, item-count-in, item-count-out, and duration — the structured model needed for post-run Activity span emission.

2. Extended `ITraceCollector` with `OnPipelineCompleted(SelectionReport, ContextBudget, IReadOnlyList<StageTraceSnapshot>)` as a default interface method (no-op), preserving full backward compatibility for existing implementations.

3. Refactored `CupelPipeline.ExecuteCore` to:
   - Create `ReportBuilder` for any enabled collector (was gated on `trace is DiagnosticTraceCollector`)
   - Accumulate `StageTraceSnapshot` entries at each stage with precise in/out item counts
   - Build and populate `ContextResult.Report` for any enabled collector
   - Invoke `OnPipelineCompleted` with the report, budget, and snapshots

4. Updated `PublicAPI.Unshipped.txt` with all new public API surface.

5. Fixed one T01 test assertion (`Report_Included_Items_Have_Score_And_Reason`) that used `IsNotEqualTo(default(InclusionReason))` — since `Scored` (value 0) is a valid reason, changed to `Enum.IsDefined()` which correctly validates the enum is populated.

## Verification

- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --treenode-filter "/*/*/OpenTelemetryReportSeamTests/*"` — 7/7 passed
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --treenode-filter "/*/*/DiagnosticTraceCollector*/*"` — 12/12 passed
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --treenode-filter "/*/*/ExplainabilityIntegrationTests/*"` — 11/11 passed
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` — 614/614 passed (full suite, no regressions)
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` — 0 errors, 0 warnings

### Slice-level verification status (intermediate task — partial pass expected)

- ✅ `OpenTelemetryReportSeamTests` — all pass
- ❌ `CupelOpenTelemetryTraceCollectorTests` — 7 failures (companion package not yet implemented, future task)
- ❌ Consumption smoke test — no OTel tests match yet (companion package not yet packaged)
- ✅ Full core test suite — 614 passed

## Diagnostics

- Inspect `ContextResult.Report` on any pipeline execution with an enabled `ITraceCollector` to see structured included/excluded items, scores, reasons, and budget metadata.
- Inspect the `OnPipelineCompleted` callback arguments in the `StubEnabledTraceCollector` seam tests.
- `StageTraceSnapshot` values assert exact in/out counts and non-negative durations in `OpenTelemetryReportSeamTests`.

## Deviations

- Fixed T01 test assertion in `Report_Included_Items_Have_Score_And_Reason`: changed `IsNotEqualTo(default(InclusionReason))` to `Enum.IsDefined()` because `Scored` (enum value 0) is a valid inclusion reason, not a sentinel.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/Diagnostics/StageTraceSnapshot.cs` — New structured stage snapshot model (readonly record struct)
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — Added `OnPipelineCompleted` default interface method
- `src/Wollax.Cupel/CupelPipeline.cs` — Report + snapshot accumulation for all enabled collectors, completion hook invocation
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Declared new public API surface
- `tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs` — Fixed T01 assertion to use `Enum.IsDefined`
