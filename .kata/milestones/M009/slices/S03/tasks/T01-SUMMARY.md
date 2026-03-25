---
id: T01
parent: S03
milestone: M009
provides:
  - spec/src/slicers/count-constrained-knapsack.md — complete spec chapter with all 10 required sections
  - D180 Phase 2 re-sort requirement documented as normative conformance note
  - D181 Phase 3 selectedCount seeding from Phase 1 documented in pseudocode and conformance notes
  - D174 pre-processing sub-optimality trade-off documented in Trade-offs section
  - 5 conformance vector outlines derived from TOML ground truth
  - SUMMARY.md and slicers.md updated with CountConstrainedKnapsackSlice links
key_files:
  - spec/src/slicers/count-constrained-knapsack.md
  - spec/src/SUMMARY.md
  - spec/src/slicers.md
key_decisions:
  - "No new decisions — spec chapter faithfully documents existing implementation decisions D174, D180, D181"
patterns_established:
  - "COUNT-KNAPSACK-CAP pseudocode follows COUNT-QUOTA-SLICE structure with two CCKS-specific insertions: Phase 2 SORT line and selectedCount seeding annotation"
observability_surfaces:
  - "grep -ci \"\\bTBD\\b\" spec/src/slicers/count-constrained-knapsack.md → 0 is the primary completeness signal"
duration: 20min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T01: Write count-constrained-knapsack spec chapter

**Complete COUNT-KNAPSACK-CAP spec chapter authored with all 10 sections, D180/D181/D174 documented, and 5 TOML-derived conformance vector outlines.**

## What Happened

Authored `spec/src/slicers/count-constrained-knapsack.md` using `count-quota.md` as the structural template. Read all 5 TOML conformance vectors and the Rust and .NET implementations to derive algorithm details, exact error messages, and Phase 2/3 behavior.

Key differences from CountQuotaSlice captured in the spec:
- CCKS hardwires KnapsackSlice (no inner slicer guard needed at construction — it IS the knapsack wrapper)
- Phase 2 adds a mandatory re-sort step (D180): `innerSelected <- SORT(innerSelected, by score descending)` between the KnapsackSlice call and the Phase 3 cap loop
- Phase 3 `selectedCount` is seeded from Phase 1 committed counts (D181), not initialized to zero — this is annotated in the pseudocode and stated as a normative conformance note
- Monotonicity section notes `is_count_quota() → true` and the absence of a construction-time KnapsackSlice guard
- Trade-offs section covers both pre-processing sub-optimality (D174) and cap waste
- Exact `.NET` ScarcityBehavior.Throw message extracted from implementation

Added SUMMARY.md entry under CountQuotaSlice and slicers.md table row.

## Verification

All 5 checks passed:
- `grep -ci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md` → 0
- `grep -q "count-constrained-knapsack" spec/src/SUMMARY.md` → exits 0
- `grep -q "CountConstrainedKnapsackSlice" spec/src/slicers.md` → exits 0
- `grep -q "SORT.*innerSelected\|innerSelected.*SORT\|by score descending" spec/src/slicers/count-constrained-knapsack.md` → exits 0
- `grep -q "is_count_quota\|isCountQuota" spec/src/slicers/count-constrained-knapsack.md` → exits 0

## Diagnostics

`grep -ci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md` → 0 is the binary completeness signal.
Section presence check: `grep "^## " spec/src/slicers/count-constrained-knapsack.md` lists all 10 sections.

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `spec/src/slicers/count-constrained-knapsack.md` — new spec chapter, 10 sections, zero TBD fields
- `spec/src/SUMMARY.md` — added CountConstrainedKnapsackSlice link under Slicers section
- `spec/src/slicers.md` — added CountConstrainedKnapsackSlice row to Slicer Summary table
