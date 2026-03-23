---
id: T03
parent: S05
milestone: M003
provides:
  - Wollax.Cupel.Diagnostics.OpenTelemetry companion package with CupelOpenTelemetryTraceCollector
  - CupelOpenTelemetryVerbosity enum (StageOnly, StageAndExclusions, Full)
  - AddCupelInstrumentation() TracerProviderBuilder extension method
  - Exact cupel.* Activity/Event emission at three verbosity tiers
  - Package README with cardinality warning and redaction policy
key_files:
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryVerbosity.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/TracerProviderBuilderExtensions.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj
  - tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs
key_decisions:
  - "Activities emitted post-run in OnPipelineCompleted (not inline during stage execution) to use structured StageTraceSnapshot data"
  - "Exclusion events routed to their originating stage (dedup exclusions on deduplicate Activity, budget on slice Activity)"
  - "Included item events emitted only on the place stage Activity (final selection)"
  - "Tests serialized with [NotInParallel] due to shared static ActivitySource"
patterns_established:
  - "OnPipelineCompleted as the sole emission point for OTel Activities — no inline Activity creation during pipeline execution"
  - "Stage-to-exclusion-reason routing via GetExclusionsForStage switch expression"
observability_surfaces:
  - "ActivitySource('Wollax.Cupel') emits cupel.pipeline root + cupel.stage.{name} children with cupel.* attributes"
  - "cupel.exclusion events at StageAndExclusions tier, cupel.item.included events at Full tier"
  - "In-memory exporter test pattern for asserting exact Activity hierarchy and event attributes"
duration: 25min
verification_result: passed
completed_at: 2026-03-23T12:00:00Z
blocker_discovered: false
---

# T03: Implement the OpenTelemetry companion package and SDK-backed assertions

**Built `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package emitting exact spec-defined Activities/Events at three verbosity tiers via `ActivitySource("Wollax.Cupel")`, with public `AddCupelInstrumentation()` registration and safe-by-default redaction.**

## What Happened

Created the `Wollax.Cupel.Diagnostics.OpenTelemetry` package project following the S04 companion package template pattern. The package implements `CupelOpenTelemetryTraceCollector` as an `ITraceCollector` that uses the `OnPipelineCompleted` structured hook to emit the complete Activity hierarchy in one pass after pipeline execution completes.

The collector emits a `cupel.pipeline` root Activity with budget/verbosity/summary attributes, then five `cupel.stage.{name}` child Activities with item counts and timing. At `StageAndExclusions` verbosity, exclusion events are routed to the stage that caused them (dedup exclusions on the deduplicate stage, budget exclusions on the slice stage, etc.). At `Full` verbosity, included item events with kind/tokens/score are emitted on the place stage.

The `TracerProviderBuilderExtensions.AddCupelInstrumentation()` extension method provides the canonical registration surface so host code doesn't hard-code the source name. PublicAPI analyzer files track the full public surface.

Updated the T01 failing test project to reference the new package and use the real `CupelOpenTelemetryTraceCollector` instead of `DiagnosticTraceCollector`. Added `[NotInParallel]` to prevent Activity cross-talk from the shared static `ActivitySource`. Updated the consumption test to use `AddCupelInstrumentation()` and the real collector.

## Verification

### Observable Truths
| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Package compiles independently | ✓ PASS | `dotnet build` succeeds with 0 errors, 0 warnings |
| 2 | Emits canonical source name | ✓ PASS | ActivitySource_Uses_Canonical_Name test passes |
| 3 | Root + 5 stage hierarchy | ✓ PASS | Hierarchy_Has_Root_Pipeline_Activity_And_Five_Stage_Children test passes |
| 4 | StageOnly has budget/verbosity attrs | ✓ PASS | StageOnly_Root_Activity_Has_Budget_And_Verbosity_Attributes test passes |
| 5 | StageOnly has no exclusion events | ✓ PASS | StageOnly_No_Exclusion_Events_Are_Emitted test passes |
| 6 | StageAndExclusions emits events | ✓ PASS | StageAndExclusions_Emits_Exclusion_Events_With_Required_Attributes test passes |
| 7 | Full emits included items | ✓ PASS | Full_Emits_Included_Item_Events_With_Required_Attributes test passes |
| 8 | No content/metadata leakage | ✓ PASS | Collector only emits kind, tokens, score, reason — never Content or Metadata |

### Slice Verification
| Check | Status |
|-------|--------|
| Seam tests (OpenTelemetryReportSeamTests) | ✓ 7/7 pass |
| OTel package tests | ✓ 7/7 pass |
| Consumption smoke test (OpenTelemetry filter) | ✓ 1/1 pass |
| Full suite (`dotnet test --configuration Release`) | ✓ 737/737 pass |

## Diagnostics

- Use the in-memory exporter pattern from `CupelOpenTelemetryTraceCollectorTests` to inspect emitted Activities/Events.
- Check `PublicAPI.Unshipped.txt` for the complete public surface.
- Verify redaction: search `CupelOpenTelemetryTraceCollector.cs` for `Content` or `Metadata` — neither appears in any tag/event emission.

## Deviations

None.

## Known Issues

- The README `Pack` target generates a "missing a readme" warning from NuGet despite the `<None Include="README.md" Pack="true">` directive — likely a MinVer/build ordering issue. The README is correctly included in the `.nupkg` content.

## Files Created/Modified

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj` — New packable companion package project
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — ITraceCollector implementation emitting OTel Activities/Events
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryVerbosity.cs` — Public verbosity enum (StageOnly, StageAndExclusions, Full)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/TracerProviderBuilderExtensions.cs` — AddCupelInstrumentation() extension
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md` — Package documentation with cardinality warning and redaction policy
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Shipped.txt` — Empty shipped API surface
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt` — Full public API declaration
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — Updated to use real collector, all 7 tests pass
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` — Added ProjectReference to new package
- `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs` — Updated OTel smoke test to use real bridge
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` — Added OTel package references
