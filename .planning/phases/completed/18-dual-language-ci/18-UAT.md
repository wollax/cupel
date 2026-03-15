---
status: complete
phase: 18-dual-language-ci
source: [18-01-SUMMARY.md, 18-02-SUMMARY.md]
started: 2026-03-15T00:00:00Z
updated: 2026-03-15T00:00:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Rust CI workflow exists with correct structure
expected: `.github/workflows/ci-rust.yml` exists with name "CI (Rust)", path filters for `crates/**`, `rust-toolchain.toml`, and self. Steps: checkout, setup rust 1.85.0, cache, fmt, clippy, test, deny.
result: pass

### 2. cargo-deny configuration and local check
expected: `crates/cupel/deny.toml` exists with advisories (yanked=deny), licenses (permissive allowlist), bans (wildcards=deny), and sources (unknown-registry=deny). Running `cargo deny --manifest-path crates/cupel/Cargo.toml check` passes locally.
result: pass

### 3. .NET CI has path filters
expected: `.github/workflows/ci.yml` now has `paths` filters on both push and pull_request triggers. Paths include `src/**`, `tests/**`, `benchmarks/**`, `*.slnx`, `*.props`, `global.json`, and self.
result: pass

### 4. Rust release workflow structure
expected: `.github/workflows/release-rust.yml` exists with `workflow_dispatch` trigger, `dry-run` boolean input (default false). Test job runs fmt, clippy, test, deny, and package --list. Publish job gated on `needs: test`, `dry-run != true`, and `environment: release`.
result: pass

### 5. Release tag namespacing
expected: GitHub Release step in `release-rust.yml` uses `rust-v` prefix for tags (e.g., `rust-v1.0.0`), distinct from .NET's `v` prefix. `make_latest: false` prevents displacing .NET releases.
result: pass

### 6. Cargo checks pass locally
expected: Running `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` all pass from `crates/cupel/`.
result: pass

## Summary

total: 6
passed: 6
issues: 0
pending: 0
skipped: 0

## Gaps

[none]
