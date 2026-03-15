---
phase: 18-dual-language-ci
plan: 01
subsystem: ci
tags: [github-actions, rust, dotnet, cargo-deny, ci]
dependency-graph:
  requires: [16-01, 16-02, 17-01]
  provides: [rust-ci-workflow, dotnet-ci-path-filters, cargo-deny-config]
  affects: [18-02, 19-01]
tech-stack:
  added: [cargo-deny]
  patterns: [path-filtered-ci, msrv-pinned-toolchain]
key-files:
  created:
    - .github/workflows/ci-rust.yml
    - crates/cupel/deny.toml
  modified:
    - .github/workflows/ci.yml
decisions:
  - id: 18-01-D1
    summary: "conformance/** excluded from ci-rust.yml paths — vendored at crates/cupel/conformance/ covered by crates/**"
metrics:
  duration: ~5min
  completed: 2026-03-15
---

# Phase 18 Plan 01: Rust CI Workflow & .NET Path Filters Summary

Rust CI workflow with fmt/clippy/test/cargo-deny checks, cargo-deny policy config, and .NET CI scoped to .NET-only paths.

## What Was Done

### Task 1: Create cargo-deny configuration and Rust CI workflow

Created `crates/cupel/deny.toml` with policies for advisories (yanked=deny), licenses (permissive OSS allowlist with 0.93 confidence), bans (wildcards=deny, multiple-versions=warn), and sources (unknown-registry/git=deny).

Created `.github/workflows/ci-rust.yml` with:
- Path-filtered triggers: `crates/**`, `rust-toolchain.toml`, self
- Toolchain pinned to 1.85.0 (MSRV) via `dtolnay/rust-toolchain`
- `Swatinem/rust-cache@v2` with workspace config
- Four check steps: fmt, clippy (-D warnings), test, cargo-deny

**Commit:** `647bf48`

### Task 2: Add path filters to .NET CI workflow

Added `paths` filters to both `push` and `pull_request` triggers in `ci.yml`:
- `src/**`, `tests/**`, `benchmarks/**`, `*.slnx`, `*.props`, `global.json`, self

**Commit:** `a40e669`

## Verification Results

| Check | Result |
|-------|--------|
| cargo fmt --check | Pass |
| cargo clippy -- -D warnings | Pass |
| cargo test | Pass (28/28) |
| cargo deny check | Pass (6 info warnings for unused license allowances — expected) |
| ci-rust.yml YAML valid | Pass |
| ci.yml YAML valid | Pass |

## Deviations from Plan

None — plan executed exactly as written.

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| 18-01-D1 | `conformance/**` excluded from ci-rust.yml path filters | Vendored conformance vectors at `crates/cupel/conformance/` are covered by `crates/**` glob. Root `conformance/` only affects spec.yml. |
