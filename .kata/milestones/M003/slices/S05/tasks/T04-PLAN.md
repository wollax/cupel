---
estimated_steps: 4
estimated_files: 5
---

# T04: Wire solution, packaging, and consumption flow for the bridge

**Slice:** S05 — OTel bridge companion package
**Milestone:** M003

## Description

Close the slice by wiring the new package into the repo’s normal build, pack, restore, and release path. The bridge is only done when it behaves like the other published companion packages: centrally versioned, part of the solution, installable from the local feed, and exercised by a real consumption smoke test.

## Steps

1. Add the required OpenTelemetry package versions to `Directory.Packages.props` and include the new src/test projects in `Cupel.slnx`.
2. Update `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` to reference `Wollax.Cupel.Diagnostics.OpenTelemetry` from the local feed and add a smoke test that configures the bridge against a real pipeline run.
3. Verify the existing `.github/workflows/release.yml` pack/copy/test flow picks up the new package automatically; adjust it only if the new project is skipped by the normal artifact/publish path.
4. Run the pack → copy-to-`./packages` → consumption-test flow, then run the full Release configuration test suite to prove the slice closes cleanly.

## Must-Haves

- [ ] The new package is discoverable by the normal solution/build path and uses centrally managed dependency versions.
- [ ] A local-feed consumption test proves the installed package can register instrumentation and observe a real pipeline execution.
- [ ] The release workflow is verified to include the new package in the same artifact/test/publish path as the existing companion packages.

## Verification

- `dotnet pack src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj --configuration Release --output ./nupkg`
- `cp ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg tests/Wollax.Cupel.ConsumptionTests/packages/`
- `dotnet test tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj --filter "FullyQualifiedName~OpenTelemetry"`
- `dotnet test --configuration Release`

## Observability Impact

- Signals added/changed: Consumption smoke test proves the installed package emits real traces through the same host registration surface production callers will use.
- How a future agent inspects this: Inspect `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs`, the local `./packages` feed, and the release workflow artifact/copy steps.
- Failure state exposed: Missing package version wiring, restore failures, or workflow omissions show up in local-feed restore errors or consumption test failures rather than being deferred to release day.

## Inputs

- `Directory.Packages.props` — central package version table to extend.
- `Cupel.slnx` — solution membership for new src/test projects.
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` and `ConsumptionTests.cs` — established local-feed smoke path from S04.
- `.github/workflows/release.yml` — current pack/copy/test/publish flow to validate against the new package.

## Expected Output

- `Directory.Packages.props` — OpenTelemetry package versions registered centrally.
- `Cupel.slnx` — new bridge package/test projects included.
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` — local-feed reference to `Wollax.Cupel.Diagnostics.OpenTelemetry`.
- `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs` — passing smoke test for installed-package instrumentation.
- `.github/workflows/release.yml` — confirmed or adjusted workflow support for the new package.
