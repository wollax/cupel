---
phase: 10-companion-packages-release
plan: 03
status: complete
started: 2026-03-14
completed: 2026-03-14
---

# 10-03 Summary: CI/CD, Consumption Tests & API Freeze

Created CI and publish workflows, consumption tests against packed .nupkg files, and finalized PublicAPI.Shipped.txt for all four packages.

## Decisions

- Consumption test project uses `*-*` version wildcard to match prerelease .nupkg versions from MinVer
- Consumption test project uses `PackageReference Update` for Microsoft.SourceLink.GitHub and MinVer to supply versions for packages inherited from Directory.Build.props/Directory.Packages.props while having `ManagePackageVersionsCentrally` disabled

## Deviations

- **Version wildcard**: Plan specified `Version="*"` but MinVer produces prerelease versions (`0.0.0-alpha.0.N`) which `*` does not match. Changed to `*-*` to include prerelease.
- **Inherited package references**: The consumption test project inherits `Microsoft.SourceLink.GitHub` from `Directory.Build.props` and `MinVer` as a `GlobalPackageReference`. With `ManagePackageVersionsCentrally` disabled, these need explicit versions via `PackageReference Update`.

## Commits

| Hash | Message |
|------|---------|
| c749eca | ci(10-03): create CI and publish workflows |
| 5a1f305 | test(10-03): create consumption tests against packed .nupkg files |
| d18de47 | chore(10-03): finalize PublicAPI.Shipped.txt for v1.0 API freeze |

## Artifacts

| File | Purpose |
|------|---------|
| .github/workflows/ci.yml | PR build+test workflow (push/PR to main) |
| .github/workflows/release.yml | Manual-dispatch publish workflow with OIDC, consumption tests, GitHub Release |
| tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj | Standalone test project using PackageReference against packed .nupkg |
| tests/Wollax.Cupel.ConsumptionTests/nuget.config | Local NuGet source with `<clear />` for isolation |
| tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs | 5 tests verifying core pipeline, JSON, DI, and Tiktoken from NuGet |
| src/*/PublicAPI.Shipped.txt | Frozen v1.0 API surface for all four packages |

## Verification

- `dotnet build --configuration Release` — zero warnings
- `dotnet test --configuration Release` — 589 tests pass
- Consumption tests: all 5 pass against locally packed .nupkg files
- All PublicAPI.Shipped.txt populated; all PublicAPI.Unshipped.txt contain only `#nullable enable`
- Both workflow YAML files validated
