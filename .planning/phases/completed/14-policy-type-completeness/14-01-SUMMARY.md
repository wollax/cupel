# 14-01 Summary: Enum Extensions, Model Classes, and PipelineBuilder Wiring

**Phase:** 14-policy-type-completeness
**Plan:** 01
**Status:** Complete
**Duration:** ~5 minutes
**Start:** 2026-03-14T22:00:03Z

## Tasks Completed

### Task 1: Enum extensions and model class changes (`e51c888`)
- Added `ScorerType.Scaled` (value 6) with `[JsonStringEnumMemberName("scaled")]`
- Added `SlicerType.Stream` (value 2) with `[JsonStringEnumMemberName("stream")]`
- Extended `ScorerEntry` with `InnerScorer` property and constructor validation:
  - Scaled type requires non-null InnerScorer
  - Non-Scaled type rejects non-null InnerScorer
- Extended `CupelPolicy` with `StreamBatchSize` property and constructor validation:
  - StreamBatchSize only valid with SlicerType.Stream
  - Must be positive when specified
  - Quotas + Stream combination rejected (QuotaSlice is sync-only)
- Updated PublicAPI files (Shipped + Unshipped)

### Task 2: PipelineBuilder wiring and tests (`1777553`)
- Extended `CreateScorer` switch with recursive `ScorerType.Scaled` → `ScaledScorer` construction
- Extended `WithPolicy` slicer switch with `SlicerType.Stream` → `UseGreedySlice()` (sync fallback) + `WithAsyncSlicer(new StreamSlice(...))` (async path)
- Added 5 ScorerEntry tests (Scaled valid, Scaled null throws, non-Scaled with inner throws, nested Scaled, default null)
- Added 7 CupelPolicy tests (Stream with batch size, null batch size, zero/negative throws, non-Stream with batch size throws, quotas+Stream throws, default null)
- Added 6 PipelineBuilder tests (Scaled builds, Scaled produces results, nested Scaled builds, Stream builds, sync execute with fallback, async ExecuteStreamAsync works, default batch size)

## Deviations

### Auto-fix: BuiltInScorerTypes refactoring (from hardcoded to enum-derived)
The linter/auto-fixer refactored `CupelJsonSerializer.BuiltInScorerTypes` from a hardcoded string array to `Enum.GetValues<ScorerType>()` with reflection on `JsonStringEnumMemberNameAttribute`. This was a planned refactoring from the RESEARCH.md and CONTEXT.md. Included in Task 1 commit.

## Verification

- `dotnet build src/Wollax.Cupel/` — zero warnings
- `dotnet build` (full solution) — zero warnings
- `dotnet test tests/Wollax.Cupel.Tests/` — 565 tests pass (0 failed, 0 skipped)
- All existing tests pass unchanged

## Files Modified

| File | Change |
|------|--------|
| `src/Wollax.Cupel/ScorerType.cs` | Added `Scaled` enum value |
| `src/Wollax.Cupel/SlicerType.cs` | Added `Stream` enum value |
| `src/Wollax.Cupel/ScorerEntry.cs` | Added `InnerScorer` property + validation |
| `src/Wollax.Cupel/CupelPolicy.cs` | Added `StreamBatchSize` property + validation |
| `src/Wollax.Cupel/PipelineBuilder.cs` | Extended `CreateScorer` + `WithPolicy` switch cases |
| `src/Wollax.Cupel/PublicAPI.Shipped.txt` | Updated constructor signatures |
| `src/Wollax.Cupel/PublicAPI.Unshipped.txt` | Added new API surface entries |
| `src/Wollax.Cupel.Json/CupelJsonSerializer.cs` | Refactored BuiltInScorerTypes to enum-derived |
| `tests/Wollax.Cupel.Tests/Policy/ScorerEntryTests.cs` | Added 5 Scaled tests |
| `tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs` | Added 7 Stream tests |
| `tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs` | Added 6 wiring tests |
