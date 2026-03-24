# S05: Rust budget simulation parity

**Goal:** Implement `get_marginal_items` and `find_min_budget_for` on the Rust `Pipeline`, matching .NET `BudgetSimulationExtensions` behavior, with monotonicity guards and unit tests.
**Demo:** `cargo test --all-targets` passes with budget simulation tests proving both methods work correctly; monotonicity guards reject QuotaSlice/CountQuotaSlice inner slicers.

## Must-Haves

- `Pipeline::get_marginal_items(&self, items, budget, slack_tokens)` returns items present in full-budget run but absent from reduced-budget run
- `Pipeline::find_min_budget_for(&self, items, budget, target, search_ceiling)` returns `Option<i32>` — minimum budget at which target is included
- `get_marginal_items` rejects `QuotaSlice` slicer with `CupelError::PipelineConfig`
- `find_min_budget_for` rejects `QuotaSlice` and `CountQuotaSlice` slicers with `CupelError::PipelineConfig`
- `Slicer` trait extended with `is_quota()` and `is_count_quota()` defaulted methods (both return `false`)
- `QuotaSlice` overrides `is_quota() → true`; `CountQuotaSlice` overrides `is_count_quota() → true`
- Content-based item matching (not reference equality) consistent with D113
- Reduced budget in `get_marginal_items` propagates `output_reserve` but uses `HashMap::default()` for `reserved_slots` and `0.0` for `estimation_safety_margin_percent` (matching .NET behavior)
- `find_min_budget_for` binary search budgets use `ContextBudget::new(mid, mid, 0, HashMap::default(), 0.0)` (matching .NET)
- `find_min_budget_for` checks both `low` and `high` after binary search loop (matching .NET, not just spec pseudocode)
- `slack_tokens == 0` short-circuits to empty vec in `get_marginal_items`
- Unit tests in `crates/cupel/tests/budget_simulation.rs` cover: marginal items basic, marginal items with slack=0, monotonicity guard for QuotaSlice, find_min_budget basic, find_min_budget not found, find_min_budget monotonicity guard for QuotaSlice + CountQuotaSlice, precondition violations
- `cargo test --all-targets` passes

## Proof Level

- This slice proves: contract + integration (methods tested with real Pipeline + dry_run calls)
- Real runtime required: no (library — unit tests exercise real pipeline execution)
- Human/UAT required: no

## Verification

- `cargo test --all-targets` — all existing + new budget simulation tests pass
- `cargo clippy --all-targets -- -D warnings` — no new warnings
- `grep -q 'get_marginal_items\|find_min_budget_for' crates/cupel/tests/budget_simulation.rs` — test file exercises both methods

## Observability / Diagnostics

- Runtime signals: `CupelError::PipelineConfig(String)` for monotonicity guard violations — error messages match .NET parity
- Inspection surfaces: none (pure library function; errors are returned as `Result`)
- Failure visibility: error messages name the violated precondition and the slicer type constraint
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `Pipeline::dry_run` (existing), `Slicer` trait (extended with `is_quota`/`is_count_quota`), `ContextBudget::new` (existing)
- New wiring introduced in this slice: `Pipeline::get_marginal_items` and `Pipeline::find_min_budget_for` as public methods; `is_quota()`/`is_count_quota()` on `Slicer` trait
- What remains before the milestone is truly usable end-to-end: nothing — this is the final slice (S05) of M004

## Tasks

- [ ] **T01: Extend Slicer trait with is_quota/is_count_quota and add budget simulation methods** `est:45m`
  - Why: Implements the full feature — trait extension, both budget simulation methods, and all tests. The scope is manageable in one task because the methods are thin orchestration over `dry_run` (~80 lines total) plus trait additions (~10 lines) and tests (~150 lines).
  - Files: `crates/cupel/src/slicer/mod.rs`, `crates/cupel/src/slicer/quota.rs`, `crates/cupel/src/slicer/count_quota.rs`, `crates/cupel/src/pipeline/mod.rs`, `crates/cupel/src/lib.rs`, `crates/cupel/tests/budget_simulation.rs`
  - Do: Add `is_quota()` and `is_count_quota()` defaulted methods to `Slicer` trait; override in `QuotaSlice` and `CountQuotaSlice`; implement `get_marginal_items` and `find_min_budget_for` as `impl Pipeline` methods in `pipeline/mod.rs` (or a new `pipeline/budget_simulation.rs` submodule); use content-based matching via `HashMap<&str, usize>` (D113 pattern); write integration tests in `tests/budget_simulation.rs` covering basic behavior, edge cases, monotonicity guards, and precondition violations.
  - Verify: `cargo test --all-targets` passes; `cargo clippy --all-targets -- -D warnings` clean
  - Done when: Both methods exist on Pipeline, all monotonicity guards work, 7+ test cases pass

## Files Likely Touched

- `crates/cupel/src/slicer/mod.rs` — add `is_quota()`, `is_count_quota()` to `Slicer` trait
- `crates/cupel/src/slicer/quota.rs` — override `is_quota() → true`
- `crates/cupel/src/slicer/count_quota.rs` — override `is_count_quota() → true`
- `crates/cupel/src/pipeline/mod.rs` — implement `get_marginal_items` and `find_min_budget_for`
- `crates/cupel/src/lib.rs` — re-export if needed
- `crates/cupel/tests/budget_simulation.rs` — new integration test file
