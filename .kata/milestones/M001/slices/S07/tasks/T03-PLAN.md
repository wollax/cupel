---
estimated_steps: 8
estimated_files: 7
---

# T03: Refactor `UShapedPlacer`, add batch unit tests, scope release-rust.yml permissions

**Slice:** S07 — Rust Quality Hardening
**Milestone:** M001

## Description

Three independent quality improvements batched together:

1. **UShapedPlacer refactor**: The current implementation uses `Vec<Option<ContextItem>>` with a `.expect()` to unwrap slots. The invariant is correct but fragile and untested at edge cases. Replacing with explicit `left` and `right` `Vec<ContextItem>` eliminates the `.expect()` entirely — the logic becomes structurally correct without requiring an unwrap.

2. **Batch unit tests**: Six test gaps identified in research — UShapedPlacer edge cases (0, 1, 2, 3, 4 items), scorer edge cases (TagScorer zero-weight, case-sensitivity; PriorityScorer range and no-priority; ScaledScorer degenerate; ReflexiveScorer NaN/Inf), and pipeline boundary cases (single item, all-negative-token items).

3. **release-rust.yml permissions**: Workflow-level `permissions: contents: write` grants write access to the `test` job unnecessarily. Scope to job level: `test` → `contents: read`; `publish` → `contents: write` + `id-token: write`.

## Steps

1. **Refactor `UShapedPlacer::place`** (`src/placer/u_shaped.rs`):
   - Keep the early returns for 0 and 1 items unchanged.
   - Replace the `Vec<Option<ContextItem>>` + left/right pointer section with:
     ```rust
     let mut left: Vec<ContextItem> = Vec::new();
     let mut right: Vec<ContextItem> = Vec::new();
     for (rank, &(_, orig_idx)) in scored.iter().enumerate() {
         let item = items[orig_idx].item.clone();
         if rank % 2 == 0 {
             left.push(item);
         } else {
             right.insert(0, item);  // or push to a separate vec and reverse
         }
     }
     left.into_iter().chain(right.into_iter()).collect()
     ```
   - Alternative (cleaner): push right-side items to a `Vec`, then call `right.reverse()` before chaining. Either approach is correct; prefer `right.push(item); ... right.reverse()` over `right.insert(0, item)` for O(1) insertion.
   - Remove the `result.into_iter().map(|o| o.expect(...)).collect()` line entirely.

2. **Add UShapedPlacer unit tests** (`src/placer/u_shaped.rs`) — add `#[cfg(test)]` module:
   - `place_zero_items`: `UShapedPlacer.place(&[])` → `assert!(result.is_empty())`
   - `place_one_item`: single item → `assert_eq!(result.len(), 1); assert_eq!(result[0].content(), "A")`
   - `place_two_items`: items A(0.9) and B(0.1) → `result[0].content() == "A"` (highest at start), `result[1].content() == "B"` (lower at end)
   - `place_three_items`: A(0.9), B(0.5), C(0.1) → `result[0].content() == "A"` (rank 0 → left), `result[1].content() == "C"` (rank 2 → left), `result[2].content() == "B"` (rank 1 → right → end)
   - `place_four_items`: A(0.9), B(0.7), C(0.5), D(0.1) → rank 0(A)→left[0], rank 1(B)→right, rank 2(C)→left[1], rank 3(D)→right; final order: [A, C, D, B] — verify first and last elements

3. **Add TagScorer unit tests** (`src/scorer/tag.rs`) — add `#[cfg(test)]` module:
   - `tag_scorer_zero_total_weight`: all configured weights are `0.0` → `score` always returns `0.0` (guarded by `self.total_weight == 0.0` check)
   - `tag_scorer_case_sensitive_no_match`: configure weight for `"Important"` (capital I), item has tag `"important"` (lower) → score returns `0.0` (spec: tag key lookup is case-sensitive)

4. **Add PriorityScorer unit tests** (`src/scorer/priority.rs`) — add `#[cfg(test)]` module:
   - `priority_scorer_scores_in_range`: create 5 items with priorities 1, 2, 3, 4, 5 → all scores in `[0.0, 1.0]`; score of highest priority item equals `1.0`; score of lowest priority item equals `0.0`
   - `priority_scorer_item_without_priority`: item with no priority field → `score == 0.0`

5. **Add ScaledScorer unit tests** (`src/scorer/scaled.rs`) — add `#[cfg(test)]` module:
   - `scaled_scorer_degenerate_all_equal_scores`: wrap a scorer that returns constant value for all items; all scaled scores should return `0.5`
   - `scaled_scorer_item_not_in_list`: call `scorer.score(&item_not_in_list, &[other_item])` → returns `0.5` (item not found via `std::ptr::eq`)

6. **Add ReflexiveScorer unit tests** (`src/scorer/reflexive.rs`) — add `#[cfg(test)]` module:
   - `reflexive_scorer_nan_hint`: item with `future_relevance_hint = f64::NAN` → check actual behavior (NaN is not finite, so `match item.future_relevance_hint() { Some(v) if v.is_finite() ... }` — verify the implementation and test accordingly; if the current code just returns `value` without finite check, test that NaN propagates; if it has a finite check, test that NaN → 0.0)
   - `reflexive_scorer_large_hint_clamped`: item with `future_relevance_hint = 2.0` → clamped to `1.0` (check `f64::clamp` or min in current impl)

7. **Add pipeline unit tests** (`src/pipeline/mod.rs`) — add `#[cfg(test)]` module:
   - `pipeline_single_item`: build a minimal pipeline (default scorers, GreedySlice, default placer), run with one item that fits in budget → result has one element
   - `pipeline_all_negative_token_items`: items all have negative token counts → result is empty (negative-token items are filtered at the Slice stage pre-filter)

8. **Scope release-rust.yml permissions to job level** (`.github/workflows/release-rust.yml`):
   - Remove the top-level `permissions` block (lines `permissions:`, `contents: write`, `id-token: write`).
   - Add `permissions:\n  contents: read` under the `test:` job (before `runs-on`).
   - Add `permissions:\n  contents: write\n  id-token: write` under the `publish:` job (before `runs-on`).

## Must-Haves

- [ ] `UShapedPlacer::place` uses explicit `left`/`right` `Vec<ContextItem>`; no `Vec<Option>` or `.expect()`
- [ ] 5 UShapedPlacer unit tests: `place_zero_items`, `place_one_item`, `place_two_items`, `place_three_items`, `place_four_items`
- [ ] 2 TagScorer tests: zero total weight, case-sensitive no-match
- [ ] 2 PriorityScorer tests: scores in [0.0, 1.0] range, item without priority → 0.0
- [ ] 2 ScaledScorer tests: degenerate (all equal → 0.5), item not in list → 0.5
- [ ] 2 ReflexiveScorer tests: NaN hint behavior, large hint clamping
- [ ] 2 pipeline unit tests: single item, all-negative-token items
- [ ] `release-rust.yml` has no workflow-level `permissions` block; `test` job has `contents: read`; `publish` job has `contents: write` + `id-token: write`
- [ ] `cargo test --manifest-path crates/cupel/Cargo.toml` passes (all tests)
- [ ] `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0
- [ ] `cargo test --features serde --manifest-path crates/cupel/Cargo.toml` passes

## Verification

```bash
# Check UShapedPlacer refactor
grep -n "Vec<Option" crates/cupel/src/placer/u_shaped.rs  # should return nothing
grep -n "expect(" crates/cupel/src/placer/u_shaped.rs     # should return nothing

# Check test counts
grep -c "#\[test\]" crates/cupel/src/placer/u_shaped.rs   # should be >= 5
grep -c "#\[test\]" crates/cupel/src/scorer/tag.rs        # should be >= 2
grep -c "#\[test\]" crates/cupel/src/scorer/priority.rs   # should be >= 2
grep -c "#\[test\]" crates/cupel/src/scorer/scaled.rs     # should be >= 2
grep -c "#\[test\]" crates/cupel/src/scorer/reflexive.rs  # should be >= 2

# Run all tests
cargo test --manifest-path crates/cupel/Cargo.toml
cargo test --features serde --manifest-path crates/cupel/Cargo.toml
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
cargo clippy --features serde --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
cargo deny check  # from crates/cupel/
```

## Observability Impact

- Signals added/changed: `UShapedPlacer::place` now cannot panic — structural correctness replaces runtime assertion; pipeline tests expose boundary behavior as documented behavior.
- How a future agent inspects this: `grep -n "Vec<Option" src/placer/u_shaped.rs` returning empty confirms the refactor; test count via `grep -c "#\[test\]"` confirms coverage.
- Failure state exposed: Pipeline tests make single-item and zero-useful-item cases visible as named test cases rather than implicit behavior.

## Inputs

- T01 and T02 completed — codebase compiles cleanly; `Slicer::slice` returns `Result`; `Scorer` trait has no `as_any`
- `crates/cupel/src/placer/u_shaped.rs` — current `Vec<Option>` implementation with `.expect()`; invariant explanation in comments; `if right == 0 { break; }` usize underflow guard to remove
- `crates/cupel/src/scorer/reflexive.rs` — check how `future_relevance_hint` is handled: verify whether there is a finiteness check before returning the value, to write the correct NaN test
- `crates/cupel/src/pipeline/mod.rs` — `Pipeline` builder pattern; need `PipelineBuilder` or `Pipeline::new` API to construct a test pipeline; check `lib.rs` exports for the builder API

## Expected Output

- `crates/cupel/src/placer/u_shaped.rs` — refactored place method + 5 unit tests in `#[cfg(test)]`
- `crates/cupel/src/scorer/tag.rs` — 2 unit tests in `#[cfg(test)]`
- `crates/cupel/src/scorer/priority.rs` — 2 unit tests in `#[cfg(test)]`
- `crates/cupel/src/scorer/scaled.rs` — 2 unit tests in `#[cfg(test)]`
- `crates/cupel/src/scorer/reflexive.rs` — 2 unit tests in `#[cfg(test)]`
- `crates/cupel/src/pipeline/mod.rs` — 2 pipeline unit tests in `#[cfg(test)]`
- `.github/workflows/release-rust.yml` — job-level permissions replacing workflow-level block
