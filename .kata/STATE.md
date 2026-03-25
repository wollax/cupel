# Kata State

**Active Milestone:** M009 — CountConstrainedKnapsackSlice + MetadataKeyScorer
**Active Slice:** S04 — MetadataKeyScorer — Rust + .NET implementation
**Active Task:** (planning)
**Phase:** Planning — S04

## Recent Decisions

- D186: `metadata-key.md` uses `defaultMultiplier` as fixed constant 1.0 (not a constructor parameter)
- D185: S03 verification strategy — contract-level (TBD grep + regression tests)
- D184: Phase 2 re-sort uses `scoreByContent` dict built from `residual` before knapsack call
- D183: S02 verification strategy — integration-level via `CupelPipeline.DryRun()`
- D180: Phase 2 output must be re-sorted by score descending before Phase 3 cap enforcement

## Blockers

- None

## Milestone Progress (M009)

- [x] S01: CountConstrainedKnapsackSlice — Rust implementation ✅
- [x] S02: CountConstrainedKnapsackSlice — .NET implementation ✅ (797/797 tests, 0 build warnings)
- [x] S03: Spec chapters — count-constrained-knapsack + metadata-key ✅ (both chapters zero TBD, all wiring complete)
- [ ] S04: MetadataKeyScorer — Rust + .NET implementation `depends:[S03]`

## Next Action

Begin S04: plan and execute MetadataKeyScorer in Rust (src/scorer/metadata_key.rs) and .NET (src/Wollax.Cupel/Scoring/MetadataKeyScorer.cs) using spec/src/scorers/metadata-key.md as the implementation contract.
