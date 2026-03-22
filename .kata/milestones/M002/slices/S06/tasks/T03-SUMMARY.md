---
id: T03
parent: S06
milestone: M002
provides:
  - spec/src/analytics/budget-simulation.md — fully-specified budget simulation chapter
  - spec/src/SUMMARY.md — Analytics section added with budget-simulation entry
key_files:
  - spec/src/analytics/budget-simulation.md
  - spec/src/SUMMARY.md
key_decisions:
  - none (D044, D048 honored as specified; no new decisions)
patterns_established:
  - none
observability_surfaces:
  - none (spec authoring task; no code changes)
duration: short
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T03: Write Budget Simulation Spec Chapter + Full Verification

**Created `spec/src/analytics/budget-simulation.md` — a fully-specified budget simulation chapter with DryRun determinism invariant (normative MUST), GetMarginalItems and FindMinBudgetFor contracts with pseudocode, both slicer guards (QuotaSlice / CountQuotaSlice), Rust-parity-deferred note, SweepBudget out-of-scope note, and zero TBD fields; Analytics section added to SUMMARY.md; all three S06 chapters pass completeness checks and both test suites pass with no regressions.**

## What Happened

Read reference files first: confirmed `DryRun(IReadOnlyList<ContextItem> items)` signature in `CupelPipeline.cs:86` uses the pipeline's stored `_budget` (confirming why an explicit `ContextBudget budget` parameter is required in extension methods); confirmed `included` and `total_candidates` field names from `spec/src/diagnostics/selection-report.md`; noted the KnapsackSlice guard message pattern from `.planning/design/count-quota-design.md` Section 5 (public API names only per D032) as the model for the QuotaSlice guard wording.

Created `spec/src/analytics/` directory and wrote `spec/src/analytics/budget-simulation.md` covering:
- Overview: extension methods on `CupelPipeline`, scoped to .NET v1.3, with explicit Rust-parity-deferred (M003+) and SweepBudget-out-of-scope (Smelt) notes
- DryRun Determinism Invariant: normative MUST text; stable tie-breaking required; hash-map iteration order explicitly called out as non-conformant
- `GetMarginalItems`: explicit `ContextBudget budget` parameter; reduced-budget formula (`MaxTokens - slackTokens`); `primary \ margin` diff direction; monotonicity assumption; QuotaSlice guard with exact `InvalidOperationException` message; GET-MARGINAL-ITEMS pseudocode in `text` fenced block
- `FindMinBudgetFor`: `int?` return type; two `ArgumentException` preconditions; binary search with `high - low <= 1` stop condition; log₂ complexity note (10–15 DryRun calls); final verification step at `high`; QuotaSlice + CountQuotaSlice guard naming both types with exact message; FIND-MIN-BUDGET-FOR pseudocode in `text` fenced block
- Conformance Notes referencing SelectionReport `included` field

Updated `spec/src/SUMMARY.md`: added `# Analytics` section after `# Integrations` with `- [Budget Simulation](analytics/budget-simulation.md)` entry.

## Verification

All T03 and S06 slice verification checks passed:

**TBD counts (all 0):**
- `grep -ci "\bTBD\b" spec/src/analytics/budget-simulation.md` → 0
- `grep -ci "\bTBD\b" spec/src/scorers/decay.md` → 0 (regression check)
- `grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md` → 0 (regression check)

**Required strings (all PASS):** GetMarginalItems, FindMinBudgetFor, QuotaSlice, CountQuotaSlice, MUST, monoton, Rust, SweepBudget, Analytics, budget-simulation, decay, opentelemetry, Integrations — all present in expected files.

**Test suites:**
- `cargo test --manifest-path crates/cupel/Cargo.toml` → 113 passed, 1 ignored (no regressions)
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → 583 passed, 0 failed (no regressions)

## Diagnostics

Spec-only task; no runtime signals. Future agents: `grep -ci "\bTBD\b" spec/src/analytics/budget-simulation.md` → 0; `grep -q "GetMarginalItems requires\|FindMinBudgetFor requires" spec/src/analytics/budget-simulation.md` to verify guard message wording matches spec.

## Deviations

none

## Known Issues

none

## Files Created/Modified

- `spec/src/analytics/budget-simulation.md` — new file; fully-specified budget simulation chapter
- `spec/src/SUMMARY.md` — Analytics section added with budget-simulation entry
