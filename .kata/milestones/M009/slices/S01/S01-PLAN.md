# S01: CountConstrainedKnapsackSlice — Rust implementation

**Goal:** Implement `CountConstrainedKnapsackSlice` in Rust using the 3-phase pre-processing algorithm: Phase 1 commits top-N items per kind by score descending, Phase 2 runs `KnapsackSlice` on residual candidates and budget, Phase 3 enforces caps by dropping over-cap items. Re-export from `lib.rs`. Verify with 5 conformance TOML vectors + matching integration tests.

**Demo:** `cargo test --all-targets` is green; `crates/cupel/tests/count_constrained_knapsack.rs` contains 5 passing tests; `CountConstrainedKnapsackSlice` is importable from the `cupel` crate root.

## Must-Haves

- `CountConstrainedKnapsackSlice` struct exists in `crates/cupel/src/slicer/count_constrained_knapsack.rs` with `new(entries: Vec<CountQuotaEntry>, knapsack: KnapsackSlice, scarcity: ScarcityBehavior) -> Result<Self, CupelError>`
- `Slicer` impl with 3-phase algorithm: Phase 1 (commit top-N per kind), Phase 2 (knapsack on residual), Phase 3 (cap enforcement)
- `is_count_quota() → true`; `is_knapsack() → false` (inherited default)
- `count_cap_map()` returns per-kind caps from entries
- `QuotaPolicy` impl: `quota_constraints()` returns `QuotaConstraintMode::Count` constraints (same as `CountQuotaSlice`)
- `CountConstrainedKnapsackSlice` re-exported from `crates/cupel/src/lib.rs`
- 5 TOML conformance vectors in all 3 locations (D082)
- 5 integration tests in `crates/cupel/tests/count_constrained_knapsack.rs` all passing
- `build_slicer_by_type` in `conformance.rs` handles `"count_constrained_knapsack"` arm
- `cargo test --all-targets` green; `cargo clippy --all-targets -- -D warnings` clean
- CHANGELOG.md unreleased section mentions `CountConstrainedKnapsackSlice`

## Proof Level

- This slice proves: integration
- Real runtime required: no
- Human/UAT required: no

## Verification

- `cd /Users/wollax/Git/personal/cupel && rtk cargo test --all-targets` — all tests pass, zero failures
- `cargo clippy --all-targets -- -D warnings` — zero warnings
- Tests in `crates/cupel/tests/count_constrained_knapsack.rs` cover all 5 vector scenarios
- `grep -r "CountConstrainedKnapsackSlice" crates/cupel/src/lib.rs` — confirms export
- `diff conformance/required/ crates/cupel/conformance/required/` — exits 0 (drift guard)

## Observability / Diagnostics

- Runtime signals: `CupelError::SlicerConfig` on construction failure (require > cap, zero cap); `CupelError::TableTooLarge` propagated from Phase 2 KnapsackSlice
- Inspection surfaces: `cargo test --all-targets 2>&1 | grep -E "FAILED|error"` for failure localization
- Failure visibility: test names encode the scenario (baseline, cap, scarcity, tag-nonexclusive, require-and-cap); assertion messages include expected vs actual contents
- Redaction constraints: none (test data only)

## Integration Closure

- Upstream surfaces consumed: `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall`, `KnapsackSlice`, `QuotaPolicy`, `QuotaConstraint`, `QuotaConstraintMode` (all existing)
- New wiring introduced: `CountConstrainedKnapsackSlice` added to `slicer/mod.rs` re-exports and `lib.rs` public surface; `"count_constrained_knapsack"` arm in conformance harness
- What remains before the milestone is truly usable end-to-end: .NET implementation (S02), spec chapter (S03), MetadataKeyScorer (S04)

## Tasks

- [x] **T01: Write 5 failing integration tests and TOML vectors** `est:45m`
  - Why: Establishes the objective verification target before implementation; tests will fail initially (struct doesn't exist yet), proving they are real
  - Files: `crates/cupel/tests/count_constrained_knapsack.rs`, `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml`, `conformance/required/slicing/count-constrained-knapsack-*.toml`, `spec/conformance/required/slicing/count-constrained-knapsack-*.toml`
  - Do: Write 5 TOML vectors covering (1) baseline satisfaction with knapsack selection, (2) cap enforcement drops over-cap items from knapsack output, (3) scarcity degrade with require unmet, (4) tag non-exclusive counting, (5) require+cap with residual knapsack picking best remaining; copy each TOML to all 3 locations (D082); write `count_constrained_knapsack.rs` test file with 5 `#[test]` functions using `run_count_quota_full_test` helper; add `mod count_constrained_knapsack;` to `tests/` (or inline with `#[path]`); tests reference the new vectors and call `slicer.slice()` — they will fail to compile until T02 adds the type
  - Verify: `cargo build --tests 2>&1 | grep "error"` — errors expected at this point because `CountConstrainedKnapsackSlice` doesn't exist yet; TOML files exist in all 3 locations; `diff conformance/required/ crates/cupel/conformance/required/` exits 0
  - Done when: 5 TOML vectors exist in all 3 locations, test file exists, `diff` on conformance dirs is clean, compilation fails only due to missing `CountConstrainedKnapsackSlice` type

- [x] **T02: Implement CountConstrainedKnapsackSlice and wire into slicer module** `est:1h30m`
  - Why: Creates the core struct, 3-phase algorithm, and all trait implementations; makes T01's tests compilable and passing
  - Files: `crates/cupel/src/slicer/count_constrained_knapsack.rs` (new), `crates/cupel/src/slicer/mod.rs`, `crates/cupel/src/lib.rs`, `crates/cupel/tests/conformance.rs`
  - Do: Create `count_constrained_knapsack.rs` — struct with `entries: Vec<CountQuotaEntry>`, `knapsack: KnapsackSlice`, `scarcity: ScarcityBehavior`; derive `Debug, Clone` (not `Copy` — has Vec); implement `new()` validating nothing special (no is_knapsack guard needed per D176 — this IS the knapsack wrapper); copy Phase 1 and Phase 3 logic verbatim from `count_quota.rs` (partitioning, sort, committed_ids HashSet, shortfall recording, cap enforcement); Phase 2 calls `self.knapsack.slice(&remaining, &sub_budget)?` instead of `self.inner.slice`; sub-budget: `ContextBudget::new(residual, residual, 0, HashMap::new(), 0.0).expect(...)` with `.max(0)` guard; implement `is_count_quota() → true` override; inherit `is_knapsack()` default (false); implement `count_cap_map()` from entries; implement `QuotaPolicy` (same as CountQuotaSlice — `QuotaConstraintMode::Count`); add `mod count_constrained_knapsack; pub use count_constrained_knapsack::CountConstrainedKnapsackSlice;` to `slicer/mod.rs`; add `CountConstrainedKnapsackSlice` to `pub use slicer::{...}` in `lib.rs`; add `"count_constrained_knapsack"` arm to `build_slicer_by_type` in `conformance.rs` (parse `bucket_size`, `scarcity_behavior`, `entries`; construct `KnapsackSlice::new(bucket_size)?` and `CountConstrainedKnapsackSlice::new(entries, knapsack, scarcity)?`)
  - Verify: `rtk cargo test --all-targets` — all tests pass including the 5 new ones; `cargo clippy --all-targets -- -D warnings` clean
  - Done when: `cargo test --all-targets` exits 0, 5 new count_constrained_knapsack tests pass, clippy clean, `CountConstrainedKnapsackSlice` importable from crate root

- [x] **T03: Update CHANGELOG and finalize** `est:15m`
  - Why: Milestone DoD requires CHANGELOG entry; completes the slice gate
  - Files: `CHANGELOG.md`
  - Do: Add `CountConstrainedKnapsackSlice` entry to unreleased section in CHANGELOG.md; briefly describe the 3-phase algorithm and re-use of `CountQuotaEntry`/`ScarcityBehavior`
  - Verify: `grep "CountConstrainedKnapsackSlice" CHANGELOG.md` exits 0; `rtk cargo test --all-targets` still green
  - Done when: CHANGELOG updated, all tests still passing

## Files Likely Touched

- `crates/cupel/src/slicer/count_constrained_knapsack.rs` (new)
- `crates/cupel/src/slicer/mod.rs`
- `crates/cupel/src/lib.rs`
- `crates/cupel/tests/count_constrained_knapsack.rs` (new)
- `crates/cupel/tests/conformance.rs`
- `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml` (5 new files)
- `conformance/required/slicing/count-constrained-knapsack-*.toml` (5 new files)
- `spec/conformance/required/slicing/count-constrained-knapsack-*.toml` (5 new files)
- `CHANGELOG.md`
