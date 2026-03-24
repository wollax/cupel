---
id: T01
parent: S05
milestone: M004
provides:
  - is_quota() and is_count_quota() defaulted methods on Slicer trait
  - QuotaSlice::is_quota() → true override
  - CountQuotaSlice::is_count_quota() → true override
  - Pipeline::get_marginal_items with content-based diff and QuotaSlice guard
  - Pipeline::find_min_budget_for with binary search, both low/high checks, and QuotaSlice+CountQuotaSlice guard
key_files:
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/slicer/quota.rs
  - crates/cupel/src/slicer/count_quota.rs
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/tests/budget_simulation.rs
key_decisions:
  - "Content-based matching (D113) for item comparison in both methods instead of .NET reference equality"
  - "Unused budget param in find_min_budget_for per D069 — binary search constructs its own budgets"
patterns_established:
  - "is_quota()/is_count_quota() follow the is_knapsack() pattern — default false, override in specific slicer impls"
  - "Monotonicity guards use CupelError::PipelineConfig with descriptive messages matching .NET error text style"
observability_surfaces:
  - CupelError::PipelineConfig for monotonicity guard violations with descriptive messages naming the violated constraint
  - CupelError::InvalidBudget for precondition failures (target not in items, ceiling too low)
duration: 5min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T01: Extend Slicer trait with is_quota/is_count_quota and add budget simulation methods

**Budget simulation parity: get_marginal_items and find_min_budget_for on Pipeline with monotonicity guards and 9 integration tests**

## What Happened

All implementation was already present on the slice branch from prior work. The Slicer trait in `mod.rs` has `is_quota()` and `is_count_quota()` as defaulted methods returning `false`, with overrides in `QuotaSlice` (returns `true` for `is_quota()`) and `CountQuotaSlice` (returns `true` for `is_count_quota()`). Both `Pipeline::get_marginal_items` and `Pipeline::find_min_budget_for` are fully implemented in `pipeline/mod.rs` with content-based item matching, proper monotonicity guards, and binary search with both low/high boundary checks.

The integration test file `crates/cupel/tests/budget_simulation.rs` contains 9 test functions covering: basic marginal items, slack-zero short-circuit, quota rejection, basic min-budget finding, not-found case, quota and count-quota rejection, target-not-in-items error, and ceiling-below-tokens error.

## Verification

- `cargo test --all-targets` — 158 passed (0 failures)
- `cargo clippy --all-targets -- -D warnings` — clean, no warnings
- `grep -c '#\[test\]' crates/cupel/tests/budget_simulation.rs` — 9 test functions (≥7 required)

## Diagnostics

- `CupelError::PipelineConfig(String)` for monotonicity guard violations — error messages name the slicer type and explain why it's incompatible
- `CupelError::InvalidBudget(String)` for precondition failures — messages specify the violated constraint

## Deviations

None — all code was already implemented on the branch.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/slicer/mod.rs` — `is_quota()` and `is_count_quota()` added to Slicer trait with defaults
- `crates/cupel/src/slicer/quota.rs` — `is_quota() → true` override in Slicer impl for QuotaSlice
- `crates/cupel/src/slicer/count_quota.rs` — `is_count_quota() → true` override in Slicer impl for CountQuotaSlice
- `crates/cupel/src/pipeline/mod.rs` — `get_marginal_items` and `find_min_budget_for` implementations with `contains_item_by_content` helper
- `crates/cupel/tests/budget_simulation.rs` — 9 integration tests for both methods
