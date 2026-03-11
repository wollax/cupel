---
phase: 01-project-scaffold-core-models
plan: 01
subsystem: build-infrastructure
tags: [dotnet, solution, cpm, sourcelink, tunit, benchmarkdotnet]
dependency-graph:
  requires: []
  provides: [solution-structure, build-configuration, central-package-management, test-runner, benchmark-runner]
  affects: [01-02, 01-03, 01-04, 01-05, 02, 03, 04, 05, 06, 07, 08, 09, 10]
tech-stack:
  added: [TUnit 1.19.22, BenchmarkDotNet 0.15.8, MinVer 7.0.0, Microsoft.SourceLink.GitHub 10.0.102, Microsoft.CodeAnalysis.PublicApiAnalyzers 3.3.4]
  patterns: [Central Package Management, Directory.Build.props shared config, PublicApiAnalyzers tracking, Microsoft.Testing.Platform runner]
key-files:
  created:
    - global.json
    - Directory.Build.props
    - Directory.Packages.props
    - .editorconfig
    - Cupel.slnx
    - src/Wollax.Cupel/Wollax.Cupel.csproj
    - src/Wollax.Cupel/PublicAPI.Shipped.txt
    - src/Wollax.Cupel/PublicAPI.Unshipped.txt
    - tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj
    - tests/Wollax.Cupel.Tests/SmokeTests.cs
    - benchmarks/Wollax.Cupel.Benchmarks/Wollax.Cupel.Benchmarks.csproj
    - benchmarks/Wollax.Cupel.Benchmarks/Program.cs
  modified: []
decisions:
  - id: D-01-01-01
    decision: "Use Cupel.slnx (XML solution format) instead of Cupel.sln — .NET 10 SDK default"
  - id: D-01-01-02
    decision: "Enable Microsoft.Testing.Platform runner via global.json test section for .NET 10 dotnet test compatibility"
metrics:
  duration: ~7 minutes
  completed: 2026-03-11
---

# Phase 01 Plan 01: Solution Scaffold & Build Configuration Summary

**.NET 10 solution with 3 projects, centralized build config (CPM, TreatWarningsAsErrors, SourceLink, PublicApiAnalyzers), TUnit test runner, and BenchmarkDotNet entry point — all building with zero warnings.**

## Tasks Completed

### Task 1: Create solution structure and build configuration (6eca18a)

- Created `global.json` pinning .NET 10 SDK with `rollForward: latestFeature`
- Created `Directory.Build.props` with TreatWarningsAsErrors, Nullable enable, LangVersion latest, ImplicitUsings, EnforceCodeStyleInBuild, deterministic build settings, SourceLink, and NuGet metadata
- Created `Directory.Packages.props` with Central Package Management (MinVer, SourceLink, PublicApiAnalyzers, TUnit, BenchmarkDotNet)
- Created `Wollax.Cupel` classlib project with PublicApiAnalyzers and public API tracking files
- Created `.editorconfig` with C# conventions (4-space indent, LF line endings)

### Task 2: Create test and benchmark projects, wire solution (0c1e8e0)

- Created `Wollax.Cupel.Tests` with TUnit (OutputType Exe, no Microsoft.NET.Test.Sdk)
- Created smoke test verifying TUnit test discovery works
- Created `Wollax.Cupel.Benchmarks` with BenchmarkDotNet entry point
- Created `Cupel.slnx` with all 3 projects organized in solution folders (src/, tests/, benchmarks/)
- Enabled Microsoft.Testing.Platform runner in `global.json` for .NET 10 `dotnet test` compatibility

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| D-01-01-01 | Use `Cupel.slnx` instead of `Cupel.sln` | .NET 10 SDK generates `.slnx` (XML format) by default. This is the modern solution format going forward. |
| D-01-01-02 | Enable MTP runner via `global.json` `test` section | .NET 10 SDK dropped VSTest support for Microsoft.Testing.Platform projects. TUnit uses MTP, so the new `dotnet test` mode must be enabled via `global.json`. |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] TUnit analyzer rejects constant assertions**

- **Found during:** Task 2
- **Issue:** Smoke test `Assert.That(true).IsTrue()` triggers `TUnitAssertions0005` error — TUnit's analyzer forbids constant values in assertions
- **Fix:** Changed to `Assert.That(AppDomain.CurrentDomain.GetAssemblies()).IsNotNull()` which uses a runtime value
- **Files modified:** `tests/Wollax.Cupel.Tests/SmokeTests.cs`

**2. [Rule 3 - Blocking] .NET 10 requires Microsoft.Testing.Platform mode for dotnet test**

- **Found during:** Task 2
- **Issue:** `dotnet test` fails with error requiring opt-in to new test experience — VSTest mode no longer supported for MTP projects in .NET 10 SDK
- **Fix:** Added `"test": { "runner": "Microsoft.Testing.Platform" }` section to `global.json`
- **Files modified:** `global.json`

**3. [Rule 3 - Blocking] .NET 10 SDK generates .slnx instead of .sln**

- **Found during:** Task 2
- **Issue:** `dotnet new sln` creates `Cupel.slnx` (new XML format), not `Cupel.sln`
- **Fix:** Used `.slnx` format as-is — this is the .NET 10 default and the modern standard
- **Files modified:** `Cupel.slnx`

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build Cupel.slnx` — zero errors, zero warnings | PASS |
| `dotnet test --solution Cupel.slnx` — 1 test discovered and passed | PASS |
| Core project has zero runtime dependencies | PASS |
| TreatWarningsAsErrors enforced in Directory.Build.props | PASS |
| ManagePackageVersionsCentrally active in Directory.Packages.props | PASS |
| PublicAPI.txt files contain `#nullable enable` | PASS |

## Next Phase Readiness

No blockers. Solution is ready for model implementation (Plan 01-02 onwards). All subsequent plans can reference `Cupel.slnx` and rely on:
- Central Package Management for any new dependencies
- TreatWarningsAsErrors enforcement
- TUnit test discovery via `dotnet test --solution Cupel.slnx`
- PublicApiAnalyzers tracking public API surface
