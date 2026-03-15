---
phase: 18
status: passed
score: 11/11
verified: 2026-03-14
---

# Phase 18 Verification: Dual-Language CI

## Must-Have Verification

### Plan 01 Must-Haves

| # | Must-Have | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Rust CI triggers on PRs touching `crates/**`, `rust-toolchain.toml`, or `ci-rust.yml` | PASS | `ci-rust.yml` lines 11-15: pull_request paths include all three |
| 2 | Rust CI runs `cargo fmt --check` | PASS | `ci-rust.yml` line 35: `cargo fmt --check --manifest-path crates/cupel/Cargo.toml` |
| 3 | Rust CI runs `cargo clippy -- -D warnings` | PASS | `ci-rust.yml` line 38: `cargo clippy --manifest-path crates/cupel/Cargo.toml -- -D warnings` |
| 4 | Rust CI runs `cargo test` | PASS | `ci-rust.yml` line 41: `cargo test --manifest-path crates/cupel/Cargo.toml` |
| 5 | Rust CI runs `cargo-deny check` | PASS | `ci-rust.yml` lines 43-46: uses `EmbarkStudios/cargo-deny-action@v2` |
| 6 | .NET CI only triggers on .NET-relevant paths (`src/**`, `tests/**`, `benchmarks/**`, `*.slnx`, `*.props`, `global.json`, own workflow file) | PASS | `ci.yml` lines 6-23: pull_request and push paths match all required patterns exactly |
| 7 | `cargo-deny` passes locally | PASS | `cargo deny check` exits 0 (warnings only for unused allowances — not errors) |

### Plan 02 Must-Haves

| # | Must-Have | Status | Evidence |
|---|-----------|--------|----------|
| 8 | Release workflow has `workflow_dispatch` trigger with `dry-run` boolean input | PASS | `release-rust.yml` lines 4-10: `workflow_dispatch` with `dry-run` boolean input, default `false` |
| 9 | Release workflow runs full test suite (fmt, clippy, test, cargo-deny) before any publish step | PASS | `release-rust.yml` `test` job (lines 16-50) runs all four checks; `publish` job declares `needs: test` |
| 10 | Publish step is gated behind `release` environment and `dry-run=false` condition | PASS | `release-rust.yml` lines 54-55: `if: ${{ inputs.dry-run != true }}` and `environment: release` |
| 11 | Successful publish creates a GitHub Release with `rust-v{version}` tag | PASS | `release-rust.yml` lines 87-92: `softprops/action-gh-release@v2` with `tag_name: rust-v${{ steps.version.outputs.version }}` |

## Local Command Verification

| Command | Result |
|---------|--------|
| `cargo fmt --check` | PASS — no output, exit 0 |
| `cargo clippy -- -D warnings` | PASS — "Finished dev profile", exit 0 |
| `cargo test` | PASS — 28 tests passed across 3 suites |
| `cargo deny check` | PASS — "advisories ok, bans ok, licenses ok, sources ok" (6 unused-allowance warnings, no errors) |

## Artifacts

| Path | Exists | Min Lines | Actual Lines |
|------|--------|-----------|-------------|
| `.github/workflows/ci-rust.yml` | Yes | 20 | 46 |
| `.github/workflows/ci.yml` | Yes | 20 | 43 |
| `crates/cupel/deny.toml` | Yes | 10 | 28 |
| `.github/workflows/release-rust.yml` | Yes | 40 | 92 |

## Summary

All 11 must-haves from Plans 01 and 02 are satisfied. The four workflow/config artifacts are present and correctly configured:

- **ci-rust.yml** triggers on the correct path filters, runs all four Rust quality gates (fmt, clippy, test, cargo-deny), and uses pinned action versions.
- **ci.yml** is correctly narrowed to .NET-relevant paths, preventing unnecessary Rust rebuilds on .NET-only PRs.
- **deny.toml** is valid and cargo-deny passes locally with zero errors.
- **release-rust.yml** implements a proper two-job pipeline: a blocking `test` job followed by a `publish` job that is environment-gated (`release`) and dry-run-gated (`inputs.dry-run != true`). The GitHub Release is tagged `rust-v{version}` as required.

All local commands pass. Phase 18 is complete.
