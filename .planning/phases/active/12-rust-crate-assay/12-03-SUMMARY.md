# Plan 12-03 Summary: Conformance Test Suite

## Result: PASS

All 28 required conformance tests pass via `cargo test -p assay-cupel`.

## Tasks Completed

### Task 1: Copy conformance vectors and build test runner infrastructure
- Copied 28 TOML test vectors preserving directory structure (scoring/13, slicing/6, placing/4, pipeline/5)
- Created `tests/conformance.rs` with shared infrastructure: `load_vector`, `build_items`, `build_scored_items`, `build_scorer`, `build_slicer`, `build_placer`, `assert_scores_match`, `assert_set_eq`, `assert_ordered_eq`
- TOML datetime parsing converts `toml::value::Datetime` to `chrono::DateTime<Utc>` via string round-trip
- Scorer/slicer/placer factories support all types including nested composite and scaled scorers

### Task 2: All 28 conformance tests
- 13 scoring tests: recency (2), priority (2), kind (2), tag (2), frequency (1), reflexive (2), composite (1), scaled (1)
- 6 slicing tests: greedy (3), knapsack (2), quota (1)
- 4 placing tests: chronological (2), u-shaped (2)
- 5 pipeline tests: greedy-chronological, greedy-ushaped, knapsack-chronological, composite-greedy-chronological, pinned-items

## Deviations

### Auto-fix: KindScorer default weights in pipeline context
- **Issue**: `composite-greedy-chronological` pipeline vector specifies `type = "kind"` as a child scorer without explicit config. The `build_scorer_by_type` factory was panicking when no config was provided for the kind scorer.
- **Fix**: Updated factory to default to `KindScorer::with_default_weights()` when no config is provided or when config has no `weights` key.
- **Impact**: None — this is the correct spec behavior (kind scorer without config uses default weights).

## Commits

1. `8da72d4` — test(12-03): copy conformance vectors and build test runner infrastructure
2. `2e177ee` — test(12-03): add all 28 conformance tests (scoring, slicing, placing, pipeline)

## Verification

```
cargo test -p assay-cupel: 28 passed, 0 failed
cargo clippy -p assay-cupel --tests -- -D warnings: clean
```

## Duration

~13 minutes
