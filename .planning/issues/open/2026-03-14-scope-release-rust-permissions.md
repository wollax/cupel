---
created: 2026-03-14T00:00
title: Scope release-rust.yml permissions to job level
area: ci
provenance: github:wollax/cupel#53
files:
  - .github/workflows/release-rust.yml:12-13
---

## Problem

`release-rust.yml` declares `permissions: contents: write` at the workflow level, granting write access to both `test` and `publish` jobs. The `test` job only needs `contents: read`. This violates least-privilege principle. The .NET `release.yml` has the same pattern — both should be fixed together.

## Solution

Move `permissions` from workflow level to job level: `test` gets `contents: read`, `publish` gets `contents: write`.
