---
estimated_steps: 5
estimated_files: 6
---

# T02: .NET PolicySensitivityReport types and implementation

**Slice:** S02 — PolicySensitivityReport — fork diagnostic
**Milestone:** M004

## Description

Implement the .NET counterpart of the fork diagnostic. Define `PolicySensitivityReport`, `PolicySensitivityDiffEntry`, and `ItemStatus` types, implement the `PolicySensitivity` static extension method that calls `DryRun` per variant and computes the content-keyed diff, update PublicAPI, and create a test exercising ≥2 pipeline configurations.

## Steps

1. Create `src/Wollax.Cupel/Diagnostics/PolicySensitivityReport.cs`:
   - `public sealed record PolicySensitivityReport` with `Variants` (`IReadOnlyList<(string Label, SelectionReport Report)>`) and `Diffs` (`IReadOnlyList<PolicySensitivityDiffEntry>`) properties.
   - Use `required` keyword for both properties.
2. Create `src/Wollax.Cupel/Diagnostics/PolicySensitivityDiffEntry.cs`:
   - `public sealed record PolicySensitivityDiffEntry` with `Content` (`string`) and `Statuses` (`IReadOnlyList<(string Label, ItemStatus Status)>`) properties.
   - `public enum ItemStatus { Included, Excluded }` — define in the same file or a separate small file.
3. Create `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs`:
   - Static class `PolicySensitivityExtensions` with method `public static PolicySensitivityReport PolicySensitivity(IReadOnlyList<ContextItem> items, ContextBudget budget, params (string Label, CupelPipeline Pipeline)[] variants)`.
   - For each variant: call `variant.Pipeline.DryRunWithBudget(items, budget)` — but note `DryRunWithBudget` is `internal`. Use the public `DryRun` if budget override is not needed, or consider an approach that works with the public API. If the caller passes distinct pipeline instances (each with their own config), call `DryRun(items)` on each. **Decision needed:** The function takes a `budget` param for symmetry with Rust. Since .NET pipelines store their budget, the simplest approach is to have callers construct pipelines with the desired budget already set. The `budget` parameter may be used to override via `DryRunWithBudget` (internal) — make the extension method `internal` if using `DryRunWithBudget`, or omit the budget parameter and let callers configure pipelines directly. **Resolution:** Use `DryRunWithBudget(items, budget)` and make the extension method `public` — `DryRunWithBudget` is internal to the same assembly. This gives budget-override semantics matching Rust.
   - Compute diff: build `Dictionary<string, List<(string, ItemStatus)>>` keyed by `item.Content`. For each variant report, iterate `Included` (mark `Included`) and `Excluded` (mark `Excluded`). Filter to entries where statuses differ across variants.
   - Return `PolicySensitivityReport`.
4. Update `src/Wollax.Cupel/PublicAPI.Unshipped.txt` with all new public types and members.
5. Create `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs`:
   - Build 2 pipelines with different configurations (e.g., different budget or scorer) such that some items swap inclusion status.
   - Call `PolicySensitivityExtensions.PolicySensitivity(items, budget, variants)`.
   - Assert: `Variants.Count == 2`, both labeled correctly.
   - Assert: `Diffs` is non-empty — at least one item has different status across variants.
   - Assert: each diff entry has exactly 2 statuses with at least one `Included` and one `Excluded`.

## Must-Haves

- [ ] `PolicySensitivityReport` record exists with `Variants` and `Diffs` properties
- [ ] `PolicySensitivityDiffEntry` record exists with `Content` and `Statuses` properties
- [ ] `ItemStatus` enum with `Included` and `Excluded` values
- [ ] `PolicySensitivity` method calls `DryRunWithBudget` per variant and produces correct diff
- [ ] `PublicAPI.Unshipped.txt` updated with all new public API entries
- [ ] Test proves ≥2 variants with a meaningful diff (at least one item swaps status)
- [ ] `dotnet test --configuration Release` passes
- [ ] `dotnet build --configuration Release` — 0 errors, 0 warnings

## Verification

- `dotnet test --configuration Release` — all tests pass including new `PolicySensitivityTests`
- `dotnet build --configuration Release` — 0 errors, 0 warnings
- Test asserts specific diff entries exist with correct per-variant `ItemStatus`

## Observability Impact

- Signals added/changed: None — pure analytics function
- How a future agent inspects this: Read `PolicySensitivityReport.Diffs` — structured data showing exactly which items swing
- Failure state exposed: Exceptions propagated from `DryRunWithBudget` if any variant pipeline fails

## Inputs

- `src/Wollax.Cupel/CupelPipeline.cs` — `DryRunWithBudget` internal method for budget-override dry runs
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — report type consumed by diff computation
- T01 summary: Rust implementation as semantic reference (types, diff algorithm, test strategy)

## Expected Output

- `src/Wollax.Cupel/Diagnostics/PolicySensitivityReport.cs` — new report type
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityDiffEntry.cs` — new diff entry type + ItemStatus enum
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — new extension method
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — updated with new public API surface
- `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs` — new test file
