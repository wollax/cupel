---
estimated_steps: 5
estimated_files: 4
---

# T02: Implement the .NET Budget-Override Seam and Public Simulation APIs

**Slice:** S06 — Budget simulation + tiebreaker + spec alignment
**Milestone:** M003

## Description

Implement the actual .NET budget-simulation feature by extending `CupelPipeline` with an internal temporary-budget execution seam and exposing public extension methods on `CupelPipeline`. This task must reuse the real `DryRun` pipeline path, not duplicate classify/score/deduplicate/slice/place logic in the extension layer. The output is the shipped public API surface for S06.

## Steps

1. Refactor `CupelPipeline` so `Execute` and `DryRun` delegate into a single execution core that can optionally receive a temporary `ContextBudget` override without changing the pipeline's stored `_budget`.
2. Add `src/Wollax.Cupel/Diagnostics/CupelPipelineBudgetSimulationExtensions.cs` with public extension methods:
   - `GetMarginalItems(this CupelPipeline pipeline, IReadOnlyList<ContextItem> items, ContextBudget budget, int slackTokens)`
   - `FindMinBudgetFor(this CupelPipeline pipeline, IReadOnlyList<ContextItem> items, ContextBudget budget, ContextItem targetItem, int searchCeiling)`
3. Implement `GetMarginalItems` by running the real dry-run twice with the provided budget and the reduced budget, diffing included items by object reference equality, and throwing the exact `QuotaSlice` monotonicity message when the configured slicer is `QuotaSlice`.
4. Implement `FindMinBudgetFor` as a binary search over real dry runs, validating `targetItem ∈ items` and `searchCeiling >= targetItem.Tokens`, returning `int?`, and throwing the exact `QuotaSlice` / `CountQuotaSlice` monotonicity guard when the configured slicer is incompatible.
5. Update `src/Wollax.Cupel/PublicAPI.Unshipped.txt` and make the focused budget-simulation tests from T01 pass without breaking normal `Execute` / `DryRun` behavior.

## Must-Haves

- [ ] `CupelPipeline` has one shared execution core used by normal runs and temporary-budget simulation runs
- [ ] `GetMarginalItems` is public, extension-based, and accepts explicit `ContextBudget budget`
- [ ] `FindMinBudgetFor` is public, extension-based, returns `int?`, and accepts explicit `ContextBudget budget`
- [ ] `GetMarginalItems` diffs included items by reference equality, not by content or structural value
- [ ] `FindMinBudgetFor` performs a real binary search and confirms the final candidate budget before returning
- [ ] Exact guard messages for `QuotaSlice` / `CountQuotaSlice` are enforced by code and tests
- [ ] `PublicAPI.Unshipped.txt` contains all new public API signatures
- [ ] Focused `BudgetSimulationTests.cs` pass after the implementation lands

## Verification

- `rtk dotnet test tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --filter "FullyQualifiedName~BudgetSimulationTests"`
- `rtk dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj`
- `rtk grep "GetMarginalItems|FindMinBudgetFor" src/Wollax.Cupel/PublicAPI.Unshipped.txt src/Wollax.Cupel`

## Observability Impact

- Signals added/changed: stable public exception messages for non-monotonic slicers and invalid search inputs; budget-simulation tests become the primary inspection surface for runtime regressions
- How a future agent inspects this: run `BudgetSimulationTests.cs`; inspect `CupelPipelineBudgetSimulationExtensions.cs` and the internal budget-override helper in `CupelPipeline.cs`
- Failure state exposed: test failures show whether the break is in the temporary budget override, monotonicity guard, reference-equality diff, or binary-search termination logic

## Inputs

- `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs` — failing-first acceptance tests from T01
- `src/Wollax.Cupel/CupelPipeline.cs` — current execution core and `_budget` storage
- `tests/Wollax.Cupel.Tests/Pipeline/DryRunTests.cs` — dry-run parity expectations that must remain true
- `spec/src/analytics/budget-simulation.md` — algorithm, guard messages, and determinism invariant
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — required manifest for new public members
- S04 summary — analytics extension pattern and PublicAPI workflow for new public surface additions

## Expected Output

- `src/Wollax.Cupel/CupelPipeline.cs` — internal temporary-budget execution seam while preserving existing behavior
- `src/Wollax.Cupel/Diagnostics/CupelPipelineBudgetSimulationExtensions.cs` — public `.NET` budget-simulation API
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — updated signatures for both new extension methods
- `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs` — now green against the real implementation
