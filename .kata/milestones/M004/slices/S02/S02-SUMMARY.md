---
id: S02
parent: M004
milestone: M004
provides:
  - Rust policy_sensitivity free function in analytics module
  - Rust PolicySensitivityReport, PolicySensitivityDiffEntry, ItemStatus types
  - .NET PolicySensitivityReport, PolicySensitivityDiffEntry, ItemStatus types
  - .NET PolicySensitivity static extension method via PolicySensitivityExtensions
  - Content-keyed diff algorithm in both languages filtering to items with differing inclusion status
requires:
  - slice: S01
    provides: SelectionReport PartialEq (Rust) and IEquatable (. NET) for programmatic report comparison
affects:
  - S03
  - S04
key_files:
  - crates/cupel/src/analytics.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/policy_sensitivity.rs
  - src/Wollax.Cupel/Diagnostics/PolicySensitivityReport.cs
  - src/Wollax.Cupel/Diagnostics/PolicySensitivityDiffEntry.cs
  - src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs
  - tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs
key_decisions:
  - "D112: Integration-level verification with real pipeline dry_run calls"
  - "D113: Content-keyed matching across variants — ContextItem has no Id field"
  - "D114: .NET uses internal DryRunWithBudget for budget override semantics"
patterns_established:
  - "policy_sensitivity accepts &[(impl AsRef<str>, &Pipeline)] for ergonomic variant labeling in Rust"
  - "PolicySensitivity follows same extension-method-on-static-class pattern as BudgetSimulationExtensions in .NET"
observability_surfaces:
  - none — pure analytics function; errors propagate as CupelError / exceptions from underlying dry_run
drill_down_paths:
  - .kata/milestones/M004/slices/S02/tasks/T01-SUMMARY.md
  - .kata/milestones/M004/slices/S02/tasks/T02-SUMMARY.md
duration: 22min
verification_result: passed
completed_at: 2026-03-23T12:30:00Z
---

# S02: PolicySensitivityReport — fork diagnostic

**Fork diagnostic returning labeled SelectionReports plus content-keyed diff showing items that swap inclusion status across pipeline variants, implemented in both Rust and .NET**

## What Happened

Implemented the `policy_sensitivity` / `PolicySensitivity` fork diagnostic in both languages. The function accepts a list of context items, a budget, and multiple labeled pipeline variants, runs `dry_run` on each variant, then computes a content-keyed diff identifying items whose inclusion status differs across at least two variants.

**T01 (Rust):** Added `ItemStatus` enum, `PolicySensitivityDiffEntry`, and `PolicySensitivityReport` types to `analytics.rs`. The `policy_sensitivity` function accepts `&[(impl AsRef<str>, &Pipeline)]` for ergonomic labeling. Diff algorithm uses `HashMap<String, Vec<(String, ItemStatus)>>` keyed by item content, filtering to entries where statuses disagree. Integration test exercises two variants with different scorers (ReflexiveScorer vs PriorityScorer) forcing items to swap — 145 Rust tests pass.

**T02 (.NET):** Created matching types in `Diagnostics/` namespace — `PolicySensitivityReport` (sealed record), `PolicySensitivityDiffEntry` (sealed record), `ItemStatus` enum. Static `PolicySensitivity` extension method calls `DryRunWithBudget` per variant (internal to same assembly). Added minimum-variants guard (throws `ArgumentException` for < 2 variants). Three tests including a meaningful-diff test using an inverted scorer — 767 .NET tests pass.

## Verification

- `cargo test --all-targets`: 145 passed, 0 failed (80 unit + 48 conformance + 15 equality + 2 policy_sensitivity)
- `cargo clippy --all-targets -- -D warnings`: clean
- `dotnet test --configuration Release`: 767 passed, 0 failed
- `dotnet build --configuration Release`: 0 errors, 0 warnings
- Both languages: tests exercise ≥2 pipeline configurations with items that swap between included/excluded in the diff

## Requirements Advanced

- R051 — PolicySensitivityReport implemented and tested in both languages; fork diagnostic returns labeled reports + structured diff for ≥2 variants

## Requirements Validated

- R051 — Both languages have `policy_sensitivity` / `PolicySensitivity` returning `PolicySensitivityReport` with labeled variants and content-keyed diff; integration tests prove items swap status across variants; all test suites pass

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

- .NET added a minimum-variants guard (throws `ArgumentException` for < 2 variants) — not in original plan but a natural safety check

## Known Limitations

- Diff uses content string as item identity — items with identical content but different metadata are treated as the same item across variants
- No performance optimization for large variant counts — each variant runs a full `dry_run`

## Follow-ups

- none

## Files Created/Modified

- `crates/cupel/src/analytics.rs` — Added ItemStatus, PolicySensitivityDiffEntry, PolicySensitivityReport, policy_sensitivity function
- `crates/cupel/src/lib.rs` — Re-exported new types and function
- `crates/cupel/tests/policy_sensitivity.rs` — Integration test with 2 test cases
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityReport.cs` — Sealed record with Variants and Diffs
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityDiffEntry.cs` — Sealed record + ItemStatus enum
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — Static PolicySensitivity method
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 15 new public API entries
- `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs` — 3 tests exercising fork diagnostic

## Forward Intelligence

### What the next slice should know
- `analytics.rs` now has both budget simulation functions (from M003/S06) and the policy sensitivity function — it's the home for all report-level analytics in Rust
- .NET `PolicySensitivityExtensions` uses `DryRunWithBudget` (internal) — same pattern available for any future analytics that need budget override

### What's fragile
- Content-keyed identity for diff entries — if a future feature adds item IDs, the diff should migrate to ID-based matching

### Authoritative diagnostics
- `PolicySensitivityReport.Diffs` / `.diffs` is the structured inspection surface — each entry shows the item content and its per-variant inclusion status

### What assumptions changed
- none — implementation matched the plan closely
