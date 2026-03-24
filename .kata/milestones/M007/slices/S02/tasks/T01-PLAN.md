---
estimated_steps: 3
estimated_files: 1
---

# T01: Write failing integration tests for dry_run_with_policy

**Slice:** S02 — Rust Policy struct and dry_run_with_policy
**Milestone:** M007

## Description

Create `crates/cupel/tests/dry_run_with_policy.rs` with 5 integration tests that define the behavioral contracts for `Policy`, `PolicyBuilder`, and `Pipeline::dry_run_with_policy`. The tests will fail to compile because those types do not exist yet — that is the correct initial state (red phase). No implementation is done in this task.

The 5 contracts to verify:
1. **scorer_is_respected** — a policy with a different scorer than the host pipeline produces a different selection under tight budget
2. **slicer_is_respected** — a policy with a different slicer than the host pipeline selects different items (e.g. `KnapsackSlice` vs `GreedySlice` with unequal item token weights)
3. **deduplication_false_allows_duplicates** — two identical-content items both appear in `report.included` when `deduplication: false`
4. **deduplication_true_excludes_duplicates** — one of two identical-content items appears in `report.excluded` with `ExclusionReason::Deduplicated`
5. **overflow_strategy_is_respected** — policy with `OverflowStrategy::Throw` and an oversized pinned item returns an error; policy with `OverflowStrategy::Truncate` succeeds

## Steps

1. Create `crates/cupel/tests/dry_run_with_policy.rs` with the standard import block: `use std::collections::HashMap; use std::sync::Arc; use cupel::{Policy, PolicyBuilder, Pipeline, PipelineBuilder, ContextBudget, ContextItemBuilder, ContextKind, ExclusionReason, OverflowStrategy, ChronologicalPlacer, GreedySlice, KnapsackSlice, PriorityScorer, ReflexiveScorer};`

2. Write the 5 test functions. Each test uses `Pipeline::builder()` to construct the host pipeline (the pipeline whose `dry_run_with_policy` method is called), and `PolicyBuilder::new()` to construct the policy. Key sizing decisions:
   - For scorer test: 3 items at 40 tokens each, budget target=80 (fits 2); `PriorityScorer` policy vs `ReflexiveScorer` pipeline forces different rankings when priority and future_relevance_hint disagree
   - For slicer test: 3 items with unequal tokens (e.g. 30, 50, 60), budget target=80; `KnapsackSlice` policy fits the optimal pair while `GreedySlice` pipeline would pick a different pair
   - For deduplication tests: 2 items with identical content at 30 tokens, budget target=100
   - For overflow test: one pinned item (via `priority(-1)` triggering pinned classification — or use a dedicated oversized item); use `OverflowStrategy::Throw` and confirm `Err(...)` is returned

3. Verify the file is syntactically valid (no syntax errors) by running: `cargo check --test dry_run_with_policy 2>&1 | grep "^error\[E" | grep -v "cannot find" | head -10` — expect zero lines (only "cannot find" errors are acceptable at this stage).

## Must-Haves

- [ ] File `crates/cupel/tests/dry_run_with_policy.rs` exists with exactly 5 `#[test]` functions named: `scorer_is_respected`, `slicer_is_respected`, `deduplication_false_allows_duplicates`, `deduplication_true_excludes_duplicates`, `overflow_strategy_is_respected`
- [ ] Each test asserts on `SelectionReport` fields (`included`, `excluded`) or on the `Result` error variant — not just that the call returns `Ok`
- [ ] No syntax errors (only "not found" compile errors for `Policy`, `PolicyBuilder`, `dry_run_with_policy`)

## Verification

```bash
# Should compile-fail only on "cannot find" (Policy/PolicyBuilder/dry_run_with_policy):
cargo check --test dry_run_with_policy 2>&1 | grep "^error\[E" | grep -v "cannot find"
# Expect: zero lines
```

## Observability Impact

- Signals added/changed: None — test file only
- How a future agent inspects this: `cargo test --test dry_run_with_policy` surfaces which contracts pass/fail
- Failure state exposed: Each test name clearly identifies the failing contract

## Inputs

- `crates/cupel/tests/policy_sensitivity.rs` — pattern for integration test structure using `Pipeline`, `ContextItemBuilder`, `ContextBudget`
- `crates/cupel/src/lib.rs` — existing exports to know which types are already available
- S02-RESEARCH.md — confirms `Arc<dyn Scorer/Slicer/Placer>` is the settled choice (D149)

## Expected Output

- `crates/cupel/tests/dry_run_with_policy.rs` — new file with 5 failing test functions; compilation fails only on missing `Policy`, `PolicyBuilder`, `dry_run_with_policy` symbols
