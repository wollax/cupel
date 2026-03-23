---
id: T02
parent: S06
milestone: M003
provides:
  - Internal `DryRunWithBudget` seam on CupelPipeline for temporary budget override
  - `GetMarginalItems` public extension method with reference-equality diff semantics
  - `FindMinBudgetFor` public extension method with binary search and low-bound verification
  - QuotaSlice and CountQuotaSlice monotonicity guards with exact spec messages
  - All 11 BudgetSimulationTests green against real implementation
key_files:
  - src/Wollax.Cupel/CupelPipeline.cs
  - src/Wollax.Cupel/BudgetSimulationExtensions.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
key_decisions:
  - "Budget override seam: added `DryRunWithBudget(items, temporaryBudget)` as internal method, with `ExecuteCore` accepting optional `budgetOverride` parameter. All `_budget` references in `ExecuteCore` replaced with local `budget` variable that defaults to `_budget` when no override supplied."
  - "Binary search final verification: added check at `low` before `high` to handle edge case where the target item fits exactly at the lower bound (targetItem.Tokens). The standard `while (high - low > 1)` loop never tests at `low`, so the single-item case returned low+1 instead of low."
patterns_established:
  - "Budget-override seam: `DryRunWithBudget` is the internal primitive for simulation; extension methods never mutate pipeline state"
  - "Reference-equality comparison via `ReferenceEqualityComparer.Instance` and `ReferenceEquals` for item identity in diff operations"
observability_surfaces:
  - BudgetSimulationTests.cs covers happy-path, guard-message, and edge-case behaviors for both APIs
  - Stable exception messages for QuotaSlice/CountQuotaSlice monotonicity violations
duration: 12min
verification_result: passed
completed_at: 2026-03-23T12:00:00Z
blocker_discovered: false
---

# T02: Implement the .NET Budget-Override Seam and Public Simulation APIs

**Added `DryRunWithBudget` internal seam and implemented `GetMarginalItems`/`FindMinBudgetFor` extension methods with reference-equality diffs, binary search, and monotonicity guards**

## What Happened

Refactored `CupelPipeline.ExecuteCore` to accept an optional `ContextBudget? budgetOverride` parameter. All `_budget` field references within `ExecuteCore` now use a local `budget` variable that defaults to `_budget` when no override is provided. Added `DryRunWithBudget(items, temporaryBudget)` as an internal method that delegates to `ExecuteCore` with the override.

Replaced the `NotImplementedException` stubs in `BudgetSimulationExtensions.cs` with full implementations:

- `GetMarginalItems`: runs two `DryRunWithBudget` calls (full budget and reduced budget), diffs included items by reference equality using `ReferenceEqualityComparer.Instance`, guards against `QuotaSlice` with the exact spec message.
- `FindMinBudgetFor`: validates preconditions (targetItem ∈ items by reference, searchCeiling >= targetItem.Tokens), binary searches over `[targetItem.Tokens, searchCeiling]` using real dry runs, verifies at `low` then `high` after loop termination, guards against `QuotaSlice` and `CountQuotaSlice`.

The binary search required a deviation: added verification at `low` before `high` because the standard `while (high - low > 1)` loop never tests at the exact lower bound, causing off-by-one for items that fit at exactly `targetItem.Tokens`.

## Verification

- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj -- --treenode-filter "/*/*/BudgetSimulationTests/*"` → 11/11 passed
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → 622/622 passed (full suite, no regressions)
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` → 0 errors, 0 warnings
- `cargo test --all-targets` (in crates/cupel) → 128/128 passed
- grep confirms both APIs in PublicAPI.Unshipped.txt and implementation file

## Diagnostics

- Run `BudgetSimulationTests.cs` to verify API behavior; test names map directly to contracts
- Inspect `BudgetSimulationExtensions.cs` for guard messages and algorithm
- Inspect `CupelPipeline.cs` `DryRunWithBudget` for the budget-override seam

## Deviations

- Added `low`-bound verification step in `FindMinBudgetFor` beyond what the spec pseudocode shows. The spec's pseudocode only checks at `high`, but the binary search loop (`while high - low > 1`) never evaluates at `low`, causing the single-item edge case to return `low+1` instead of `low`. This is a correctness fix aligned with the spec's intent (find the *minimum* budget).

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/CupelPipeline.cs` — Added `DryRunWithBudget` internal method; refactored `ExecuteCore` to accept optional budget override
- `src/Wollax.Cupel/BudgetSimulationExtensions.cs` — Full implementation replacing NotImplementedException stubs
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Already contained correct entries from T01 (no changes needed)
