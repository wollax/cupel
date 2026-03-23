---
estimated_steps: 10
estimated_files: 8
---

# T01: Add `CupelError::TableTooLarge`, change `Slicer::slice → Result`, add KnapsackSlice guard + flat keep table

**Slice:** S07 — Rust Quality Hardening
**Milestone:** M001

## Description

This task delivers R002 (KnapsackSlice DP table size guard). It makes `KnapsackSlice::slice` return `Err(CupelError::TableTooLarge)` when `capacity × n > 50_000_000` before allocating the DP table, preventing unchecked OOM. As a prerequisite, `Slicer::slice` must return `Result<Vec<ContextItem>, CupelError>` — the trait change threads through `GreedySlice`, `KnapsackSlice`, `QuotaSlice`, `pipeline::slice_items`, and both `run`/`run_traced` call sites. The flat `Vec<bool>` keep table replaces the nested `Vec<Vec<bool>>` for a single-allocation DP table.

**Semver note:** Changing `Slicer::slice` from `→ Vec<ContextItem>` to `→ Result<Vec<ContextItem>, CupelError>` is a breaking change for any downstream implementor. This is accepted for v1.2.0; all three built-in impls are in-crate and the break is compile-time-visible.

## Steps

1. **Add `TableTooLarge` to `CupelError`** (`src/error.rs`): Add variant `#[error("KnapsackSlice DP table exceeds the 50,000,000-cell limit: candidates={candidates}, capacity={capacity}, cells={cells}")] TableTooLarge { candidates: usize, capacity: usize, cells: u64 }` after `SlicerConfig`.

2. **Change `Slicer` trait** (`src/slicer/mod.rs`): Change `fn slice(&self, sorted: &[ScoredItem], budget: &ContextBudget) -> Vec<ContextItem>;` to `fn slice(&self, sorted: &[ScoredItem], budget: &ContextBudget) -> Result<Vec<ContextItem>, CupelError>;`. Add `use crate::CupelError;` import to mod.rs. Update the doc-test example: change `let selected = GreedySlice.slice(&items, &budget);` to `let selected = GreedySlice.slice(&items, &budget)?;` and update `assert_eq!(selected.len(), 1);` (stays the same).

3. **Update `GreedySlice::slice`** (`src/slicer/greedy.rs`): Change return type to `Result<Vec<ContextItem>, CupelError>`. Wrap all early `return Vec::new();` statements as `return Ok(Vec::new());`. Wrap final return value `result` as `Ok(result)`. Add `use crate::CupelError;` import.

4. **Update `KnapsackSlice::slice`** (`src/slicer/knapsack.rs`):
   - Change return type to `Result<Vec<ContextItem>, CupelError>`.
   - Wrap early returns: `return Ok(Vec::new());`, `return Ok(zero_token_items);`.
   - After computing `capacity` in Step 3, add the guard:
     ```rust
     let cells = (capacity as u64) * (n as u64);
     if cells > 50_000_000 {
         return Err(CupelError::TableTooLarge { candidates: n, capacity, cells });
     }
     ```
   - Replace `let mut keep = vec![vec![false; capacity + 1]; n];` with flat allocation:
     ```rust
     let stride = capacity + 1;
     let mut keep = vec![false; n * stride];
     ```
   - In the DP inner loop, replace `keep[i][w] = true;` with `keep[i * stride + w] = true;`.
   - In the reconstruction loop, replace `if keep[i][remaining_capacity]` with `if keep[i * stride + remaining_capacity]`.
   - Wrap final return as `Ok(result)`.
   - Update the doc-test example: `let selected = slicer.slice(&items, &budget)?;`.
   - Add `#[cfg(test)]` module with test `knapsack_table_too_large` (see Step 10 below).

5. **Update `QuotaSlice::slice`** (`src/slicer/quota.rs`):
   - Change return type to `Result<Vec<ContextItem>, CupelError>`.
   - Change `let selected = self.inner.slice(items, &sub_budget);` to `let selected = self.inner.slice(items, &sub_budget)?;`.
   - Wrap early returns: `return Ok(Vec::new());`.
   - Wrap final return: `Ok(all_selected)`.
   - Add `use crate::CupelError;` import.
   - Update the doc-test example: `let selected = slicer.slice(&items, &budget)?;`.

6. **Update `pipeline::slice_items`** (`src/pipeline/slice.rs`):
   - Change `slice_items` return type to `Result<Vec<ContextItem>, CupelError>`.
   - Change the body to: `let adjusted = compute_effective_budget(budget, pinned_tokens); slicer.slice(sorted, &adjusted)`.
   - Add `use crate::CupelError;` import.

7. **Update `pipeline/mod.rs` call sites**: At lines 148 and 287, change `let sliced = slice::slice_items(...)` to `let sliced = slice::slice_items(...)?`. Both call sites are already inside functions that return `Result<_, CupelError>`, so `?` propagation is clean.

8. **Update conformance test harness** (`tests/conformance/slicing.rs`): Change `let selected = slicer.slice(&scored_items, &budget);` to `let selected = slicer.slice(&scored_items, &budget).expect("conformance vector slicing should not error");`.

9. **Verify compile**: Run `cargo test --manifest-path crates/cupel/Cargo.toml` and `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings`.

10. **Add `knapsack_table_too_large` unit test** in `src/slicer/knapsack.rs` `#[cfg(test)]` block:
    - Create a `KnapsackSlice::new(1)?` (bucket_size=1 so capacity = target_tokens directly).
    - Create `n = 1001` items each with 1 token and score 0.5.
    - Use a budget with `target_tokens = 50_001` (so capacity = 50_001 and `50_001 × 1001 > 50_000_000`).
    - Assert the result is `Err(CupelError::TableTooLarge { candidates: 1001, capacity: 50001, cells: c })` where `c > 50_000_000`.

## Must-Haves

- [ ] `CupelError::TableTooLarge { candidates, capacity, cells }` variant present in `error.rs`
- [ ] `Slicer::slice` returns `Result<Vec<ContextItem>, CupelError>` in the trait definition
- [ ] `KnapsackSlice::slice` returns `Err(TableTooLarge)` when `(capacity as u64) * (n as u64) > 50_000_000`
- [ ] Guard uses discretized `capacity` (not raw `target_tokens`) — same value used for DP table
- [ ] Flat `Vec<bool>` keep table with `stride = capacity + 1`; index reads/writes use `i * stride + w`
- [ ] `QuotaSlice::slice` propagates error from `inner.slice(...)` via `?`
- [ ] `pipeline::slice_items` returns `Result`; both `run`/`run_traced` call sites use `?`
- [ ] All 6 conformance slicing tests still pass (with `.expect()` in harness)
- [ ] `knapsack_table_too_large` unit test passes
- [ ] `cargo clippy --all-targets -- -D warnings` exits 0

## Verification

```bash
cargo test --manifest-path crates/cupel/Cargo.toml
cargo test --manifest-path crates/cupel/Cargo.toml -- knapsack_table_too_large --nocapture
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
```

All three must exit 0. The `knapsack_table_too_large` test should print nothing to stderr (clean pass).

## Observability Impact

- Signals added/changed: New `CupelError::TableTooLarge` error variant with three diagnostic fields (`candidates`, `capacity`, `cells`). Callers get structured data without parsing strings.
- How a future agent inspects this: `cargo test -- knapsack_table_too_large` confirms the guard fires; `CupelError::TableTooLarge { candidates, capacity, cells }` match arm extracts the fields programmatically.
- Failure state exposed: When the guard fires, the exact cell count is captured — callers can log it or display it to users to explain why their budget/item-count combination is rejected.

## Inputs

- `crates/cupel/src/error.rs` — `CupelError` enum; `#[non_exhaustive]`; add `TableTooLarge` after `SlicerConfig`
- `crates/cupel/src/slicer/mod.rs` — `Slicer` trait; change `fn slice` return type
- `crates/cupel/src/slicer/knapsack.rs` — current DP implementation; `capacity = (budget.target_tokens() / self.bucket_size) as usize` is the discretized value to use in the guard
- `crates/cupel/src/slicer/quota.rs` — `self.inner.slice(items, &sub_budget)` call at line 275
- `crates/cupel/src/pipeline/slice.rs` — `slice_items` calls `slicer.slice` at line 35
- `crates/cupel/src/pipeline/mod.rs` — two `slice_items` call sites at lines 148 and 287
- `.NET guard reference`: `(capacity as u64) * (n as u64) > 50_000_000` using u64 arithmetic to avoid overflow; error message format: `candidates=N, capacity=C, cells=K`

## Expected Output

- `crates/cupel/src/error.rs` — `TableTooLarge` variant added
- `crates/cupel/src/slicer/mod.rs` — `Slicer::slice` returns `Result<Vec<ContextItem>, CupelError>`
- `crates/cupel/src/slicer/greedy.rs` — `Ok(...)` wrapping, `Result` return type
- `crates/cupel/src/slicer/knapsack.rs` — guard before DP allocation, flat `Vec<bool>`, `#[cfg(test)]` with `knapsack_table_too_large`
- `crates/cupel/src/slicer/quota.rs` — `inner.slice(...)?` propagation, `Result` return type
- `crates/cupel/src/pipeline/slice.rs` — `Result` return type
- `crates/cupel/src/pipeline/mod.rs` — `?` on both `slice_items` call sites
- `crates/cupel/tests/conformance/slicing.rs` — `.expect()` on `slicer.slice()` call
