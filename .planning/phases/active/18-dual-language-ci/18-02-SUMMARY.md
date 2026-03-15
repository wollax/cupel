---
phase: 18-dual-language-ci
plan: 02
subsystem: ci
tags: [github-actions, rust, release, crates-io, workflow-dispatch]
dependency-graph:
  requires: [18-01]
  provides: [rust-release-workflow]
  affects: [19-01]
tech-stack:
  added: []
  patterns: [workflow-dispatch-dry-run, environment-gated-publish, cargo-metadata-version]
key-files:
  created:
    - .github/workflows/release-rust.yml
  modified: []
decisions: []
metrics:
  tasks: 1/1
  files_created: 1
  files_modified: 0
  lines_added: 92
---

# Phase 18 Plan 02: Rust Release Workflow Summary

Rust release workflow with dry-run support, environment-gated publishing, and GitHub Release creation using `rust-v` tag prefix to avoid collision with .NET `v` tags.

## What Was Done

- Created `.github/workflows/release-rust.yml` mirroring the .NET `release.yml` conventions
- Test job runs full pre-publish validation: fmt, clippy, test, cargo-deny, and `cargo package --list`
- Publish job gated behind `release` environment, `dry-run != true` condition, and main branch verification
- Version extracted via `cargo metadata` (not manual parsing)
- GitHub Release uses `rust-v{version}` tag prefix to coexist with .NET releases

## Deviations

None — plan executed exactly as written.

## Verification Results

- YAML syntax validated
- workflow_dispatch trigger with dry-run boolean input confirmed
- Test job contains all 5 validation steps (fmt, clippy, test, deny, package)
- Publish job has correct triple gating: needs test, dry-run condition, release environment
- Main branch check prevents publishing from feature branches
- rust-v tag prefix confirmed in GitHub Release step
- 92 lines (exceeds 60-line minimum)
