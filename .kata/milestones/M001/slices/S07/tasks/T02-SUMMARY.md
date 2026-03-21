---
id: T02
parent: S07
milestone: M001
provides:
  - Scorer trait without as_any; all 8 impl Scorer blocks compile cleanly
  - CompositeScorer without cycle detection DFS or children() accessor
  - ScaledScorer without inner() accessor
  - CupelError::CycleDetected retained with updated "never emitted / reserved" doc
key_files:
  - crates/cupel/src/scorer/mod.rs
  - crates/cupel/src/scorer/composite.rs
  - crates/cupel/src/scorer/scaled.rs
  - crates/cupel/src/scorer/frequency.rs
  - crates/cupel/src/scorer/kind.rs
  - crates/cupel/src/scorer/priority.rs
  - crates/cupel/src/scorer/recency.rs
  - crates/cupel/src/scorer/reflexive.rs
  - crates/cupel/src/scorer/tag.rs
  - crates/cupel/src/error.rs
key_decisions:
  - Removed Any supertrait bound from Scorer; the trait now requires only Send + Sync
  - CycleDetected variant kept with updated doc ("Never emitted. Structural cycles are impossible...")
patterns_established:
  - Scorer trait is minimal (score() only); no downcast machinery needed since owned Box<dyn Scorer> prevents cycles structurally
observability_surfaces:
  - "grep -r \"as_any\" crates/cupel/src/ returns empty — confirms cleanup complete"
  - CupelError::CycleDetected still constructible but library code never returns it
duration: short
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T02: Remove `CompositeScorer` cycle detection and `Scorer::as_any`

**Removed dead cycle-detection DFS from `CompositeScorer::new`, eliminated `as_any` from the `Scorer` trait and all 8 `impl Scorer` blocks, and dropped `ScaledScorer::inner()` — all with zero test regressions and clean clippy.**

## What Happened

The cycle detection in `CompositeScorer::new` was dead code: child scorers are owned `Box<dyn Scorer>`, making structural cycles impossible. The DFS used data pointer identity on heap-allocated objects (always unique after `Box::new`), so `detect_cycles_dfs` and `scorer_identity` could never actually detect a cycle.

Changes made:

1. **`scorer/mod.rs`**: Removed `use std::any::Any` and `fn as_any` from the `Scorer` trait. Also removed the `Any` supertrait bound — `Scorer` now requires only `Send + Sync`.
2. **`scorer/composite.rs`**: Removed `use std::any::Any`, `use std::collections::HashSet`, the `scorer_identity` free function, the `detect_cycles_dfs` free function, the cycle-detection loop in `CompositeScorer::new`, the `children()` pub(crate) accessor, and the `as_any` impl. Added doc paragraph explaining structural impossibility of cycles.
3. **`scorer/scaled.rs`**: Removed `use std::any::Any`, `inner()` pub(crate) accessor, and `as_any` impl.
4. **6 leaf scorer files** (`frequency.rs`, `kind.rs`, `priority.rs`, `recency.rs`, `reflexive.rs`, `tag.rs`): Removed `use std::any::Any` import and `fn as_any` method from each `impl Scorer` block.
5. **`error.rs`**: Added "Never emitted. Structural cycles are impossible with owned `Box<dyn Scorer>` children. Reserved for future use." doc comment above `CycleDetected` variant.

## Verification

```
# All removed symbols gone
grep -r "as_any" crates/cupel/src/           → no output (CLEAN)
grep -r "detect_cycles_dfs" crates/cupel/src/ → no output (CLEAN)
grep -r "scorer_identity" crates/cupel/src/   → no output (CLEAN)

# CycleDetected variant still present
grep "CycleDetected" crates/cupel/src/error.rs → 1 result

# Tests: 30 unit + 33 conformance + 35 doc-tests = 98 total, all passed
cargo test --manifest-path crates/cupel/Cargo.toml → ok

# Clippy: clean, no warnings
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings → ok
```

## Diagnostics

- `grep -r "as_any" crates/cupel/src/` returning empty is the authoritative signal that cleanup is complete.
- `CupelError::CycleDetected` remains in the enum and is constructible; library code never returns it.

## Deviations

None — implementation followed the plan exactly. The `Any` supertrait bound was removed from `Scorer` along with the `as_any` method (the plan said to remove `as_any` but implied `Any` in the bound could stay; removing it is strictly correct since nothing needed it).

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/scorer/mod.rs` — removed `use std::any::Any` and `fn as_any` from `Scorer` trait; removed `Any` supertrait
- `crates/cupel/src/scorer/composite.rs` — removed DFS cycle detection machinery, `children()`, `as_any`; added cycle-impossibility doc
- `crates/cupel/src/scorer/scaled.rs` — removed `inner()`, `as_any`, `use std::any::Any`
- `crates/cupel/src/scorer/frequency.rs` — removed `use std::any::Any` and `as_any`
- `crates/cupel/src/scorer/kind.rs` — removed `use std::any::Any` and `as_any`
- `crates/cupel/src/scorer/priority.rs` — removed `use std::any::Any` and `as_any`
- `crates/cupel/src/scorer/recency.rs` — removed `use std::any::Any` and `as_any`
- `crates/cupel/src/scorer/reflexive.rs` — removed `use std::any::Any` and `as_any`
- `crates/cupel/src/scorer/tag.rs` — removed `use std::any::Any` and `as_any`
- `crates/cupel/src/error.rs` — updated `CycleDetected` doc to mark as never-emitted/reserved
