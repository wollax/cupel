---
id: T03
parent: S04
milestone: M003
provides:
  - Patterns 8–13 in SelectionReportAssertionChain (HaveAtLeastNExclusions, ExcludedItemsAreSortedByScoreDescending, HaveBudgetUtilizationAbove, HaveKindCoverageCount, PlaceItemAtEdge, PlaceTopNScoredAtEdges)
  - tests/Wollax.Cupel.Testing.Tests project with 26 TUnit tests covering all 13 patterns (happy + failure paths)
  - Wollax.Cupel.ConsumptionTests references Wollax.Cupel.Testing via PackageReference Version="*-*"
  - nupkg/Wollax.Cupel.Testing.*.nupkg produced; copied to consumption tests local feed
  - Cupel.slnx updated with new test project; full dotnet test: 708 tests, 0 failures
key_files:
  - src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs
  - src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj
  - tests/Wollax.Cupel.Testing.Tests/AssertionChainTests.cs
  - tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj
  - tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs
  - Cupel.slnx
key_decisions:
  - Consumption tests use a local `./packages` NuGet feed (not `./nupkg`); nupkg must be copied there for `Version="*-*"` resolution to work
  - PlaceTopNScoredAtEdges uses HashSet membership check against topN items + HashSet of edge positions; ties handled by minTopScore comparison
  - Test helper MakeReport/MakeIncluded/MakeExcluded pattern used for concise inline SelectionReport construction without pipeline
patterns_established:
  - Failure-path tests use try/catch SelectionReportAssertionException with message content assertions (not Assert.Throws, avoids TUnit async complexity)
observability_surfaces:
  - dotnet test --project tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj — shows all 26 tests
  - SelectionReportAssertionException.Message carries structured name+params+actual for every pattern
duration: ~30min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T03: Patterns 8–13 + Test Project + Consumption Test Wiring

**Completed the 13-pattern assertion vocabulary, proved all patterns via 26 TUnit tests, and wired Wollax.Cupel.Testing into consumption tests as an installed NuGet package.**

## What Happened

Added patterns 8–13 to `SelectionReportAssertionChain.cs`:
- **P8 HaveAtLeastNExclusions(n)** — count check on Excluded
- **P9 ExcludedItemsAreSortedByScoreDescending()** — adjacent-pair loop
- **P10 HaveBudgetUtilizationAbove(threshold, budget)** — includedTokens / budget.MaxTokens
- **P11 HaveKindCoverageCount(n)** — distinct Kind values in Included
- **P12 PlaceItemAtEdge(predicate)** — first/last position check
- **P13 PlaceTopNScoredAtEdges(n)** — alternating edge positions (lo++, hi--)

Added `using Wollax.Cupel;` to the file for `ContextBudget`. All 6 entries added to `PublicAPI.Unshipped.txt`; build clean at 0 errors, 0 warnings.

Created `tests/Wollax.Cupel.Testing.Tests/` project (referenced via solution) with `AssertionChainTests.cs` containing 26 tests — 2 per pattern (happy path + failure path). Failure-path tests assert on `SelectionReportAssertionException.Message` content to verify error message format matches spec.

For consumption tests: discovered local feed is `./packages` (not `./nupkg`) per `nuget.config`. Packed to `./nupkg` then copied `.nupkg` file to `./packages`. Added `PackageReference Include="Wollax.Cupel.Testing" Version="*-*"` to consumption csproj. Added smoke test `Testing_Package_Should_Extension_Compiles_And_Works` that calls `report.Should().IncludeItemWithKind(...).HaveAtLeastNExclusions(0).HaveKindCoverageCount(1)` to prove end-to-end package installation works.

## Verification

- `dotnet test --project tests/Wollax.Cupel.Testing.Tests/...` → 26 passed, 0 failed
- `dotnet test` (full solution) → 708 passed, 0 failed
- `dotnet test --project tests/Wollax.Cupel.ConsumptionTests/...` → 6 passed, 0 failed (was 5)
- `cargo test --all-targets` (crates/cupel) → 124 passed, 0 failed
- `ls ./nupkg/Wollax.Cupel.Testing.*.nupkg | wc -l` → 1
- `grep "Wollax.Cupel.Testing" tests/Wollax.Cupel.ConsumptionTests/...csproj` → match found
- `grep -c "public.*SelectionReportAssertionChain" src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` → 14 (≥13)
- PublicAPI compliance: `dotnet build src/Wollax.Cupel.Testing/...` → 0 errors

## Diagnostics

- `dotnet test --project tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj --verbosity normal` — see all 26 tests with names
- `SelectionReportAssertionException.Message` carries structured text for every assertion failure — no debugger needed
- If consumption tests fail to restore `Wollax.Cupel.Testing`: check `./packages` feed contains the `.nupkg`, not just `./nupkg`

## Deviations

- nupkg output goes to `./nupkg` by default but consumption tests use `./packages` as local feed. Added copy step as part of wiring workflow.
- Added smoke test to `ConsumptionTests.cs` (not listed in plan explicitly) to prove `Should()` compiles and chains work end-to-end from the installed package.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — added patterns 8–13 (+~120 lines); added `using Wollax.Cupel;`
- `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — added 6 new method signatures
- `tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj` — new; references Testing + Cupel projects
- `tests/Wollax.Cupel.Testing.Tests/AssertionChainTests.cs` — new; 26 TUnit tests covering all 13 patterns
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` — added Wollax.Cupel.Testing PackageReference
- `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs` — added Testing using + smoke test
- `Cupel.slnx` — added Wollax.Cupel.Testing.Tests project
