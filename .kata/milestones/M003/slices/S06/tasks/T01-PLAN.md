---
estimated_steps: 6
estimated_files: 4
---

# T01: Add budget-override DryRun seam + GetMarginalItems + FindMinBudgetFor

**Slice:** S06 — Budget simulation + tiebreaker + spec alignment
**Milestone:** M003

## Description

Add the internal budget-override seam to `CupelPipeline` and implement both budget simulation extension methods (`GetMarginalItems` and `FindMinBudgetFor`) per the spec in `spec/src/analytics/budget-simulation.md`. Both methods need to run `DryRun` at alternate budgets, which requires adding an internal `DryRunWithBudget` method that overrides the pipeline's stored `_budget`. The extension methods implement the spec's guards (QuotaSlice for GetMarginalItems; QuotaSlice + CountQuotaSlice for FindMinBudgetFor) and the binary search algorithm for FindMinBudgetFor.

## Steps

1. **Add `DryRunWithBudget` internal method to `CupelPipeline`**: Add an `internal ContextResult DryRunWithBudget(IReadOnlyList<ContextItem> items, ContextBudget budget)` method. This creates a `DiagnosticTraceCollector` and calls a new private `ExecuteCore` overload that accepts a `ContextBudget` parameter instead of using `_budget`. The simplest approach: extract `_budget` usage from `ExecuteCore` into a parameter, make the existing `ExecuteCore` pass `_budget`, and have `DryRunWithBudget` pass the override budget.

2. **Create `BudgetSimulationExtensions.cs`**: New static class in `Wollax.Cupel` namespace. Implement `GetMarginalItems` following the spec pseudocode: (a) check if pipeline's `Slicer` is `QuotaSlice` → throw `InvalidOperationException` with exact spec message; (b) construct `reducedBudget` with `budget.MaxTokens - slackTokens` and `budget.TargetTokens - slackTokens` (same `OutputReserve`); (c) call `pipeline.DryRunWithBudget` twice (full budget and reduced budget); (d) return items in primary.Included that are absent from margin.Included via `ReferenceEquals` loop.

3. **Implement `FindMinBudgetFor`**: In the same extension class: (a) check slicer is not `QuotaSlice` and not `CountQuotaSlice` → throw with exact spec message; (b) validate preconditions (`targetItem` must be in `items` via `ReferenceEquals`; `searchCeiling >= targetItem.Tokens`); (c) binary search loop `while (high - low > 1)`; (d) construct midBudget with `mid` as MaxTokens (preserve OutputReserve, use same reserved slots pattern); (e) final confirmation DryRun at `high`; (f) return `high` if target present, else `null`.

4. **Update `PublicAPI.Unshipped.txt`**: Add entries for `BudgetSimulationExtensions` class, `GetMarginalItems`, and `FindMinBudgetFor` method signatures.

5. **Create `BudgetSimulationTests.cs`**: In `tests/Wollax.Cupel.Tests/Pipeline/`. Build a simple pipeline with GreedySlice for happy-path tests. Tests:
   - `GetMarginalItems_ReturnsItemsOnlyInFullBudget` — 3 items with different token counts; slack removes the lowest-density item
   - `GetMarginalItems_EmptyWhenSlackIsZero` — same budget for both runs → empty marginal list
   - `GetMarginalItems_ThrowsForQuotaSlice` — pipeline with QuotaSlice → InvalidOperationException
   - `FindMinBudgetFor_FindsCorrectMinimum` — target item included at some budget, binary search converges
   - `FindMinBudgetFor_ReturnsNullWhenUnreachable` — search ceiling too small for target + competing items
   - `FindMinBudgetFor_ThrowsForCountQuotaSlice` — pipeline with CountQuotaSlice → InvalidOperationException
   - `FindMinBudgetFor_ThrowsWhenTargetNotInItems` — ArgumentException
   - `FindMinBudgetFor_ThrowsWhenCeilingTooLow` — ArgumentException

6. **Build and run tests**: `dotnet build src/Wollax.Cupel/` to verify RS0016 compliance, then `dotnet test --filter BudgetSimulation`.

## Must-Haves

- [ ] `DryRunWithBudget` internal method on CupelPipeline allows alternate-budget DryRun
- [ ] `GetMarginalItems` extension method matches spec signature and behavior
- [ ] `FindMinBudgetFor` extension method matches spec signature and behavior with binary search
- [ ] QuotaSlice guard on GetMarginalItems throws InvalidOperationException with exact spec message
- [ ] QuotaSlice + CountQuotaSlice guard on FindMinBudgetFor throws with exact spec message
- [ ] FindMinBudgetFor preconditions (targetItem membership, searchCeiling) throw ArgumentException
- [ ] Reference equality used for item identity in marginal diff
- [ ] PublicAPI.Unshipped.txt has all new public members
- [ ] ≥6 tests pass covering happy paths, guards, and edge cases

## Verification

- `rtk dotnet test --filter BudgetSimulation` → all tests pass
- `dotnet build src/Wollax.Cupel/ 2>&1 | grep RS0016 | wc -l` → 0
- `grep -q "GetMarginalItems" src/Wollax.Cupel/PublicAPI.Unshipped.txt` → match

## Observability Impact

- Signals added/changed: `InvalidOperationException` with structured slicer-naming messages for guard violations; `ArgumentException` for precondition failures on FindMinBudgetFor
- How a future agent inspects this: Test output from `BudgetSimulationTests`; exception messages in test failure output; PublicAPI.Unshipped.txt diff for API surface audit
- Failure state exposed: Guard violations surface as typed exceptions with actionable messages naming the offending slicer and the monotonicity requirement

## Inputs

- `src/Wollax.Cupel/CupelPipeline.cs` — `ExecuteCore` must be extended with budget parameter
- `spec/src/analytics/budget-simulation.md` — authoritative spec for signatures, guards, pseudocode
- `src/Wollax.Cupel/Slicing/QuotaSlice.cs` — type identity for guard checks
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — type identity for FindMinBudgetFor guard
- S04 forward intelligence: PublicAPI.Unshipped.txt workflow (build → capture RS0016 → populate)

## Expected Output

- `src/Wollax.Cupel/CupelPipeline.cs` — modified with `DryRunWithBudget` internal method and `ExecuteCore` budget parameter
- `src/Wollax.Cupel/BudgetSimulationExtensions.cs` — new file with both extension methods
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — updated with new public API entries
- `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs` — new file with ≥6 TUnit tests
