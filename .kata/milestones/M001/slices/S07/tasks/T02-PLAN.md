---
estimated_steps: 5
estimated_files: 10
---

# T02: Remove `CompositeScorer` cycle detection and `Scorer::as_any`

**Slice:** S07 — Rust Quality Hardening
**Milestone:** M001

## Description

The cycle detection in `CompositeScorer::new` cannot fire: child scorers are stored as owned `Box<dyn Scorer>`, which means no two `CompositeScorer` instances can share a child — structural cycles are impossible. The DFS that attempts to detect them uses data pointer identity on heap-allocated objects, which is always unique after a fresh `Box::new`. The `as_any` method on the `Scorer` trait exists only to support the downcast in `detect_cycles_dfs`. Removing the dead detection code also removes the need for `as_any` from all 8 `impl Scorer` blocks and `ScaledScorer::inner()`.

`CupelError::CycleDetected` is kept as a reserved variant with updated documentation — removing it would be a semver break for any downstream `match` arm on `CupelError`.

## Steps

1. **Update `scorer/mod.rs`**: Remove `use std::any::Any;` import. Remove the two lines `#[doc(hidden)]` and `fn as_any(&self) -> &dyn Any;` from the `Scorer` trait definition.

2. **Update `scorer/composite.rs`**:
   - Remove `use std::any::Any;` and `use std::collections::HashSet;` imports.
   - Remove the `scorer_identity` free function entirely.
   - Remove the `detect_cycles_dfs` free function entirely.
   - In `CompositeScorer::new`, remove the cycle detection loop (the block that iterates `entries` and calls `detect_cycles_dfs`).
   - Remove `pub(crate) fn children(&self) -> &[Box<dyn Scorer>]` method from the `CompositeScorer` impl block.
   - Remove `fn as_any(&self) -> &dyn Any { self }` from `impl Scorer for CompositeScorer`.
   - Add a doc comment paragraph on the `CompositeScorer` struct, after the existing doc: `/// Cycles are structurally impossible: children are stored as owned \`Box<dyn Scorer>\`, so no two instances can share a child via reference — a scorer cannot reference its own ancestor.`

3. **Update `scorer/scaled.rs`**:
   - Remove `use std::any::Any;` import.
   - Remove `pub(crate) fn inner(&self) -> &dyn Scorer` method (only caller was `detect_cycles_dfs`).
   - Remove `fn as_any(&self) -> &dyn Any { self }` from `impl Scorer for ScaledScorer`.

4. **Update the remaining 6 scorer files** (`frequency.rs`, `kind.rs`, `priority.rs`, `recency.rs`, `reflexive.rs`, `tag.rs`): In each file, remove `use std::any::Any;` import and remove the `fn as_any(&self) -> &dyn Any { self }` method from the `impl Scorer` block.

5. **Update `error.rs`**: Replace the `CycleDetected` variant doc comment `#[error("cycle detected: scorer appears in its own dependency graph")]` with `/// Never emitted. Structural cycles are impossible with owned \`Box<dyn Scorer>\` children.\n    /// Reserved for future use.\n    #[error("cycle detected: scorer appears in its own dependency graph")]`. The error message string stays unchanged (it is the Display format and changing it would affect any existing error string comparisons).

## Must-Haves

- [ ] `Scorer` trait has no `as_any` method
- [ ] All 8 `impl Scorer` blocks compile without `as_any`
- [ ] `CompositeScorer::new` still validates: non-empty entries, positive weights, finite weights
- [ ] `detect_cycles_dfs` and `scorer_identity` are gone from `composite.rs`
- [ ] `ScaledScorer::inner()` removed (no callers after cycle detection removal)
- [ ] `CupelError::CycleDetected` variant still present in `error.rs` with updated doc
- [ ] `cargo clippy --all-targets -- -D warnings` exits 0 (no dead_code warnings)
- [ ] All existing tests still pass

## Verification

```bash
# No traces of cycle detection machinery
grep -r "as_any" crates/cupel/src/
grep -r "detect_cycles_dfs" crates/cupel/src/
grep -r "scorer_identity" crates/cupel/src/

# CycleDetected variant still present
grep "CycleDetected" crates/cupel/src/error.rs

# Tests and clippy
cargo test --manifest-path crates/cupel/Cargo.toml
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
```

All `grep` commands for removed symbols should return no output. `CycleDetected` grep should return 1 result. Tests and clippy must exit 0.

## Observability Impact

- Signals added/changed: None — cycle detection was never observable (it only fired on an impossible code path). The removal has no runtime observability impact.
- How a future agent inspects this: `grep -r "as_any" crates/cupel/src/` returning empty is the signal that cleanup is complete.
- Failure state exposed: None new — `CupelError::CycleDetected` remains constructible by pattern matching but can never be returned by library code.

## Inputs

- T01 completed — `Slicer::slice` already returns `Result`; codebase compiles cleanly
- `crates/cupel/src/scorer/composite.rs` — `scorer_identity`, `detect_cycles_dfs`, `children()`, `as_any` all present; all to be removed
- `crates/cupel/src/scorer/mod.rs` — `Scorer` trait has `as_any` at line 105; to be removed
- `crates/cupel/src/scorer/scaled.rs` — `inner()` pub(crate) used only by `detect_cycles_dfs`; to be removed
- `crates/cupel/src/error.rs` — `CycleDetected` variant to be re-documented (not removed)
- Research note: keeping `CycleDetected` in the enum is safer for semver — downstream code matching on `CupelError::CycleDetected` would fail to compile if the variant were removed; `#[non_exhaustive]` on `CupelError` only protects against non-exhaustive matches, not direct variant references

## Expected Output

- `crates/cupel/src/scorer/mod.rs` — `Scorer` trait without `as_any`
- `crates/cupel/src/scorer/composite.rs` — cleaned-up file: constructor validates entries but no DFS; `CompositeScorer` struct has new doc paragraph; no `children()`, no `as_any`, no DFS functions
- `crates/cupel/src/scorer/scaled.rs` — no `inner()`, no `as_any`
- 6 other scorer files — each has `as_any` and `use std::any::Any` removed
- `crates/cupel/src/error.rs` — `CycleDetected` with updated doc comment marking it as never-emitted/reserved
