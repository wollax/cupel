---
phase: 19-first-publish--assay-switchover
plan: 01
subsystem: ci-cd
tags: [github-actions, crates-io, oidc, release]
dependency-graph:
  requires: [18-02]
  provides: [release-workflow-oidc, cargo-publish-ready]
  affects: [19-02]
tech-stack:
  added: [rust-lang/crates-io-auth-action@v1]
  patterns: [oidc-with-token-fallback]
key-files:
  created: []
  modified: [.github/workflows/release-rust.yml]
decisions:
  - id: 19-01-D1
    decision: "OIDC auth action with continue-on-error + secret fallback for first publish compatibility"
metrics:
  duration: ~2min
  completed: 2026-03-15
---

# Phase 19 Plan 01: Release Workflow OIDC & First Publish Fixes Summary

**OIDC auth action with token fallback, --locked removed, make_latest true**

## What Was Done

Updated `.github/workflows/release-rust.yml` with four changes to prepare for first publish and future OIDC-based publishing:

1. **Added `id-token: write` permission** -- required for GitHub OIDC token exchange with crates.io trusted publishing.

2. **Added `rust-lang/crates-io-auth-action@v1`** with `continue-on-error: true` -- attempts OIDC authentication first, falls back gracefully to `secrets.CARGO_REGISTRY_TOKEN` for first publish (OIDC requires an existing crate on crates.io).

3. **Removed `--locked` flag** from `cargo publish` -- Cargo.lock is gitignored per library crate convention (decision 16-01-D3), so `--locked` would fail with "lock file needs to be updated".

4. **Changed `make_latest: false` to `make_latest: true`** -- Rust is the primary distribution going forward.

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| 19-01-D1 | OIDC auth with continue-on-error fallback to secret | First publish requires token (OIDC needs existing crate); subsequent publishes use OIDC automatically |

## Deviations from Plan

None -- plan executed exactly as written.

## Commits

| Hash | Message |
|------|---------|
| 53cfc22 | feat(19-01): prepare release workflow for first publish and OIDC auth |

## Verification Results

- YAML valid: PASS
- `id-token: write` present: PASS (count: 1)
- `crates-io-auth-action` present: PASS (count: 1)
- `--locked` absent: PASS (count: 0)
- `make_latest: true` present: PASS (count: 1)
- Secret fallback present: PASS (count: 1)

## Next Phase Readiness

Plan 19-02 (first publish execution and assay switchover) can proceed. The workflow is ready for manual `workflow_dispatch` trigger with `CARGO_REGISTRY_TOKEN` secret configured in the `release` environment.
