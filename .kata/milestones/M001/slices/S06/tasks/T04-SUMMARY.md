---
task: T04
slice: S06
milestone: M001
status: done
blocker_discovered: false
net_test_delta: +5
---

# T04: Fill test coverage gaps and remove test hygiene issues

## What Was Built

Six test files updated: 6 new tests added, 1 duplicate removed. Total suite grew from 658 to 663 tests (net +5).

### Changes Made

1. **`CupelPolicyTests.cs`** — Added `Validation_StreamBatchSizeWithKnapsackSlicer_Throws`: verifies that `SlicerType.Knapsack` + `streamBatchSize: 10` throws `ArgumentException`, symmetric to the existing Greedy variant.

2. **`QuotaSliceTests.cs`** — Added `QuotaSlice_NullSlicer_ThrowsArgumentNull` and `QuotaSlice_NullQuotas_ThrowsArgumentNull`: both use `ThrowsExactly<ArgumentNullException>` and pass `null!` for the respective constructor argument; backed by real `ArgumentNullException.ThrowIfNull` guards in the constructor.

3. **`PriorityScorerTests.cs`** — Added `ScoresAreInZeroToOneRange`: creates items with varying priority values (1, 5, 10, 100, null) and asserts every `Score()` result is `>= 0.0` and `<= 1.0`. Mirrors the `RecencyScorerTests` pattern.

4. **`TagScorerTests.cs`** — Added two tests:
   - `TagScorer_CaseInsensitiveMatch`: scorer key `"important"` (weight 1.0), item tag `"IMPORTANT"` (uppercase) → score > 0. Works because `TagScorer` preserves the `StringComparer.OrdinalIgnoreCase` comparer from the source `Dictionary` when building the internal `FrozenDictionary`.
   - `TagScorer_ZeroTotalWeight_ReturnsZeroScore`: scorer with `"important"` weight `0.0` → `_totalWeight == 0.0` → early return `0.0` without dividing by zero.

5. **`CupelServiceCollectionExtensionsTests.cs`** — Removed the duplicate `AddCupelTracing_IsTransient_DifferentInstancesPerResolve` at line ~288 (second occurrence). The equivalent test at line ~155 (`AddCupelTracing_RegistersTransientTraceCollector`) already asserts `ReferenceEquals(collector1, collector2)` is false.

6. **`PipelineBuilderTests.cs`** — No change required: the `Count > 0` assertion described in the plan was already `IsEqualTo(3)` in the current codebase. Verified via `grep -n "Count > 0"` returning no matches.

## Verification

- `dotnet test` (all 5 projects) — 663 tests, 0 failures
- `dotnet build` — zero errors, zero warnings
- All new exception tests use `ThrowsExactly<T>` (not `Throws<T>`)
- Each test class uses its own `CreateItem` helper — no shared fixtures introduced

## Slice-Level Verification Status (final task)

- ✅ `dotnet test` — 663 tests pass (649+ goal met)
- ✅ `dotnet test --filter KnapsackSlice` — guard boundary tests pass (T01)
- ✅ `dotnet test --filter CupelPolicyTests` — Knapsack stream batch size test passes (T04)
- ✅ `dotnet test --filter QuotaSlice` — null constructor arg tests pass (T04)
- ✅ `dotnet test --filter PriorityScorer|TagScorer` — new scorer tests pass (T04)
- ✅ `dotnet build` — zero errors, zero warnings

## Provides

- `Validation_StreamBatchSizeWithKnapsackSlicer_Throws` in `CupelPolicyTests` — Knapsack + StreamBatchSize validation coverage
- `QuotaSlice_NullSlicer_ThrowsArgumentNull` / `QuotaSlice_NullQuotas_ThrowsArgumentNull` — null-safety coverage on `QuotaSlice` constructor
- `ScoresAreInZeroToOneRange` in `PriorityScorerTests` — output range invariant for PriorityScorer
- `TagScorer_CaseInsensitiveMatch` / `TagScorer_ZeroTotalWeight_ReturnsZeroScore` — case-insensitive lookup and zero-weight branch coverage for TagScorer
- Duplicate DI test removed — clean test inventory, no false confidence from redundant assertions

## Decisions

None — all changes follow established patterns in the test suite. No new fixtures, helpers, or abstractions introduced.
