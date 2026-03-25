# S01: CountConstrainedKnapsackSlice — Rust implementation — UAT

**Milestone:** M009
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: `CountConstrainedKnapsackSlice` is a pure library function with no I/O, UI, or runtime dependencies. All behavioral claims are fully verified by the 5 conformance integration tests and `cargo test --all-targets`. Human execution cannot reveal information that the test suite does not already prove.

## Preconditions

- Rust toolchain installed (MSRV 1.85+)
- Working directory: `crates/cupel/` (or run from repo root with full paths)

## Smoke Test

```bash
cd crates/cupel && cargo test --test count_constrained_knapsack
```

Expected: `5 tests` reported, `0 failed`.

## Test Cases

### 1. Baseline — all 3 items selected (Phase 1 + Phase 2 both contribute)

1. Run: `cargo test --test count_constrained_knapsack count_constrained_knapsack_baseline`
2. **Expected:** test passes; 2 tool items committed in Phase 1, 1 msg item selected in Phase 2; all 3 in `selected`

### 2. Cap enforcement — over-cap items dropped by Phase 3

1. Run: `cargo test --test count_constrained_knapsack count_constrained_knapsack_cap_exclusion`
2. **Expected:** test passes; 4 tool items, cap=2; 2 selected, 2 excluded with `cap_excluded_count == 2`

### 3. Scarcity degrade — shortfall recorded when require not satisfiable

1. Run: `cargo test --test count_constrained_knapsack count_constrained_knapsack_scarcity_degrade`
2. **Expected:** test passes; require=3 but only 1 candidate; `shortfall_count == 1`; slicer returns successfully (degrade, not error)

### 4. Tag non-exclusive counting — items satisfying multiple kinds counted correctly

1. Run: `cargo test --test count_constrained_knapsack count_constrained_knapsack_tag_nonexclusive`
2. **Expected:** test passes; require 1 "tool" AND 1 "memory" independently; all 3 items selected

### 5. Require+cap with residual knapsack

1. Run: `cargo test --test count_constrained_knapsack count_constrained_knapsack_require_and_cap`
2. **Expected:** test passes; 2 tools committed (at cap), 3 msg items selected from residual; all 5 selected

## Edge Cases

### Import from crate root

1. In any Rust project: `use cupel::CountConstrainedKnapsackSlice;`
2. Run: `grep "CountConstrainedKnapsackSlice" crates/cupel/src/lib.rs`
3. **Expected:** match found — type is part of public API

### TOML drift guard

1. Run: `diff -r conformance/required/ crates/cupel/conformance/required/` from repo root
2. **Expected:** exits 0 — no output, no divergence

## Failure Signals

- Any test in `count_constrained_knapsack` suite FAILED → algorithm regression
- `unknown slicer type: count_constrained_knapsack` → dispatch arm missing from conformance.rs or standalone build_slicer_by_type
- `diff -r conformance/required/ crates/cupel/conformance/required/` non-zero exit → TOML drift
- `cargo clippy --all-targets -- -D warnings` warnings → code quality regression

## Requirements Proved By This UAT

- R062 (Rust half) — `CountConstrainedKnapsackSlice` constructable in Rust, passes 5 conformance integration tests, exported from `cupel` crate root; 3-phase algorithm (count-satisfy → knapsack-distribute → cap-enforce) proven across all 5 behavioral scenarios

## Not Proven By This UAT

- R062 (.NET half) — `.NET CountConstrainedKnapsackSlice` implementation is S02's responsibility; R062 is not fully validated until S02 completes
- Pipeline-level `count_requirement_shortfalls` wiring — shortfalls are produced by the slicer but not propagated to `SelectionReport.count_requirement_shortfalls` when running through a full `Pipeline`. This is a known limitation (D086 equivalent) not covered by S01 tests.
- `find_min_budget_for` interaction — `is_count_quota()=true` enables the monotonicity guard; correct interaction with binary search was proven in R054 for `CountQuotaSlice` and the same mechanism applies, but no new integration test was added specifically for `CountConstrainedKnapsackSlice` + `find_min_budget_for`
- Spec chapter — `spec/src/slicers/count-constrained-knapsack.md` is S03's responsibility

## Notes for Tester

All behavioral verification is automated — the conformance test suite provides complete coverage of the 5 specified scenarios. The human tester should check that `cargo test --test count_constrained_knapsack` outputs exactly 5 passing tests and that the crate root import works. The remaining items (pipeline wiring, .NET parity, spec chapter) are deferred to S02/S03.
