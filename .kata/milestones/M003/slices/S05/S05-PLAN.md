# S05: OTel bridge companion package

**Goal:** Ship `Wollax.Cupel.Diagnostics.OpenTelemetry` as a separate NuGet companion package that bridges Cupel pipeline execution to `ActivitySource` with exact `cupel.*` attributes, three verbosity tiers, and no parsing of free-text trace messages.
**Demo:** A real OpenTelemetry SDK harness registers `AddCupelInstrumentation()`, runs a Cupel pipeline, and captures `Wollax.Cupel` Activities/Events proving `StageOnly`, `StageAndExclusions`, and `Full` output with the exact hierarchy and attribute names from the spec.

## Must-Haves

- R022 is advanced directly: the slice adds a real `Wollax.Cupel.Diagnostics.OpenTelemetry` package with canonical `ActivitySource` name `"Wollax.Cupel"`, `CupelOpenTelemetryTraceCollector`, `CupelOpenTelemetryVerbosity`, and `AddCupelInstrumentation(this TracerProviderBuilder)`.
- Core diagnostics expose structured stage summaries and the final `SelectionReport` to any enabled collector without relying on `TraceEvent.Message` parsing, so the OTel bridge can emit spec-accurate stage/exclusion/included-item telemetry.
- Real SDK-backed tests prove the exact 5-Activity hierarchy (`cupel.pipeline` + 5 `cupel.stage.*` children), `cupel.budget.max_tokens`, `cupel.stage.item_count_in`, `cupel.stage.item_count_out`, `cupel.exclusion.*`, and `cupel.item.*` attributes/events at all three verbosity tiers.
- The companion package is installable from the local feed, documented with the spec-required cardinality warning, and does not emit item content or raw metadata values.
- Solution/package/release wiring includes the new package and test project, and the final `dotnet test` / package-consumption flow stays green.

## Proof Level

- This slice proves: integration
- Real runtime required: yes
- Human/UAT required: no

## Verification

- `tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs` via `dotnet test tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --filter "FullyQualifiedName~OpenTelemetryReportSeamTests"`
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` via `dotnet test tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj`
- `dotnet pack src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj --configuration Release --output ./nupkg && cp ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg tests/Wollax.Cupel.ConsumptionTests/packages/ && dotnet test tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj --filter "FullyQualifiedName~OpenTelemetry"`
- `dotnet test --configuration Release`

## Observability / Diagnostics

- Runtime signals: `ActivitySource("Wollax.Cupel")` root/stage Activities, `cupel.exclusion` / `cupel.item.included` events, structured stage snapshots, and a populated `ContextResult.Report` for enabled collectors.
- Inspection surfaces: in-memory exporter assertions in the new OTel test project, seam assertions in `OpenTelemetryReportSeamTests.cs`, consumption smoke test output, and PublicAPI analyzer diagnostics during build.
- Failure visibility: missing attributes, wrong hierarchy, missing report handoff, or accidental message parsing fail deterministic TUnit assertions with exact expected carrier/attribute names.
- Redaction constraints: emit only `kind`, `tokens`, `score`, `reason`, stage names, budget, and verbosity; never emit item content, raw metadata values, or exporter credentials.

## Integration Closure

- Upstream surfaces consumed: `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs`, `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`, `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs`, `src/Wollax.Cupel/CupelPipeline.cs`, and the S04 package/release pattern.
- New wiring introduced in this slice: additive core completion hook + stage snapshot handoff from `CupelPipeline` to enabled collectors, the `Wollax.Cupel.Diagnostics.OpenTelemetry` package/runtime registration surface, and local-feed consumption wiring for the new package.
- What remains before the milestone is truly usable end-to-end: M003/S06 still needs budget simulation, tie-break/spec alignment, and final milestone closure; for the OTel bridge itself, nothing remains once this slice passes verification.

## Tasks

- [x] **T01: Write failing-first OTel seam and package contract tests** `est:45m`
  - Why: Lock the exact R022 boundary before changing public APIs so the slice is driven by real runtime proof instead of ad hoc implementation.
  - Files: `tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs`, `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj`, `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs`, `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs`
  - Do: Add focused failing tests that assert the future core seam, exact source name/hierarchy/attributes for all three verbosity tiers, and a local-feed consumption smoke path for the companion package.
  - Verify: Run the focused `dotnet test` commands and confirm they fail for missing seam/package behavior rather than broken test scaffolding.
  - Done when: The repo contains executable failing-first tests that name the exact files, source name, hierarchy, and attribute/event contract this slice must satisfy.
- [x] **T02: Add structured completion handoff in core diagnostics** `est:1h`
  - Why: The bridge cannot emit spec-accurate telemetry from `TraceEvent.Message`; core must provide structured stage counts, timing, budget, and final-report data to any enabled collector.
  - Files: `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs`, `src/Wollax.Cupel/Diagnostics/StageTraceSnapshot.cs`, `src/Wollax.Cupel/CupelPipeline.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`, `tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs`
  - Do: Add an additive no-op completion hook and stage snapshot model, build `SelectionReport` for enabled collectors, capture exact per-stage in/out counts plus timing/budget data, and keep existing `DiagnosticTraceCollector` behavior intact.
  - Verify: `dotnet test tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --filter "FullyQualifiedName~OpenTelemetryReportSeamTests|FullyQualifiedName~DiagnosticTraceCollector|FullyQualifiedName~ExplainabilityIntegrationTests"`
  - Done when: The seam tests pass, existing diagnostics tests stay green, and the new handoff contains enough structured data for the bridge to emit the spec without message parsing.
- [x] **T03: Implement the OpenTelemetry companion package and SDK-backed assertions** `est:1h`
  - Why: This is the slice’s real product output — the bridge package itself, not just core scaffolding.
  - Files: `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj`, `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs`, `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryVerbosity.cs`, `src/Wollax.Cupel.Diagnostics.OpenTelemetry/TracerProviderBuilderExtensions.cs`, `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md`, `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt`, `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs`
  - Do: Create the package from the S04 template, add the minimum OpenTelemetry dependencies, implement the collector against the structured handoff, emit exact `cupel.*` attributes/events for `StageOnly`/`StageAndExclusions`/`Full`, and keep content out of emitted telemetry.
  - Verify: `dotnet test tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj`
  - Done when: Real `Sdk.CreateTracerProviderBuilder()` capture proves the exact source name, hierarchy, attributes, event counts, and verbosity behavior, and the package builds clean with PublicAPI analyzers.
- [x] **T04: Wire solution, packaging, and consumption flow for the bridge** `est:45m`
  - Why: R022 is not complete until the package is packable, restorable from the local feed, and included in the repo’s normal build/release path.
  - Files: `Directory.Packages.props`, `Cupel.slnx`, `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj`, `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs`, `.github/workflows/release.yml`
  - Do: Register OpenTelemetry package versions centrally, add the new src/test projects to the solution, add package-consumption smoke coverage, and verify the existing release workflow/glob copies and tests the new package (editing it only if the new project is skipped).
  - Verify: `dotnet pack src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj --configuration Release --output ./nupkg && cp ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg tests/Wollax.Cupel.ConsumptionTests/packages/ && dotnet test tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj --filter "FullyQualifiedName~OpenTelemetry" && dotnet test --configuration Release`
  - Done when: The bridge package restores from `./packages`, the consumption smoke test exercises a real OTel registration path, and the full .NET suite passes with the new project wired into the normal build.

## Files Likely Touched

- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs`
- `src/Wollax.Cupel/Diagnostics/StageTraceSnapshot.cs`
- `src/Wollax.Cupel/CupelPipeline.cs`
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj`
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs`
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryVerbosity.cs`
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/TracerProviderBuilderExtensions.cs`
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md`
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj`
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs`
- `tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs`
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj`
- `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs`
- `Directory.Packages.props`
- `Cupel.slnx`
- `.github/workflows/release.yml`
