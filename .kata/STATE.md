# Kata State

**Active Milestone:** M002 — v1.3 Design Sprint
**Active Slice:** S05 — Cupel.Testing Vocabulary Design (next)
**Active Task:** (none — S04 complete; S05 not started)
**Phase:** Planning
**Slice Branch:** kata/root/M002/S04
**Active Workspace:** /Users/wollax/Git/personal/cupel
**Next Action:** Start S05 (Cupel.Testing Vocabulary Design; depends:S01 ✅)
**Last Updated:** 2026-03-21 (S04 complete — MetadataTrustScorer spec chapter written; R042 validated; 35 Rust + 583 .NET tests pass)
**Requirements Status:** R042, R041, R045, R040 validated; 3 active (R043–R045) · 14 validated (R001–R006, R010–R014, R040–R042, R045) · 3 deferred · 3 out of scope

## Completed Slices This Milestone (M002)

- [x] S01 — Post-v1.2 Brainstorm Sprint (2026-03-21): 9 brainstorm files committed; 4 explorer/challenger pairs; S03/S05/S06 downstream inputs written; 5 deferred items re-evaluated; 13 M003+ backlog candidates; 113 Rust + 583 .NET tests pass; R045 validated
- [x] S02 — Spec Editorial Debt (2026-03-21): 20 spec/phase24 issue files deleted; 13 spec files updated (ordering rules, normative alignment, algorithm clarifications, reserved variant JSON examples, pseudocode completion); TOML drift guard satisfied; 35 Rust + 583 .NET tests pass; R041 validated
- [x] S03 — Count-Based Quota Design (2026-03-21): `.planning/design/count-quota-design.md` written; all 6 DI rulings settled; COUNT-DISTRIBUTE-BUDGET pseudocode; 5 conformance vector outlines; 0 TBD fields; 35 Rust + 583 .NET tests pass; R040 validated
- [x] S04 — Metadata Convention System Spec (2026-03-21): `spec/src/scorers/metadata-trust.md` written; `"cupel:"` namespace reserved normatively; `cupel:trust`/`cupel:source-type` conventions defined; MetadataTrustScorer algorithm with configurable `defaultScore`; 5 conformance vector outlines; 0 TBD fields; SUMMARY.md + scorers.md updated; 35 Rust + 583 .NET tests pass; R042 validated

## M002 Slices

- [x] S01 — Post-v1.2 Brainstorm Sprint (complete)
- [x] S02 — Spec Editorial Debt (complete)
- [x] S03 — Count-Based Quota Design (complete)
- [x] S04 — Metadata Convention System Spec (complete)
- [ ] S05 — Cupel.Testing Vocabulary Design (depends:S01 ✅; risk:medium)
- [ ] S06 — Future Features Spec Chapters (depends:S01 ✅, S03 ✅; risk:medium)

## Milestone DoD Status (M002)

- [x] S01 complete with summary
- [x] S02 complete with summary
- [x] S03 complete with summary (count-quota design record, no TBD fields, R040 validated)
- [x] S04 complete with summary (MetadataTrustScorer spec chapter, R042 validated)
- [ ] S05 complete with summary (testing vocabulary ≥10 patterns)
- [ ] S06 complete with summary (DecayScorer, OTel, budget simulation spec chapters)
- [x] All R041 spec issues closed (removed from .planning/issues/open/) — only deferred checksum issue remains
- [x] All new spec chapters reachable via spec/src/SUMMARY.md — ✅ (S04: metadata-trust.md linked)
- [x] Brainstorm output committed to .planning/brainstorms/ — ✅ (S01)
- [x] cargo test passes (35 Rust passed)
- [x] dotnet test passes (583 .NET passed)

## S05 Starting Context

S05 depends on S01 ✅ (vocabulary candidates from brainstorm). Key deliverable: `spec/src/testing/vocabulary.md` with ≥10 named assertion patterns over `SelectionReport`.

Key inputs from S01 brainstorm:
- `.planning/brainstorms/2026-03-21-brainstorm/S05-testing-vocabulary-inputs.md` — vocabulary candidate list with 10+ patterns
- Patterns include: `IncludeItemWith`, `ExcludeItemWith`, `HaveTokenUtilizationAbove`, `HaveKindInIncluded`, `HaveAtLeastNExclusions`, `PlaceItemAtEdge`, `HaveKindDiversity`, `ExcludeWithReason`, `HaveBudgetUtilizationAbove`, `HaveNoExclusionsForKind`
- Each pattern must specify: what it asserts (precise), tolerance/edge cases, tie-breaking behavior, error message format on failure
- No snapshot assertions (ordering stability not yet guaranteed)
- No FluentAssertions dependency (D041)

## S04 Key Outputs (informing S06)

- `spec/src/scorers/metadata-trust.md` — `"cupel:"` namespace is reserved for Cupel's own conventions
- `cupel:source-type` open string convention (4 RECOMMENDED values: "user","tool","external","system")
- S06 OTel spec uses `cupel.*` attribute names — consistent with (not in conflict with) `cupel:` metadata namespace

## S03 Key Outputs (consumed by S06)

- `.planning/design/count-quota-design.md` — authoritative design record
- Section 5 (KnapsackSlice) + D052 guard message → S06 must reference when specifying `FindMinBudgetFor + CountQuotaSlice` incompatibility note
- COUNT-DISTRIBUTE-BUDGET pseudocode → M003 implementation starting point

## Recent Decisions (M002/S04)

- D058: S04 verification strategy — contract-level only (grep + test suite)
- D059: MetadataTrustScorer .NET type handling — accept double or string; string is canonical wire format
- D060: cupel:source-type is an open string convention, not a closed enum; MUST NOT reject unknown values

## Blockers

- (none)
