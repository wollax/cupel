---
id: T01
parent: S02
milestone: M004
provides:
  - PolicySensitivityReport struct with variants and diffs fields
  - PolicySensitivityDiffEntry struct with content and statuses fields
  - ItemStatus enum (Included, Excluded)
  - policy_sensitivity orchestration function
  - Integration test proving ≥2 variants with meaningful diff
key_files:
  - crates/cupel/src/analytics.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/policy_sensitivity.rs
key_decisions:
  - "Content-keyed diff uses HashMap<String, Vec<(String, ItemStatus)>> to join across variants, filtering to entries where statuses disagree"
patterns_established:
  - "policy_sensitivity accepts &[(impl AsRef<str>, &Pipeline)] for ergonomic variant labeling"
observability_surfaces:
  - none — pure analytics function, errors propagate as CupelError from dry_run
duration: 10min
verification_result: passed
completed_at: 2026-03-23T12:00:00Z
blocker_discovered: false
---

# T01: Rust PolicySensitivityReport types and implementation

**Added `policy_sensitivity` fork diagnostic with `ItemStatus`, `PolicySensitivityDiffEntry`, and `PolicySensitivityReport` types plus content-keyed diff logic**

## What Happened

Defined three new types in `analytics.rs`: `ItemStatus` (Included/Excluded enum), `PolicySensitivityDiffEntry` (content + per-variant statuses), and `PolicySensitivityReport` (labeled variant reports + diff entries). Implemented `policy_sensitivity` function that calls `dry_run` on each variant pipeline, builds a content-keyed status map, and filters to items where inclusion status differs across variants. Re-exported all three types and the function from `lib.rs`. Created an integration test with two test cases: one exercising two variants with same-ranking scorers, and a guaranteed-diff test using `ReflexiveScorer` vs `PriorityScorer` with items that have opposing relevance/priority rankings and a budget that forces single-item selection.

## Verification

- `cargo test --all-targets` — 145 tests pass (7 suites) including both new `policy_sensitivity` tests
- `cargo clippy --all-targets -- -D warnings` — clean, no warnings
- `policy_sensitivity_guaranteed_diff` test asserts: 2 variants labeled correctly, 2 diff entries (both items swap), each entry has exactly 2 statuses with one Included and one Excluded

## Diagnostics

None — pure function. Errors propagate as `CupelError` from underlying `Pipeline::dry_run`.

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/analytics.rs` — Added ItemStatus, PolicySensitivityDiffEntry, PolicySensitivityReport types and policy_sensitivity function
- `crates/cupel/src/lib.rs` — Re-exported new types and function
- `crates/cupel/tests/policy_sensitivity.rs` — New integration test with 2 test cases
