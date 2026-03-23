---
estimated_steps: 4
estimated_files: 4
---

# T01: Write failing-first OTel seam and package contract tests

**Slice:** S05 — OTel bridge companion package
**Milestone:** M003

## Description

Create the executable proof targets for R022 before implementation work starts. This task adds focused failing tests that describe the required core seam, the exact OpenTelemetry source/hierarchy/attribute contract, and the local-feed consumption path for the companion package.

## Steps

1. Add `tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs` covering the future structured completion handoff: enabled non-diagnostic collectors receive a final `SelectionReport`, stage snapshots expose exact in/out counts and timestamps, and no test depends on `TraceEvent.Message` parsing.
2. Create `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/` with a TUnit project and `CupelOpenTelemetryTraceCollectorTests.cs` asserting the exact `Wollax.Cupel` source name, 5-stage hierarchy, and `cupel.*` attributes/events for `StageOnly`, `StageAndExclusions`, and `Full` using the OpenTelemetry SDK in-memory exporter.
3. Add a failing local-feed smoke test to `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs` that references the future companion package and exercises `AddCupelInstrumentation()` against a real pipeline run.
4. Wire the new test project into `Cupel.slnx` so the failing-first proof is visible to the normal `dotnet test` flow.

## Must-Haves

- [ ] The new seam test file names the exact structured data the core must provide: final report, stage counts, timing, budget handoff, and a no-message-parsing invariant.
- [ ] The new package test project asserts the exact canonical source name `Wollax.Cupel`, the 5-Activity hierarchy, and the spec attribute/event names for all three verbosity tiers.
- [ ] The consumption smoke test fails specifically because the package/API does not exist yet, not because the test harness or local-feed setup is broken.

## Verification

- `dotnet test tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --filter "FullyQualifiedName~OpenTelemetryReportSeamTests"`
- `dotnet test tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj`
- `dotnet test tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj --filter "FullyQualifiedName~OpenTelemetry"`

## Observability Impact

- Signals added/changed: Failing-first assertions for stage snapshot/report handoff and exact OTel attribute/event carriers.
- How a future agent inspects this: Run the three focused `dotnet test` commands to see precisely which seam, attribute, or package contract is still missing.
- Failure state exposed: Missing type, wrong source name, wrong hierarchy, wrong attribute/event name, or missing local-feed package restore are all surfaced as deterministic test failures.

## Inputs

- `spec/src/integrations/opentelemetry.md` — exact source name, hierarchy, verbosity tiers, and `cupel.*` attribute contract.
- `src/Wollax.Cupel/CupelPipeline.cs` — current seam only builds `SelectionReport` for `DiagnosticTraceCollector`; this task locks the required additive change.
- `tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj` — template for a new package-specific TUnit project.
- `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs` — existing local-feed smoke pattern to extend for the new package.

## Expected Output

- `tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs` — focused failing tests for the core structured handoff.
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` — new package-specific test project.
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — failing SDK-backed contract tests for all verbosity tiers.
- `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs` — failing smoke test for installed-package usage.
