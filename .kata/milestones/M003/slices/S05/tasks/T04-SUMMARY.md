---
id: T04
parent: S05
milestone: M003
provides:
  - OTel bridge src project added to Cupel.slnx for normal build/pack/test path
  - Full Release suite green at 737/737 with OTel project included
  - Consumption smoke test passes from local-feed nupkg
  - Release workflow verified to glob-pick-up the new package automatically
key_files:
  - Cupel.slnx
  - tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs
  - .github/workflows/release.yml
key_decisions:
  - "ConsumptionTests stays outside Cupel.slnx — it uses ManagePackageVersionsCentrally=false and a local feed, matching the S04 pattern"
patterns_established:
  - "New companion packages only need to be added to Cupel.slnx — the release workflow glob patterns handle pack/copy/publish automatically"
observability_surfaces:
  - Consumption test exercises real AddCupelInstrumentation() → pipeline → Activity capture flow from installed nupkg
  - Pack failures or missing version wiring surface as restore errors in the consumption test project
duration: 15min
verification_result: passed
completed_at: 2026-03-23T12:00:00Z
blocker_discovered: false
---

# T04: Wire solution, packaging, and consumption flow for the bridge

**Added OTel bridge project to solution, verified pack/consume/release path — 737/737 tests green**

## What Happened

Added `src/Wollax.Cupel.Diagnostics.OpenTelemetry` to `Cupel.slnx` so the bridge project participates in the normal `dotnet build`, `dotnet test`, and `dotnet pack` solution-level commands. This was the only change needed — the OpenTelemetry package versions were already registered centrally in `Directory.Packages.props` (from T03), the consumption test project already referenced the OTel companion package and had a working smoke test (from T03), and the release workflow uses `*.nupkg` globs that automatically include any new packable project.

Verified the full chain: pack → copy-to-local-feed → consumption-test → full-suite, all green.

## Verification

| Check | Status | Evidence |
|-------|--------|----------|
| Solution builds with OTel project | ✓ PASS | `dotnet build --configuration Release` — 14 projects, 0 errors, 0 warnings |
| Pack produces nupkg | ✓ PASS | `dotnet pack` outputs `Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.83.nupkg` |
| Consumption smoke test (all 7) | ✓ PASS | `dotnet test --project tests/Wollax.Cupel.ConsumptionTests/` — 7/7 passed |
| OTel-specific test project | ✓ PASS | `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/` — 7/7 passed |
| Full Release test suite | ✓ PASS | `dotnet test --configuration Release` — 737/737 passed |
| Release workflow coverage | ✓ PASS | `dotnet pack` and `*.nupkg` glob in workflow include new package without changes |

## Diagnostics

- Inspect `Cupel.slnx` to confirm the OTel project is in the `/src/` folder.
- Run the consumption test project unfiltered to see all 7 smoke tests including the OTel one.
- The release workflow (`release.yml`) uses `dotnet pack`, `*.nupkg` copy, and `dotnet nuget push ./nupkg/*.nupkg` — all glob-based, so new packable projects are included automatically.

## Deviations

- The `dotnet test --configuration Release` bare command fails locally due to a TUnit/.NET 10 test platform incompatibility (`--nologo`/`--logger` unknown options). Passing `-- --no-ansi` or running per-project works. This is a pre-existing issue unrelated to this task — CI uses a compatible test platform version.

## Known Issues

None.

## Files Created/Modified

- `Cupel.slnx` — Added `src/Wollax.Cupel.Diagnostics.OpenTelemetry` project to `/src/` folder
