# Phase 14, Plan 03 — DI Singleton Lifetime Fix

**Status:** Complete
**Started:** 2026-03-14T22:11:12Z
**Completed:** 2026-03-14T22:14:44Z
**Duration:** ~4 minutes

## Objective

Fix DI lifetime divergence so scorers, slicers, and placers are singletons shared across pipeline resolves (matching the ROADMAP specification). Verify via reference equality tests.

## Tasks

### Task 1: DI singleton component lifetime fix
**Commit:** `7dbcbca`

Changes:
- Added `InternalsVisibleTo` entries in `Wollax.Cupel.csproj` for `Wollax.Cupel.Extensions.DependencyInjection` and `Wollax.Cupel.Extensions.DependencyInjection.Tests`
- Added internal accessor properties to `CupelPipeline`: `Scorer`, `Slicer`, `Placer`, `AsyncSlicer`, `DeduplicationEnabled`, `OverflowStrategyValue`
- Created internal `PolicyComponents` sealed record in `CupelServiceCollectionExtensions` to hold pre-built components
- Refactored `AddCupelPipeline` to register keyed singleton `PolicyComponents` (built once on first resolve) and keyed transient `CupelPipeline` (new instance per resolve wrapping singleton components)
- `ITraceCollector` remains transient (unchanged)

Files modified:
- `src/Wollax.Cupel/Wollax.Cupel.csproj`
- `src/Wollax.Cupel/CupelPipeline.cs`
- `src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs`

### Task 2: Singleton verification tests and PublicAPI updates
**Commit:** `6d1ffc3`

Changes:
- Added 4 new DI tests verifying singleton component lifetime:
  - `AddCupelPipeline_ComponentsAreSingletons_SameInstanceAcrossResolves` — verifies scorer/slicer/placer are same instance
  - `AddCupelPipeline_ScaledScorerPolicy_ComponentsAreSingletons` — verifies composed ScaledScorer tree shares singleton reference
  - `AddCupelPipeline_StreamPolicy_AsyncSlicerIsSingleton` — verifies async slicer is singleton for Stream policies
  - `AddCupelTracing_IsTransient_DifferentInstancesPerResolve` — verifies ITraceCollector remains transient
- PublicAPI files already complete from plans 01 and 02 — no changes needed

Files modified:
- `tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs`

## Deviations

- Added extra internal accessors to `CupelPipeline` beyond plan (`AsyncSlicer`, `DeduplicationEnabled`, `OverflowStrategyValue`) — needed for the `PolicyComponents` record to fully capture all pipeline configuration that varies per-policy. Without these, the transient pipeline factory would lose async slicer, deduplication, and overflow strategy settings.

## Verification

- Full solution build: zero warnings (including PublicApiAnalyzers)
- Full solution tests: 641 passed, 0 failed, 0 skipped
- DI singleton test: two resolves of same intent return different pipeline instances but same scorer/slicer/placer instances
- Stream policy test: async slicer verified as singleton
- ITraceCollector: verified as transient
