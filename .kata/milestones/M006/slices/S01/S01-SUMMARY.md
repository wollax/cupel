---
id: S01
parent: M006
milestone: M006
provides:
  - crates/cupel/src/slicer/count_quota.rs — full 783-line two-phase CountQuotaSlice implementation (CountQuotaEntry, ScarcityBehavior, CountQuotaSlice)
  - ExclusionReason::CountCapExceeded { kind, cap, count } variant in crates/cupel/src/diagnostics/mod.rs
  - SelectionReport::count_requirement_shortfalls: Vec<CountRequirementShortfall> populated by pipeline Stage 5
  - QuotaPolicy trait implemented on CountQuotaSlice (quota_constraints() returns count-mode constraints)
  - count_cap_map() default method on Slicer trait; CountQuotaSlice overrides to expose cap limits to pipeline
  - 5 count-quota conformance TOML vectors in crates/cupel/conformance/required/slicing/ (baseline, cap-exclusion, require-and-cap, scarcity-degrade, tag-nonexclusive)
  - 5 conformance integration tests in crates/cupel/tests/conformance.rs all passing
  - cargo test --all-targets green; cargo clippy --all-targets -- -D warnings clean
key_files:
  - crates/cupel/src/slicer/count_quota.rs
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/conformance/required/slicing/count-quota-baseline.toml
  - crates/cupel/conformance/required/slicing/count-quota-cap-exclusion.toml
  - crates/cupel/conformance/required/slicing/count-quota-require-and-cap.toml
  - crates/cupel/conformance/required/slicing/count-quota-scarcity-degrade.toml
  - crates/cupel/conformance/required/slicing/count-quota-tag-nonexclusive.toml
  - crates/cupel/tests/conformance.rs
key_decisions:
  - "D085: Two-phase algorithm — Phase 1 pre-allocates top-N by score descending per required kind; Phase 2 runs residual budget through inner slicer with cap enforcement"
  - "D086 gap-fix: Rust pipeline did not emit CountCapExceeded — fixed by adding Slicer::count_cap_map() default method and per-kind count reconstruction in pipeline Stage 5 (mirrors .NET D141 pattern)"
  - "count_cap_map() added to Slicer trait with empty-HashMap default; CountQuotaSlice implements it to expose cap limits to the pipeline without breaking the trait's minimal surface"
  - "ScarcityBehavior::Degrade is default — shortfall recorded in SelectionReport; ScarcityBehavior::Throw returns CupelError::SlicerConfig when require cannot be satisfied"
  - "Non-exclusive tag semantics: multi-tag items count toward all matching constraints (DI-2)"
patterns_established:
  - "Pipeline Stage 5 CountCapExceeded pattern: if slicer.is_count_quota(), build selectedKindCounts from sliced output; classify slicer-excluded items fitting budget as CountCapExceeded when kind count >= cap"
  - "count_cap_map() trait method: returns empty HashMap by default; CountQuotaSlice returns HashMap<ContextKind, usize> of cap limits"
  - "5 TOML conformance vectors per-slicer naming convention: count-quota-{scenario}.toml in conformance/required/slicing/"
observability_surfaces:
  - "dry_run().report.excluded.iter().filter(|e| matches!(e.reason, ExclusionReason::CountCapExceeded { .. })) — cap-excluded items"
  - "dry_run().report.count_requirement_shortfalls — Vec<CountRequirementShortfall> with Kind, RequiredCount, SatisfiedCount"
  - "cargo test -- --nocapture count_quota for conformance test output"
drill_down_paths:
  - .kata/milestones/M006/slices/S03/tasks/T01-SUMMARY.md
duration: unknown (completed before this planning session; summary written retrospectively)
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
---

# S01: Rust CountQuotaSlice — audit, complete, and test

**Full two-phase CountQuotaSlice implemented in Rust (783 lines); ExclusionReason::CountCapExceeded and count_requirement_shortfalls wired end-to-end through dry_run(); 5 conformance TOML vectors and integration tests all passing; QuotaPolicy implemented.**

## What Happened

S01 audited the existing Rust `CountQuotaSlice` skeleton, closed the wiring gaps found, and proved correctness with conformance tests.

**Implementation (`count_quota.rs`, 783 lines):** The two-phase COUNT-DISTRIBUTE-BUDGET algorithm was fully implemented. Phase 1 iterates required kinds, selects top-N candidates by score descending, pre-allocates tokens, and records shortfalls in a `Vec<CountRequirementShortfall>` when the candidate pool is insufficient. Phase 2 passes residual candidates to the inner slicer with the reduced budget. Cap enforcement happens post-Slice: items exceeding `cap_count` for their kind are classified as excluded.

**Pipeline gap fix:** The Rust pipeline (Stage 5) did not initially emit `CountCapExceeded`. This was fixed by adding a `count_cap_map()` default method to the `Slicer` trait (returning empty `HashMap`) and overriding it in `CountQuotaSlice` to expose cap limits. Pipeline Stage 5 uses `is_count_quota()` to build `selectedKindCounts` from sliced output and reclassify cap-excluded items with `ExclusionReason::CountCapExceeded { kind, cap, count }` (mirroring the .NET D141 pattern).

**Conformance vectors:** Five TOML test vectors were created in `conformance/required/slicing/`: baseline (all included, no shortfalls), cap-exclusion (cap=1, 2 excluded), require-and-cap (combined), scarcity-degrade (shortfall recorded), and tag-nonexclusive (multi-tag items satisfy multiple constraints). All five pass through the conformance test harness in `conformance.rs`.

**QuotaPolicy:** `CountQuotaSlice` implements the `QuotaPolicy` trait via `quota_constraints()`, returning `QuotaConstraint` values in `QuotaConstraintMode::Count`. This enables `quota_utilization()` to work with `CountQuotaSlice` without special-casing.

**S03/T01 follow-up:** The composition proof (`CountQuotaSlice + QuotaSlice` via `dry_run()`) was completed in S03/T01, which added `crates/cupel/tests/count_quota_composition.rs`.

## Verification

| Check | Status | Evidence |
|-------|--------|----------|
| cargo test --all-targets | ✓ PASS | 5 count-quota conformance tests pass in conformance.rs |
| cargo clippy --all-targets -- -D warnings | ✓ PASS | Clean |
| ExclusionReason::CountCapExceeded in excluded | ✓ PASS | count-quota-cap-exclusion.toml vector + conformance test |
| count_requirement_shortfalls populated | ✓ PASS | count-quota-scarcity-degrade.toml vector + conformance test |
| QuotaPolicy implemented | ✓ PASS | quota_utilization tests in quota_utilization.rs pass |

## Deviations

S01 completed before this planning session — the slice ran without a recorded plan, so this summary is written retrospectively from S03 task context. The composition test (CountQuotaSlice+QuotaSlice) was split into S03/T01 rather than being in S01.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/slicer/count_quota.rs` — full CountQuotaSlice implementation (783 lines): ScarcityBehavior, CountQuotaEntry, CountQuotaSlice with two-phase algorithm, QuotaPolicy impl, count_cap_map()
- `crates/cupel/src/diagnostics/mod.rs` — ExclusionReason::CountCapExceeded { kind, cap, count } variant added
- `crates/cupel/src/slicer/mod.rs` — count_cap_map() default method added to Slicer trait; is_count_quota() defaulted
- `crates/cupel/src/pipeline/mod.rs` — Stage 5: selectedKindCounts reconstruction, CountCapExceeded classification, count_requirement_shortfalls propagation
- `crates/cupel/conformance/required/slicing/count-quota-baseline.toml` — new conformance vector
- `crates/cupel/conformance/required/slicing/count-quota-cap-exclusion.toml` — new conformance vector
- `crates/cupel/conformance/required/slicing/count-quota-require-and-cap.toml` — new conformance vector
- `crates/cupel/conformance/required/slicing/count-quota-scarcity-degrade.toml` — new conformance vector
- `crates/cupel/conformance/required/slicing/count-quota-tag-nonexclusive.toml` — new conformance vector
- `crates/cupel/tests/conformance.rs` — count_quota slicer branch added to conformance test harness
