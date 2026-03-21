# S07: Rust Quality Hardening

**Goal:** Resolve ~10-15 high-signal Rust issues: add `CupelError::TableTooLarge` + KnapsackSlice DP guard, change `Slicer::slice` to `Result`, remove the ineffective CompositeScorer cycle detection and `Scorer::as_any`, refactor `UShapedPlacer` to eliminate `Vec<Option>`, add targeted unit tests, and scope release-rust.yml permissions to job level.
**Demo:** `cargo test --manifest-path crates/cupel/Cargo.toml` passes; `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0; `KnapsackSlice::slice` returns `Err(CupelError::TableTooLarge)` when `capacity × n > 50_000_000`; `Scorer` trait has no `as_any`; `UShapedPlacer::place` uses explicit left/right vecs.

## Must-Haves

- `CupelError::TableTooLarge { candidates, capacity, cells }` variant added (non-breaking due to `#[non_exhaustive]`)
- `Slicer::slice` returns `Result<Vec<ContextItem>, CupelError>` (semver-breaking — acknowledged for v1.2.0)
- `KnapsackSlice::slice` returns `Err(TableTooLarge)` when `(capacity as u64) * (n as u64) > 50_000_000`; uses flat `Vec<bool>` for DP keep table
- `QuotaSlice::slice` propagates errors from `inner.slice(...)` via `?`
- `pipeline::slice_items` propagates `Result`; both `run` and `run_traced` call sites use `?`
- `Scorer` trait has no `as_any` method; all 8 `impl Scorer` blocks remove the boilerplate
- `CompositeScorer` cycle detection logic removed; `CycleDetected` variant kept as reserved/never-emitted
- `UShapedPlacer::place` uses explicit left/right `Vec<ContextItem>` (no `Vec<Option>`)
- Unit tests added: `knapsack_table_too_large` (in `src/slicer/knapsack.rs`), `UShapedPlacer` 0/1/2/3/4 items, scorer edge cases, pipeline boundary cases
- `release-rust.yml` permissions scoped to job level (`test` → `contents: read`, `publish` → `contents: write`)
- `cargo clippy --all-targets -- -D warnings` exits 0 after all changes

## Proof Level

- This slice proves: contract verification
- Real runtime required: no — pure library; `cargo test` is the full verification surface
- Human/UAT required: no

## Verification

All checks must exit 0:

```bash
# After T01
cargo test --manifest-path crates/cupel/Cargo.toml
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings

# After T02
cargo test --manifest-path crates/cupel/Cargo.toml
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings

# After T03 (full slice verification)
cargo test --manifest-path crates/cupel/Cargo.toml
cargo test --features serde --manifest-path crates/cupel/Cargo.toml
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
cargo clippy --features serde --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
cargo deny check  # from crates/cupel/
```

Specific test assertions:
- `KnapsackSlice::slice` with `capacity × n > 50_000_000` → returns `Err(CupelError::TableTooLarge { .. })` — verified by `knapsack_table_too_large` unit test in `src/slicer/knapsack.rs`
- `UShapedPlacer::place` with 0, 1, 2, 3, 4 items — verified by `#[cfg(test)]` block in `src/placer/u_shaped.rs`
- All 6 existing conformance slicing tests still pass after `slicer.slice()` returns `Result`

## Observability / Diagnostics

- Runtime signals: `CupelError::TableTooLarge { candidates, capacity, cells }` — all three fields available at error site; message format `"KnapsackSlice DP table exceeds the 50,000,000-cell limit: candidates={n}, capacity={capacity}, cells={cells}"`
- Inspection surfaces: `cargo test -- --nocapture 2>&1 | grep TableTooLarge` to confirm guard fires; `cargo clippy --all-targets` as post-change lint gate
- Failure visibility: `CupelError` implements `Display` via thiserror — callers see the full message with field values; `#[non_exhaustive]` means downstream match arms stay valid
- Redaction constraints: none — only token counts and table dimensions in error messages (no content)

## Integration Closure

- Upstream surfaces consumed: `CupelError` (`error.rs`), `Slicer` trait (`slicer/mod.rs`), `pipeline/mod.rs` run/run_traced call sites, `S05` CI baseline (`cargo clippy --all-targets` clean)
- New wiring introduced in this slice: `Slicer::slice → Result` propagates through `QuotaSlice`, `pipeline::slice_items`, and into `run`/`run_traced` via `?`; `KnapsackSlice` guard fires before DP allocation
- What remains before the milestone is truly usable end-to-end: nothing — S07 completes M001's final active requirements (R002, R005); all milestone DoD criteria met after this slice

## Tasks

- [x] **T01: Add `CupelError::TableTooLarge`, change `Slicer::slice → Result`, add KnapsackSlice guard + flat keep table** `est:45m`
  - Why: R002 primary delivery; eliminates the unchecked DP table allocation that can OOM at large inputs; the `Result` return type is the prerequisite for all slicer error propagation
  - Files: `crates/cupel/src/error.rs`, `crates/cupel/src/slicer/mod.rs`, `crates/cupel/src/slicer/greedy.rs`, `crates/cupel/src/slicer/knapsack.rs`, `crates/cupel/src/slicer/quota.rs`, `crates/cupel/src/pipeline/slice.rs`, `crates/cupel/src/pipeline/mod.rs`, `crates/cupel/tests/conformance/slicing.rs`
  - Do: (1) Add `TableTooLarge { candidates: usize, capacity: usize, cells: u64 }` to `CupelError` with `#[error("KnapsackSlice DP table exceeds the 50,000,000-cell limit: candidates={candidates}, capacity={capacity}, cells={cells}")]`. (2) Change `Slicer::slice` signature to `fn slice(&self, sorted: &[ScoredItem], budget: &ContextBudget) -> Result<Vec<ContextItem>, CupelError>`. (3) Update `GreedySlice::slice` body to wrap return value in `Ok(...)`. (4) In `KnapsackSlice::slice`: after computing `capacity`, add guard `let cells = (capacity as u64) * (n as u64); if cells > 50_000_000 { return Err(CupelError::TableTooLarge { candidates: n, capacity, cells }); }`; replace `vec![vec![false; capacity + 1]; n]` keep table with flat `vec![false; n * (capacity + 1)]` where stride = `capacity + 1`; update keep table read/write to use `keep[i * stride + w]`; update reconstruction loop to use `keep[i * stride + remaining_capacity]`; wrap return in `Ok(...)`. (5) Change `QuotaSlice::slice`: change `self.inner.slice(items, &sub_budget)` call to `self.inner.slice(items, &sub_budget)?`; change return type to `Result<Vec<ContextItem>, CupelError>`; wrap final `all_selected` return in `Ok(...)`. (6) Change `pipeline::slice_items` signature and body to return `Result<Vec<ContextItem>, CupelError>`: `slicer.slice(sorted, &adjusted)`. (7) In `pipeline/mod.rs` at lines 148 and 287, change `slice::slice_items(...)` to `slice::slice_items(...)?`. (8) In `tests/conformance/slicing.rs`, change `let selected = slicer.slice(&scored_items, &budget);` to `let selected = slicer.slice(&scored_items, &budget).expect("conformance vector slicing should not error");`. (9) Update doc-test examples in `slicer/mod.rs`, `slicer/knapsack.rs`, `slicer/quota.rs` that call `.slice()` without `?` to add `?`. (10) Add `#[cfg(test)]` block to `src/slicer/knapsack.rs` with test `knapsack_table_too_large` that creates a KnapsackSlice, constructs enough items to exceed 50M cells, and asserts the result is `Err(CupelError::TableTooLarge { .. })`.
  - Verify: `cargo test --manifest-path crates/cupel/Cargo.toml` passes; `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0; `cargo test -- knapsack_table_too_large` exits 0
  - Done when: All 6 conformance slicing tests pass with `.expect()` unwrap; `knapsack_table_too_large` test passes; clippy clean; `Slicer::slice` returns `Result` in the public API

- [x] **T02: Remove `CompositeScorer` cycle detection and `Scorer::as_any`** `est:30m`
  - Why: The DFS cycle detection cannot fire (owned `Box<dyn Scorer>` prevents structural cycles); `as_any` pollutes the public `Scorer` trait with hidden boilerplate that every downstream implementor must copy; removing both is a net simplification with no loss of protection
  - Files: `crates/cupel/src/scorer/mod.rs`, `crates/cupel/src/scorer/composite.rs`, `crates/cupel/src/scorer/frequency.rs`, `crates/cupel/src/scorer/kind.rs`, `crates/cupel/src/scorer/priority.rs`, `crates/cupel/src/scorer/recency.rs`, `crates/cupel/src/scorer/reflexive.rs`, `crates/cupel/src/scorer/scaled.rs`, `crates/cupel/src/scorer/tag.rs`, `crates/cupel/src/error.rs`
  - Do: (1) In `scorer/mod.rs`: remove `use std::any::Any;`; remove the `fn as_any(&self) -> &dyn Any;` method from `Scorer` trait (including `#[doc(hidden)]` line). (2) In `scorer/composite.rs`: remove `use std::any::Any;` and `use std::collections::HashSet;`; remove `scorer_identity` free function; remove `detect_cycles_dfs` free function; remove the cycle detection loop in `CompositeScorer::new` (the 3-line block calling `detect_cycles_dfs`); remove `pub(crate) fn children(&self)` method; remove `fn as_any` from `impl Scorer for CompositeScorer`; add doc comment on the struct explaining why cycles are impossible: `/// Cycles are structurally impossible: children are stored as owned \`Box<dyn Scorer>\`, so no two CompositeScorer instances can share a child — a child cannot reference its own ancestor.` (3) In `scorer/scaled.rs`: remove `use std::any::Any;`; remove `pub(crate) fn inner(&self)` method (no longer called); remove `fn as_any` from `impl Scorer for ScaledScorer`. (4) In the remaining 6 scorer files (`frequency.rs`, `kind.rs`, `priority.rs`, `recency.rs`, `reflexive.rs`, `tag.rs`): remove `use std::any::Any;` and remove the `fn as_any(&self) -> &dyn Any { self }` method from each `impl Scorer` block. (5) In `error.rs`: update the `CycleDetected` variant doc to `/// Never emitted. Cycles are structurally impossible with owned \`Box<dyn Scorer>\` children. Reserved for future use.` — do NOT remove the variant (removing it would be a semver break for downstream match arms).
  - Verify: `cargo test --manifest-path crates/cupel/Cargo.toml` passes (all existing tests still compile and pass); `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0; `grep -r "as_any" crates/cupel/src/` returns no results; `grep -r "detect_cycles_dfs" crates/cupel/src/` returns no results
  - Done when: `Scorer` trait compiles without `as_any`; `CompositeScorer::new` still validates empty/zero/infinite weights but no longer calls cycle detection; `CycleDetected` variant stays in `CupelError` with updated doc; clippy clean

- [x] **T03: Refactor `UShapedPlacer`, add batch unit tests, scope release-rust.yml permissions** `est:45m`
  - Why: `UShapedPlacer` `Vec<Option>` + `.expect()` is fragile and untested at edge cases; scorer and pipeline test gaps reduce confidence in correctness; release workflow has overly-broad permissions; all are bounded fixes that complete R005
  - Files: `crates/cupel/src/placer/u_shaped.rs`, `crates/cupel/src/scorer/tag.rs`, `crates/cupel/src/scorer/priority.rs`, `crates/cupel/src/scorer/scaled.rs`, `crates/cupel/src/scorer/reflexive.rs`, `crates/cupel/src/pipeline/mod.rs`, `.github/workflows/release-rust.yml`
  - Do: (1) Refactor `UShapedPlacer::place`: replace `Vec<Option<ContextItem>>` + pointer approach with explicit `left: Vec<ContextItem>` and `right: Vec<ContextItem>` — even-ranked items pushed to `left`, odd-ranked items inserted at position 0 of `right` (or equivalently pushed and reversed); chain `left.into_iter().chain(right.into_iter())` for the final result; no `.expect()` anywhere. (2) Add `#[cfg(test)]` block to `u_shaped.rs` with tests: `place_zero_items` (assert empty), `place_one_item` (assert single item returned), `place_two_items` (higher-scored at index 0, lower at index 1), `place_three_items` (highest at 0, second-highest at 2, lowest at 1), `place_four_items` (verify U-shape: highest at 0, third at 1 (left side), second-highest at 3, fourth at 2 (right side, reversed)). (3) Add `#[cfg(test)]` block to `scorer/tag.rs` with tests: `tag_scorer_zero_total_weight` (all weights 0.0 → always returns 0.0), `tag_scorer_case_sensitive_no_match` (key "Important" does not match tag "important"). (4) Add `#[cfg(test)]` block to `scorer/priority.rs` with tests: `priority_scorer_range_multiple_items` (assert all scores in [0.0, 1.0] for a list with diverse priorities), `priority_scorer_no_priority` (items without priority field score 0.0). (5) Add `#[cfg(test)]` block to `scorer/scaled.rs` with tests: `scaled_scorer_degenerate_all_equal` (all scores equal → all return 0.5), `scaled_scorer_item_not_in_list` (item not in `all_items` → 0.5). (6) Add `#[cfg(test)]` block to `scorer/reflexive.rs` with tests: `reflexive_scorer_nan_hint` (NaN future_relevance_hint → 0.0), `reflexive_scorer_inf_hint` (Inf → clamped to 1.0 if `clamp` is used, or 0.0 if checked — match actual implementation). (7) In `pipeline/mod.rs`, add `#[cfg(test)]` tests: `pipeline_single_item` (pipeline with one item returns it), `pipeline_all_negative_token_items` (all items with negative tokens → empty result). (8) In `release-rust.yml`: remove the workflow-level `permissions` block; add `permissions: contents: read` under the `test` job; add `permissions: contents: write\n  id-token: write` under the `publish` job.
  - Verify: `cargo test --manifest-path crates/cupel/Cargo.toml` passes (including all new unit tests); `cargo test --features serde --manifest-path crates/cupel/Cargo.toml` passes; `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0; `cargo clippy --features serde --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0; `grep -n "place_zero_items\|place_one_item\|place_two_items\|place_three_items\|place_four_items" crates/cupel/src/placer/u_shaped.rs` shows all 5 tests
  - Done when: `UShapedPlacer::place` has no `Vec<Option>` or `.expect()`; 5 UShapedPlacer tests pass; ≥2 tests per scorer (tag, priority, scaled, reflexive); ≥2 pipeline unit tests pass; release-rust.yml has job-level permissions; clippy clean with serde feature

## Files Likely Touched

- `crates/cupel/src/error.rs`
- `crates/cupel/src/slicer/mod.rs`
- `crates/cupel/src/slicer/greedy.rs`
- `crates/cupel/src/slicer/knapsack.rs`
- `crates/cupel/src/slicer/quota.rs`
- `crates/cupel/src/pipeline/slice.rs`
- `crates/cupel/src/pipeline/mod.rs`
- `crates/cupel/src/scorer/mod.rs`
- `crates/cupel/src/scorer/composite.rs`
- `crates/cupel/src/scorer/frequency.rs`
- `crates/cupel/src/scorer/kind.rs`
- `crates/cupel/src/scorer/priority.rs`
- `crates/cupel/src/scorer/recency.rs`
- `crates/cupel/src/scorer/reflexive.rs`
- `crates/cupel/src/scorer/scaled.rs`
- `crates/cupel/src/scorer/tag.rs`
- `crates/cupel/src/placer/u_shaped.rs`
- `crates/cupel/tests/conformance/slicing.rs`
- `.github/workflows/release-rust.yml`
