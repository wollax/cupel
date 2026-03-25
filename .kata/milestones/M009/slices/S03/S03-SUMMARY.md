---
id: S03
parent: M009
milestone: M009
provides:
  - spec/src/slicers/count-constrained-knapsack.md — complete 10-section spec chapter for CountConstrainedKnapsackSlice; D180 re-sort, D181 Phase 3 seeding, D174 trade-off documented; zero TBD fields
  - spec/src/scorers/metadata-key.md — complete 10-section spec chapter for MetadataKeyScorer; cupel:priority convention; multiplicative semantics; construction validation (boost > 0.0); zero TBD fields; serves as S04 implementation contract
  - spec/src/SUMMARY.md — CountConstrainedKnapsackSlice and MetadataKeyScorer links added under their respective sections
  - spec/src/slicers.md — CountConstrainedKnapsackSlice row added to Slicer Summary table
  - spec/src/scorers.md — MetadataKeyScorer row and Absolute Scorers list entry added
  - CHANGELOG.md — .NET CountConstrainedKnapsackSlice entry added to unreleased section (deferred from S02)
requires:
  - slice: S01
    provides: Rust CountConstrainedKnapsackSlice implementation and 5 conformance TOML files as ground truth for spec algorithm and conformance vector outlines
  - slice: S02
    provides: .NET CountConstrainedKnapsackSlice implementation validating Phase 1/2/3 cross-language parity; exact .NET ArgumentException error messages
affects:
  - S04
key_files:
  - spec/src/slicers/count-constrained-knapsack.md
  - spec/src/scorers/metadata-key.md
  - spec/src/SUMMARY.md
  - spec/src/slicers.md
  - spec/src/scorers.md
  - CHANGELOG.md
key_decisions:
  - "No new decisions — spec chapters faithfully document existing implementation decisions D174, D178, D180, D181"
patterns_established:
  - "COUNT-KNAPSACK-CAP pseudocode follows COUNT-QUOTA-SLICE structure with two CCKS-specific insertions: Phase 2 SORT line and selectedCount seeding annotation"
  - "METADATA-KEY-SCORE pseudocode diverges from METADATA-TRUST-SCORE: no defaultScore parameter, no clamping, returns multiplier (1.0 or boost) not absolute score"
observability_surfaces:
  - "grep -ci \"\\bTBD\\b\" spec/src/slicers/count-constrained-knapsack.md → 0 is the primary completeness signal for the slicer chapter"
  - "grep -ci \"\\bTBD\\b\" spec/src/scorers/metadata-key.md → 0 is the primary completeness signal for the scorer chapter"
drill_down_paths:
  - .kata/milestones/M009/slices/S03/tasks/T01-SUMMARY.md
  - .kata/milestones/M009/slices/S03/tasks/T02-SUMMARY.md
duration: 40min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
---

# S03: Spec chapters — count-constrained-knapsack + metadata-key

**Two complete spec chapters authored from working implementations: COUNT-KNAPSACK-CAP (10 sections, D180/D181/D174) and METADATA-KEY-SCORE (10 sections, cupel:priority convention); all wiring updated; cargo and dotnet regression suites green.**

## What Happened

T01 authored `spec/src/slicers/count-constrained-knapsack.md` using `count-quota.md` as the structural template. The chapter documents the 3-phase algorithm (COUNT-KNAPSACK-CAP pseudocode), with two key insertions relative to CountQuotaSlice: the mandatory Phase 2 re-sort step (D180) and Phase 3 `selectedCount` seeding from Phase 1 committed counts (D181). The Trade-offs section covers pre-processing sub-optimality (D174) and cap waste. The Monotonicity section notes `is_count_quota() → true`. All 5 conformance vector outlines were derived from the ground-truth TOML files. SUMMARY.md and slicers.md were updated.

T02 authored `spec/src/scorers/metadata-key.md` using `metadata-trust.md` as the structural template, with intentional divergences: no `defaultScore` constructor parameter (neutral multiplier is a fixed 1.0), no clamping (returns multiplicative values), and construction error is `ArgumentException` not `ArgumentOutOfRangeException` per D178. The Conventions section introduces `cupel:priority` as a reserved metadata key with RECOMMENDED values and cross-references `cupel:trust`/`cupel:source-type`. SUMMARY.md and scorers.md were updated. CHANGELOG.md received the .NET CountConstrainedKnapsackSlice entry that was deferred from S02 (per D184).

## Verification

All 9 verification checks passed:
- `grep -ci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md` → 0
- `grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md` → 0
- `grep -q "count-constrained-knapsack" spec/src/SUMMARY.md` → exits 0
- `grep -q "metadata-key" spec/src/SUMMARY.md` → exits 0
- `grep -q "CountConstrainedKnapsackSlice" spec/src/slicers.md` → exits 0
- `grep -q "MetadataKeyScorer" spec/src/scorers.md` → exits 0
- `grep -q "\.NET.*CountConstrainedKnapsackSlice" CHANGELOG.md` → exits 0
- `cargo test --all-targets` (crates/cupel) → exit 0, 175 tests passed
- `dotnet test --solution Cupel.slnx` → exit 0, 797 passed

## Requirements Advanced

- R062 — spec chapter for CountConstrainedKnapsackSlice authored; all S03 obligations fulfilled; R062 now fully validated pending S04 completion
- R063 — `spec/src/scorers/metadata-key.md` exists as the implementation contract for S04; cupel:priority convention documented

## Requirements Validated

- R062 — Rust and .NET implementations were already complete from S01/S02; spec chapter now exists with zero TBD fields and 5 conformance vector outlines; all validation criteria met except S04 (MetadataKeyScorer still pending)

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

None. Both tasks executed exactly as planned.

## Known Limitations

- R062 validation notes "Pending: S03 spec chapter" — this slice resolves that gap. R062 is now fully validated.
- R063 remains unmapped until S04 ships MetadataKeyScorer in both languages.

## Follow-ups

- S04: implement MetadataKeyScorer in Rust (`crates/cupel/src/scorer/metadata_key.rs`) and .NET (`src/Wollax.Cupel/Scoring/MetadataKeyScorer.cs`) using `spec/src/scorers/metadata-key.md` as the contract; 5 conformance tests; PublicAPI.Approved.txt updated; R063 validated.

## Files Created/Modified

- `spec/src/slicers/count-constrained-knapsack.md` — new spec chapter, 10 sections, zero TBD fields
- `spec/src/scorers/metadata-key.md` — new spec chapter, 10 sections, zero TBD fields, S04 implementation contract
- `spec/src/SUMMARY.md` — CountConstrainedKnapsackSlice and MetadataKeyScorer links added
- `spec/src/slicers.md` — CountConstrainedKnapsackSlice row added to Slicer Summary table
- `spec/src/scorers.md` — MetadataKeyScorer table row and Absolute Scorers list entry added
- `CHANGELOG.md` — .NET CountConstrainedKnapsackSlice entry added to unreleased section

## Forward Intelligence

### What the next slice should know
- `spec/src/scorers/metadata-key.md` is the authoritative contract for MetadataKeyScorer. The construction signature is `(key, value, boost)` where `boost > 0.0`. Error type is `CupelError::ScorerConfig` in Rust and `ArgumentException` (not `ArgumentOutOfRangeException`) in .NET per D178.
- The neutral multiplier for non-matching items is a hardcoded constant `1.0` — there is no `defaultMultiplier` constructor parameter.
- `cupel:priority` is the reserved metadata key for priority signaling; RECOMMENDED values are `"high"`, `"medium"`, `"low"`, `"critical"`.
- The scorer is composable with `CompositeScorer` — multiplicative semantics mean boost values compound correctly.

### What's fragile
- None — spec-only slice; no implementation risk introduced.

### Authoritative diagnostics
- `grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md` → 0 confirms S04 can trust the spec as a complete contract.
- `grep "^## " spec/src/scorers/metadata-key.md` lists all 10 sections to confirm structural completeness.

### What assumptions changed
- No assumptions changed. Both spec chapters were derived from working implementations with no surprises.
