# S02: CountConstrainedKnapsackSlice — .NET implementation

**Goal:** Port `CountConstrainedKnapsackSlice` from Rust to C#, extend `CupelPipeline` with the three required pipeline-wiring checks, and prove correctness against the 5 TOML conformance vectors via real `DryRun()` integration tests.
**Demo:** `CountConstrainedKnapsackSlice` is constructable in .NET, passes 5 integration tests through `CupelPipeline.DryRun()` covering all conformance scenarios, `PublicAPI.Unshipped.txt` is complete, and `dotnet test` is green.

## Must-Haves

- `CountConstrainedKnapsackSlice` class exists in `src/Wollax.Cupel/Slicing/`, implements `ISlicer` and `IQuotaPolicy`, and accepts `KnapsackSlice` as a constructor parameter (not `ISlicer`).
- Phase 1 (count-satisfy), Phase 2 (KnapsackSlice delegate + score-descending re-sort), and Phase 3 (cap enforcement seeded from Phase 1 counts) match Rust semantics exactly (D180, D181).
- `LastShortfalls` is `public`, `Entries` is `internal`, `GetConstraints()` returns `QuotaConstraintMode.Count` entries.
- `ScarcityBehavior.Throw` throws `InvalidOperationException` using the same message pattern as `CountQuotaSlice`.
- All three `CupelPipeline` pipeline-wiring checks extended for `CountConstrainedKnapsackSlice`: shortfall wiring, `selectedKindCounts` construction, and cap-classification in the re-association loop.
- `PublicAPI.Unshipped.txt` contains all public members of `CountConstrainedKnapsackSlice` (build must pass with RS0016 analyzer).
- 5 integration tests in `tests/Wollax.Cupel.Tests/Pipeline/CountConstrainedKnapsackTests.cs` mirror the TOML conformance vectors and all pass via `CupelPipeline.DryRun()`.
- `dotnet test` green (0 failures). `dotnet build` 0 warnings.

## Proof Level

- This slice proves: integration (real pipeline `DryRun()` calls exercise the full Execute path including shortfall wiring and cap-classification re-association loop)
- Real runtime required: no (unit tests are sufficient — library function with no I/O)
- Human/UAT required: no

## Verification

```bash
# Red baseline after T01
dotnet build                           # Should show RS0016 errors for missing class
dotnet test --filter "CountConstrainedKnapsack"   # 5 tests fail (class missing)

# Green after T02
dotnet test --filter "CountConstrainedKnapsack"   # 5 tests pass
dotnet test                            # Full suite green
dotnet build                           # 0 warnings
```

- `tests/Wollax.Cupel.Tests/Pipeline/CountConstrainedKnapsackTests.cs` — 5 tests, one per TOML vector:
  - `Baseline_AllItemsIncluded_NoShortfalls_NoCap`
  - `CapExclusion_TwoCapExcluded`
  - `ScarcityDegrade_ShortfallRecorded`
  - `TagNonExclusive_MultipleKindsRequiredIndependently`
  - `RequireAndCap_NoResidualExcluded`

## Observability / Diagnostics

- Runtime signals: `result.Report.CountRequirementShortfalls` (shortfall count), `result.Report.Excluded.Where(e => e.Reason == ExclusionReason.CountCapExceeded)` (cap exclusion count)
- Inspection surfaces: `CountConstrainedKnapsackSlice.LastShortfalls` (test inspection); `dotnet test --filter "CountConstrainedKnapsack" -- --verbosity detailed` (per-test failure localization)
- Failure visibility: Test failure messages include expected vs actual counts and item contents; `dotnet build` RS0016 errors name missing PublicAPI.Unshipped.txt entries explicitly
- Redaction constraints: none — test data is synthetic

## Integration Closure

- Upstream surfaces consumed: `CountQuotaSlice.cs` (Phase 1/Phase 3 algorithm template), `KnapsackSlice.cs` (Phase 2 delegate), `CupelPipeline.cs` lines 347–420 (3 checks to extend), `CountQuotaIntegrationTests.cs` (test helper pattern), 5 TOML conformance vectors (test data)
- New wiring introduced in this slice: `CountConstrainedKnapsackSlice` class in `Wollax.Cupel.Slicing`; extended 3 pipeline checks to handle `CountConstrainedKnapsackSlice` alongside `CountQuotaSlice`
- What remains before the milestone is truly usable end-to-end: S03 (spec chapter for count-constrained-knapsack), S04 (MetadataKeyScorer)

## Tasks

- [x] **T01: Write failing integration tests and PublicAPI.Unshipped.txt entries** `est:20m`
  - Why: Establishes the red baseline — 5 tests that describe exactly what `CountConstrainedKnapsackSlice` must do; RS0016 analyzer entries ensure the build fails deterministically until the class exists with the correct public surface.
  - Files: `tests/Wollax.Cupel.Tests/Pipeline/CountConstrainedKnapsackTests.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
  - Do: Write `CountConstrainedKnapsackTests.cs` using the same `Item()` + `DryRun()` helper pattern as `CountQuotaIntegrationTests.cs`. Each test uses `CupelPipeline.CreateBuilder().WithSlicer(new CountConstrainedKnapsackSlice(...))` and asserts `result.Report.Included` contents, `result.Report.CountRequirementShortfalls.Count`, and `result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded)`. Map each test to its TOML vector. Add 5 PublicAPI entries: class declaration, constructor, `LastShortfalls`, `GetConstraints()`, `Slice()`.
  - Verify: `dotnet build` fails with CS0246 ("type or namespace 'CountConstrainedKnapsackSlice' not found") — confirms tests reference the unimplemented class. `dotnet test --filter "CountConstrainedKnapsack"` reports 5 build/compile errors.
  - Done when: 5 test methods exist, each asserting the correct included/shortfall/cap counts from its TOML vector; `PublicAPI.Unshipped.txt` has all 5 entries for `CountConstrainedKnapsackSlice`; build fails only because the class is absent, not due to test logic errors.

- [x] **T02: Implement CountConstrainedKnapsackSlice and extend pipeline wiring** `est:45m`
  - Why: Closes the red baseline — implements the class and the 3 pipeline checks so all 5 integration tests pass through `DryRun()`.
  - Files: `src/Wollax.Cupel/Slicing/CountConstrainedKnapsackSlice.cs`, `src/Wollax.Cupel/CupelPipeline.cs`
  - Do: (1) Create `CountConstrainedKnapsackSlice.cs` in `Wollax.Cupel.Slicing` namespace. Copy Phase 1 and Phase 3 blocks verbatim from `CountQuotaSlice.cs` adapting field names. Replace Phase 2 `_innerSlicer.Slice(residual, ...)` with: build `scoreByContent` dict from `residual` before calling `_knapsack.Slice(residual, residualBudget, traceCollector)`, then sort Phase 2 output by score descending using the dict before Phase 3 cap loop (D180). Seed Phase 3 `selectedCount` from Phase 1 committed counts (D181). Constructor takes `KnapsackSlice` directly (not `ISlicer`) — no KnapsackSlice guard needed. Store `_knapsack` as `KnapsackSlice` field. Expose `internal IReadOnlyList<CountQuotaEntry> Entries => _entries`. `ScarcityBehavior.Throw` message: `$"CountConstrainedKnapsackSlice: candidate pool for kind '{entry.Kind}' has {satisfied} items but RequireCount is {entry.RequireCount}."`. (2) Extend `CupelPipeline.cs` — Change 1 (shortfall wiring ~line 349): add `else if (_slicer is CountConstrainedKnapsackSlice ccksShort && reportBuilder is not null && ccksShort.LastShortfalls.Count > 0) { reportBuilder.SetCountRequirementShortfalls(ccksShort.LastShortfalls); }`. Change 2 (selectedKindCounts construction ~line 378): extend condition to `|| _slicer is CountConstrainedKnapsackSlice`. Change 3 (cap-classification ~line 408): add `else if (selectedKindCounts is not null && _slicer is CountConstrainedKnapsackSlice ccks)` block mirroring the `CountQuotaSlice cqs` block using `ccks.Entries.FirstOrDefault(...)`.
  - Verify: `dotnet test --filter "CountConstrainedKnapsack"` — 5 pass. `dotnet test` — full suite green. `dotnet build` — 0 warnings.
  - Done when: All 5 integration tests pass; `dotnet build` exits with 0 warnings; no existing tests broken.

## Files Likely Touched

- `src/Wollax.Cupel/Slicing/CountConstrainedKnapsackSlice.cs` — New
- `src/Wollax.Cupel/CupelPipeline.cs` — 3 pipeline check extensions
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 5 new entries
- `tests/Wollax.Cupel.Tests/Pipeline/CountConstrainedKnapsackTests.cs` — New: 5 integration tests
