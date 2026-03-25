# S03: Spec chapters — count-constrained-knapsack + metadata-key

**Goal:** Two spec chapters (`count-constrained-knapsack.md` and `metadata-key.md`) exist with zero TBD fields; both are linked from `spec/src/SUMMARY.md` and the respective index pages; CHANGELOG.md has the .NET CountConstrainedKnapsackSlice entry.
**Demo:** `grep -ci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md` → 0; `grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md` → 0; both files linked in SUMMARY.md; `cargo test --all-targets` green; `dotnet test --solution Cupel.slnx` green.

## Must-Haves

- `spec/src/slicers/count-constrained-knapsack.md` exists: 3-phase pseudocode (COUNT-KNAPSACK-CAP algorithm); Phase 2 re-sort note (D180); Phase 3 seeded from Phase 1 counts (D181); pre-processing sub-optimality trade-off documented; cap waste documented; Monotonicity section with is_count_quota() → true and cross-reference to budget-simulation.md; ScarcityBehavior section with exact Throw message; 5 conformance vector outlines; zero TBD fields.
- `spec/src/scorers/metadata-key.md` exists: multiplicative algorithm pseudocode (METADATA-KEY-SCORE); `cupel:priority` convention; boost validation `> 0.0` at construction; 5 conformance vector outlines (match, no-match, missing-key, zero-boost error, negative-boost error); zero TBD fields.
- Both files linked from `spec/src/SUMMARY.md` under their respective sections.
- `spec/src/slicers.md` summary table updated with CountConstrainedKnapsackSlice row.
- `spec/src/scorers.md` summary table updated with MetadataKeyScorer row.
- `CHANGELOG.md` unreleased section has .NET CountConstrainedKnapsackSlice entry (Rust was added in S01).
- `cargo test --all-targets` stays green (no regressions from spec-only changes).
- `dotnet test --solution Cupel.slnx` stays green.

## Proof Level

- This slice proves: contract (document completeness via grep) + regression (no test suite breakage)
- Real runtime required: no (spec-only authoring)
- Human/UAT required: no

## Verification

- `grep -ci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md` → 0
- `grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md` → 0
- `grep -q "count-constrained-knapsack" spec/src/SUMMARY.md`
- `grep -q "metadata-key" spec/src/SUMMARY.md`
- `grep -q "CountConstrainedKnapsackSlice" spec/src/slicers.md`
- `grep -q "MetadataKeyScorer" spec/src/scorers.md`
- `grep -q "\.NET.*CountConstrainedKnapsackSlice\|CountConstrainedKnapsackSlice.*\.NET" CHANGELOG.md`
- `cargo test --all-targets` exits 0 (no regressions)
- `dotnet test --solution Cupel.slnx` exits 0 (no regressions)

## Observability / Diagnostics

- Runtime signals: none (spec-only changes)
- Inspection surfaces: `grep -rci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md spec/src/scorers/metadata-key.md` — primary spec completeness check
- Failure visibility: TBD-count > 0 → spec incomplete; grep for section headers confirms structural completeness
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `spec/src/slicers/count-quota.md` (structural template for CCKS chapter); `spec/src/scorers/metadata-trust.md` (structural template for MetadataKeyScorer chapter); 5 TOML conformance vectors in `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml` (ground truth for algorithm behavior); `crates/cupel/src/slicer/count_constrained_knapsack.rs` and `src/Wollax.Cupel/Slicing/CountConstrainedKnapsackSlice.cs` (implementations to validate spec accuracy)
- New wiring introduced in this slice: SUMMARY.md links; slicers.md and scorers.md table rows; CHANGELOG.md .NET entry
- What remains before the milestone is truly usable end-to-end: S04 — MetadataKeyScorer implementation in Rust and .NET using `metadata-key.md` as the contract

## Tasks

- [x] **T01: Write count-constrained-knapsack spec chapter** `est:30m`
  - Why: R062 requires a spec chapter for CountConstrainedKnapsackSlice; S04 depends on both spec chapters existing; this chapter documents the 3-phase algorithm (D180 re-sort, D181 Phase 3 init, D174 trade-off) for all future language binding implementors
  - Files: `spec/src/slicers/count-constrained-knapsack.md`, `spec/src/SUMMARY.md`, `spec/src/slicers.md`
  - Do: Create `count-constrained-knapsack.md` by adapting `count-quota.md` structure. Key differences: (1) Phase 2 delegates to stored KnapsackSlice (not configurable inner slicer); (2) Phase 2 output is re-sorted by score descending before Phase 3 (D180); (3) Phase 3 `selectedCount` seeded from Phase 1 committed counts (D181); (4) no KnapsackSlice guard section (CCKS hardwires it); (5) Monotonicity section: `is_count_quota() → true` triggers FindMinBudgetFor guard (cross-ref to budget-simulation.md); (6) pre-processing sub-optimality trade-off documented (D174); (7) cap waste (knapsack selects items then Phase 3 drops them) documented; (8) exact ScarcityBehavior.Throw message; (9) 5 conformance vector outlines derived from the 5 TOML files. Add CountConstrainedKnapsackSlice row to `slicers.md` table. Add link to `SUMMARY.md` under the Slicers section after CountQuotaSlice.
  - Verify: `grep -ci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md` → 0; `grep -q "count-constrained-knapsack" spec/src/SUMMARY.md`; `grep -q "CountConstrainedKnapsackSlice" spec/src/slicers.md`
  - Done when: File exists; zero TBD fields; linked from SUMMARY.md; slicers.md table updated

- [x] **T02: Write metadata-key scorer spec chapter and finish wiring** `est:20m`
  - Why: R063 depends on `metadata-key.md` as the implementation contract for S04; the `cupel:priority` convention needs to be introduced here; CHANGELOG.md needs the .NET CountConstrainedKnapsackSlice entry that S02 deferred
  - Files: `spec/src/scorers/metadata-key.md`, `spec/src/SUMMARY.md`, `spec/src/scorers.md`, `CHANGELOG.md`
  - Do: Create `metadata-key.md` by adapting `metadata-trust.md` structure. Key differences from MetadataTrustScorer: (1) multiplicative semantics — scorer returns `boost` for matching items and `1.0` for non-matching (not a clamped trust value); (2) no `defaultScore` parameter — `defaultMultiplier` is hardcoded to `1.0` (not a constructor parameter); (3) construction parameter is `(key, value, boost)` where `boost > 0.0` (non-positive or non-finite → construction error); (4) no clamping — output is the raw multiplier; (5) `cupel:priority` convention section; (6) 5 conformance vector outlines (match→boost, no-match→1.0, missing-key→1.0, zero-boost→error, negative-boost→error). Add MetadataKeyScorer row to `scorers.md` table. Add link to SUMMARY.md under Scorers after DecayScorer. Add `.NET: CountConstrainedKnapsackSlice` note to CHANGELOG.md unreleased section (D184 noted this was deferred from S02).
  - Verify: `grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md` → 0; `grep -q "metadata-key" spec/src/SUMMARY.md`; `grep -q "MetadataKeyScorer" spec/src/scorers.md`; `grep -q "CountConstrainedKnapsackSlice" CHANGELOG.md | grep -i "net\|dotnet"` (or equivalent)
  - Done when: File exists; zero TBD fields; linked from SUMMARY.md; scorers.md table updated; CHANGELOG.md has .NET entry; `cargo test --all-targets` and `dotnet test --solution Cupel.slnx` both green

## Files Likely Touched

- `spec/src/slicers/count-constrained-knapsack.md` (new)
- `spec/src/scorers/metadata-key.md` (new)
- `spec/src/SUMMARY.md`
- `spec/src/slicers.md`
- `spec/src/scorers.md`
- `CHANGELOG.md`
