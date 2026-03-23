---
estimated_steps: 4
estimated_files: 6
---

# T04: Complete Spec Navigation, CountQuotaSlice Docs, and Changelog Alignment

**Slice:** S06 — Budget simulation + tiebreaker + spec alignment
**Milestone:** M003

## Description

Finish the documentation cleanup that makes M003 shippable and understandable: add the missing CountQuotaSlice spec page, repair mdBook navigation/index entries, complete scorer index coverage for DecayScorer and MetadataTrustScorer, and update the changelog plus budget-simulation parity note so the checked-in spec matches the features the code now ships.

## Steps

1. Write `spec/src/slicers/count-quota.md` as the real CountQuotaSlice chapter, covering its decorator shape, scarcity behavior, Knapsack guard, and SelectionReport shortfall surface at the same level of detail as the other slicer pages.
2. Update `spec/src/SUMMARY.md` and `spec/src/slicers.md` so CountQuotaSlice is linked and described in the navigation/index.
3. Update `spec/src/scorers.md` so DecayScorer and MetadataTrustScorer are fully represented in the summary table/categories, not just the mdBook nav.
4. Update `spec/src/changelog.md` and the parity note in `spec/src/analytics/budget-simulation.md` so v1.3 additions, the final `FindMinBudgetFor` signature, and the Rust deferral rationale are all documented.

## Must-Haves

- [ ] `spec/src/slicers/count-quota.md` exists with real content, not a placeholder
- [ ] `spec/src/SUMMARY.md` links to `slicers/count-quota.md`
- [ ] `spec/src/slicers.md` includes CountQuotaSlice in the slicer summary table/section
- [ ] `spec/src/scorers.md` includes DecayScorer and MetadataTrustScorer wherever the scorer index is expected to enumerate shipped scorers
- [ ] `spec/src/changelog.md` records the v1.3 feature additions and deterministic tie-break clarification
- [ ] `spec/src/analytics/budget-simulation.md` documents the final public signature/parity decision consistently with the implementation

## Verification

- `rtk grep "count-quota.md|CountQuotaSlice" spec/src/SUMMARY.md spec/src/slicers.md spec/src/slicers/count-quota.md spec/src/changelog.md`
- `rtk grep "DecayScorer|MetadataTrustScorer" spec/src/scorers.md spec/src/changelog.md`
- `rtk grep "FindMinBudgetFor|Language Parity Note" spec/src/analytics/budget-simulation.md`

## Observability Impact

- Signals added/changed: none at runtime; documentation becomes an explicit inspection surface for future agents and implementors
- How a future agent inspects this: grep the updated spec/nav/changelog files or open the new CountQuotaSlice chapter directly
- Failure state exposed: missing navigation/changelog references are mechanically visible via grep instead of being latent doc debt

## Inputs

- `spec/src/SUMMARY.md` — current mdBook navigation missing CountQuotaSlice
- `spec/src/slicers.md` — current slicer index missing CountQuotaSlice
- `spec/src/scorers.md` — scorer summary currently under-reports shipped M003 scorers
- `spec/src/changelog.md` — still mostly 1.0.0-only
- `spec/src/analytics/budget-simulation.md` — parity note and signature text to align with the shipped API
- S03/S04 summaries and M003 roadmap — authoritative description of CountQuotaSlice, DecayScorer, and MetadataTrustScorer behavior

## Expected Output

- `spec/src/slicers/count-quota.md` — new real CountQuotaSlice chapter
- `spec/src/SUMMARY.md` — navigation updated to include CountQuotaSlice
- `spec/src/slicers.md` — slicer summary/index updated
- `spec/src/scorers.md` — scorer summary/index updated for DecayScorer and MetadataTrustScorer
- `spec/src/changelog.md` — v1.3 alignment entry added
- `spec/src/analytics/budget-simulation.md` — final signature/parity note documented consistently with code
