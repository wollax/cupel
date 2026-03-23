# S05: OTel bridge companion package — UAT

**Milestone:** M003
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: The OTel bridge is a library package — all behavior is exercised via deterministic unit tests with real OpenTelemetry SDK in-memory exporter capture. There is no UI, service, or human-visible runtime to inspect. The test harness proves the exact Activity hierarchy, attribute names, verbosity tier behavior, and package installability.

## Preconditions

- .NET 10 SDK installed
- Repository checked out with all S05 changes on the branch
- `dotnet restore` completed successfully

## Smoke Test

Run `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` — expect 7/7 passed. This single command proves the package compiles, the collector emits Activities, and all three verbosity tiers produce the correct hierarchy and attributes.

## Test Cases

### 1. Core report seam — any enabled collector gets a populated report

1. `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --treenode-filter "/*/*/OpenTelemetryReportSeamTests/*"`
2. **Expected:** 7/7 pass — proves `ContextResult.Report` is populated for non-diagnostic collectors, stage snapshots are present, and existing diagnostic tests remain green.

### 2. OTel Activity hierarchy and attributes at all three verbosity tiers

1. `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj`
2. **Expected:** 7/7 pass — proves canonical `Wollax.Cupel` source name, root `cupel.pipeline` + 5 `cupel.stage.*` children, budget/verbosity attributes at StageOnly, exclusion events at StageAndExclusions, and included-item events at Full.

### 3. Companion package installs from local feed

1. `dotnet pack src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj --configuration Release --output ./nupkg`
2. `cp ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg tests/Wollax.Cupel.ConsumptionTests/packages/`
3. `dotnet test --project tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj --treenode-filter "/*/*/*/OpenTelemetry*"`
4. **Expected:** 1/1 pass — proves the nupkg restores from the local feed and the consumption test exercises `AddCupelInstrumentation()` → pipeline → Activity capture.

### 4. Full suite regression check

1. `dotnet test --configuration Release`
2. **Expected:** 737/737 pass — zero regressions across all 7 test projects.

## Edge Cases

### No content or metadata leakage in emitted telemetry

1. Open `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs`
2. Search for `Content` or `Metadata` in any tag/event emission code
3. **Expected:** Neither appears — only `kind`, `tokens`, `score`, `reason`, stage names, budget, and verbosity are emitted.

### Collector disabled (not subscribed) emits nothing

1. The `ActivitySource` only emits when a listener is registered via `AddCupelInstrumentation()` or manual `ActivityListener`
2. **Expected:** Running a pipeline without OTel registration produces zero Activities — no overhead in the non-observed path.

## Failure Signals

- Missing `cupel.*` attributes on Activities → collector is not emitting structured data from the handoff
- Wrong Activity hierarchy (missing stages, wrong parent) → stage-to-Activity mapping broken
- Consumption test restore failure → package version or local feed wiring broken
- `OpenTelemetryReportSeamTests` failures → core completion hook regressed (report not populated for enabled collectors)

## Requirements Proved By This UAT

- R022 — OTel bridge: all four test cases collectively prove the companion package emits correct OTel telemetry at all verbosity tiers from an installable NuGet package, matching the spec's `cupel.*` attribute contract.

## Not Proven By This UAT

- Production OTel backend ingestion (Jaeger, Honeycomb, Aspire) — tests use in-memory exporter only
- Cross-process trace propagation — the bridge emits Activities locally but does not test distributed tracing contexts
- Performance impact of OTel instrumentation at high pipeline throughput — no benchmark suite

## Notes for Tester

- The `dotnet test --configuration Release` bare command may fail locally on .NET 10 due to a TUnit test platform incompatibility. If so, run individual projects. This is a pre-existing environment issue.
- The NuGet pack step produces a "missing a readme" warning — this is cosmetic and the README is correctly included in the package.
