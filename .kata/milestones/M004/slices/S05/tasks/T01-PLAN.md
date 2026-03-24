---
estimated_steps: 5
estimated_files: 6
---

# T01: Extend Slicer trait with is_quota/is_count_quota and add budget simulation methods

**Slice:** S05 — Rust budget simulation parity
**Milestone:** M004

## Description

Implement `get_marginal_items` and `find_min_budget_for` on the Rust `Pipeline`, matching the .NET `BudgetSimulationExtensions` API. Extend the `Slicer` trait with `is_quota()` and `is_count_quota()` defaulted methods for monotonicity guards, following the established `is_knapsack()` pattern (D085). Write comprehensive integration tests exercising both methods.

## Steps

1. **Add `is_quota()` and `is_count_quota()` to the `Slicer` trait** in `crates/cupel/src/slicer/mod.rs` — both default to `false`, following the `is_knapsack()` pattern. Override `is_quota() → true` in `QuotaSlice` (`slicer/quota.rs`) and `is_count_quota() → true` in `CountQuotaSlice` (`slicer/count_quota.rs`).

2. **Implement `get_marginal_items` on `Pipeline`** — either inline in `pipeline/mod.rs` or in a new `pipeline/budget_simulation.rs` submodule. Signature: `pub fn get_marginal_items(&self, items: &[ContextItem], budget: &ContextBudget, slack_tokens: i32) -> Result<Vec<ContextItem>, CupelError>`. Key behavior:
   - Guard: if `self.slicer.is_quota()` return `Err(CupelError::PipelineConfig(...))`
   - Short-circuit: if `slack_tokens == 0` return `Ok(vec![])`
   - Full-budget dry run with `budget`
   - Reduced-budget dry run with `ContextBudget::new(budget.max_tokens() - slack_tokens as i64, budget.target_tokens() - slack_tokens as i64, budget.output_reserve(), HashMap::default(), 0.0)`
   - Diff via content-based matching: build `HashMap<&str, usize>` from reduced-run included items, iterate primary included items, if content not in reduced set → marginal

3. **Implement `find_min_budget_for` on `Pipeline`** — Signature: `pub fn find_min_budget_for(&self, items: &[ContextItem], budget: &ContextBudget, target: &ContextItem, search_ceiling: i32) -> Result<Option<i32>, CupelError>`. Key behavior:
   - Guard: if `self.slicer.is_quota() || self.slicer.is_count_quota()` return `Err(CupelError::PipelineConfig(...))`
   - Precondition: `target` must be in `items` (by content match); `search_ceiling >= target.tokens()`
   - Binary search over `[target.tokens() as i32, search_ceiling]`; budgets use `ContextBudget::new(mid as i64, mid as i64, 0, HashMap::default(), 0.0)`
   - After loop: check `low` first, then `high` (matching .NET behavior, not just spec pseudocode)
   - Content-based `contains_item` check: iterate included, compare `.item.content() == target.content()`

4. **Update re-exports in `lib.rs`** if methods are in a submodule; if they're `impl Pipeline` methods they're automatically available.

5. **Write integration tests in `crates/cupel/tests/budget_simulation.rs`**:
   - `get_marginal_items_basic` — 3 items with varying token sizes, verify correct marginal items returned
   - `get_marginal_items_slack_zero` — returns empty vec
   - `get_marginal_items_rejects_quota_slice` — `PipelineConfig` error
   - `find_min_budget_basic` — target item included at some budget, verify correct minimum found
   - `find_min_budget_not_found` — search ceiling too low, returns `None`
   - `find_min_budget_rejects_quota_slice` — `PipelineConfig` error
   - `find_min_budget_rejects_count_quota_slice` — `PipelineConfig` error
   - `find_min_budget_target_not_in_items` — `InvalidBudget` or argument error
   - `find_min_budget_ceiling_below_tokens` — argument error

## Must-Haves

- [ ] `Slicer` trait has `is_quota()` and `is_count_quota()` defaulted methods returning `false`
- [ ] `QuotaSlice::is_quota()` returns `true`; `CountQuotaSlice::is_count_quota()` returns `true`
- [ ] `Pipeline::get_marginal_items` exists with correct signature and behavior
- [ ] `Pipeline::find_min_budget_for` exists returning `Option<i32>`
- [ ] Monotonicity guard: `get_marginal_items` rejects `QuotaSlice`
- [ ] Monotonicity guard: `find_min_budget_for` rejects `QuotaSlice` and `CountQuotaSlice`
- [ ] Content-based item matching (not reference equality)
- [ ] Binary search checks both `low` and `high` after loop exits
- [ ] `cargo test --all-targets` passes with all new tests
- [ ] `cargo clippy --all-targets -- -D warnings` clean

## Verification

- `cargo test --all-targets` — all tests pass including new `budget_simulation.rs`
- `cargo clippy --all-targets -- -D warnings` — no warnings
- `grep -c '#\[test\]' crates/cupel/tests/budget_simulation.rs` — at least 7 test functions

## Observability Impact

- Signals added/changed: `CupelError::PipelineConfig` used for monotonicity guard errors with descriptive messages matching .NET error text style
- How a future agent inspects this: read the error message in `PipelineConfig` variant; it names the constraint violated
- Failure state exposed: error messages describe which slicer type is incompatible and why

## Inputs

- `src/Wollax.Cupel/BudgetSimulationExtensions.cs` — .NET reference implementation to port from
- `spec/src/analytics/budget-simulation.md` — spec chapter with pseudocode and preconditions
- `crates/cupel/src/pipeline/mod.rs` — existing `Pipeline::dry_run` used as the primitive
- `crates/cupel/src/slicer/mod.rs` — `Slicer` trait with `is_knapsack()` pattern to follow
- D069 (explicit budget param), D098 (API shape), D099 (tiebreak contract), D113 (content-keyed matching)

## Expected Output

- `crates/cupel/src/slicer/mod.rs` — `is_quota()`, `is_count_quota()` added to `Slicer` trait
- `crates/cupel/src/slicer/quota.rs` — `is_quota() → true` override
- `crates/cupel/src/slicer/count_quota.rs` — `is_count_quota() → true` override
- `crates/cupel/src/pipeline/mod.rs` — `get_marginal_items` and `find_min_budget_for` implementations
- `crates/cupel/tests/budget_simulation.rs` — 7+ integration tests
