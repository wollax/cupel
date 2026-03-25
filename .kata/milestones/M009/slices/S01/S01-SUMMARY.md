---
id: S01
parent: M009
milestone: M009
provides:
  - CountConstrainedKnapsackSlice struct (Clone, Debug) in crates/cupel/src/slicer/count_constrained_knapsack.rs
  - Full 3-phase algorithm: Phase 1 count-satisfy (top-N per kind by score), Phase 2 knapsack-distribute (KnapsackSlice on residual), Phase 3 cap-enforce (drop over-cap items score-descending)
  - Slicer trait impl with is_count_quota()=true, count_cap_map(), QuotaPolicy returning QuotaConstraintMode::Count
  - Re-exported from cupel crate root (lib.rs pub use slicer block)
  - 5 TOML conformance vectors in all 3 required locations (15 files total, drift guard clean)
  - 5 integration tests in crates/cupel/tests/count_constrained_knapsack.rs (all passing)
  - "count_constrained_knapsack" dispatch arm in conformance.rs build_slicer_by_type
  - CHANGELOG.md unreleased section entry describing 3-phase algorithm and reused types
requires: []
affects:
  - S02
  - S03
key_files:
  - crates/cupel/src/slicer/count_constrained_knapsack.rs
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/count_constrained_knapsack.rs
  - crates/cupel/tests/conformance.rs
  - crates/cupel/conformance/required/slicing/count-constrained-knapsack-baseline.toml
  - crates/cupel/conformance/required/slicing/count-constrained-knapsack-cap-exclusion.toml
  - crates/cupel/conformance/required/slicing/count-constrained-knapsack-scarcity-degrade.toml
  - crates/cupel/conformance/required/slicing/count-constrained-knapsack-tag-nonexclusive.toml
  - crates/cupel/conformance/required/slicing/count-constrained-knapsack-require-and-cap.toml
  - CHANGELOG.md
key_decisions:
  - "D174: Pre-processing path (5A) chosen over full constrained-DP (5D) — Phase 1 commits top-N by score, Phase 2 standard KnapsackSlice on residual"
  - "D175: Reuses CountQuotaEntry, ScarcityBehavior, CountRequirementShortfall from M006 — no parallel types needed"
  - "D176: is_count_quota()=true (find_min_budget_for monotonicity guard), is_knapsack()=false (not a raw knapsack)"
  - "D180: Phase 2 output re-sorted by score descending before Phase 3 cap enforcement — KnapsackSlice reconstructs in DP-reverse-index order, not score order"
  - "D181: Phase 3 selected_count starts from Phase 1 committed counts, not zero"
  - "D182: Self-contained integration test pattern — inline helpers in test file when cross-binary import is impossible"
patterns_established:
  - "Phase 2 output re-sort before cap enforcement: always sort KnapsackSlice output by score descending before applying count caps"
  - "Self-contained integration test pattern: inline minimal helpers in test file when tests/ binaries cannot share code (see count_quota_composition.rs precedent)"
observability_surfaces:
  - "cargo test --test count_constrained_knapsack -- --nocapture — 5 conformance scenario tests with descriptive names"
  - "cargo test --all-targets 2>&1 | grep -E 'FAILED|error' — failure localization surface"
  - "CupelError::TableTooLarge { candidates, capacity, cells } propagated from KnapsackSlice when DP table exceeds 50M cells"
  - "CupelError::SlicerConfig when ScarcityBehavior::Throw and candidate pool < require_count"
  - "diff -r conformance/required/ crates/cupel/conformance/required/ — TOML drift guard"
drill_down_paths:
  - .kata/milestones/M009/slices/S01/tasks/T01-SUMMARY.md
  - .kata/milestones/M009/slices/S01/tasks/T02-SUMMARY.md
  - .kata/milestones/M009/slices/S01/tasks/T03-SUMMARY.md
duration: ~60m (T01: 30m, T02: 25m, T03: 5m)
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
---

# S01: CountConstrainedKnapsackSlice — Rust implementation

**3-phase CountConstrainedKnapsackSlice (count-satisfy → knapsack-distribute → cap-enforce) in Rust; 175 tests passing, clippy clean, crate root exported, CHANGELOG updated.**

## What Happened

T01 established the red baseline: 5 TOML conformance vectors (baseline, cap-exclusion, scarcity-degrade, tag-nonexclusive, require-and-cap) were written to all 3 required locations, and a self-contained integration test file was created. A key design constraint discovered in T01 was that Rust integration test binaries in `tests/` cannot share helpers across files — the test file inlines all needed helpers rather than importing from `conformance.rs`.

T02 created the full implementation in `crates/cupel/src/slicer/count_constrained_knapsack.rs` (~250 lines). Phase 1 is lifted verbatim from `CountQuotaSlice` — partitions candidates by kind, sorts score-descending, commits top-N, accumulates `pre_alloc_tokens` and `committed_ids`. Phase 2 calls `self.knapsack.slice()` on the stored-by-value `KnapsackSlice`. Phase 3 enforces caps using `selected_count` seeded from Phase 1.

One critical deviation from the plan was discovered during testing: `KnapsackSlice` reconstructs the DP solution set in reverse-index order (backtracking iterates items in reverse), not score-descending order. Without re-sorting Phase 2 output before Phase 3 cap enforcement, the `cap-exclusion` conformance vector failed — a lower-scoring item was returned earlier in DP output than a higher-scoring one, causing the wrong item to be capped. The fix adds a score-descending sort using a `score_by_content` HashMap built from `remaining` before the knapsack call. This became D180.

T03 added the CHANGELOG entry describing the 3-phase algorithm, constructor parameters, and reuse of M006 types.

## Verification

- `cargo test --test count_constrained_knapsack`: 5/5 pass (baseline, cap_exclusion, scarcity_degrade, tag_nonexclusive, require_and_cap)
- `cargo test --all-targets`: 175 passed, 0 failed, 14 suites
- `cargo clippy --all-targets -- -D warnings`: 0 warnings
- `grep "CountConstrainedKnapsackSlice" crates/cupel/src/lib.rs`: re-export confirmed
- `diff -r conformance/required/ crates/cupel/conformance/required/`: exits 0, drift-free
- `grep "CountConstrainedKnapsackSlice" CHANGELOG.md`: exits 0, entry confirmed

## Requirements Advanced

- R062 — `CountConstrainedKnapsackSlice` implemented in Rust with 5 passing conformance vectors; re-exported from crate root; Rust half of R062 satisfied

## Requirements Validated

- None validated yet — R062 requires both Rust (S01) and .NET (S02); validation deferred to S02 completion

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

- **Phase 2 output re-sort before cap enforcement (T02)**: Task plan did not specify needing to sort `KnapsackSlice` output before Phase 3. Required because `KnapsackSlice` reconstructs in reverse-index DP order, not score order. Added score-descending sort using `score_by_content` lookup. Recorded as D180.
- **Self-contained test file (T01)**: Task plan suggested using `run_count_quota_full_test` from `conformance/slicing.rs`. Impossible in standalone test binary — helpers inlined instead. Recorded as D182.

## Known Limitations

- Pipeline-level `count_requirement_shortfalls` wiring: `CountConstrainedKnapsackSlice` does not currently inject shortfall diagnostics into the `SelectionReport` via the pipeline's `TraceCollector`. The shortfalls are produced by the slicer and returned in the `SliceResult`, but the pipeline stage does not thread them through to the report's `count_requirement_shortfalls` field. This matches the equivalent limitation in `CountQuotaSlice` (D086 equivalent) and is accepted for v1.
- Pre-processing sub-optimality at tight budgets: Phase 1 commits required items before the knapsack sees the full candidate pool. If required items are token-heavy, the residual budget may be insufficient for optimal selection from Phase 2. Known and accepted trade-off (D174).

## Follow-ups

- S02: .NET implementation of `CountConstrainedKnapsackSlice` using Rust semantics as the spec-by-example
- S03: Spec chapter for `count-constrained-knapsack` — document 3-phase algorithm, pre-processing trade-off, cap enforcement after knapsack, and `is_count_quota()` interaction with `find_min_budget_for`

## Files Created/Modified

- `crates/cupel/src/slicer/count_constrained_knapsack.rs` — New: full CountConstrainedKnapsackSlice implementation (~250 lines)
- `crates/cupel/src/slicer/mod.rs` — Added mod declaration and pub use for CountConstrainedKnapsackSlice
- `crates/cupel/src/lib.rs` — Added CountConstrainedKnapsackSlice to pub use slicer block
- `crates/cupel/tests/count_constrained_knapsack.rs` — New: 5 integration tests with self-contained conformance helpers
- `crates/cupel/tests/conformance.rs` — Added CountConstrainedKnapsackSlice import and dispatch arm
- `crates/cupel/conformance/required/slicing/count-constrained-knapsack-baseline.toml` — New: baseline vector
- `crates/cupel/conformance/required/slicing/count-constrained-knapsack-cap-exclusion.toml` — New: cap enforcement vector
- `crates/cupel/conformance/required/slicing/count-constrained-knapsack-scarcity-degrade.toml` — New: scarcity degrade vector
- `crates/cupel/conformance/required/slicing/count-constrained-knapsack-tag-nonexclusive.toml` — New: non-exclusive tag vector
- `crates/cupel/conformance/required/slicing/count-constrained-knapsack-require-and-cap.toml` — New: require+cap with residual knapsack
- `conformance/required/slicing/count-constrained-knapsack-*.toml` — 5 files (copies of crates/cupel/conformance/)
- `spec/conformance/required/slicing/count-constrained-knapsack-*.toml` — 5 files (copies of crates/cupel/conformance/)
- `CHANGELOG.md` — Added CountConstrainedKnapsackSlice entry to unreleased section

## Forward Intelligence

### What the next slice should know
- The algorithm phases are fully symmetric between Rust and .NET — copy Phase 1 and Phase 3 logic verbatim from Rust `count_constrained_knapsack.rs`, adapting idioms to C#. Phase 2 calls `knapsack.Slice()` on the stored `KnapsackSlice` instance.
- The Phase 2 re-sort (D180) is critical: sort `KnapsackSlice` output by score descending before Phase 3 cap enforcement. Without this, the `cap-exclusion` conformance vector will fail.
- `KnapsackSlice` is a `Copy`/value type in Rust — in .NET it will be a class stored by reference. Both work; just store it as a field and call it directly.
- The standalone test file pattern (D182) is Rust-specific — .NET test projects can share helpers normally.
- `is_count_quota()` returning `true` is important for `.NET`'s `IsSlicer(ISlicer, out bool)` pattern — ensure the .NET interface implementation reflects this.

### What's fragile
- Pipeline-level shortfall wiring is absent — `count_requirement_shortfalls` will not appear in reports from pipeline runs. This is the same gap as CountQuotaSlice (D086). Do not promise this in S02.
- Phase 3 starts from Phase 1 selected_count (D181) — if this is re-implemented incorrectly starting from zero, cap enforcement will be too permissive.

### Authoritative diagnostics
- `cargo test --test count_constrained_knapsack -- --nocapture` — scenario-named test output, best failure localization
- `diff -r conformance/required/ crates/cupel/conformance/required/` — TOML drift; run after any TOML changes
- `cargo test --all-targets 2>&1 | grep -E "FAILED|error"` — broad regression surface

### What assumptions changed
- Phase 2 sort assumption: original plan assumed KnapsackSlice output was in score order. It is not — DP backtracking iterates items in reverse-index order. The sort was added after the cap-exclusion test failed.
- Test helper sharing: plan assumed `run_count_quota_full_test` was importable from conformance.rs. Not possible in standalone test binaries — helpers must be inlined.
