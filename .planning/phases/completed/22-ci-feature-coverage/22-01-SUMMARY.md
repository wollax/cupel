# Phase 22 Plan 01: CI Feature Coverage Summary

**Plan:** 22-01
**Phase:** 22 — CI Feature Coverage
**Completed:** 2026-03-15
**Duration:** ~3 minutes

## One-liner

Added `--all-features` Clippy and Test steps to both CI and release workflows, closing a silent gap where 33 serde-gated tests and 15 `cfg(feature = "serde")` code blocks were never exercised on any CI run.

## Tasks Completed

| # | Task | Commit | Result |
|---|------|--------|--------|
| 1 | Add `--all-features` steps to `ci-rust.yml` | c68f489 | ✓ Pass |
| 2 | Add `--all-features` steps to `release-rust.yml` | c68f489 | ✓ Pass |

## Changes Made

### `.github/workflows/ci-rust.yml`
- Renamed `Clippy` → `Clippy (default features)`
- Added `Clippy (all features)` step with `--all-features` flag
- Renamed `Test` → `Test (default features)`
- Added `Test (all features)` step with `--all-features` flag
- Step order: Format → Clippy (default features) → Clippy (all features) → Test (default features) → Test (all features) → Deny

### `.github/workflows/release-rust.yml`
- Same four changes applied to the `test` job
- Step order: Format → Clippy (default features) → Clippy (all features) → Test (default features) → Test (all features) → Deny → Verify package contents
- `publish` job unchanged

## Verification Results

| Check | Result |
|-------|--------|
| `ci-rust.yml` YAML valid | ✓ |
| `release-rust.yml` YAML valid | ✓ |
| `cargo clippy` (default features) | ✓ 0 warnings |
| `cargo clippy --all-features` | ✓ 0 warnings |
| `cargo test` (default features) | ✓ 28 conformance + 33 doctests |
| `cargo test --all-features` | ✓ 28 conformance + 33 serde + 33 doctests = 94 tests |

## Impact

- **33 serde tests** (`tests/serde.rs`) now run on every PR and push to main
- **15 `cfg(feature = "serde")` code blocks** in source now covered by Clippy
- Serde regressions now block release via the `test` job gate
- No other workflow steps or jobs modified

## Deviations

None — plan executed exactly as written.
