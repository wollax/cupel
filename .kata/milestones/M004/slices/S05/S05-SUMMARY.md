---
id: S05
parent: M004
milestone: M004
provides:
  - is_quota() and is_count_quota() defaulted methods on Slicer trait
  - QuotaSlice::is_quota() → true, CountQuotaSlice::is_count_quota() → true overrides
  - Pipeline::get_marginal_items — content-based diff between full and reduced-budget dry runs
  - Pipeline::find_min_budget_for — binary search with low/high boundary checks, returns Option<i32>
  - Monotonicity guards rejecting QuotaSlice (marginal) and QuotaSlice+CountQuotaSlice (find_min_budget)
requires:
  - slice: none
    provides: Independent slice — uses existing dry_run and Pipeline infrastructure
affects: []
key_files:
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/slicer/quota.rs
  - crates/cupel/src/slicer/count_quota.rs
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/tests/budget_simulation.rs
key_decisions:
  - "D122: Contract + integration verification with real Pipeline + dry_run"
  - "D123: Budget simulation methods as impl Pipeline, not free functions"
  - "D124: CupelError::PipelineConfig for monotonicity guard errors"
  - "D125: Separate is_quota()/is_count_quota() methods following is_knapsack() pattern"
patterns_established:
  - "is_quota()/is_count_quota() default-false trait methods following is_knapsack() pattern (D085)"
  - "Content-based item matching via HashMap<&str, usize> for diff operations (D113)"
  - "Monotonicity guards as runtime checks on slicer type before budget simulation"
observability_surfaces:
  - CupelError::PipelineConfig(String) for monotonicity guard violations with descriptive messages
  - CupelError::InvalidBudget(String) for precondition failures (target not in items, ceiling too low)
drill_down_paths:
  - .kata/milestones/M004/slices/S05/tasks/T01-SUMMARY.md
duration: 10min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
---

# S05: Rust budget simulation parity

**Budget simulation API on Rust Pipeline: get_marginal_items and find_min_budget_for with monotonicity guards and 9 integration tests matching .NET behavior**

## What Happened

Extended the `Slicer` trait with `is_quota()` and `is_count_quota()` defaulted methods (both return `false`), following the established `is_knapsack()` pattern (D085). `QuotaSlice` overrides `is_quota() → true`; `CountQuotaSlice` overrides `is_count_quota() → true`.

Implemented `Pipeline::get_marginal_items` which performs two dry runs (full budget and reduced budget), then diffs included items using content-based matching via `HashMap<&str, usize>` (D113). Returns items present at full budget but absent at reduced budget. Guards against `QuotaSlice` which produces non-monotonic inclusion.

Implemented `Pipeline::find_min_budget_for` which binary-searches over `[target.tokens(), search_ceiling]` using real dry runs at each midpoint. After the loop, checks both `low` and `high` boundaries (matching .NET behavior). Returns `Option<i32>`. Guards against both `QuotaSlice` and `CountQuotaSlice`.

Wrote 9 integration tests in `crates/cupel/tests/budget_simulation.rs` covering: basic marginal items, slack-zero short-circuit, quota rejection for marginal items, basic min-budget finding, not-found case, quota rejection and count-quota rejection for find_min_budget, target-not-in-items error, and ceiling-below-tokens error.

## Verification

- `cargo test --all-targets` — 158 passed (0 failures)
- `cargo clippy --all-targets -- -D warnings` — clean, no warnings
- `grep -c '#\[test\]' crates/cupel/tests/budget_simulation.rs` — 9 test functions (≥7 required)
- Both `get_marginal_items` and `find_min_budget_for` exercised in test file

## Requirements Advanced

- R054 — Both budget simulation methods implemented and tested; monotonicity guards work; content-based matching matches .NET semantics

## Requirements Validated

- R054 — `get_marginal_items` and `find_min_budget_for` in Rust pass 9 unit tests matching .NET behavior; monotonicity guard rejects QuotaSlice/CountQuotaSlice; `cargo test --all-targets` passes

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

None.

## Known Limitations

- `find_min_budget_for` accepts a `budget` parameter per D069 (explicit budget param) but does not use it — the binary search constructs its own budgets. This matches the .NET API shape for consistency.
- Content-based matching (D113) differs from .NET's reference equality — this is intentional and consistent with the Rust crate's design where items are cloned during pipeline execution.

## Follow-ups

- None — this is the final slice of M004.

## Files Created/Modified

- `crates/cupel/src/slicer/mod.rs` — `is_quota()` and `is_count_quota()` added to Slicer trait
- `crates/cupel/src/slicer/quota.rs` — `is_quota() → true` override
- `crates/cupel/src/slicer/count_quota.rs` — `is_count_quota() → true` override
- `crates/cupel/src/pipeline/mod.rs` — `get_marginal_items`, `find_min_budget_for`, `contains_item_by_content` implementations
- `crates/cupel/tests/budget_simulation.rs` — 9 integration tests

## Forward Intelligence

### What the next slice should know
- This is the final slice of M004. All 5 success criteria are met.

### What's fragile
- Nothing — budget simulation methods are thin orchestration over the stable `dry_run` primitive.

### Authoritative diagnostics
- `CupelError::PipelineConfig` messages name the violated constraint and slicer type — grep for "monotonic" in error messages.
- `CupelError::InvalidBudget` messages specify the precondition violation.

### What assumptions changed
- None — implementation matched the plan exactly.
