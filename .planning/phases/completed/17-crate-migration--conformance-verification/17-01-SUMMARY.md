---
phase: 17-crate-migration--conformance-verification
plan: 01
subsystem: crate-source
tags: [rust, migration, conformance, cargo]
dependency-graph:
  requires: [16-01, 16-02]
  provides: [compilable-crate, conformance-vectors-vendored]
  affects: [17-02, 18-01]
tech-stack:
  added: []
  patterns: [cross-repo-copy, edition-2024-formatting]
key-files:
  created:
    - crates/cupel/src/lib.rs
    - crates/cupel/src/error.rs
    - crates/cupel/src/model/ (7 files)
    - crates/cupel/src/pipeline/ (7 files)
    - crates/cupel/src/placer/ (3 files)
    - crates/cupel/src/scorer/ (9 files)
    - crates/cupel/src/slicer/ (4 files)
    - crates/cupel/conformance/required/ (28 .toml files in 4 subdirs)
  modified:
    - crates/cupel/Cargo.toml
decisions:
  - id: 17-01-D1
    decision: Applied cargo fmt to align source with edition 2024 style rules
    context: Source files from assay-cupel used edition 2021 formatting; cupel crate uses edition 2024
metrics:
  duration: 6m 14s
  completed: 2026-03-15
---

# Phase 17 Plan 01: Source & Conformance Vector Migration Summary

**One-liner:** Migrated 32 Rust source files from assay-cupel and vendored 28 conformance vectors into the cupel crate, with edition 2024 formatting applied.

## What Was Done

### Task 1: Copy source files from assay into cupel crate
- Removed Phase 16 placeholder `lib.rs`
- Copied 32 `.rs` files (26 source + 6 `mod.rs`) from `/Users/wollax/Git/personal/assay/crates/assay-cupel/src/`
- Verified byte-identical copy via `diff -r`
- Confirmed zero references to `assay_cupel` in source
- **Commit:** `016e2db`

### Task 2: Copy conformance vectors and update Cargo.toml include
- Copied 28 required conformance `.toml` vectors into `crates/cupel/conformance/required/`
- Vectors span 4 subdirectories: pipeline (5), placing (4), scoring (12), slicing (7)
- Verified byte-identical to repo-root canonical copies
- Added `"conformance/**/*.toml"` to Cargo.toml `include` array
- **Commit:** `822092b`

### Task 3: Verify crate builds cleanly
- `cargo build` — passed
- `cargo clippy -- -D warnings` — passed
- `cargo fmt --check` — initially failed due to edition 2024 formatting differences
- Applied `cargo fmt` to reformat 14 files, all three checks now pass
- **Commit:** `8c19c3b`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Edition 2024 formatting mismatch**
- **Found during:** Task 3
- **Issue:** Source files from assay-cupel used edition 2021 formatting style. The cupel crate targets edition 2024, which has stricter import ordering and different line-wrapping rules. `cargo fmt --check` failed on 14 files.
- **Fix:** Ran `cargo fmt` to apply edition 2024 formatting rules automatically.
- **Files modified:** 14 source files (lib.rs, pipeline/classify.rs, pipeline/mod.rs, pipeline/place.rs, pipeline/slice.rs, pipeline/sort.rs, placer/chronological.rs, placer/u_shaped.rs, scorer/composite.rs, scorer/kind.rs, scorer/scaled.rs, slicer/greedy.rs, slicer/knapsack.rs, slicer/quota.rs)
- **Commit:** `8c19c3b`

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| 17-01-D1 | Applied cargo fmt for edition 2024 | Source used edition 2021 style; cupel targets 2024. Automated formatting is safe and non-semantic. |

## Verification Results

| Check | Result |
|-------|--------|
| Source file count (32 .rs) | PASS |
| No `assay_cupel` references | PASS |
| Conformance vector count (28 .toml) | PASS |
| Conformance vectors byte-identical to canonical | PASS |
| `cargo build` | PASS |
| `cargo clippy -- -D warnings` | PASS |
| `cargo fmt --check` | PASS |

## Next Phase Readiness

- Crate compiles with all production source code in place
- Conformance vectors are vendored and included in the package tarball
- Ready for Plan 17-02: test migration and conformance test wiring
- No blockers identified
