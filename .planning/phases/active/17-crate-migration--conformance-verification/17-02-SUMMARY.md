---
phase: 17-crate-migration--conformance-verification
plan: 02
subsystem: crate-tests-and-hooks
tags: [rust, migration, conformance, testing, pre-commit]
dependency-graph:
  requires: [17-01]
  provides: [conformance-tests-passing, pre-commit-guard, tarball-verified]
  affects: [18-01, 19-01]
tech-stack:
  added: []
  patterns: [cross-repo-copy, edition-2024-formatting, git-hooks]
key-files:
  created:
    - crates/cupel/tests/conformance.rs
    - crates/cupel/tests/conformance/pipeline.rs
    - crates/cupel/tests/conformance/placing.rs
    - crates/cupel/tests/conformance/scoring.rs
    - crates/cupel/tests/conformance/slicing.rs
    - .githooks/pre-commit
  modified:
    - crates/cupel/Cargo.toml (added tests/**/*.rs to include list)
decisions:
  - id: 17-02-D1
    decision: Added tests/**/*.rs to Cargo.toml include list for tarball round-trip verification
    context: Cargo package excluded test files by default due to explicit include list; tests must be in the tarball for unpacked-crate verification to work
metrics:
  duration: 21m 12s
  tasks: 3/3
  tests: 28 conformance tests passing
  replacements: 8 assay_cupel references replaced across 5 files
---

# Phase 17 Plan 02: Conformance Test Migration & Verification Summary

## Objective
Migrate conformance test files from assay-cupel, update all imports and paths, set up a pre-commit diff guard, and verify the complete crate passes tests, lint, and packaging.

## Tasks Completed

### Task 1: Copy test files and update imports
- Copied 5 test files from `/Users/wollax/Git/personal/assay/crates/assay-cupel/tests/`
- Replaced 8 `assay_cupel` references with `cupel` across 5 files:
  - conformance.rs: 1 `use` import
  - pipeline.rs: 1 `use` import + 3 qualified paths (`assay_cupel::Scorer`, `assay_cupel::Scorer`, `assay_cupel::CompositeScorer`)
  - placing.rs: 1 `use` import
  - scoring.rs: 0 (only uses `super::` imports)
  - slicing.rs: 1 `use` import
- Updated vector path: removed `.join("tests")` so path resolves to `CARGO_MANIFEST_DIR/conformance/required/`
- Commit: `5c3fd30`

### Task 2: Pre-commit hook for conformance vector diff guard
- Created `.githooks/pre-commit` that blocks commits when `conformance/required/` and `crates/cupel/conformance/required/` diverge
- Configured git hooks path: `git config core.hooksPath .githooks`
- Verified hook blocks on divergent vectors and passes when in sync
- Commit: `3f3b973`

### Task 3: Full verification
- Applied `cargo fmt` for edition 2024 formatting on test files (commit: `0d21f7e`)
- Added `tests/**/*.rs` to Cargo.toml `include` list for tarball verification (commit: `bf568c7`)
- All verifications passed:
  - `cargo test`: 28 tests passing (3 suites)
  - `cargo fmt --check`: exit 0
  - `cargo clippy --tests -- -D warnings`: exit 0
  - `cargo package --list`: 28 conformance vectors included
  - Tarball round-trip: unpacked crate passes all 28 tests independently
  - Zero `assay_cupel` references remain

## Deviations
1. **Auto-fix: edition 2024 formatting** — Test files from assay-cupel used edition 2021 style; applied `cargo fmt` to match edition 2024 conventions (same pattern as Plan 01)
2. **Auto-fix: test files missing from package** — Cargo.toml `include` list didn't have `tests/**/*.rs`, causing tarball to omit test files. Added the glob to enable tarball round-trip verification.

## Verification Results

| Check | Result |
|-------|--------|
| cargo test (28 vectors) | PASS |
| cargo fmt --check | PASS |
| cargo clippy --tests -- -D warnings | PASS |
| cargo package --list (28 vectors) | PASS |
| Tarball round-trip cargo test | PASS (28/28) |
| Pre-commit hook blocks divergence | PASS |
| Pre-commit hook passes when in sync | PASS |
| Zero assay_cupel references | PASS |
