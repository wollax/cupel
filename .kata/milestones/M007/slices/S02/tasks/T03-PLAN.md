---
estimated_steps: 3
estimated_files: 1
---

# T03: Make dry_run_with_policy integration tests pass

**Slice:** S02 — Rust Policy struct and dry_run_with_policy
**Milestone:** M007

## Description

With `Policy`, `PolicyBuilder`, and `Pipeline::dry_run_with_policy` implemented in T02, all 5 tests in `dry_run_with_policy.rs` should compile. This task runs the tests, diagnoses any failures, and fixes assertion sizing issues (e.g. budget token counts, item token counts) without weakening the behavioral contracts. Implementation bugs are fixed in `pipeline/mod.rs`; test setup bugs are fixed in `dry_run_with_policy.rs`.

The behavioral contracts that must remain intact:
- **scorer_is_respected**: the policy's scorer determines ranking, not the pipeline's
- **slicer_is_respected**: the policy's slicer algorithm is used, not the pipeline's
- **deduplication_false/true**: the policy's `deduplication` flag governs deduplication, not the pipeline's
- **overflow_strategy_is_respected**: the policy's `overflow_strategy` is used when placing pinned items

## Steps

1. Run `cargo test --test dry_run_with_policy 2>&1` and inspect output. Identify which tests fail and why (assertion failure vs. implementation bug).

2. For each failing test: if the failure is a numeric sizing issue (e.g. a `KnapsackSlice` test where `GreedySlice` accidentally produces the same selection), adjust item token counts and budget in the test fixture. Do not weaken the assertion (i.e. the test must still assert that the policy component is what drove the selection, not the pipeline component). For `overflow_strategy_is_respected`: confirm that "pinned" semantics work — in Cupel, items with `priority(-1)` or `priority <= -1` are classified as pinned by `classify::classify`. If the pinned mechanism is not straightforward to exercise, use an item whose token count exceeds `budget.target_tokens()` when pinned, triggering `OverflowStrategy` handling.

3. Run the full suite to confirm no regressions: `cargo test --all-targets` and `cargo clippy --all-targets -- -D warnings`.

## Must-Haves

- [ ] All 5 tests in `crates/cupel/tests/dry_run_with_policy.rs` report `ok`
- [ ] No existing tests broken (`cargo test --all-targets` exits 0)
- [ ] `cargo clippy --all-targets -- -D warnings` exits 0
- [ ] The `scorer_is_respected` test proves the policy scorer was used (not the pipeline's scorer) by asserting different `included` items between the two scorer strategies
- [ ] The `deduplication_false_allows_duplicates` test asserts `report.included.len() == 2` with two identical-content items and budget to fit both
- [ ] The `overflow_strategy_is_respected` test proves the policy `overflow_strategy` governs behavior, not the pipeline's

## Verification

```bash
cargo test --test dry_run_with_policy -- --nocapture
# All 5 tests: ok

cargo test --all-targets
# exit 0

cargo clippy --all-targets -- -D warnings
# exit 0
```

## Observability Impact

- Signals added/changed: None — test-only task
- How a future agent inspects this: `cargo test --test dry_run_with_policy` is the direct check
- Failure state exposed: Test names clearly identify the failing contract; `--nocapture` shows panic messages with assertion values

## Inputs

- `crates/cupel/tests/dry_run_with_policy.rs` — test file from T01 (may need fixture adjustments)
- `crates/cupel/src/pipeline/mod.rs` — `Policy`, `PolicyBuilder`, `dry_run_with_policy` from T02
- `crates/cupel/src/pipeline/classify.rs` — pinned item classification rules (to understand how to trigger overflow strategy)

## Expected Output

- `crates/cupel/tests/dry_run_with_policy.rs` — finalized test file; all 5 tests pass
- `cargo test --all-targets` exits 0
- `cargo clippy --all-targets -- -D warnings` exits 0
- S02 is complete: `Policy`, `PolicyBuilder`, `Pipeline::dry_run_with_policy` are public, correct, and tested
