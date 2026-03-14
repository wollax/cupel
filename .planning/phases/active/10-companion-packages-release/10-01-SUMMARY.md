---
phase: 10-companion-packages-release
plan: 01
subsystem: dependency-injection
tags: [di, ioc, keyed-services, ioptions, extension-methods]
depends_on:
  requires: [08-policy-system]
  provides: [di-integration-package, addcupel-extensions, keyed-pipeline-resolution]
  affects: [10-02, 10-03]
tech_stack:
  added: [Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Options]
  patterns: [keyed-services, ioptions-pattern, extension-method-on-iservicecollection]
key_files:
  created:
    - src/Wollax.Cupel.Extensions.DependencyInjection/Wollax.Cupel.Extensions.DependencyInjection.csproj
    - src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs
    - src/Wollax.Cupel.Extensions.DependencyInjection/PublicAPI.Shipped.txt
    - src/Wollax.Cupel.Extensions.DependencyInjection/PublicAPI.Unshipped.txt
    - tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/Wollax.Cupel.Extensions.DependencyInjection.Tests.csproj
    - tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs
  modified:
    - Directory.Packages.props
    - Cupel.slnx
decisions:
  - Namespace is Microsoft.Extensions.DependencyInjection (standard .NET convention for IServiceCollection extensions)
  - AddCupelTracing uses TryAddTransient to avoid overriding user-provided ITraceCollector registrations
  - Budget is a registration-time parameter on AddCupelPipeline, not part of CupelOptions
metrics:
  duration: 2m 38s
  completed: 2026-03-14
---

# Phase 10 Plan 01: DI Integration Package Summary

DI companion package with AddCupel/AddCupelPipeline/AddCupelTracing extensions using IOptions pattern and keyed transient services.

## What Was Done

### Task 1: Create DI integration project with AddCupel extension methods
- Created `Wollax.Cupel.Extensions.DependencyInjection` packable project following the Wollax.Cupel.Json companion pattern
- Implemented three extension methods on `IServiceCollection`:
  - `AddCupel(Action<CupelOptions>)` — registers CupelOptions via IOptions<T>
  - `AddCupelPipeline(string intent, ContextBudget budget)` — registers keyed transient CupelPipeline per intent
  - `AddCupelTracing()` — registers DiagnosticTraceCollector as transient ITraceCollector
- Added package versions for Microsoft.Extensions.DependencyInjection.Abstractions, Options, and DI (test-only) to Directory.Packages.props
- Updated Cupel.slnx with both new projects
- Populated PublicAPI.Unshipped.txt with all public API entries
- **Commit:** `4a380bb`

### Task 2: Create DI integration tests
- Created test project with TUnit and concrete Microsoft.Extensions.DependencyInjection container
- 9 tests covering:
  - Options registration and policy accessibility
  - Keyed pipeline resolution and functional execution
  - Transient lifetime verification (different instances per resolve)
  - Missing policy → KeyNotFoundException
  - Multiple named pipelines resolve independently
  - Null/whitespace/empty intent argument validation (3 tests)
  - AddCupelTracing registers transient ITraceCollector
- **Commit:** `3e03a36`

## Deviations from Plan

None — plan executed exactly as written.

## Verification

- Solution-wide `dotnet build` succeeds with zero warnings
- All 9 DI integration tests pass
- DI project references only abstractions packages (not full container)

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Namespace: `Microsoft.Extensions.DependencyInjection` | Standard .NET convention for IServiceCollection extension methods |
| `TryAddTransient` for tracing | Avoids overriding user-provided ITraceCollector registrations |
| Budget as registration parameter | Token budgets vary by model/deployment, not by policy intent |

## Next Phase Readiness

No blockers. The DI integration package is complete and ready for the remaining Phase 10 plans.
