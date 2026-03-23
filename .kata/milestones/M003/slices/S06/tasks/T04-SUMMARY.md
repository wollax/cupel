---
id: T04
parent: S06
milestone: M003
provides:
  - Real CountQuotaSlice spec page covering decorator shape, scarcity, Knapsack guard, and shortfall surface
  - SUMMARY.md navigation link for CountQuotaSlice
  - Slicer index table entry for CountQuotaSlice
  - DecayScorer fully represented in scorer summary table and Absolute Scorers category
  - v1.3.0 changelog entry documenting all M003 feature additions and spec decisions
  - Budget-simulation parity note with deferral rationale for Rust
key_files:
  - spec/src/slicers/count-quota.md
  - spec/src/SUMMARY.md
  - spec/src/slicers.md
  - spec/src/scorers.md
  - spec/src/changelog.md
  - spec/src/analytics/budget-simulation.md
key_decisions:
  - "DecayScorer placed in Absolute Scorers category (uses only item timestamp + injected time, ignores allItems)"
patterns_established:
  - "Changelog entries reference spec chapter links for traceability"
observability_surfaces:
  - "All documentation references are mechanically verifiable via grep"
duration: 12min
verification_result: passed
completed_at: 2026-03-23T12:00:00Z
blocker_discovered: false
---

# T04: Complete spec navigation, CountQuotaSlice docs, and milestone-facing changelog alignment

**Added real CountQuotaSlice spec chapter, completed scorer/slicer index coverage for all M003 features, and wrote the v1.3.0 changelog with deterministic tie-break clarification and budget-simulation Rust deferral rationale**

## What Happened

Wrote `spec/src/slicers/count-quota.md` as a full spec page covering the three-phase COUNT-DISTRIBUTE-BUDGET algorithm, CountQuotaEntry configuration and validation rules, ScarcityBehavior enum, KnapsackSlice guard, scarcity reporting via CountRequirementShortfall, monotonicity incompatibility with budget simulation, edge cases, complexity, and conformance notes. The page is at the same level of detail as the existing QuotaSlice and GreedySlice pages.

Updated `spec/src/SUMMARY.md` to include CountQuotaSlice in the mdBook sidebar navigation under Slicers. Updated `spec/src/slicers.md` to add CountQuotaSlice to the slicer summary table.

Updated `spec/src/scorers.md` to add DecayScorer to both the summary table and the Absolute Scorers category section. MetadataTrustScorer was already present in all three locations.

Wrote the `[1.3.0]` changelog entry in `spec/src/changelog.md` documenting: DecayScorer and MetadataTrustScorer (scorers), CountQuotaSlice (slicer), budget simulation extension methods (analytics), BudgetUtilization/KindDiversity/TimestampCoverage (analytics), Cupel.Testing package, OTel bridge package, the deterministic tie-break contract change on GreedySlice, and all relevant spec decisions (D042, D047, D059, KnapsackSlice guard, Rust deferral, SweepBudget scope).

Updated `spec/src/analytics/budget-simulation.md` Language Parity Note to specify the exact API methods scoped, the deferral rationale (Rust Pipeline lacks public DryRun equivalent), and the future parity trigger.

## Verification

- `grep "count-quota.md|CountQuotaSlice"` across SUMMARY.md, slicers.md, count-quota.md, changelog.md → 15 matches across all 4 files
- `grep "DecayScorer|MetadataTrustScorer"` across scorers.md, changelog.md → 8 matches covering summary table, categories, and changelog
- `grep "FindMinBudgetFor|Language Parity Note"` in budget-simulation.md → 7 matches confirming signature and parity note presence
- Full slice-level spec grep across all 7 spec files → 34 matches, all documentation/index/changelog references resolve to real text

## Diagnostics

Future agents can verify documentation completeness via:
- `grep "CountQuotaSlice" spec/src/SUMMARY.md spec/src/slicers.md spec/src/slicers/count-quota.md`
- `grep "DecayScorer" spec/src/scorers.md` (should appear in table + category)
- `grep "1.3.0" spec/src/changelog.md` (should return the version header)

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `spec/src/slicers/count-quota.md` — New CountQuotaSlice spec chapter
- `spec/src/SUMMARY.md` — Added CountQuotaSlice to mdBook navigation
- `spec/src/slicers.md` — Added CountQuotaSlice to slicer summary table
- `spec/src/scorers.md` — Added DecayScorer to summary table and Absolute Scorers category
- `spec/src/changelog.md` — Added v1.3.0 changelog entry
- `spec/src/analytics/budget-simulation.md` — Expanded Language Parity Note with deferral rationale
