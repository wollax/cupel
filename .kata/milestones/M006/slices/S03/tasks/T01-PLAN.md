---
estimated_steps: 5
estimated_files: 2
---

# T01: Rust composition integration test

**Slice:** S03 — Integration proof + summaries
**Milestone:** M006

## Description

Write a new Rust integration test file that chains `CountQuotaSlice(inner: QuotaSlice(inner: GreedySlice))` and runs a real `dry_run()` call. This proves the only composition not yet exercised in S01: a count-quota layer wrapping a percentage-quota layer. The test must assert that count-cap exclusions appear in `report.excluded` — proving the two policies interact correctly without panics or constraint conflicts.

No production code is written in this task. This is exclusively a new test file.

## Steps

1. Check whether `crates/cupel/Cargo.toml` auto-discovers tests from `tests/` (look for `[[test]]` section or absence thereof — if tests already use auto-discovery from `tests/*.rs`, no Cargo.toml change needed). Verify by checking if existing test files like `quota_utilization.rs` are registered.

2. Create `crates/cupel/tests/count_quota_composition.rs`. Add the standard imports matching `quota_utilization.rs`: `use cupel::{ChronologicalPlacer, ContextBudget, ContextItemBuilder, ContextKind, CountQuotaEntry, CountQuotaSlice, GreedySlice, OverflowStrategy, Pipeline, QuotaEntry, QuotaSlice, ReflexiveScorer, ScarcityBehavior};` plus `use std::collections::HashMap;`. Add the `kind()` and `budget()` helper functions identical to those in `quota_utilization.rs`.

3. Write the test function `count_quota_composition_quota_slice_inner`:
   - Create quota entries for `QuotaSlice` inner slicer: `QuotaEntry::new(kind("ToolOutput"), 10.0, 60.0)` (ToolOutput require 10% cap 60% of target_tokens)
   - Create count entries for `CountQuotaSlice` outer: `CountQuotaEntry::new(kind("ToolOutput"), 1, 2)` (require 1, cap 2)
   - Construct `QuotaSlice::new(vec![quota_entry], Box::new(GreedySlice)).unwrap()` then `CountQuotaSlice::new(vec![count_entry], Box::new(quota_slicer), ScarcityBehavior::Degrade).unwrap()`
   - Build pipeline: `Pipeline::builder().scorer(Box::new(ReflexiveScorer)).slicer(Box::new(slicer)).placer(Box::new(ChronologicalPlacer)).overflow_strategy(OverflowStrategy::Throw).build().unwrap()`
   - Items: 3 ToolOutput items (100 tokens each, FutureRelevanceHint 0.9/0.7/0.5) + 2 Message items (100 tokens each, hints 0.8/0.6). Budget 400 tokens max/target.
   - Run `pipeline.dry_run(&items, &b).unwrap()`
   - Assertions: (a) `assert!(report.included.iter().filter(|i| i.item.kind() == &kind("ToolOutput")).count() <= 2, "count cap=2 must hold");` (b) `assert!(report.excluded.iter().any(|e| matches!(e.reason, cupel::ExclusionReason::CountCapExceeded { .. })), "at least one item must be cap-excluded");`

4. Run `cargo test --all-targets 2>&1 | grep -E "count_quota_composition|FAILED|error"` to verify the new test passes.

5. Run `cargo clippy --all-targets -- -D warnings` and fix any warnings (use `std::slice::from_ref` for single-item slice args if needed per D038; ensure no unused imports).

## Must-Haves

- [ ] `crates/cupel/tests/count_quota_composition.rs` exists with at least one `#[test]` function
- [ ] The test constructs `CountQuotaSlice(QuotaSlice(GreedySlice))` — the outer slicer wrapping the inner
- [ ] `pipeline.dry_run()` completes without panic or error
- [ ] `report.excluded` contains at least one item with `ExclusionReason::CountCapExceeded { .. }` (matched via `matches!()`)
- [ ] The count-cap assertion `included ToolOutput ≤ 2` passes
- [ ] `cargo test --all-targets` exits 0 (all tests pass including this new one)
- [ ] `cargo clippy --all-targets -- -D warnings` exits 0

## Verification

- `cargo test --all-targets 2>&1 | grep "count_quota_composition"` → shows test listed as `ok`
- `cargo clippy --all-targets -- -D warnings` → exits 0, no output
- `cargo test --all-targets 2>&1 | grep "FAILED"` → no output

## Observability Impact

- Signals added/changed: None — test-only file; no production code changed
- How a future agent inspects this: `cargo test -- --nocapture count_quota_composition` prints full assertion details on failure
- Failure state exposed: If the composition panics, the test output will include the panic message from `unwrap()` at the construction site; if the assertion fails, the message includes the actual count and excluded reasons

## Inputs

- `crates/cupel/tests/quota_utilization.rs` — copy the `kind()`, `budget()` helpers and the `Pipeline::builder()` pattern
- `crates/cupel/tests/conformance.rs` — reference for `CountQuotaSlice` construction in Rust integration tests
- S01 summary / S02 summary — confirmed both implementations working; `CountCapExceeded` observable in `report.excluded`

## Expected Output

- `crates/cupel/tests/count_quota_composition.rs` — new test file (~60 lines) with one passing integration test
- `cargo test --all-targets` baseline rises by 1 test
