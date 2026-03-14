# Phase 14, Plan 02 — JSON Serialization Completeness

**Status:** Complete
**Started:** 2026-03-14T22:00:05Z
**Completed:** 2026-03-14T22:04:00Z
**Duration:** ~4 minutes

## Objective

Refactor the hardcoded `BuiltInScorerTypes` array in `CupelJsonSerializer` to derive from `ScorerType` enum values via reflection, and add JSON round-trip tests for policies containing `ScaledScorer` (nested `InnerScorer`) and `StreamSlice` (with `StreamBatchSize`).

## Tasks

### Task 1: Refactor BuiltInScorerTypes to enum-derived
**Commit:** `e51c888`
**Status:** Complete

- The refactoring was already performed by plan 01 (commit `6056522`) as part of the model extension work
- Added 3 verification tests to `CustomScorerTests.cs`:
  - `BuiltInScorerTypes_DerivedFromEnum_HasExpectedCount` — asserts 7 entries
  - `BuiltInScorerTypes_ContainsScaled` — confirms "scaled" is present
  - `BuiltInScorerTypes_ContainsAllOriginalTypes` — confirms all 6 original types present
- Changed `BuiltInScorerTypes` from `private` to `internal` (done by plan 01) enabling direct test access via `InternalsVisibleTo`

### Task 2: JSON round-trip tests for Scaled and Stream
**Commit:** `e4ca7b9`
**Status:** Complete

Added 6 tests to `RoundTripTests.cs` and 1 to `CustomScorerTests.cs`:
1. `ScaledScorer_RoundTrips` — Scaled wrapping Recency, verifies type/weight/innerScorer
2. `NestedScaledScorer_RoundTrips` — Scaled(Scaled(Priority)), verifies 2 levels of nesting
3. `StreamSlice_WithBatchSize_RoundTrips` — Stream slicer with batchSize=16
4. `StreamSlice_NullBatchSize_RoundTrips` — Stream slicer with null batchSize
5. `MixedPolicy_ScaledScorerAndStreamSlicer_RoundTrips` — Scaled+Kind inner scorer with Stream slicer, full fidelity
6. `Deserialize_UnknownScorerType_IncludesScaledInKnownTypes` — unknown type error message includes "scaled"
7. Updated `NullOptionalFields_OmittedInJson` to also assert `streamBatchSize` and `innerScorer` are omitted

## Deviations

1. **PublicAPI files updated (blocking issue):** Plan 01 modified source files (enums, ScorerEntry, CupelPolicy) but had not yet updated the PublicAPI analyzer files when this plan started execution. Updated `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` to unblock the build. Plan 01 subsequently committed these changes itself.

2. **Task 1 refactoring already done by plan 01:** The `BuiltInScorerTypes` reflection refactoring and `private`→`internal` visibility change were included in plan 01's commit `6056522`. This plan contributed only the verification tests for Task 1.

## Verification

- `dotnet build` — zero warnings across entire solution
- `dotnet test --project tests/Wollax.Cupel.Json.Tests/` — 47/47 tests pass (38 existing + 9 new)
- Unknown scorer error message includes "scaled" in known types list (verified by test)

## Files Modified

- `tests/Wollax.Cupel.Json.Tests/RoundTripTests.cs` — 6 new round-trip tests + 1 updated assertion
- `tests/Wollax.Cupel.Json.Tests/CustomScorerTests.cs` — 3 BuiltInScorerTypes tests + 1 unknown type error test

## Success Criteria

| Criterion | Status |
|-----------|--------|
| BuiltInScorerTypes derived from enum reflection | Done (by plan 01, verified by tests) |
| JSON round-trip handles InnerScorer nesting | Done |
| JSON round-trip handles StreamBatchSize | Done |
| Unknown scorer detection works with enum-derived array | Done |
| All existing JSON tests pass unchanged | Done (38/38) |
