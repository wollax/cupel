---
id: T03
parent: S07
milestone: M001
provides:
  - UShapedPlacer refactored to explicit left/right Vecs with no Vec<Option> or .expect()
  - 15 new unit tests across UShapedPlacer, TagScorer, PriorityScorer, ScaledScorer, ReflexiveScorer, Pipeline
  - release-rust.yml permissions scoped to job level
key_files:
  - crates/cupel/src/placer/u_shaped.rs
  - crates/cupel/src/scorer/tag.rs
  - crates/cupel/src/scorer/priority.rs
  - crates/cupel/src/scorer/scaled.rs
  - crates/cupel/src/scorer/reflexive.rs
  - crates/cupel/src/pipeline/mod.rs
  - .github/workflows/release-rust.yml
key_decisions:
  - right.push() + right.reverse() used over right.insert(0, ...) for O(1) insertion in UShapedPlacer
  - std::slice::from_ref(&item) used in scorer tests instead of &[item.clone()] to satisfy clippy's cloned_ref_to_slice_refs lint
patterns_established:
  - Scorer unit tests pass std::slice::from_ref(&item) as all_items when testing single-item behavior
  - Pipeline tests use ChronologicalPlacer (available) rather than a non-existent GreedyPlacer
observability_surfaces:
  - UShapedPlacer::place cannot panic — structural correctness replaces runtime assertion
  - grep -n "Vec<Option" src/placer/u_shaped.rs returning empty confirms refactor
  - grep -c "#[test]" per file confirms coverage counts
duration: short
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T03: Refactor `UShapedPlacer`, add batch unit tests, scope release-rust.yml permissions

**Replaced `Vec<Option<ContextItem>>` + `.expect()` in `UShapedPlacer` with explicit `left`/`right` vecs; added 15 unit tests across 5 modules; scoped `release-rust.yml` permissions to job level.**

## What Happened

**UShapedPlacer refactor:** The `Vec<Option<ContextItem>>` + pointer-based fill was replaced with two explicit `Vec<ContextItem>` (`left` and `right`). Even-ranked items go to `left`, odd-ranked items to `right`. After the loop, `right.reverse()` is called so higher-ranked right-side items appear at the tail. The final result is `left.chain(right)`. The old `.expect("UShapedPlacer: all result slots must be filled")` and the usize underflow guard (`if right == 0 { break; }`) are gone entirely.

**Unit tests:** 15 new `#[cfg(test)]` tests added:
- 5 `UShapedPlacer` tests: zero/one/two/three/four items
- 2 `TagScorer` tests: zero total weight, case-sensitive no-match
- 2 `PriorityScorer` tests: scores in [0.0, 1.0] range, item without priority → 0.0
- 2 `ScaledScorer` tests: degenerate all-equal → 0.5, item not in list → 0.5
- 2 `ReflexiveScorer` tests: NaN hint → 0.0 (finiteness guard fires), large hint (2.0) → clamped to 1.0
- 2 `Pipeline` tests: single item passes through, all-negative-token items → empty result

**release-rust.yml:** Removed the workflow-level `permissions: contents: write / id-token: write` block. Added `permissions: contents: read` under the `test` job and `permissions: contents: write / id-token: write` under the `publish` job.

## Verification

```
cargo test --manifest-path crates/cupel/Cargo.toml             # 35 passed, 0 failed
cargo test --features serde --manifest-path crates/cupel/Cargo.toml  # 35 passed, 0 failed
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings  # clean
cargo clippy --features serde --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings  # clean
cargo deny check  # advisories ok, bans ok, licenses ok, sources ok
```

Test counts confirmed:
- u_shaped: 5 tests
- tag: 2 tests
- priority: 2 tests
- scaled: 2 tests
- reflexive: 2 tests

Structural checks:
- `grep -n "Vec<Option" crates/cupel/src/placer/u_shaped.rs` → NONE
- `grep -n "expect(" crates/cupel/src/placer/u_shaped.rs` → NONE

## Diagnostics

- `grep -n "Vec<Option" src/placer/u_shaped.rs` returning empty is the authoritative signal that the refactor is in place.
- `grep -c "#[test]"` per scorer/placer/pipeline file confirms minimum coverage counts.
- `ReflexiveScorer` has explicit finiteness guard before clamp; NaN and Inf both return 0.0 — confirmed by `reflexive_scorer_nan_hint` test.

## Deviations

- Used `std::slice::from_ref(&item)` instead of `&[item.clone()]` in single-item scorer tests — required to satisfy clippy's `cloned_ref_to_slice_refs` lint (treated as -D warnings). Not mentioned in the plan but a necessary mechanical change.
- Pipeline tests use `ChronologicalPlacer` instead of a `GreedyPlacer` — no `GreedyPlacer` exists in the codebase; `ChronologicalPlacer` is the correct available alternative.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/placer/u_shaped.rs` — refactored place() + 5 unit tests
- `crates/cupel/src/scorer/tag.rs` — 2 unit tests
- `crates/cupel/src/scorer/priority.rs` — 2 unit tests
- `crates/cupel/src/scorer/scaled.rs` — 2 unit tests
- `crates/cupel/src/scorer/reflexive.rs` — 2 unit tests
- `crates/cupel/src/pipeline/mod.rs` — 2 pipeline unit tests
- `.github/workflows/release-rust.yml` — job-level permissions replacing workflow-level block
