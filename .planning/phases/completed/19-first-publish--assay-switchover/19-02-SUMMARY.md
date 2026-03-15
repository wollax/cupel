---
phase: 19-first-publish--assay-switchover
plan: 02
subsystem: publishing
tags: [crates-io, oidc, registry-dependency, assay-switchover]
dependency-graph:
  requires: [19-01]
  provides: [cupel-on-crates-io, assay-registry-dependency]
  affects: [20-01]
tech-stack:
  added: []
  patterns: [oidc-trusted-publishing, patch-crates-io-local-dev]
key-files:
  created: []
  modified:
    - /Users/wollax/Git/personal/assay/Cargo.toml
    - /Users/wollax/Git/personal/assay/CONTRIBUTING.md
decisions: []
metrics:
  duration: ~5min
  completed: 2026-03-15
---

# Phase 19 Plan 02: First Publish & Assay Switchover Summary

**cupel 1.0.0 published to crates.io, OIDC configured, assay switched to registry dependency**

## What Was Done

### Task 1 (human-action checkpoint): First publish to crates.io

- Created scoped crates.io API token with `publish-new` + `publish-update` scopes
- Added `CARGO_REGISTRY_TOKEN` as GitHub repository secret
- Triggered `release-rust.yml` workflow from `main` branch
- Verified cupel 1.0.0 is live on crates.io with correct metadata
- Verified `rust-v1.0.0` GitHub release created and marked as Latest

### Task 2 (human-action checkpoint): OIDC trusted publishing

- Configured trusted publisher on crates.io: `wollax/cupel/release-rust.yml/release`
- Deleted `CARGO_REGISTRY_TOKEN` repository secret from GitHub
- Revoked personal API token from crates.io

### Task 3 (auto): Switch assay to registry dependency

- Changed `cupel = { path = "../cupel/crates/cupel" }` to `cupel = "1.0.0"` in assay workspace Cargo.toml
- Added "Working with Local cupel" section to CONTRIBUTING.md documenting `[patch.crates-io]` local development pattern
- All 714 assay tests pass against the published crate from crates.io

## Authentication Gates

Two human-action checkpoints were required because:

1. **First publish** -- crates.io requires a personal API token for the initial publish of a new crate. OIDC trusted publishing cannot be configured until the crate exists on the registry.
2. **OIDC configuration** -- trusted publisher setup is a manual crates.io UI operation. Once configured, the API token was revoked, leaving OIDC as the sole authentication method for future publishes.

## Deviations from Plan

None -- plan executed exactly as written.

## Commits

| Hash | Repo | Message |
|------|------|---------|
| (human) | cupel | Tasks 1-2: first publish and OIDC configuration |
| 0b34fbd | assay | feat(19-02): switch to cupel registry dependency |

## Verification Results

- `cupel = "1.0.0"` in assay Cargo.toml: PASS
- No path dependency for cupel remaining: PASS (0 matches)
- `[patch.crates-io]` documented in CONTRIBUTING.md: PASS
- Assay test suite: PASS (714 passed, 3 ignored, 12 suites)
