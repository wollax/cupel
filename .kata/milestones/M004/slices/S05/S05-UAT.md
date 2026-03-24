# S05: Rust budget simulation parity — UAT

**Milestone:** M004
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: Pure library — no runtime service, UI, or user-facing flow. All behavior is mechanically verifiable via cargo test and cargo clippy. Integration tests exercise real Pipeline + dry_run calls with assertions on output.

## Preconditions

- Rust toolchain installed (`cargo`, `rustc`)
- Working directory at project root (`/Users/wollax/Git/personal/cupel`)

## Smoke Test

```bash
cd crates/cupel && cargo test budget_simulation -- --test-threads=1
```
All 9 tests should pass.

## Test Cases

### 1. Marginal items with budget reduction

1. Run `cargo test get_marginal_items_basic`
2. **Expected:** Test passes — items present at full budget but absent at reduced budget are returned.

### 2. Slack-zero short-circuit

1. Run `cargo test get_marginal_items_slack_zero`
2. **Expected:** Returns empty vec immediately without running any dry runs.

### 3. Monotonicity guard for QuotaSlice

1. Run `cargo test get_marginal_items_rejects_quota_slice`
2. **Expected:** Returns `CupelError::PipelineConfig` with message mentioning "QuotaSlice".

### 4. Find minimum budget via binary search

1. Run `cargo test find_min_budget_basic`
2. **Expected:** Returns `Some(budget)` where the target item is included; budget ≥ target.tokens().

### 5. Find minimum budget — not found

1. Run `cargo test find_min_budget_not_found`
2. **Expected:** Returns `None` when search ceiling is too low for target alongside higher-scored competitors.

### 6. Find minimum budget rejects quota slicers

1. Run `cargo test find_min_budget_rejects_quota_slice`
2. Run `cargo test find_min_budget_rejects_count_quota_slice`
3. **Expected:** Both return `CupelError::PipelineConfig`.

### 7. Precondition violations

1. Run `cargo test find_min_budget_target_not_in_items`
2. Run `cargo test find_min_budget_ceiling_below_tokens`
3. **Expected:** Both return `CupelError::InvalidBudget`.

## Edge Cases

### Empty items list

1. Call `get_marginal_items` with empty items slice.
2. **Expected:** Returns empty vec (no panic, no error).

### Single item that is always included

1. Call `find_min_budget_for` where target is the only item and ceiling equals target tokens.
2. **Expected:** Returns `Some(target.tokens())`.

## Failure Signals

- Any `cargo test budget_simulation` failure
- `cargo clippy --all-targets -- -D warnings` producing warnings in `pipeline/mod.rs` or `slicer/mod.rs`
- Missing `is_quota()` or `is_count_quota()` methods on Slicer trait
- `get_marginal_items` or `find_min_budget_for` not found as public methods on Pipeline

## Requirements Proved By This UAT

- R054 — Rust budget simulation parity: both methods implemented, monotonicity guards work, content-based matching, binary search with boundary checks, 9 integration tests pass

## Not Proven By This UAT

- .NET budget simulation behavior (already validated in prior milestones)
- Cross-language behavioral equivalence (would require conformance vectors; not in scope for M004)
- Performance characteristics under large item counts (no benchmark tests)

## Notes for Tester

- All tests are deterministic — no timing dependencies, no file I/O, no network.
- The `find_min_budget_for` `budget` parameter is accepted but unused (D069 API shape consistency). This is intentional.
- Content-based matching (Rust) differs from reference equality (.NET). This is by design (D113).
