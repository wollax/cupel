---
created: 2026-03-14T00:00
title: Add --all-targets to clippy in ci-rust.yml
area: ci
provenance: github:wollax/cupel#54
files:
  - .github/workflows/ci-rust.yml:38
---

## Problem

`cargo clippy --manifest-path crates/cupel/Cargo.toml` without `--all-targets` misses integration tests, examples, and benchmarks. Adding `--all-targets` would lint the full crate surface.

## Solution

Change clippy step to: `cargo clippy --manifest-path crates/cupel/Cargo.toml --all-targets -- -D warnings`. Apply same change to `release-rust.yml` test job for consistency.
