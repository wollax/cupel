---
id: T01
parent: S07
milestone: M001
provides:
  - CupelError::TableTooLarge variant with candidates/capacity/cells fields
  - Slicer::slice trait method returns Result<Vec<ContextItem>, CupelError>
  - KnapsackSlice DP table size guard (50_000_000-cell limit)
  - Flat Vec<bool> keep table replacing nested Vec<Vec<bool>>
  - knapsack_table_too_large unit test
key_files:
  - crates/cupel/src/error.rs
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/slicer/greedy.rs
  - crates/cupel/src/slicer/knapsack.rs
  - crates/cupel/src/slicer/quota.rs
  - crates/cupel/src/pipeline/slice.rs
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/tests/conformance/slicing.rs
key_decisions:
  - Guard uses discretized capacity (budget.target_tokens() / bucket_size), not raw target_tokens, matching what the DP table actually allocates
  - Flat Vec<bool> with stride = capacity + 1 replaces nested Vec<Vec<bool>> for a single allocation
patterns_established:
  - Slicer::slice returns Result; callers use ? propagation; conformance tests use .expect()
observability_surfaces:
  - CupelError::TableTooLarge { candidates, capacity, cells } — structured error with all three diagnostic fields; inspect via `cargo test -- knapsack_table_too_large`
duration: ~15 minutes
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T01: Add `CupelError::TableTooLarge`, change `Slicer::slice → Result`, add KnapsackSlice guard + flat keep table

**Added `CupelError::TableTooLarge`, changed `Slicer::slice` to return `Result`, and added a 50M-cell DP table size guard with a flat `Vec<bool>` keep table to `KnapsackSlice`.**

## What Happened

All steps executed in sequence without deviations:

1. Added `CupelError::TableTooLarge { candidates: usize, capacity: usize, cells: u64 }` to `error.rs` after `SlicerConfig`.
2. Changed `Slicer::slice` trait signature to `-> Result<Vec<ContextItem>, CupelError>` in `slicer/mod.rs`; added `use crate::CupelError` import and updated the module-level doc-test.
3. Updated `GreedySlice::slice` to return `Result`, wrapping early returns and final result in `Ok(...)`.
4. Updated `KnapsackSlice::slice`: changed return type, added OOM guard `if cells > 50_000_000 { return Err(CupelError::TableTooLarge {...}) }` before allocation, replaced nested `Vec<Vec<bool>>` with flat `Vec<bool>` using `stride = capacity + 1`, updated all index reads/writes to `i * stride + w`, wrapped all returns in `Ok(...)`. Added `#[cfg(test)]` block with `knapsack_table_too_large` test.
5. Updated `QuotaSlice::slice`: changed return type, propagated inner slicer error with `?`, wrapped returns in `Ok(...)`.
6. Updated `pipeline::slice_items` to return `Result<Vec<ContextItem>, CupelError>`.
7. Added `?` to both `slice_items` call sites in `pipeline/mod.rs` (lines 148 and 287).
8. Updated conformance test harness to use `.expect("conformance vector slicing should not error")`.

## Verification

```
cargo test --manifest-path crates/cupel/Cargo.toml
# Result: 35 passed; 0 failed (including all 6 conformance slicing tests + all doc-tests)

cargo test --manifest-path crates/cupel/Cargo.toml -- knapsack_table_too_large --nocapture
# Result: 1 passed; 0 failed; clean stderr

cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
# Result: Finished with 0 warnings, exit 0
```

## Diagnostics

- `CupelError::TableTooLarge { candidates, capacity, cells }` — structured error; match arm extracts all three fields programmatically.
- Inspect the guard: `cargo test -- knapsack_table_too_large` confirms it fires at capacity=50_001, n=1001, cells=50_051_001.
- `CupelError` implements `Display` via thiserror: callers see `"KnapsackSlice DP table exceeds the 50,000,000-cell limit: candidates=1001, capacity=50001, cells=50051001"`.

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/error.rs` — Added `TableTooLarge { candidates, capacity, cells }` variant
- `crates/cupel/src/slicer/mod.rs` — Changed `Slicer::slice` return type; updated doc-test
- `crates/cupel/src/slicer/greedy.rs` — `Result` return type; `Ok(...)` wrapping
- `crates/cupel/src/slicer/knapsack.rs` — Guard, flat keep table, `Result` return, `#[cfg(test)]` with `knapsack_table_too_large`
- `crates/cupel/src/slicer/quota.rs` — `Result` return type; `inner.slice(...)?` propagation
- `crates/cupel/src/pipeline/slice.rs` — `slice_items` returns `Result`
- `crates/cupel/src/pipeline/mod.rs` — `?` on both `slice_items` call sites
- `crates/cupel/tests/conformance/slicing.rs` — `.expect()` on `slicer.slice()` call
