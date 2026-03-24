---
estimated_steps: 4
estimated_files: 3
---

# T02: Rust quota_utilization function + tests

**Slice:** S03 — IQuotaPolicy abstraction + QuotaUtilization
**Milestone:** M004

## Description

Add the `KindQuotaUtilization` struct and `quota_utilization` free function to `analytics.rs`. The function takes a `SelectionReport` and a `&dyn QuotaPolicy`, computes per-kind utilization against the policy constraints, and returns `Vec<KindQuotaUtilization>`. Write integration tests exercising both QuotaSlice and CountQuotaSlice policies.

## Steps

1. In `crates/cupel/src/analytics.rs`, define `KindQuotaUtilization` struct:
   - `kind: ContextKind`, `mode: QuotaConstraintMode`, `require: f64`, `cap: f64`, `actual: f64`, `utilization: f64`
   - Derive `Debug, Clone, PartialEq`
   - `utilization` = `actual / cap` clamped to [0.0, 1.0]; if cap == 0, utilization = 0.0
   - For percentage mode: `actual` = `sum(included tokens for kind) / target_tokens * 100.0` — note: the function needs a `budget` parameter to compute target_tokens
   - For count mode: `actual` = count of included items of that kind (as f64)
2. Define `quota_utilization(report: &SelectionReport, policy: &dyn QuotaPolicy, budget: &ContextBudget) -> Vec<KindQuotaUtilization>`:
   - Call `policy.quota_constraints()` to get per-kind constraints
   - For each constraint, compute the actual value from the report's included items
   - Compute utilization ratio
   - Return one `KindQuotaUtilization` per constraint, sorted by kind for determinism
3. In `crates/cupel/src/lib.rs`, add `KindQuotaUtilization` and `quota_utilization` to the `pub use analytics::` re-export block
4. Create `crates/cupel/tests/quota_utilization.rs` with integration tests:
   - Test with `QuotaSlice` (percentage mode): configure 2 kinds, run pipeline, check utilization values
   - Test with `CountQuotaSlice` (count mode): configure count quotas, run pipeline, check utilization counts
   - Test empty report returns utilization 0.0 for all constraints
   - Test kind present in policy but absent from report returns actual=0.0

## Must-Haves

- [ ] `KindQuotaUtilization` struct with kind, mode, require, cap, actual, utilization fields
- [ ] `quota_utilization` function accepts `&SelectionReport`, `&dyn QuotaPolicy`, `&ContextBudget`
- [ ] Percentage mode computes actual as percentage of target_tokens consumed by that kind
- [ ] Count mode computes actual as count of included items of that kind
- [ ] Utilization = actual / cap (clamped [0.0, 1.0]; 0.0 when cap is 0)
- [ ] Integration tests cover QuotaSlice and CountQuotaSlice policies
- [ ] All new types re-exported from lib.rs
- [ ] `cargo test --all-targets` passes

## Verification

- `cargo test --all-targets` — all tests pass including new quota_utilization tests
- `cargo clippy --all-targets -- -D warnings` — clean
- Integration tests exercise both QuotaSlice (percentage) and CountQuotaSlice (count) policies

## Observability Impact

- None — pure analytics function

## Inputs

- T01 output: `QuotaPolicy` trait, `QuotaConstraint`, `QuotaConstraintMode` in slicer module
- `crates/cupel/src/analytics.rs` — existing analytics pattern (budget_utilization, kind_diversity, etc.)
- `SelectionReport` from report generation or test construction

## Expected Output

- `crates/cupel/src/analytics.rs` — `KindQuotaUtilization` struct, `quota_utilization` function
- `crates/cupel/src/lib.rs` — updated re-exports
- `crates/cupel/tests/quota_utilization.rs` — 4+ integration tests covering both policy types
