# S02: Rust Policy struct and dry_run_with_policy — UAT

**Milestone:** M007
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All behavioral contracts are verified by deterministic integration tests exercising real pipeline execution. There is no UI, no network boundary, and no human-perceptible runtime state — `cargo test` is the authoritative oracle.

## Preconditions

- Rust toolchain installed (`cargo`, `clippy`)
- Working directory: `crates/cupel/` or project root

## Smoke Test

```bash
cargo test --test dry_run_with_policy
```

Expected: `5 passed; 0 failed`

## Test Cases

### 1. scorer_is_respected

1. Build a `Policy` via `PolicyBuilder` with `PriorityScorer` (priorities: C=10, B=5, A=1), `GreedySlice`, `SequentialPlacer`, `deduplication: false`.
2. Build a host `Pipeline` with `ReflexiveScorer`, `GreedySlice`, `SequentialPlacer`.
3. Call `pipeline.dry_run_with_policy(&items, &budget, &policy)` with a tight budget (fits 2 of 3 items).
4. **Expected:** `report.included` contains C and B (policy scorer drives selection), not A and B (pipeline scorer would drive).

### 2. slicer_is_respected

1. Build a `Policy` with `ReflexiveScorer`, `KnapsackSlice::new(1)`, `SequentialPlacer`.
2. Build a host `Pipeline` with `ReflexiveScorer`, `GreedySlice`, `SequentialPlacer`.
3. Call `pipeline.dry_run_with_policy(&items, &budget, &policy)` with items of unequal token weights.
4. **Expected:** Items selected differ from those the greedy pipeline would select — Knapsack optimizes total value within budget; Greedy picks by score order.

### 3. deduplication_false_allows_duplicates

1. Build a `Policy` with `deduplication: false`; two items with identical content.
2. Call `pipeline.dry_run_with_policy(&items, &budget, &policy)`.
3. **Expected:** `report.included.len() == 2` — both duplicate items are included.

### 4. deduplication_true_excludes_duplicates

1. Build a `Policy` with `deduplication: true` (default); two items with identical content.
2. Call `pipeline.dry_run_with_policy(&items, &budget, &policy)`.
3. **Expected:** One item in `report.included`; one item in `report.excluded` with `ExclusionReason::Deduplicated`.

### 5. overflow_strategy_is_respected — Throw path

1. Build a `Policy` with `overflow_strategy: OverflowStrategy::Throw`; include a pinned item of 110 tokens with budget target=100, max=200.
2. Call `pipeline.dry_run_with_policy(&items, &budget, &policy)`.
3. **Expected:** Returns `Err(CupelError::Overflow)`.

### 6. overflow_strategy_is_respected — Truncate path

1. Same setup as above but `overflow_strategy: OverflowStrategy::Truncate`.
2. Call `pipeline.dry_run_with_policy(&items, &budget, &policy)`.
3. **Expected:** Returns `Ok(report)` — truncation applied, no error.

## Edge Cases

### PolicyBuilder missing required fields

1. Call `PolicyBuilder::new().build()` (no scorer/slicer/placer set).
2. **Expected:** Returns `Err(CupelError::PipelineConfig)` with message `"scorer is required"`.

### Policy with non-default overflow_strategy doesn't affect host pipeline

1. Call `pipeline.dry_run(&items)` on a pipeline with `OverflowStrategy::Throw` after `dry_run_with_policy` with `OverflowStrategy::Truncate`.
2. **Expected:** Pipeline's own `overflow_strategy` is unaffected — `run_with_components` receives injected values, not mutating `self`.

## Failure Signals

- Any of the 5 named tests in `dry_run_with_policy.rs` reporting `FAILED`
- `cargo clippy --all-targets -- -D warnings` reporting warnings or errors
- `cargo test --all-targets` count drops below 164 (regression)

## Requirements Proved By This UAT

- R056 (partial) — Rust `Policy`, `PolicyBuilder`, and `Pipeline::dry_run_with_policy` are implemented and behave correctly across scorer, slicer, deduplication, and overflow_strategy dimensions. Proves the Rust `dry_run_with_policy` component of R056.

## Not Proven By This UAT

- R056 full validation — `policy_sensitivity` free function, `PolicySensitivityReport`, `PolicySensitivityDiffEntry` types, and the spec chapter at `spec/src/analytics/policy-sensitivity.md` are S03's scope. R056 cannot be marked validated until S03 completes.
- .NET `DryRunWithPolicy` and policy-accepting `PolicySensitivity` overload — those are S01's scope (already complete).
- No human/UAT review required — this slice has no UI, no runtime deployment, and no human-perceptible behavior beyond test output.

## Notes for Tester

- `KnapsackSlice::with_default_bucket_size()` (bucket_size=100) will silently produce empty results if budget target < 100. Use `KnapsackSlice::new(1)` for tight-budget tests.
- The host pipeline's own scorer/slicer/placer/deduplication/overflow_strategy fields are entirely bypassed by `dry_run_with_policy` — only the `Pipeline` instance's internal infrastructure (stage wiring, collector setup) is reused.
- `Policy` fields are `pub(crate)` — external callers must use `PolicyBuilder` to construct policies; direct struct initialization is not available.
