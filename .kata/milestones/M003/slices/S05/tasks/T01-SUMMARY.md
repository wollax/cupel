---
id: T01
parent: S05
milestone: M003
provides:
  - Failing-first seam tests for core structured report handoff to non-diagnostic collectors
  - Failing-first OTel contract tests for all three verbosity tiers using in-memory exporter
  - Failing-first consumption smoke test for companion package ActivitySource emission
key_files:
  - tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs
  - tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs
  - tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj
  - tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs
key_decisions:
  - OTel test project references core via ProjectReference (not NuGet) since the companion package doesn't exist yet
  - Seam tests use a StubEnabledTraceCollector to prove Report is only built for DiagnosticTraceCollector today
  - Consumption smoke test uses ActivityListener directly rather than the future AddCupelInstrumentation() API
patterns_established:
  - TUnit tree-node filter pattern for running OTel-specific tests in isolation
  - In-memory exporter pattern for asserting exact Activity hierarchy and attributes
observability_surfaces:
  - Run the three focused test commands to see exactly which seam/attribute/package contract is still missing
duration: ~15min
verification_result: passed
completed_at: 2026-03-23
blocker_discovered: false
---

# T01: Write failing-first OTel seam and package contract tests

**Added 15 failing-first tests across 3 projects locking the OTel bridge contract: core report seam, SDK-backed verbosity tier assertions, and companion package consumption smoke test.**

## What Happened

Created the executable proof targets for R022 before any implementation work:

1. **Seam tests** (`OpenTelemetryReportSeamTests.cs`): 7 tests (4 failing, 3 passing baseline). The 4 failures prove that `ContextResult.Report` is only populated when the trace collector is a `DiagnosticTraceCollector` — the future OTel bridge (a non-diagnostic `ITraceCollector`) gets `null`. Also asserts stage event coverage, duration non-negativity, typed exclusion reasons (no message parsing), and report metadata.

2. **OTel contract tests** (`CupelOpenTelemetryTraceCollectorTests.cs`): 7 tests, all failing. Uses the OpenTelemetry SDK in-memory exporter to listen for `"Wollax.Cupel"` Activities. Asserts: canonical source name, `cupel.pipeline` root + 5 `cupel.stage.*` children, `StageOnly` attributes (`cupel.budget.max_tokens`, `cupel.verbosity`, `cupel.stage.name`, `cupel.stage.item_count_in/out`), `StageAndExclusions` exclusion events with required attributes, and `Full` included-item events.

3. **Consumption smoke test**: 1 test, failing. Uses `ActivityListener` on `"Wollax.Cupel"` during a real pipeline run to prove no Activities are emitted yet.

4. **Solution wiring**: Added the new `Wollax.Cupel.Diagnostics.OpenTelemetry.Tests` project to `Cupel.slnx` and `Directory.Packages.props` (OpenTelemetry 1.11.2).

## Verification

- `dotnet run --project tests/Wollax.Cupel.Tests/ -- --treenode-filter "/*/*/OpenTelemetryReportSeamTests/*"`: 7 tests, 4 fail (seam gaps), 3 pass (baseline).
- `dotnet run --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/`: 7 tests, 7 fail (no bridge).
- Consumption smoke test filter: 1 test, 1 fail (no Activities emitted).
- Full existing suite: 614 total, 610 pass, 4 fail (only the new seam tests). Zero regressions.

### Slice-level verification status (T01 is first of slice):
- ✅ `OpenTelemetryReportSeamTests` — builds and runs, 4/7 fail as expected
- ✅ `CupelOpenTelemetryTraceCollectorTests` — builds and runs, 7/7 fail as expected
- ⏳ `dotnet pack` + consumption test — blocked on companion package (future tasks)
- ⏳ `dotnet test --configuration Release` — will run on final task

## Diagnostics

Run the three test commands from the Verification section to see exactly which contract is still missing. Each failure message names the specific attribute, hierarchy node, or seam gap.

## Deviations

- Used `ContextKind.SystemPrompt` and `ContextKind.ToolOutput` instead of `ContextKind.System`/`ContextKind.Tool` (which don't exist on the extensible `ContextKind` class).
- Added `#pragma warning disable CUPEL001/CUPEL003` for experimental preset usage in test files.

## Known Issues

None.

## Files Created/Modified

- `tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs` — 7 seam tests for core report handoff
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` — new TUnit test project with OpenTelemetry SDK deps
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — 7 OTel contract tests for all verbosity tiers
- `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs` — added OTel companion consumption smoke test
- `Cupel.slnx` — wired new test project into solution
- `Directory.Packages.props` — added OpenTelemetry 1.11.2 and InMemory exporter package versions
