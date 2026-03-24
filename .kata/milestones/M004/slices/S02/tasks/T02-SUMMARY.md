---
id: T02
parent: S02
milestone: M004
provides:
  - PolicySensitivityReport record with Variants and Diffs properties
  - PolicySensitivityDiffEntry record with Content and Statuses properties
  - ItemStatus enum (Included, Excluded)
  - PolicySensitivityExtensions.PolicySensitivity static method using DryRunWithBudget
  - Test exercising 2 variants with different scorers proving items swap status
key_files:
  - src/Wollax.Cupel/Diagnostics/PolicySensitivityReport.cs
  - src/Wollax.Cupel/Diagnostics/PolicySensitivityDiffEntry.cs
  - src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs
  - tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs
key_decisions:
  - "Extension method is public and uses internal DryRunWithBudget for budget-override semantics matching Rust, since it lives in the same assembly"
  - "Content-keyed diff uses Dictionary<string, List<(string, ItemStatus)>> to join across variants, filtering to entries where statuses disagree — mirrors Rust HashMap approach"
patterns_established:
  - "PolicySensitivity follows same extension-method-on-static-class pattern as BudgetSimulationExtensions"
observability_surfaces:
  - none — pure analytics function, exceptions propagate from DryRunWithBudget
duration: 12min
verification_result: passed
completed_at: 2026-03-23T12:00:00Z
blocker_discovered: false
---

# T02: .NET PolicySensitivityReport types and implementation

**Added `PolicySensitivityReport`, `PolicySensitivityDiffEntry`, `ItemStatus`, and `PolicySensitivity` extension method with content-keyed diff logic mirroring the Rust implementation**

## What Happened

Implemented the .NET counterpart of the fork diagnostic. Created three source files: `PolicySensitivityReport.cs` (sealed record with `Variants` and `Diffs`), `PolicySensitivityDiffEntry.cs` (sealed record with `Content` and `Statuses`, plus `ItemStatus` enum), and `PolicySensitivityExtensions.cs` (static `PolicySensitivity` method). The extension method calls `DryRunWithBudget` per variant, builds a `Dictionary<string, List<(string, ItemStatus)>>` keyed by item content, and filters to entries where at least one status is Included and at least one is Excluded. Updated `PublicAPI.Unshipped.txt` with all new public API surface.

Created `PolicySensitivityTests.cs` with three tests: a basic two-variant test, a meaningful-diff test using an inverted scorer to force items to swap status, and a guard test for fewer than two variants.

## Verification

- `dotnet build --configuration Release` — 0 errors, 0 warnings
- `dotnet test --configuration Release` — 767 tests passed (all existing + 3 new PolicySensitivity tests)
- Test `TwoVariants_DifferentScorers_ItemsSwapStatus` asserts: 2 variants labeled correctly, diffs non-empty, each diff has exactly 2 statuses with both Included and Excluded present, specific items (alpha, delta) verified by content with correct per-variant status

## Diagnostics

None — pure analytics function. Read `PolicySensitivityReport.Diffs` for structured data. Exceptions from `DryRunWithBudget` propagate unmodified.

## Deviations

- Added a minimum-variants guard (throws `ArgumentException` for < 2 variants) — not in the original plan but a natural safety check matching Rust's approach
- First test (`TwoVariants_DifferentBudgets`) was simplified to a basic variant-count check since both pipelines use the same scorer and DryRunWithBudget overrides the budget, making the second test the real diff exerciser

## Known Issues

None

## Files Created/Modified

- `src/Wollax.Cupel/Diagnostics/PolicySensitivityReport.cs` — new sealed record with Variants and Diffs
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityDiffEntry.cs` — new sealed record + ItemStatus enum
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — static PolicySensitivity method with content-keyed diff
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — updated with 15 new public API entries
- `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs` — 3 tests exercising fork diagnostic
