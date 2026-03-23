# Kata State

**Active Milestone:** M003 — v1.3 Implementation Sprint
**Active Slice:** S06 — Budget simulation + tiebreaker + spec alignment
**Active Task:** T01 — Write failing-first verification for budget simulation and deterministic ties
**Phase:** S06 planned; ready to execute
**Slice Branch:** kata/root/M003/S06
**Active Workspace:** /Users/wollax/Git/personal/cupel
**Next Action:** Execute S06/T01 — add `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs`, extend .NET/Rust GreedySlice tie-break tests, and run the focused failing-first verification commands
**Last Updated:** 2026-03-23 (S06 planned; S05 complete; M003 final implementation slice queued)

## M003 Overview

6 slices, all implementation against locked M002 spec chapters:

| Slice | Feature | Risk | Status |
|-------|---------|------|--------|
| S01 | DecayScorer (Rust + .NET) | high | ✅ complete |
| S02 | MetadataTrustScorer (Rust + .NET) | medium | ✅ complete |
| S03 | CountQuotaSlice (Rust + .NET) | high | ✅ complete |
| S04 | Core analytics + Cupel.Testing package | medium | ✅ complete |
| S05 | OTel bridge companion package | high | ✅ complete |
| S06 | Budget simulation + tiebreaker + spec alignment | low | 🟡 planned |

## Recent Planning Decisions

- D097: S06 proof is integration-level — failing-first .NET budget-simulation tests, explicit .NET/Rust GreedySlice tie regressions, full-suite regression checks, and grep-based spec alignment checks
- D098: Both public `.NET` budget-simulation APIs take explicit `ContextBudget budget` and reuse an internal `CupelPipeline` temporary-budget execution seam
- D099: GreedySlice tie-breaking is the existing stable original-order / original-index contract; no new `ContextItem.Id` surface is added in M003
- D095: Local NuGet consumption tests restore from `./packages`, not `./nupkg`
- D089: Rust analytics live as free functions in `crates/cupel/src/analytics.rs`

## S06 Planned Deliverables

- `.NET` public APIs: `GetMarginalItems(items, budget, slackTokens)` and `FindMinBudgetFor(items, budget, targetItem, searchCeiling)` on `CupelPipeline`
- Internal budget-override seam in `src/Wollax.Cupel/CupelPipeline.cs` so simulation reuses the real execution core
- Deterministic GreedySlice tie-break tests in both .NET and Rust, plus spec wording aligned to original-index ordering
- Spec/index/changelog alignment: `spec/src/SUMMARY.md`, `spec/src/slicers.md`, `spec/src/scorers.md`, `spec/src/changelog.md`, and new `spec/src/slicers/count-quota.md`
- Rust budget-simulation parity decision documented explicitly as deferred for v1.3

## Inputs Available from Prior Slices

- S04 provides the analytics extension pattern and the PublicAPI/new-package workflow
- S05 proves the second standalone NuGet package pattern and leaves M003 with only S06 feature/documentation gaps remaining
- `CupelPipeline.DryRun()` and GreedySlice implementations already exist in both languages; S06 adds budget override + tests rather than inventing a new pipeline path

## Blockers

- None
