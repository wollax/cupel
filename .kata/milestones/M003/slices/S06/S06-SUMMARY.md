---
id: S06
parent: M003
milestone: M003
provides:
  - GetMarginalItems public extension method on CupelPipeline with reference-equality diff semantics
  - FindMinBudgetFor public extension method on CupelPipeline with binary search and int? return
  - Internal DryRunWithBudget budget-override seam reusing ExecuteCore
  - QuotaSlice and CountQuotaSlice monotonicity guards with exact spec-defined messages
  - Deterministic tie-break contract (original-index ascending) documented across .NET, Rust, and spec
  - CountQuotaSlice full spec page with decorator shape, scarcity, Knapsack guard, shortfall surface
  - SUMMARY.md, slicers.md, scorers.md navigation/index coverage for all M003 features
  - v1.3.0 changelog entry with all M003 additions and spec decisions
  - Budget-simulation Rust-parity deferral rationale in spec
requires:
  - slice: S04
    provides: SelectionReport analytics extension methods (BudgetUtilization, KindDiversity, TimestampCoverage)
  - slice: S01-S03
    provides: Existing Pipeline, DryRun, GreedySlice, QuotaSlice, CountQuotaSlice implementations
affects: []
key_files:
  - src/Wollax.Cupel/BudgetSimulationExtensions.cs
  - src/Wollax.Cupel/CupelPipeline.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs
  - tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs
  - crates/cupel/src/slicer/greedy.rs
  - spec/src/slicers/greedy.md
  - spec/src/analytics/budget-simulation.md
  - spec/src/slicers/count-quota.md
  - spec/src/SUMMARY.md
  - spec/src/slicers.md
  - spec/src/scorers.md
  - spec/src/changelog.md
key_decisions:
  - "D097: S06 verification uses failing-first tests, explicit tie-break regressions, and grep-based spec checks"
  - "D098: Budget simulation APIs take explicit ContextBudget parameter and reuse internal CupelPipeline seam"
  - "D099: GreedySlice tie-break uses stable original-index ascending; no ContextItem.Id introduced"
patterns_established:
  - "Budget-override seam: DryRunWithBudget is the internal primitive for simulation; extension methods never mutate pipeline state"
  - "Reference-equality comparison via ReferenceEqualityComparer.Instance for item identity in diff operations"
  - "Deterministic tie-break contract language: 'original-index ascending' used consistently across .NET, Rust, and spec"
observability_surfaces:
  - BudgetSimulationTests.cs covers happy-path, guard-message, and edge-case behaviors for both APIs
  - GreedySliceTests.cs has 4 explicit tie-break regression tests in .NET; greedy.rs has 4 matching Rust tests
  - Stable exception messages for QuotaSlice/CountQuotaSlice monotonicity violations
  - Spec/doc alignment mechanically verifiable via grep across 7 spec files
drill_down_paths:
  - .kata/milestones/M003/slices/S06/tasks/T01-SUMMARY.md
  - .kata/milestones/M003/slices/S06/tasks/T02-SUMMARY.md
  - .kata/milestones/M003/slices/S06/tasks/T03-SUMMARY.md
  - .kata/milestones/M003/slices/S06/tasks/T04-SUMMARY.md
duration: 40min
verification_result: passed
completed_at: 2026-03-23T12:30:00Z
---

# S06: Budget simulation + tiebreaker + spec alignment

**Shipped .NET budget-simulation API (GetMarginalItems + FindMinBudgetFor) with internal DryRunWithBudget seam, locked deterministic tie-break contract across both languages and spec, and completed M003 spec navigation/changelog alignment including CountQuotaSlice spec page**

## What Happened

Built the slice test-first: T01 created 11 failing budget-simulation tests with NotImplementedException stubs plus 8 passing tie-break regression tests across .NET (4) and Rust (4). The existing GreedySlice implementations already preserved input order via stable sort with index tiebreak, so tie-break tests passed immediately.

T02 implemented the real budget-simulation API by adding a `DryRunWithBudget(items, temporaryBudget)` internal method on `CupelPipeline` — `ExecuteCore` now accepts an optional `budgetOverride` parameter while preserving all existing `Execute`/`DryRun` behavior. `GetMarginalItems` diffs two dry runs by reference equality; `FindMinBudgetFor` binary-searches over `[targetItem.Tokens, searchCeiling]` with a low-bound verification step that the spec pseudocode omits (needed for items that fit at exactly the lower bound). All 11 tests went green.

T03 locked the tie-break contract in documentation: enhanced doc comments in both .NET and Rust GreedySlice with explicit "original-index ascending" language, replaced the vague "Sort Stability" spec section with a concrete "Deterministic Tie-Break Contract" section, and added a cross-reference from the budget-simulation determinism invariant.

T04 completed M003 spec alignment: wrote the full CountQuotaSlice spec page, updated SUMMARY.md/slicers.md/scorers.md navigation and index tables, wrote the v1.3.0 changelog, and expanded the budget-simulation Rust-parity deferral rationale.

## Verification

- `dotnet test` — 723/723 passed (full .NET suite, no regressions)
- `cargo test --all-targets` — 128/128 passed (full Rust suite)
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` — 0 errors, 0 warnings
- BudgetSimulationTests: 11/11 passed (happy-path, guards, edge cases)
- GreedySliceTests: 14/14 passed (including 4 deterministic tie-break regressions)
- PublicAPI grep: both GetMarginalItems and FindMinBudgetFor present in Unshipped.txt and implementation
- Spec grep: 34 matches across 7 spec files — all M003 features reachable from nav/index/changelog

## Requirements Advanced

- None — S06 closes milestone-level acceptance gaps rather than advancing active requirements

## Requirements Validated

- None directly — S06 does not own any requirement; it implements the budget simulation API designed in R044 and locks the tie-break contract from the roadmap

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

- T01 added `BudgetSimulationExtensions.cs` stub file and `PublicAPI.Unshipped.txt` entries not in the original plan — required for tests to compile
- T02 added low-bound verification step in `FindMinBudgetFor` beyond what the spec pseudocode shows — the binary search loop never evaluates at the exact lower bound, causing off-by-one for items fitting at exactly `targetItem.Tokens`

## Known Limitations

- Budget simulation is .NET only — Rust parity deferred (Rust Pipeline lacks public DryRun equivalent); deferral rationale documented in `spec/src/analytics/budget-simulation.md`
- `FindMinBudgetFor` monotonicity guard rejects both QuotaSlice and CountQuotaSlice — no workaround for callers using these slicers

## Follow-ups

- None — this is the final slice of M003

## Files Created/Modified

- `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs` — 11 new budget-simulation tests
- `src/Wollax.Cupel/BudgetSimulationExtensions.cs` — Full implementation of GetMarginalItems + FindMinBudgetFor
- `src/Wollax.Cupel/CupelPipeline.cs` — Internal DryRunWithBudget seam; ExecuteCore budget override
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Budget simulation API entries
- `tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs` — 4 deterministic tie-break regression tests
- `crates/cupel/src/slicer/greedy.rs` — 4 Rust tie-break regression tests + enhanced doc comment
- `src/Wollax.Cupel/GreedySlice.cs` — Enhanced XML doc with tie-break contract
- `spec/src/slicers/greedy.md` — Deterministic Tie-Break Contract section
- `spec/src/analytics/budget-simulation.md` — DryRun determinism cross-reference + Rust parity note
- `spec/src/slicers/count-quota.md` — New CountQuotaSlice spec page
- `spec/src/SUMMARY.md` — CountQuotaSlice navigation link
- `spec/src/slicers.md` — CountQuotaSlice table entry
- `spec/src/scorers.md` — DecayScorer in summary table + Absolute Scorers category
- `spec/src/changelog.md` — v1.3.0 changelog entry

## Forward Intelligence

### What the next slice should know
- M003 is complete — all 6 slices done. The next work is milestone summary and v1.3.0 release tagging.

### What's fragile
- `FindMinBudgetFor` binary search has a non-obvious low-bound verification step — if the spec pseudocode is ever implemented literally without it, single-item edge cases will be off-by-one

### Authoritative diagnostics
- `BudgetSimulationTests.cs` is the single source of truth for budget-simulation contract behavior — test names map directly to API contracts
- Grep across 7 spec files is the authoritative check for documentation completeness

### What assumptions changed
- The roadmap's "id ascending" tiebreak wording was resolved to "original-index ascending" — no ContextItem.Id field was needed because both implementations already used stable sort with insertion index
