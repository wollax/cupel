---
estimated_steps: 4
estimated_files: 3
---

# T01: Write Failing-First Verification for Budget Simulation and Deterministic Ties

**Slice:** S06 — Budget simulation + tiebreaker + spec alignment
**Milestone:** M003

## Description

Create the executable verification surfaces for S06 before implementation: focused .NET tests for the new budget-simulation API, plus explicit GreedySlice tie-break regression tests in .NET and Rust. This task is intentionally failing-first for the budget-simulation API so later tasks have a precise stop condition. It also locks the deterministic-tie contract into test code before any doc cleanup happens.

## Steps

1. Add `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs` with focused TUnit cases for:
   - `GetMarginalItems(items, budget, slackTokens)` returning the exact reference-equal items present only in the full-budget dry run
   - `FindMinBudgetFor(items, budget, targetItem, searchCeiling)` returning the first successful budget, not just any successful budget
   - `ArgumentException` for `targetItem` not in `items`
   - `ArgumentException` for `searchCeiling < targetItem.Tokens`
   - `InvalidOperationException` with the spec-defined monotonicity messages for `QuotaSlice` and `CountQuotaSlice`
2. Extend `tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs` with equal-density and zero-token tie-order assertions that prove original input order is preserved when densities tie.
3. Extend the Rust GreedySlice unit tests in `crates/cupel/src/slicer/greedy.rs` with the same deterministic-tie scenarios so both implementations have executable coverage.
4. Run the focused test commands and confirm the expected initial state: budget-simulation tests fail because the API is not implemented yet, while GreedySlice tie tests either pass immediately or fail only for real tie-order mismatches.

## Must-Haves

- [ ] `BudgetSimulationTests.cs` exists and names both public APIs exactly as the slice intends to ship them
- [ ] The .NET test file asserts object-reference diff semantics, not value-equality diff semantics
- [ ] The .NET test file locks the exact guard and argument failure messages for monotonicity violations and invalid search input
- [ ] `GreedySliceTests.cs` includes an explicit equal-density deterministic-order regression test
- [ ] `crates/cupel/src/slicer/greedy.rs` includes a matching Rust deterministic-order regression test
- [ ] Focused verification is run once so later tasks inherit known-failing tests instead of unexercised scaffolding

## Verification

- `rtk dotnet test tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --filter "FullyQualifiedName~BudgetSimulationTests|FullyQualifiedName~GreedySliceTests"`
- `rtk cargo test greedy -- --nocapture`

## Observability Impact

- Signals added/changed: focused assertion failures for budget override wiring, diff semantics, binary-search termination, and monotonicity guards
- How a future agent inspects this: run the two focused test commands and read the failing assertion names/messages
- Failure state exposed: exact missing API names or mismatched tie-order expectations become visible immediately in test output

## Inputs

- `src/Wollax.Cupel/CupelPipeline.cs` — current `DryRun` behavior and the missing budget-override seam
- `tests/Wollax.Cupel.Tests/Pipeline/DryRunTests.cs` — existing dry-run test style and helper patterns
- `tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs` — current .NET GreedySlice coverage
- `crates/cupel/src/slicer/greedy.rs` — current Rust GreedySlice implementation and embedded unit-test style
- `spec/src/analytics/budget-simulation.md` — budget-simulation contract, guards, and determinism requirements
- `spec/src/slicers/greedy.md` — existing tie-break wording to encode into tests before docs are rewritten

## Expected Output

- `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs` — new failing-first .NET test file covering budget-simulation happy paths and guard rails
- `tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs` — updated with deterministic-tie regression coverage
- `crates/cupel/src/slicer/greedy.rs` — updated with matching Rust tie-break regression coverage
- Focused test output demonstrating the exact remaining implementation gaps for S06
