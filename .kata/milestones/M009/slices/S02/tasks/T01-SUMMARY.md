---
id: T01
parent: S02
milestone: M009
provides:
  - CountConstrainedKnapsackTests.cs with 5 [Test] methods, one per TOML conformance vector
  - PublicAPI.Unshipped.txt extended with 5 entries for CountConstrainedKnapsackSlice public surface
  - Red baseline confirmed — dotnet build fails with RS0017 naming CountConstrainedKnapsackSlice explicitly
  - DryRun() helper pattern with explicit bucketSize and scarcity parameters (mirrors CountQuotaIntegrationTests)
  - Tests assert Included.Count, Included.Content membership, CountRequirementShortfalls.Count, and CountCapExceeded exclusion count
requires: []
affects: [T02]
key_files:
  - tests/Wollax.Cupel.Tests/Pipeline/CountConstrainedKnapsackTests.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
key_decisions:
  - "DryRun helper accepts bucketSize int parameter (not a fixed constant) because count-constrained-knapsack-require-and-cap.toml uses bucket_size=1 for exact knapsack precision"
  - "tag-nonexclusive test uses ContextKind.Memory (well-known) not new ContextKind('memory') — matches TOML 'memory' kind which maps to the canonical Memory constant"
patterns_established:
  - "CountConstrainedKnapsack tests follow same Item()/DryRun() helper structure as CountQuotaIntegrationTests.cs"
drill_down_paths:
  - .kata/milestones/M009/slices/S02/tasks/T01-PLAN.md
duration: 15min
verification_result: pass
blocker_discovered: false
completed_at: 2026-03-24T00:00:00Z
---

# T01: Write failing integration tests and PublicAPI.Unshipped.txt entries

**5 failing integration tests and PublicAPI.Unshipped.txt surface established; dotnet build fails with RS0017 for CountConstrainedKnapsackSlice (class absent)**

## What Happened

Created `CountConstrainedKnapsackTests.cs` with 5 `[Test]` methods mirroring the TOML conformance vectors. The `DryRun()` helper builds a pipeline using `CountConstrainedKnapsackSlice` directly (via `WithSlicer`) and calls `pipeline.DryRun(items)`. Added a `bucketSize` parameter to the helper to accommodate the `require-and-cap` vector which uses `bucket_size=1`.

The five tests cover:
- `Baseline_AllItemsIncluded_NoShortfalls_NoCap` — 3 items (2 tool, 1 msg), require=2 cap=4, all 3 selected
- `CapExclusion_TwoCapExcluded` — 4 tool items, require=1 cap=2, top-2 included, 2 cap-excluded
- `ScarcityDegrade_ShortfallRecorded` — 1 tool item against require=3, shortfall Kind/RequiredCount/SatisfiedCount asserted
- `TagNonExclusive_MultipleKindsRequiredIndependently` — tool + Memory kinds, independent require=1 each, all 3 included
- `RequireAndCap_NoResidualExcluded` — bucket_size=1, 2 tool + 3 msg items, all 5 included with no cap exclusions

Added all 5 `PublicAPI.Unshipped.txt` entries: class declaration, constructor (taking `IReadOnlyList<CountQuotaEntry>!`, `KnapsackSlice!`, `ScarcityBehavior`), `LastShortfalls`, `GetConstraints()`, `Slice()`.

Build fails with 5 RS0017 errors naming `CountConstrainedKnapsackSlice` explicitly — correct red baseline. The library project fails to build, which cascades to the test project.

## Deviations

- The task plan said build should fail with CS0246 (type not found). In practice, the RS0017 errors in PublicAPI.Unshipped.txt cause the library to fail first, preventing the test project from compiling at all. This is functionally equivalent — the tests cannot run and the missing class is named explicitly in the error output. The red baseline is correctly established.
- `ContextKind.Memory` (well-known constant) used for the `memory` kind in `TagNonExclusive` instead of `new ContextKind("memory")` — these are equal by value due to case-insensitive comparison in `ContextKind.Equals`, but using the constant is idiomatic.

## Files Created/Modified

- `tests/Wollax.Cupel.Tests/Pipeline/CountConstrainedKnapsackTests.cs` — New: 5 integration tests
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 5 new entries appended for CountConstrainedKnapsackSlice
