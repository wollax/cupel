---
id: T03
parent: S05
milestone: M003
provides:
  - Verified complete OTel companion package with all 3 verbosity tiers passing SDK-backed assertions
  - Latest nupkg copied to ./packages for consumption test feed
  - Full solution green (712 tests, 0 failures) including OTel package, consumption, and core tests
key_files:
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/TracerProviderBuilderExtensions.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj
key_decisions:
  - D106 (from T02) superseded D097: companion package takes OpenTelemetry NuGet dependency for AddCupelInstrumentation() extension method; core Wollax.Cupel remains dependency-free
patterns_established:
  - AddCupelInstrumentation() extension method on TracerProviderBuilder as the canonical registration surface
observability_surfaces:
  - ActivityListener.ActivityStopped captures all cupel.pipeline and cupel.stage.* Activities with exact cupel.* attributes
  - In-memory exporter tests verify all 3 tiers plus null-report graceful degradation
  - PublicAPI analyzer enforces no undocumented public surface (0 RS0016 errors)
duration: ~10m
verification_result: passed
completed_at: 2026-03-23
blocker_discovered: false
---

# T03: Verified OTel companion package completeness — PublicAPI, README, pack, and full solution green

**Verified and closed out the `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package: 0 RS0016 errors, README with cardinality warning, nupkg artifact in ./packages, all 712 tests passing including 4 SDK-backed OTel contract tests and 7 consumption tests.**

## What Happened

T01 and T02 frontloaded most of T03's deliverables (PublicAPI.Unshipped.txt populated in T01, README written in T02, TracerProviderBuilderExtensions added in T01). T03 verified all must-haves are met and completed the remaining packaging step:

1. **PublicAPI compliance confirmed**: `dotnet build` produces 0 RS0016 errors. `PublicAPI.Unshipped.txt` contains all public symbols including `CupelVerbosity` enum values, `CupelActivitySource.SourceName`, `CupelOpenTelemetryTraceCollector` constructor/methods, and `TracerProviderBuilderExtensions.AddCupelInstrumentation`.

2. **README already present**: Includes pre-stability disclaimer, verbosity tier table with recommended environments, cardinality warning for Full tier, `AddCupelInstrumentation()` usage example, dispose behavior note, and activity hierarchy diagram.

3. **Package packed and distributed**: `dotnet pack --configuration Release` produces `Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.86.nupkg`; copied to `./packages/` for consumption test feed.

4. **Core isolation confirmed**: `grep -r "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj` returns nothing — core has no OTel dependency.

5. **Full solution green**: `dotnet test --configuration Release` passes all 712 tests across 7 test projects.

## Verification

```
dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/ 2>&1 | grep RS0016 | wc -l
→ 0

dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/
→ total: 4, failed: 0, succeeded: 4 (StageOnly, StageAndExclusions, Full, NullReport)

dotnet test --project tests/Wollax.Cupel.ConsumptionTests/
→ total: 7, failed: 0, succeeded: 7 (includes OpenTelemetry consumption smoke test)

dotnet test --configuration Release
→ total: 712, failed: 0, succeeded: 712

ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg
→ Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.86.nupkg

ls ./packages/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg
→ 3 versions (0.78, 0.84, 0.86)

grep -r "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj || echo "OK"
→ OK (no OTel in core)
```

## Diagnostics

- `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/` — 4 tests covering all 3 verbosity tiers and null-report graceful degradation
- `dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/ 2>&1 | grep RS0016` — confirms PublicAPI surface is complete
- `PublicAPI.Unshipped.txt` lists every public member — any accidental public API addition will trigger RS0016

## Deviations

- **Most T03 work was completed in T01/T02**: PublicAPI.Unshipped.txt was populated in T01 (plan said T03); README and TracerProviderBuilderExtensions were added during T01/T02. T03 was primarily a verification pass.
- **`OpenTelemetryReportSeamTests.cs` in core tests does not exist**: The slice plan references `tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs` but T01 placed all seam tests directly in the OTel test project. The OTel contract tests cover the equivalent assertions.

## Known Issues

None.

## Files Created/Modified

- `./packages/Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.86.nupkg` — latest package artifact copied to local feed
