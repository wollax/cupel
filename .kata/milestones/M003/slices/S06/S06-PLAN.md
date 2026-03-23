# S06: Budget simulation + tiebreaker + spec alignment

**Goal:** Ship the .NET budget-simulation API on `CupelPipeline`, lock GreedySlice tie-breaking to a concrete deterministic contract in both implementations, and finish the remaining M003 spec/index/changelog alignment so the shipped feature set and docs agree.
**Demo:** After this slice, a caller can run `pipeline.GetMarginalItems(items, budget, slackTokens)` and `pipeline.FindMinBudgetFor(items, budget, targetItem, searchCeiling)` in .NET, equal-density GreedySlice selections stay deterministic across .NET and Rust, and the spec navigation/changelog clearly documents CountQuotaSlice, DecayScorer, MetadataTrustScorer, budget simulation, and the tie-break rule.

## Must-Haves

- `.NET` exposes `GetMarginalItems(IReadOnlyList<ContextItem>, ContextBudget, int)` on `CupelPipeline`; it re-runs the real pipeline with a temporary budget override, diffs by object reference equality, and throws the spec-defined `InvalidOperationException` for `QuotaSlice`
- `.NET` exposes `FindMinBudgetFor(IReadOnlyList<ContextItem>, ContextBudget, ContextItem, int)` on `CupelPipeline`; it binary-searches via real `DryRun` calls, returns `int?`, validates `targetItem ∈ items` and `searchCeiling >= targetItem.Tokens`, and throws the spec-defined monotonicity guard for `QuotaSlice` and `CountQuotaSlice`
- The internal budget-override seam lives inside `CupelPipeline` so normal `Execute`/`DryRun` and simulation `DryRun` share the same execution core rather than duplicating pipeline logic
- Focused .NET tests prove happy paths, argument guards, monotonicity guards, and object-reference diff behavior for budget simulation; the first task creates these tests before implementation
- GreedySlice tie-breaking is explicitly tested in both .NET and Rust and documented as deterministic original-order / original-index ascending behavior for equal densities; no undefined `Id` field is introduced into `ContextItem`
- `spec/src/slicers/greedy.md` and `spec/src/analytics/budget-simulation.md` describe the same deterministic tie-breaking contract the code implements
- `spec/src/SUMMARY.md`, `spec/src/slicers.md`, `spec/src/scorers.md`, and `spec/src/changelog.md` all reference the shipped M003 features accurately; `spec/src/slicers/count-quota.md` exists and is linked
- Rust budget-simulation parity remains deferred for v1.3 and the deferral rationale is documented in the budget-simulation spec chapter
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` is updated for every new public budget-simulation API surface
- `rtk dotnet test` and `rtk cargo test --all-targets` both exit 0 after the slice lands

## Requirement Coverage

- Active requirements owned/supported by this slice: none. `REQUIREMENTS.md` has 0 Active requirements; S06 closes milestone-level M003 acceptance gaps instead of advancing a still-active requirement.

## Proof Level

- This slice proves: integration (real `CupelPipeline` dry-run behavior is reused for budget simulation in .NET; deterministic tie-breaking is locked by executable tests in both languages; spec/index/changelog alignment is verified by artifact checks)
- Real runtime required: no (library-level test harnesses and artifact verification are sufficient)
- Human/UAT required: no

## Verification

- `rtk dotnet test tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --filter "FullyQualifiedName~BudgetSimulationTests|FullyQualifiedName~GreedySliceTests"`
  - Expected: budget-simulation happy-path and failure-path assertions pass; .NET tie-break tests pass
- `rtk cargo test --all-targets`
  - Expected: Rust GreedySlice tie-break regression tests pass with the full crate suite green
- `rtk dotnet test`
  - Expected: whole .NET suite stays green after adding the new API and tests
- `rtk dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj`
  - Expected: 0 errors, including no RS0016/PublicAPI failures
- `rtk grep "GetMarginalItems|FindMinBudgetFor" src/Wollax.Cupel/PublicAPI.Unshipped.txt src/Wollax.Cupel`
  - Expected: both public API entries and implementation call sites exist
- `rtk grep "CountQuotaSlice|DecayScorer|MetadataTrustScorer|budget simulation|original-index|original order" spec/src/SUMMARY.md spec/src/slicers.md spec/src/scorers.md spec/src/changelog.md spec/src/slicers/greedy.md spec/src/analytics/budget-simulation.md spec/src/slicers/count-quota.md`
  - Expected: all documentation/index/changelog references resolve to real text in the checked-in spec

## Observability / Diagnostics

- Runtime signals: stable exception messages for monotonicity violations and bad arguments (`ArgumentException` / `InvalidOperationException`); existing `SelectionReport` from `DryRun` remains the source of truth for inclusion/exclusion inspection
- Inspection surfaces: focused `BudgetSimulationTests.cs`, existing `DryRunTests.cs`, .NET/Rust GreedySlice regression tests, and grep-able spec files for navigation/changelog alignment
- Failure visibility: failing tests localize whether the break is in budget override wiring, diff semantics, binary-search termination, or tie-breaking; PublicAPI build failures identify missing signatures explicitly
- Redaction constraints: none (library/test/doc slice; no secrets or PII)

## Integration Closure

- Upstream surfaces consumed: `src/Wollax.Cupel/CupelPipeline.cs`, `tests/Wollax.Cupel.Tests/Pipeline/DryRunTests.cs`, `src/Wollax.Cupel/GreedySlice.cs`, `crates/cupel/src/slicer/greedy.rs`, `spec/src/analytics/budget-simulation.md`, `spec/src/slicers/greedy.md`, and the S04 analytics surface used by downstream callers
- New wiring introduced in this slice: internal budget-override execution seam in `CupelPipeline`; public `.NET` budget-simulation extension methods; deterministic tie-break regression tests in both languages; CountQuotaSlice spec page plus mdBook navigation/changelog/index updates
- What remains before the milestone is truly usable end-to-end: no feature gaps remain after this slice; only slice summary, milestone summary, and milestone-wide final verification remain

## Tasks

- [x] **T01: Write failing-first verification for budget simulation and deterministic ties** `est:45m`
  - Why: This slice changes core selection behavior and public API surface; the stopping condition needs executable tests before implementation so regressions localize cleanly and spec/doc drift cannot hide behind green builds.
  - Files: `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs`, `tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs`, `crates/cupel/src/slicer/greedy.rs`
  - Do: Create `BudgetSimulationTests.cs` with initially failing tests that assert `GetMarginalItems` returns reference-equal marginal items, `FindMinBudgetFor` binary-searches to the first successful budget, and both methods throw the exact guard/argument messages for non-monotonic or invalid inputs; add explicit equal-density / zero-token tie-order regression tests to the existing .NET and Rust GreedySlice test surfaces.
  - Verify: `rtk dotnet test tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --filter "FullyQualifiedName~BudgetSimulationTests|FullyQualifiedName~GreedySliceTests"` should fail for missing API/docs rather than malformed tests; `rtk cargo test greedy -- --nocapture` should pass or fail only on the new tiebreak assertions.
  - Done when: the repo contains focused failing-first tests that name the intended budget-simulation API and deterministic tie behavior explicitly, and any failure points to unimplemented slice work rather than ambiguous test scaffolding.

- [x] **T02: Implement the .NET budget-override seam and public simulation APIs** `est:1h`
  - Why: The slice's main functional deliverable is real budget simulation on `CupelPipeline`; this task closes the runtime gap by reusing the real `DryRun` path instead of cloning pipeline logic in extensions.
  - Files: `src/Wollax.Cupel/CupelPipeline.cs`, `src/Wollax.Cupel/Diagnostics/CupelPipelineBudgetSimulationExtensions.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`, `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs`
  - Do: Add an internal execution seam in `CupelPipeline` that accepts a temporary `ContextBudget` override while preserving the existing `Execute`/`DryRun` behavior and diagnostics pipeline; implement public extension methods `GetMarginalItems(items, budget, slackTokens)` and `FindMinBudgetFor(items, budget, targetItem, searchCeiling)` against that seam; keep item comparison reference-based; enforce the spec-defined `QuotaSlice` / `CountQuotaSlice` guards and precondition messages; update `PublicAPI.Unshipped.txt`; make the focused tests from T01 pass.
  - Verify: `rtk dotnet test tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --filter "FullyQualifiedName~BudgetSimulationTests"`; `rtk dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj`; `rtk grep "GetMarginalItems|FindMinBudgetFor" src/Wollax.Cupel/PublicAPI.Unshipped.txt src/Wollax.Cupel`.
  - Done when: both public APIs exist with the explicit `ContextBudget` parameter, all budget-simulation tests pass, the guard/error messages are locked by tests, and `CupelPipeline` still has a single execution core.

- [x] **T03: Lock the GreedySlice tie-break contract across .NET, Rust, and spec text** `est:45m`
  - Why: The roadmap's “id ascending” wording is currently impossible literally because `ContextItem` has no `Id`; this task resolves the ambiguity by committing the real deterministic contract and preventing future regressions.
  - Files: `src/Wollax.Cupel/GreedySlice.cs`, `tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs`, `crates/cupel/src/slicer/greedy.rs`, `spec/src/slicers/greedy.md`, `spec/src/analytics/budget-simulation.md`
  - Do: Keep the implementations on stable original-index ordering for equal densities, adding or tightening comments/tests where needed; update the spec language from ambiguous “id ascending” intent to the concrete original-order/original-index contract that callers can actually rely on; ensure the budget-simulation determinism section references the same rule so repeated dry runs have a single documented tie behavior.
  - Verify: `rtk dotnet test tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --filter "FullyQualifiedName~GreedySliceTests"`; `rtk cargo test --all-targets`; `rtk grep "original index|original order|stable" spec/src/slicers/greedy.md spec/src/analytics/budget-simulation.md src/Wollax.Cupel/GreedySlice.cs crates/cupel/src/slicer/greedy.rs`.
  - Done when: .NET and Rust both have explicit executable regression coverage for equal-density ties, and the spec text matches the actual implementation without inventing a new `ContextItem.Id` surface.

- [x] **T04: Complete spec navigation, CountQuotaSlice docs, and milestone-facing changelog alignment** `est:45m`
  - Why: S06 is the cleanup slice for M003 feature/spec alignment; without this task, the implementation can ship while the mdBook navigation and release notes still misrepresent what v1.3 contains.
  - Files: `spec/src/SUMMARY.md`, `spec/src/slicers.md`, `spec/src/scorers.md`, `spec/src/slicers/count-quota.md`, `spec/src/changelog.md`, `spec/src/analytics/budget-simulation.md`
  - Do: Write the real `CountQuotaSlice` spec page and link it from `SUMMARY.md` and `slicers.md`; update `scorers.md` so DecayScorer and MetadataTrustScorer are fully represented in the scorer table/categories; update `changelog.md` with the v1.3 additions and the deterministic tie-break clarification; extend the budget-simulation chapter with the explicit Rust-parity deferral rationale and the final `FindMinBudgetFor` signature so all cross-references agree.
  - Verify: `rtk grep "count-quota.md|CountQuotaSlice" spec/src/SUMMARY.md spec/src/slicers.md spec/src/slicers/count-quota.md spec/src/changelog.md`; `rtk grep "DecayScorer|MetadataTrustScorer" spec/src/scorers.md spec/src/changelog.md`; `rtk grep "FindMinBudgetFor|Language Parity Note" spec/src/analytics/budget-simulation.md`.
  - Done when: every shipped M003 scorer/slicer surface is reachable from the spec nav/index files, the changelog mentions the new functionality, and the budget-simulation chapter documents the final parity/signature decisions.

## Files Likely Touched

- `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs` — new focused .NET verification surface
- `src/Wollax.Cupel/CupelPipeline.cs` — internal budget override seam
- `src/Wollax.Cupel/Diagnostics/CupelPipelineBudgetSimulationExtensions.cs` — new public budget-simulation APIs
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — new API signatures
- `tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs` — .NET tie-break regressions
- `crates/cupel/src/slicer/greedy.rs` — Rust tie-break regressions / comments
- `src/Wollax.Cupel/GreedySlice.cs` — comment or comparator clarification if needed
- `spec/src/slicers/greedy.md` — deterministic tie-break contract
- `spec/src/analytics/budget-simulation.md` — final API/signature/parity note
- `spec/src/SUMMARY.md` — mdBook navigation updates
- `spec/src/slicers.md` — slicer index updates
- `spec/src/scorers.md` — scorer index/category updates
- `spec/src/slicers/count-quota.md` — new spec page
- `spec/src/changelog.md` — v1.3 alignment entry
