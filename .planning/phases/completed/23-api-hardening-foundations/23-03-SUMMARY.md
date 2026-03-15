# Phase 23 Plan 03: Computed Budget Properties Summary

**One-liner:** Added `total_reserved`, `unreserved_capacity`, and `has_capacity` as first-class computed properties to `ContextBudget` in both Rust and .NET, exposing the "how much budget remains" formula as an API to prevent callers from reimplementing it.

## Tasks

| # | Name | Status | Commit |
|---|------|--------|--------|
| 1 | Add computed properties to Rust ContextBudget | Done | `8220a30` |
| 2 | Add computed properties to .NET ContextBudget | Done | `429dbb6` |

## Verification

- `cargo test`: 61 passed (all features), 94 passed (serde features)
- `cargo clippy --all-features`: No issues
- `dotnet test`: 641 passed
- `dotnet build`: No warnings
- All six methods/properties confirmed present in respective files

## Deviations

One deviation from plan: The .NET project uses the Roslyn `PublicApiAnalyzers` (`RS0016`) package, which enforces that all public API symbols are declared in `PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt`. The plan did not mention this file. The three new public properties were added to `PublicAPI.Unshipped.txt` to satisfy the analyzer. This is consistent with the project's existing workflow for introducing new public API.

## Artifacts

- `crates/cupel/src/model/context_budget.rs` — `total_reserved()`, `unreserved_capacity()`, `has_capacity()` with `#[must_use]`
- `src/Wollax.Cupel/ContextBudget.cs` — `TotalReserved`, `UnreservedCapacity`, `HasCapacity` with `[JsonIgnore]`
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — three new symbols declared
