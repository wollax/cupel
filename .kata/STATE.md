# Kata State

**Active Milestone:** M002 — v1.3 Design Sprint
**Active Slice:** S06 — Future Features Spec Chapters
**Active Task:** —
**Phase:** Planning (S05 complete; S06 not yet started)
**Slice Branch:** kata/root/M002/S05 (to be merged; S06 branch TBD)
**Active Workspace:** /Users/wollax/Git/personal/cupel
**Next Action:** Start S06 (DecayScorer, OTel, budget simulation spec chapters)
**Last Updated:** 2026-03-21 (S05 complete — 13 patterns in vocabulary.md; 0 TBD; 15 error message formats; both test suites green; R043 validated)
**Requirements Status:** R044, R045 active; 13 validated (R001–R006, R010–R014, R040–R043) · 3 deferred · 3 out of scope

## Completed Slices This Milestone (M002)

- [x] S01 — Post-v1.2 Brainstorm Sprint (2026-03-21): 9 brainstorm files committed; 4 explorer/challenger pairs; S03/S05/S06 downstream inputs written; 5 deferred items re-evaluated; 13 M003+ backlog candidates; 35 Rust + 583 .NET tests pass; R045 validated
- [x] S02 — Spec Editorial Debt (2026-03-21): 20 spec/phase24 issue files deleted; 13 spec files updated (ordering rules, normative alignment, algorithm clarifications, reserved variant JSON examples, pseudocode completion); TOML drift guard satisfied; 35 Rust + 583 .NET tests pass; R041 validated
- [x] S03 — Count-Based Quota Design (2026-03-21): `.planning/design/count-quota-design.md` written; all 6 DI rulings settled; COUNT-DISTRIBUTE-BUDGET pseudocode; 5 conformance vector outlines; 0 TBD fields; 35 Rust + 583 .NET tests pass; R040 validated
- [x] S04 — Metadata Convention System Spec (2026-03-21): `spec/src/scorers/metadata-trust.md` written; `"cupel:"` namespace reserved normatively; `cupel:trust`/`cupel:source-type` conventions defined; MetadataTrustScorer algorithm with configurable `defaultScore`; 5 conformance vector outlines; 0 TBD fields; SUMMARY.md + scorers.md updated; 35 Rust + 583 .NET tests pass; R042 validated
- [x] S05 — Cupel.Testing Vocabulary Design (2026-03-21): `spec/src/testing/vocabulary.md` written with 13 fully-specified named assertion patterns over SelectionReport; PD-1 through PD-4 locked; Placer dependency caveat on ordering assertions; ExcludeItemWithBudgetDetails .NET language-asymmetry note; D041 snapshot prohibition noted; 0 TBD fields; 15 error message formats; SUMMARY.md updated with Testing section; 35 Rust + 583 .NET tests pass; R043 validated

## M002 Slices

- [x] S01 — Post-v1.2 Brainstorm Sprint (complete)
- [x] S02 — Spec Editorial Debt (complete)
- [x] S03 — Count-Based Quota Design (complete)
- [x] S04 — Metadata Convention System Spec (complete)
- [x] S05 — Cupel.Testing Vocabulary Design (complete)
- [ ] S06 — Future Features Spec Chapters (depends:S01 ✅, S03 ✅; risk:medium)

## Milestone DoD Status (M002)

- [x] S01 complete with summary
- [x] S02 complete with summary
- [x] S03 complete with summary (count-quota design record, no TBD fields, R040 validated)
- [x] S04 complete with summary (MetadataTrustScorer spec chapter, R042 validated)
- [x] S05 complete with summary (13 patterns, 0 TBD, both test suites green, R043 validated)
- [ ] S06 complete with summary (DecayScorer, OTel, budget simulation spec chapters)
- [x] All R041 spec issues closed (removed from .planning/issues/open/) — only deferred checksum issue remains
- [x] All new spec chapters reachable via spec/src/SUMMARY.md — ✅ (S04: metadata-trust.md; S05: testing/vocabulary.md)
- [x] Brainstorm output committed to .planning/brainstorms/ — ✅ (S01)
- [x] cargo test passes (35 Rust passed)
- [x] dotnet test passes (583 .NET passed)

## S06 Starting Context

S06 depends on S01 ✅ and S03 ✅. Produces three spec chapters:

1. **`spec/src/scorers/decay.md`** — DecayScorer: algorithm, TimeProvider injection (mandatory), three curve factories (Exponential, Step, Window), null-timestamp policy, 5 conformance vector outlines, Rust TimeProvider trait note
2. **`spec/src/integrations/opentelemetry.md`** — OTel verbosity: StageOnly / StageAndExclusions / Full tiers; exact `cupel.*` attribute names per tier; pre-stability disclaimer; cardinality warning
3. **`spec/src/analytics/budget-simulation.md`** — Budget simulation: `GetMarginalItems` (single DryRun diff); `FindMinBudgetFor` (binary search ~10-15 invocations, monotonicity precondition); QuotaSlice incompatibility guard

Key S03 output to consume:
- `.planning/design/count-quota-design.md` section 5 (KnapsackSlice guard via D052) → reference in `FindMinBudgetFor + CountQuotaSlice` incompatibility note

Key S01 outputs to consume:
- `.planning/brainstorms/2026-03-21-brainstorm/decay-scorer-design.md` — fresh angles on DecayScorer curves
- `.planning/brainstorms/2026-03-21-brainstorm/otel-verbosity-design.md` — fresh angles on OTel attribute names and tiers

## S05 Key Outputs (for reference)

- `spec/src/testing/vocabulary.md` — 13 fully-specified assertion patterns; PD-1/PD-2/PD-3/PD-4 locked; language-asymmetry note on ExcludeItemWithBudgetDetails
- `spec/src/SUMMARY.md` updated with `# Testing` section
- D065: predicate type = IncludedItem/ExcludedItem (not raw ContextItem)
- D066: SelectionReport.Should() entry point; SelectionReportAssertionException as dedicated type

## Recent Decisions (M002/S05)

- D061: S05 verification strategy — contract-level only (grep + test suite)
- D062: HaveBudgetUtilizationAbove denominator = budget.MaxTokens (not TargetTokens)
- D063: S05 targets 13 patterns (10 baseline + 3 additions)
- D064: floating-point threshold comparisons use exact >= / <= with no epsilon
- D065: predicate type is IncludedItem/ExcludedItem, not raw ContextItem
- D066: Should() entry point; SelectionReportAssertionChain; SelectionReportAssertionException dedicated type

## Blockers

- (none)
