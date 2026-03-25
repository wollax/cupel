# Kata State

**Active Milestone:** M009 — CountConstrainedKnapsackSlice + MetadataKeyScorer
**Active Slice:** S03 — Spec chapters — count-constrained-knapsack + metadata-key
**Active Task:** None — S03 planning pending
**Phase:** S02 Complete — Advance to S03

## Recent Decisions

- D184: Phase 2 re-sort uses `scoreByContent` dict built from `residual` before knapsack call; sort `IReadOnlyList<ContextItem>` output via `.OrderByDescending(item => scoreByContent.GetValueOrDefault(item.Content, 0.0))`
- D183: S02 verification strategy — integration-level via `CupelPipeline.DryRun()` (matches D137 rationale from M006/S02)
- D180: Phase 2 output must be re-sorted by score descending before Phase 3 cap enforcement
- D181: Phase 3 `selected_count` seeded from Phase 1 committed counts, not zero
- D179: S01 verification strategy — direct `slicer.slice()` calls in 5 integration tests (Rust)

## Blockers

- None

## Milestone Progress (M009)

- [x] S01: CountConstrainedKnapsackSlice — Rust implementation ✅
- [x] S02: CountConstrainedKnapsackSlice — .NET implementation ✅ (797/797 tests, 0 build warnings)
- [ ] S03: Spec chapters — count-constrained-knapsack + metadata-key `depends:[S01,S02]`
- [ ] S04: MetadataKeyScorer — Rust + .NET implementation `depends:[S03]`

## Next Action

S02 complete (797/797 tests green, 0 build warnings). Start S03: write `spec/src/slicers/count-constrained-knapsack.md` and `spec/src/scorers/metadata-key.md` with zero TBD fields. Read M009-ROADMAP.md S03 entry and TOML conformance vectors in `crates/cupel/conformance/required/slicing/` before planning.
