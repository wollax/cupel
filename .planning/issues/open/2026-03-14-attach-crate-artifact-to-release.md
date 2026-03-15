---
created: 2026-03-14T00:00
title: Attach .crate artifact to GitHub Release
area: ci
provenance: github:wollax/cupel#56
files:
  - .github/workflows/release-rust.yml:87-94
---

## Problem

The Rust release workflow creates a GitHub Release but doesn't attach the packaged `.crate` file. The .NET workflow attaches `.nupkg` files via artifact upload/download. Attaching the crate would provide a secondary download source beyond crates.io.

## Solution

TBD — could use `cargo package` to produce the `.crate` file in the test job, upload as artifact, then attach to the GitHub Release in the publish job.
