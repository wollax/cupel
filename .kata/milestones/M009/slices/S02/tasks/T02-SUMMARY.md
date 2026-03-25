---
id: T02
parent: S02
milestone: M009
provides:
  - CountConstrainedKnapsackSlice.cs — full ISlicer + IQuotaPolicy implementation (~230 lines)
  - Phase 1 count-satisfy copied from CountQuotaSlice, Phase 2 delegates to KnapsackSlice with score-descending re-sort (D180), Phase 3 cap-enforcement seeded from Phase 1 counts (D181)
  - CupelPipeline.cs — 3 parallel wiring extensions for CountConstrainedKnapsackSlice (shortfall wiring, selectedKindCounts construction, cap-classification)
  - All 5 integration tests in CountConstrainedKnapsackTests.cs passing through CupelPipeline.DryRun()
  - dotnet build 0 warnings, full test suite 684/684 green
key_files:
  - src/Wollax.Cupel/Slicing/CountConstrainedKnapsackSlice.cs
  - src/Wollax.Cupel/CupelPipeline.cs
key_decisions:
  - "D180 confirmed: Phase 2 knapsack output re-sorted by score descending using scoreByContent dict before Phase 3 cap loop — ensures highest-score items survive cap, verified by CapExclusion_TwoCapExcluded test"
  - "D181 confirmed: selectedCount seeded from Phase 1 committed counts before Phase 3 — committed items correctly count against cap"
  - "Constructor accepts KnapsackSlice without guard (unlike CountQuotaSlice which rejects it) — KnapsackSlice is the whole point of this class"
patterns_established:
  - "CountConstrainedKnapsackSlice mirrors CountQuotaSlice structure: same Phase 1 and Phase 3 blocks, same selectedCount seeding, same shortfall wiring. Difference is Phase 2 delegates to KnapsackSlice instead of an ISlicer, and re-sorts output by score."
  - "Pipeline wiring follows else-if pattern: CountConstrainedKnapsackSlice blocks added after existing CountQuotaSlice blocks in all 3 wiring locations"
observability_surfaces:
  - "result.Report.CountRequirementShortfalls — populated for CountConstrainedKnapsackSlice via pipeline Change 1"
  - "result.Report.Excluded where Reason == CountCapExceeded — classified for CountConstrainedKnapsackSlice via pipeline Change 3"
  - "CountConstrainedKnapsackSlice.LastShortfalls — inspection surface for tests and diagnostics"
duration: 20min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T02: Implement CountConstrainedKnapsackSlice and extend pipeline wiring

**CountConstrainedKnapsackSlice implemented with Phase 1/2/3 algorithm and CupelPipeline extended with 3 parallel wiring checks; all 5 integration tests pass, 684/684 suite green, 0 build warnings.**

## What Happened

Created `CountConstrainedKnapsackSlice.cs` in `Wollax.Cupel.Slicing` namespace. The class is a faithful port combining CountQuotaSlice's Phase 1 and Phase 3 logic with KnapsackSlice delegation for Phase 2. Phase 1 (count-satisfy) and Phase 3 (cap-enforce) are copied verbatim from CountQuotaSlice with minimal field-name adaptations. Phase 2 builds a `scoreByContent` dictionary from the residual pool, delegates to `_knapsack.Slice()`, then re-sorts the output by score descending via `OrderByDescending(item => scoreByContent.GetValueOrDefault(item.Content, 0.0))` before Phase 3 (D180). Phase 3 cap enforcement uses `selectedCount` seeded from Phase 1 committed counts so committed items count against the cap (D181).

Three pipeline wiring changes applied to `CupelPipeline.cs`:
1. **Shortfall wiring** (~line 349): `else if (_slicer is CountConstrainedKnapsackSlice ccksShortfall ...)` after existing CountQuotaSlice shortfall block.
2. **selectedKindCounts construction** (~line 381): Extended condition to `(_slicer is CountQuotaSlice || _slicer is CountConstrainedKnapsackSlice)`.
3. **Cap-classification** (~line 411): `else if (_slicer is CountConstrainedKnapsackSlice ccks)` with identical logic using `ccks.Entries.FirstOrDefault(e => e.Kind == kind)`.

## Verification

- `dotnet build` — Build succeeded, 0 Warning(s), 0 Error(s)
- `dotnet test --project tests/Wollax.Cupel.Tests/` — 684 total, 684 succeeded, 0 failed (includes all 5 CountConstrainedKnapsack tests from T01)
- Pre-T01 count was 679; current count 684 confirms all 5 new tests are included and passing
- `CapExclusion_TwoCapExcluded` confirms D180: only tool-a and tool-b (highest scores) survive cap=2, tool-c and tool-d are cap-excluded
- `ScarcityDegrade_ShortfallRecorded` confirms shortfall wiring: CountRequirementShortfalls populated with RequiredCount=3, SatisfiedCount=1
- `RequireAndCap_NoResidualExcluded` confirms D181: tool-a and tool-b committed in Phase 1, cap=2 reached, no residual cap exclusions

## Diagnostics

- `result.Report.CountRequirementShortfalls` populated when CountConstrainedKnapsackSlice is used with insufficient candidates
- `result.Report.Excluded.Where(e => e.Reason == ExclusionReason.CountCapExceeded)` lists cap-excluded items
- `CountConstrainedKnapsackSlice.LastShortfalls` readable directly after Slice() for test inspection
- D180 regression visible as `CapExclusion_TwoCapExcluded` failure (wrong items survive cap)
- D181 regression visible as too many items passing Phase 3 cap check in `RequireAndCap_NoResidualExcluded`

## Deviations

None. Implementation followed T02-PLAN exactly.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/Slicing/CountConstrainedKnapsackSlice.cs` — New: full ISlicer + IQuotaPolicy implementation (~230 lines)
- `src/Wollax.Cupel/CupelPipeline.cs` — Modified: 3 pipeline wiring extensions for CountConstrainedKnapsackSlice
