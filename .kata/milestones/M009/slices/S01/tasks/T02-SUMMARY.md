---
id: T02
parent: S01
milestone: M009
provides:
  - CountConstrainedKnapsackSlice struct (Clone, Debug) in crates/cupel/src/slicer/count_constrained_knapsack.rs
  - Full 3-phase algorithm: Phase 1 count-satisfy, Phase 2 knapsack-distribute (KnapsackSlice by value), Phase 3 cap enforcement
  - Slicer trait impl with is_count_quota()=true and count_cap_map()
  - QuotaPolicy trait impl returning QuotaConstraintMode::Count constraints
  - Re-exported from cupel crate root (lib.rs pub use slicer block)
  - "count_constrained_knapsack" dispatch arm in conformance.rs build_slicer_by_type
  - "count_constrained_knapsack" dispatch arm in count_constrained_knapsack.rs build_slicer_by_type (standalone test file)
key_files:
  - crates/cupel/src/slicer/count_constrained_knapsack.rs
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/conformance.rs
  - crates/cupel/tests/count_constrained_knapsack.rs
key_decisions:
  - "Phase 2 output sorted by score descending before Phase 3 cap enforcement: KnapsackSlice returns items in DP-reconstruction order (reverse index), not score order. Without re-sorting, lower-scoring items could survive the cap while higher-scoring ones are dropped."
  - "score_by_content lookup map built from `remaining` slice before calling knapsack — avoids needing to re-scan scored_items after Phase 1 filtering"
  - "No constructor guard against is_knapsack() — CountConstrainedKnapsackSlice IS the knapsack wrapper; the guard in CountQuotaSlice::new explicitly does not apply here"
  - "KnapsackSlice stored by value (Copy), not Box<dyn Slicer> — Phase 2 calls self.knapsack.slice() directly"
  - "Phase 3 starts with selected_count from Phase 1 — starting from zero would incorrectly allow cap+1 items across both phases"
patterns_established:
  - "Phase 2 output re-sort before cap enforcement: always sort knapsack output by score descending before applying count caps, since knapsack DP reconstruction does not guarantee score order"
observability_surfaces:
  - "cargo test --test count_constrained_knapsack -- --nocapture — 5 conformance tests with scenario-named test functions"
  - "CupelError::TableTooLarge { candidates, capacity, cells } propagated from KnapsackSlice when DP table would exceed 50M cells"
  - "CupelError::SlicerConfig from ScarcityBehavior::Throw when require_count not satisfiable"
duration: ~25min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T02: Implement CountConstrainedKnapsackSlice and wire into slicer module

**3-phase CountConstrainedKnapsackSlice (count-satisfy → knapsack-distribute → cap-enforce) implemented, re-exported from crate root, wired into conformance harness; all 5 integration tests pass, 175 total tests pass, clippy clean.**

## What Happened

Created `crates/cupel/src/slicer/count_constrained_knapsack.rs` (~250 lines) with the full 3-phase algorithm. Phase 1 is copied verbatim from `CountQuotaSlice` — partitions by kind, sorts score-descending, commits top-N, accumulates `pre_alloc_tokens` and `committed_ids`. Phase 2 calls `self.knapsack.slice()` on the stored-by-value `KnapsackSlice` (Copy) rather than a `Box<dyn Slicer>`. Phase 3 filters Phase 2 output using `selected_count` from Phase 1 as the starting state.

One deviation from the task plan was discovered during testing: KnapsackSlice returns items in DP-reconstruction order (iterating items in reverse index during backtracking), not score-descending order. Without re-sorting Phase 2 output before Phase 3 cap enforcement, `cap_excluded_count` vector failed because a lower-scoring item (tool-d, score 0.6) was returned earlier in the DP output than a higher-scoring item (tool-b, score 0.8), causing tool-d to survive the cap while tool-b was dropped. Fixed by adding a score-descending sort of phase2_selected before the cap loop, using a `score_by_content` HashMap built from `remaining` before the knapsack call.

Wired into `slicer/mod.rs` (mod declaration + pub use), `lib.rs` (CountConstrainedKnapsackSlice in pub use slicer block), `conformance.rs` (import + arm parsing bucket_size/scarcity_behavior/entries, constructing KnapsackSlice then CountConstrainedKnapsackSlice), and the standalone `count_constrained_knapsack.rs` test file (replacing the T02 placeholder comment with the live arm).

## Verification

- `cargo test --test count_constrained_knapsack`: 5/5 pass (baseline, cap_exclusion, scarcity_degrade, tag_nonexclusive, require_and_cap)
- `rtk cargo test --all-targets`: 175 passed, 0 failed, 14 suites
- `cargo clippy --all-targets -- -D warnings`: 0 warnings
- `grep "CountConstrainedKnapsackSlice" crates/cupel/src/lib.rs`: confirmed re-export present

## Diagnostics

- `cargo test --test count_constrained_knapsack -- --nocapture` for detailed per-test output
- `cargo test count_constrained_knapsack 2>&1 | grep -E "test .+ ok|FAILED"` — all 5 ok
- Phase 2 OOM guard: `CupelError::TableTooLarge { candidates, capacity, cells }` propagated from KnapsackSlice when DP table exceeds 50M cells
- Phase 1 scarcity: `CupelError::SlicerConfig` returned when `ScarcityBehavior::Throw` and candidate pool < require_count

## Deviations

**Phase 2 output re-sort added (not in task plan):** Task plan did not mention needing to sort KnapsackSlice output before Phase 3. This was required because KnapsackSlice reconstructs the selected set in reverse-index DP order, not score order. Added a score-descending sort using a `score_by_content` lookup built from `remaining`. Without this, the cap_exclusion conformance vector failed.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/slicer/count_constrained_knapsack.rs` — New: full CountConstrainedKnapsackSlice implementation (~250 lines)
- `crates/cupel/src/slicer/mod.rs` — Added mod declaration and pub use for CountConstrainedKnapsackSlice
- `crates/cupel/src/lib.rs` — Added CountConstrainedKnapsackSlice to pub use slicer block
- `crates/cupel/tests/conformance.rs` — Added CountConstrainedKnapsackSlice import and "count_constrained_knapsack" arm
- `crates/cupel/tests/count_constrained_knapsack.rs` — Replaced placeholder comment with live "count_constrained_knapsack" arm; updated imports
