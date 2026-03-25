---
estimated_steps: 5
estimated_files: 17
---

# T01: Write 5 failing integration tests and TOML vectors

**Slice:** S01 — CountConstrainedKnapsackSlice — Rust implementation
**Milestone:** M009

## Description

Before implementing `CountConstrainedKnapsackSlice`, create the 5 TOML conformance vectors and the integration test file. The test file will reference the not-yet-existing type, so it will fail to compile — this is the intended red state that T02 makes green.

The 5 vectors cover the key behavioral scenarios established in the boundary map and proof strategy:

1. **baseline**: 3 items, require 2 of kind `tool`, knapsack selects the best residual — all selected
2. **cap-exclusion**: knapsack would select N > cap items; Phase 3 drops the excess
3. **scarcity-degrade**: fewer candidates than require_count; degrade continues with shortfall recorded
4. **tag-nonexclusive**: item with two matching kind tags counts toward both require constraints (per D055)
5. **require-and-cap**: combined require + cap with residual knapsack choosing by token efficiency

Each vector is copied to all 3 locations (D082):
- `spec/conformance/required/slicing/`
- `conformance/required/slicing/` (repo root)
- `crates/cupel/conformance/required/slicing/`

The integration test file uses the existing `run_count_quota_full_test` helper from `conformance/slicing.rs` since `CountConstrainedKnapsackSlice` has identical shortfall/cap semantics.

## Steps

1. Write `count-constrained-knapsack-baseline.toml`: budget=1000, 3 items at 100 tokens each (tool-a/0.9, tool-b/0.7, msg-x/0.5); require_count=2 cap_count=4 for "tool"; bucket_size=100; slicer="count_constrained_knapsack"; expected: all 3 selected (Phase 1 commits tool-a+tool-b, Phase 2 knapsack selects msg-x from residual 800 tokens)

2. Write `count-constrained-knapsack-cap-exclusion.toml`: budget=600, 4 items at 100 tokens each (tool-a/0.9, tool-b/0.8, tool-c/0.7, tool-d/0.6 all kind "tool"); require_count=1 cap_count=2; expected: 2 items selected (cap drops 2), cap_excluded_count=2; all items fit in budget so cap is the binding constraint

3. Write `count-constrained-knapsack-scarcity-degrade.toml`: budget=500, only 1 item of kind "tool" (tool-a/0.9, 100t); require_count=3 cap_count=5 for "tool"; scarcity_behavior="degrade"; expected: tool-a selected; shortfall_count=1 (only 1 of 3 required satisfied)

4. Write `count-constrained-knapsack-tag-nonexclusive.toml`: Use a single item tagged as both "tool" and "memory" (D055: non-exclusive); require_count=1 for both kinds; expected: item is selected and counts toward both constraints (no shortfall); note: TOML vectors use a single `kind` per item — this vector instead tests two separate items each satisfying different require constraints (one "tool" + one "memory"), verifying that both require constraints are satisfied independently

5. Write `count-constrained-knapsack-require-and-cap.toml`: budget=1000, 5 items: 2 "tool" items (100t each, required), 3 "msg" items (different token sizes: 50t, 150t, 200t); require_count=2 cap_count=2 for "tool"; no constraint on "msg"; bucket_size=1; Phase 1 commits 2 tool items (200t); residual=800t; knapsack picks best-fitting msg items — with bucket_size=1 it can be exact; expected selected_contents verified from algorithm trace

6. Copy each TOML to all 3 locations; write `crates/cupel/tests/count_constrained_knapsack.rs` with 5 `#[test]` functions calling `run_count_quota_full_test("slicing/count-constrained-knapsack-*.toml")` — this will fail to compile until T02 adds the type to the conformance harness arm; verify `diff conformance/required/ crates/cupel/conformance/required/` is clean

## Must-Haves

- [ ] 5 TOML files exist in `spec/conformance/required/slicing/`, `conformance/required/slicing/`, and `crates/cupel/conformance/required/slicing/` (15 total, 3 copies × 5 vectors)
- [ ] Each TOML has `slicer = "count_constrained_knapsack"` in `[test]` section
- [ ] `count-constrained-knapsack-baseline.toml`: expected selects all 3 items
- [ ] `count-constrained-knapsack-cap-exclusion.toml`: cap_excluded_count=2
- [ ] `count-constrained-knapsack-scarcity-degrade.toml`: shortfall_count=1
- [ ] `crates/cupel/tests/count_constrained_knapsack.rs` exists with 5 test functions
- [ ] `diff conformance/required/ crates/cupel/conformance/required/` exits 0

## Verification

- `ls spec/conformance/required/slicing/count-constrained-knapsack-*.toml | wc -l` → 5
- `ls conformance/required/slicing/count-constrained-knapsack-*.toml | wc -l` → 5
- `ls crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml | wc -l` → 5
- `diff -r conformance/required/ crates/cupel/conformance/required/` exits 0
- `cargo build --tests 2>&1 | grep "count_constrained_knapsack"` — compilation error expected (type not yet defined); if it somehow passes, the type was already added elsewhere and T02 starts immediately

## Observability Impact

- Signals added/changed: None (test scaffolding only)
- How a future agent inspects this: `ls crates/cupel/tests/count_constrained_knapsack.rs` confirms test file exists; TOML vectors encode the expected behavior in a human-readable format
- Failure state exposed: TOML parse errors would surface in `load_vector()` with the file path; test function names encode the scenario so failing tests are immediately identifiable

## Inputs

- `crates/cupel/conformance/required/slicing/count-quota-baseline.toml` — reference TOML format for count_quota vectors
- `crates/cupel/tests/conformance/slicing.rs` lines 80–211 — `run_count_quota_full_test` helper signature and usage pattern
- Research notes: Phase 1 commits top-N by score descending; Phase 2 is KnapsackSlice; Phase 3 drops over-cap items

## Expected Output

- 5 TOML files in each of the 3 conformance locations (15 files total)
- `crates/cupel/tests/count_constrained_knapsack.rs` with 5 test stubs that compile-fail due to missing type (expected)
- Clean diff between `conformance/required/` and `crates/cupel/conformance/required/`
