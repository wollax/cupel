---
id: T01
parent: S01
milestone: M009
provides:
  - 5 TOML conformance vectors for CountConstrainedKnapsackSlice in all 3 required locations (15 files total)
  - crates/cupel/tests/count_constrained_knapsack.rs with 5 self-contained integration tests
  - Test infrastructure (load_vector, build_scored_items, build_slicer, shortfall/cap checks) inlined in test file
  - Verified red state: all 5 tests panic with "unknown slicer type: count_constrained_knapsack" at runtime
  - Clean diff between conformance/required/ and crates/cupel/conformance/required/
key_files:
  - crates/cupel/tests/count_constrained_knapsack.rs
  - crates/cupel/conformance/required/slicing/count-constrained-knapsack-baseline.toml
  - crates/cupel/conformance/required/slicing/count-constrained-knapsack-cap-exclusion.toml
  - crates/cupel/conformance/required/slicing/count-constrained-knapsack-scarcity-degrade.toml
  - crates/cupel/conformance/required/slicing/count-constrained-knapsack-tag-nonexclusive.toml
  - crates/cupel/conformance/required/slicing/count-constrained-knapsack-require-and-cap.toml
key_decisions:
  - Self-contained test file: count_constrained_knapsack.rs inlines all conformance helpers (load_vector, build_scored_items, build_slicer_by_type, assert_set_eq, shortfall/cap checks) rather than importing from conformance.rs — the integration test binaries in tests/ are each independent and cannot share code across test entry points via normal module imports
  - Red state via runtime panic (not compile error): the tests compile cleanly but panic at runtime with "unknown slicer type: count_constrained_knapsack" — this is acceptable since T02 adds the dispatch arm in both conformance.rs and the standalone build_slicer_by_type copy in count_constrained_knapsack.rs
  - require-and-cap vector uses bucket_size=1 so all msg items fit exactly without knapsack approximation artifacts, making the expected output deterministic
patterns_established:
  - Self-contained integration test pattern: when a test binary cannot share helpers with conformance.rs, inline the minimal needed helpers directly in the new test file — avoids #[path] hacks and matches the project's existing self-contained test style (e.g. count_quota_composition.rs)
observability_surfaces:
  - cargo test --test count_constrained_knapsack shows 5 FAILED with "unknown slicer type" — confirms tests are wired and vectors load correctly
  - diff -r conformance/required/ crates/cupel/conformance/required/ confirms drift-free state
duration: 30m
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T01: Write 5 failing integration tests and TOML vectors

**5 TOML conformance vectors (15 files total across 3 locations) + self-contained failing integration test file for CountConstrainedKnapsackSlice**

## What Happened

Created 5 TOML conformance vectors covering the key behavioral scenarios for `CountConstrainedKnapsackSlice`:
- **baseline**: 3 items (2 tool + 1 msg), Phase 1 commits 2 tools, Phase 2 knapsack picks msg-x from residual — all 3 selected
- **cap-exclusion**: 4 tool items, cap=2; Phase 1 commits 1, Phase 2 knapsack picks 1 more, Phase 3 drops 2 — cap_excluded_count=2
- **scarcity-degrade**: require=3 but only 1 tool candidate; shortfall_count=1, 1 item selected
- **tag-nonexclusive**: require 1 "tool" and 1 "memory" independently; Phase 1 satisfies both; all 3 items selected
- **require-and-cap**: require=2 cap=2 tool + 3 msg items; Phase 1 commits 2 tools (at cap), Phase 2 knapsack picks all 3 msg items — all 5 selected

Each TOML was copied to all 3 required locations per D082.

The integration test file (`crates/cupel/tests/count_constrained_knapsack.rs`) is self-contained — it inlines the minimal conformance helpers rather than trying to import from `conformance.rs`. This is because Rust's integration test binaries are independent and cannot share code across test entry points without a shared library. The `#[path]` include approach was considered but rejected because `conformance/slicing.rs` uses `super::` imports that would break when relocated.

The tests compile cleanly (pre-commit passed) and fail at runtime with "unknown slicer type: count_constrained_knapsack" — the intended red state.

## Verification

- `ls spec/conformance/required/slicing/count-constrained-knapsack-*.toml | wc -l` → 5 ✓
- `ls conformance/required/slicing/count-constrained-knapsack-*.toml | wc -l` → 5 ✓
- `ls crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml | wc -l` → 5 ✓
- `diff -r conformance/required/ crates/cupel/conformance/required/` → exits 0 ✓
- `cargo test --test count_constrained_knapsack` → 5 FAILED with "unknown slicer type: count_constrained_knapsack" ✓ (expected red)
- `cargo build --tests` → compiled successfully ✓

## Diagnostics

- `cargo test --test count_constrained_knapsack 2>&1` shows all 5 test names and failure reason
- `diff -r conformance/required/ crates/cupel/conformance/required/` confirms drift-free; run this after any TOML changes
- TOML vectors encode expected behavior in comments with phase-by-phase traces — readable without running the code

## Deviations

- **Test design**: Task plan suggested using `run_count_quota_full_test` from `conformance/slicing.rs`. This is impossible in a standalone test binary (integration tests cannot share code across `tests/*.rs` files). Instead, the full helper logic was inlined in `count_constrained_knapsack.rs`. Functionally identical.
- **Red state**: Expected compile error; actual red state is a runtime panic. The pre-commit hook runs `cargo fmt` and `cargo clippy --deny warnings`, so the test binary must compile — the type stub `other => panic!("unknown slicer type: {other}")` satisfies the compiler while preserving the expected failure.

## Known Issues

None.

## Files Created/Modified

- `spec/conformance/required/slicing/count-constrained-knapsack-baseline.toml` — baseline TOML vector (all 3 selected)
- `spec/conformance/required/slicing/count-constrained-knapsack-cap-exclusion.toml` — cap enforcement vector (cap_excluded_count=2)
- `spec/conformance/required/slicing/count-constrained-knapsack-scarcity-degrade.toml` — scarcity degrade vector (shortfall_count=1)
- `spec/conformance/required/slicing/count-constrained-knapsack-tag-nonexclusive.toml` — two-kind independent require vector
- `spec/conformance/required/slicing/count-constrained-knapsack-require-and-cap.toml` — combined require+cap with residual knapsack
- `conformance/required/slicing/count-constrained-knapsack-*.toml` — 5 files (copies of spec/)
- `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml` — 5 files (copies of spec/)
- `crates/cupel/tests/count_constrained_knapsack.rs` — 5 integration test functions with self-contained conformance helpers
