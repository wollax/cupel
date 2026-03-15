# Phase 22: CI Feature Coverage — Verification

**Status:** passed
**Verified:** 2026-03-15
**Score:** 4/4 must-haves verified

## Must-Have Verification

| # | Must-Have | Status | Evidence |
|---|-----------|--------|----------|
| 1 | CI runs cargo test with both default features and --all-features on every PR and push to main | PASS | `ci-rust.yml` lines 43-44 (`Test (default features)`) and lines 46-47 (`Test (all features)`) |
| 2 | Release workflow runs cargo test with both default features and --all-features before publishing | PASS | `release-rust.yml` lines 43-44 (`Test (default features)`) and lines 46-47 (`Test (all features)`) in `test` job, which gates `publish` job |
| 3 | CI runs cargo clippy with both default features and --all-features | PASS | `ci-rust.yml` lines 37-38 (`Clippy (default features)`) and lines 40-41 (`Clippy (all features)`); same in `release-rust.yml` |
| 4 | 33 serde-gated tests and 15 cfg(feature = serde) code blocks are exercised in CI | PASS | `--all-features` flag enables `serde` feature; local `cargo test --all-features` confirms 94 tests (28 conformance + 33 serde + 33 doctests) |

## Artifact Verification

| Artifact | Expected | Actual |
|----------|----------|--------|
| `.github/workflows/ci-rust.yml` | Clippy and Test steps for both default and all-features | Present: 4 quality gate steps in correct order |
| `.github/workflows/release-rust.yml` | Clippy and Test steps for both default and all-features | Present: 4 quality gate steps in correct order, publish job unchanged |

## Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| `ci-rust.yml` | `crates/cupel/Cargo.toml` | `--manifest-path` and `--all-features` flag | PASS |
| `release-rust.yml` | `crates/cupel/Cargo.toml` | `--manifest-path` and `--all-features` flag | PASS |

## Result

All 4 must-haves verified against actual workflow files. Phase goal achieved.
