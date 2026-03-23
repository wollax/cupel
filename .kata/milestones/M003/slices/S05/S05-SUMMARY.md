---
id: S05
parent: M003
milestone: M003
provides:
  - Wollax.Cupel.Diagnostics.OpenTelemetry NuGet companion package with CupelOpenTelemetryTraceCollector
  - CupelOpenTelemetryVerbosity enum (StageOnly, StageAndExclusions, Full)
  - AddCupelInstrumentation() TracerProviderBuilder extension for canonical registration
  - Structured ITraceCollector.OnPipelineCompleted hook with StageTraceSnapshot handoff
  - Report population for all enabled ITraceCollector implementations (not just DiagnosticTraceCollector)
requires:
  - slice: S04
    provides: Cupel.Testing package pattern (csproj template, consumption test wiring, PublicAPI analyzers, release workflow glob)
  - slice: S04
    provides: ITraceCollector and SelectionReport stable types from Wollax.Cupel core
affects: []
key_files:
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryVerbosity.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/TracerProviderBuilderExtensions.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj
  - src/Wollax.Cupel/Diagnostics/ITraceCollector.cs
  - src/Wollax.Cupel/Diagnostics/StageTraceSnapshot.cs
  - src/Wollax.Cupel/CupelPipeline.cs
  - tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs
  - tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs
key_decisions:
  - "D100: Structured completion hook on ITraceCollector — never derive OTel data from TraceEvent.Message parsing"
  - "D101: SDK-backed verification with real OpenTelemetry in-memory exporter, not mocked"
  - "D102: Package-specific CupelOpenTelemetryVerbosity enum (not reusing core TraceDetailLevel)"
  - "Activities emitted post-run in OnPipelineCompleted, not inline during stage execution"
  - "Exclusion events routed to originating stage (dedup exclusions on deduplicate Activity, budget on slice)"
  - "Included item events emitted only on place stage Activity (final selection)"
  - "OnPipelineCompleted is a default interface method — no-op by default, backward compatible"
patterns_established:
  - "Default interface method pattern for additive ITraceCollector hooks"
  - "Post-run Activity emission — all OTel spans created from structured StageTraceSnapshot after pipeline completes"
  - "Stage-to-exclusion-reason routing via switch expression mapping ExclusionReason variants to stage names"
  - "In-memory exporter test pattern for asserting exact Activity hierarchy and cupel.* attributes"
observability_surfaces:
  - "ActivitySource('Wollax.Cupel') emits cupel.pipeline root + cupel.stage.{name} children with exact cupel.* attributes"
  - "cupel.exclusion events at StageAndExclusions tier with reason/kind/tokens attributes"
  - "cupel.item.included events at Full tier with kind/tokens/score attributes"
  - "ContextResult.Report is now populated for any enabled ITraceCollector — not only DiagnosticTraceCollector"
  - "OnPipelineCompleted callback provides SelectionReport, ContextBudget, and IReadOnlyList<StageTraceSnapshot>"
drill_down_paths:
  - .kata/milestones/M003/slices/S05/tasks/T01-SUMMARY.md
  - .kata/milestones/M003/slices/S05/tasks/T02-SUMMARY.md
  - .kata/milestones/M003/slices/S05/tasks/T03-SUMMARY.md
  - .kata/milestones/M003/slices/S05/tasks/T04-SUMMARY.md
duration: ~70min
verification_result: passed
completed_at: 2026-03-23
---

# S05: OTel bridge companion package

**Shipped `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package bridging Cupel pipeline execution to `ActivitySource("Wollax.Cupel")` with exact `cupel.*` attributes at three verbosity tiers, structured core completion handoff, and local-feed consumption proof — 737/737 tests green.**

## What Happened

The slice shipped R022 in four tasks following a failing-first test strategy:

**T01** locked the exact contract before any implementation — 15 failing tests across three projects asserting the core report seam, SDK-backed verbosity tier behavior, and companion package consumption. This meant every subsequent task had a concrete, executable definition of done.

**T02** added the structured completion hook in core. `ITraceCollector` gained `OnPipelineCompleted(SelectionReport, ContextBudget, IReadOnlyList<StageTraceSnapshot>)` as a default interface method (backward-compatible no-op). `CupelPipeline.ExecuteCore` was refactored to build `SelectionReport` for any enabled collector (previously gated on `DiagnosticTraceCollector`), accumulate `StageTraceSnapshot` entries at each stage, and invoke the completion hook. This gave the bridge structured data instead of requiring message parsing.

**T03** built the actual package. `CupelOpenTelemetryTraceCollector` implements `ITraceCollector`, creates Activities post-run in `OnPipelineCompleted` using the structured snapshots, emits `cupel.pipeline` root + 5 `cupel.stage.*` children with exact attributes, routes exclusion events to originating stages at `StageAndExclusions` verbosity, and emits included-item events at `Full` verbosity. Content and metadata are never emitted. `AddCupelInstrumentation()` on `TracerProviderBuilder` provides the canonical registration surface.

**T04** wired the package into the solution (`Cupel.slnx`), verified pack/consume/release flow, and confirmed the release workflow's `*.nupkg` glob automatically includes the new package. No workflow changes needed.

## Verification

| Check | Status | Evidence |
|-------|--------|---------|
| OpenTelemetryReportSeamTests | ✓ 7/7 pass | Core report populated for any enabled collector; stage snapshots present |
| CupelOpenTelemetryTraceCollectorTests | ✓ 7/7 pass | Canonical source, 5-Activity hierarchy, all verbosity tiers, exact attributes |
| Pack + consumption smoke test | ✓ 1/1 pass | Local-feed nupkg, real AddCupelInstrumentation() → pipeline → Activity capture |
| Full Release suite | ✓ 737/737 pass | Zero regressions across 7 test projects |

## Requirements Advanced

- R022 — OTel bridge fully implemented: companion package with `CupelOpenTelemetryTraceCollector`, `CupelOpenTelemetryVerbosity` (3 tiers), `AddCupelInstrumentation()`, exact `cupel.*` attributes, cardinality warning in README, and local-feed consumption proof.

## Requirements Validated

- R022 — All proof gates met: real `ActivitySource("Wollax.Cupel")` emission at all three verbosity tiers captured by OpenTelemetry SDK in-memory exporter; package packs, installs from local feed, and exercises the full registration path; redaction policy enforced (no content/metadata leakage); 737/737 tests green.

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

- T01 used `ContextKind.SystemPrompt` and `ContextKind.ToolOutput` instead of plan-mentioned `ContextKind.System`/`ContextKind.Tool` (which don't exist on the extensible `ContextKind` class). No impact.
- T02 fixed a T01 assertion (`IsNotEqualTo(default(InclusionReason))` → `Enum.IsDefined()`) because `Scored` (enum value 0) is a valid inclusion reason, not a sentinel. Bug in the test, not a plan deviation.

## Known Limitations

- The README `Pack` target generates a "missing a readme" NuGet warning despite the `<None Include="README.md" Pack="true">` directive — likely a MinVer/build ordering issue. The README is correctly included in the `.nupkg`.
- `dotnet test --configuration Release` bare command may fail locally due to a TUnit/.NET 10 test platform incompatibility (`--nologo`/`--logger` unknown options). Per-project invocation works fine. Pre-existing issue unrelated to this slice.

## Follow-ups

- none

## Files Created/Modified

- `src/Wollax.Cupel/Diagnostics/StageTraceSnapshot.cs` — Structured stage snapshot model (readonly record struct)
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — Added OnPipelineCompleted default interface method
- `src/Wollax.Cupel/CupelPipeline.cs` — Report + snapshot accumulation for all enabled collectors, completion hook
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — New public API surface declared
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj` — New companion package project
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — ITraceCollector → ActivitySource bridge
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryVerbosity.cs` — Verbosity enum (StageOnly, StageAndExclusions, Full)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/TracerProviderBuilderExtensions.cs` — AddCupelInstrumentation() extension
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md` — Package docs with cardinality warning
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Shipped.txt` — Empty shipped surface
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt` — Full public API declaration
- `tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs` — 7 seam tests for core report handoff
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` — New TUnit test project
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — 7 SDK-backed OTel contract tests
- `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs` — OTel companion consumption smoke test
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` — OTel package references added
- `Cupel.slnx` — OTel src + test projects wired into solution
- `Directory.Packages.props` — OpenTelemetry 1.11.2 and InMemory exporter versions registered

## Forward Intelligence

### What the next slice should know
- The OTel bridge emits Activities post-run, not inline during execution. This means latency for very long pipelines is pushed to the end, not spread across stages. If future work adds streaming telemetry, the architecture would need a fundamental change.
- The `OnPipelineCompleted` default interface method pattern is non-breaking and reusable for any future additive `ITraceCollector` hooks.

### What's fragile
- `[NotInParallel]` on OTel tests due to shared static `ActivitySource` — removing the attribute causes cross-talk between concurrent tests. Any future OTel test classes must also use this attribute.
- The NuGet readme warning is cosmetic but may confuse future builds.

### Authoritative diagnostics
- `CupelOpenTelemetryTraceCollectorTests.cs` is the single source of truth for the Activity hierarchy and attribute contract — run this project to verify the OTel bridge is working correctly.
- `OpenTelemetryReportSeamTests.cs` proves the core handoff — if these fail, the bridge has no data to emit.

### What assumptions changed
- The original S05 branch (from the first attempt) diverged from main after S06 merged. This fresh implementation started from the current main with S06 already integrated, so no rebase was needed.
- Report population was previously gated on `DiagnosticTraceCollector` specifically; now any enabled collector gets a populated report. This is a broader API change than originally scoped but was necessary for the bridge to function.
