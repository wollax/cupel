---
created: 2026-03-15T12:00
title: Add working-directory comment to CI drift guard step
area: ci
provenance: github:wollax/cupel#81
files:
  - .github/workflows/ci-rust.yml:36
---

## Problem

The conformance drift guard step uses paths relative to the repo root (`spec/conformance/...`, `crates/cupel/conformance/...`) without a `working-directory` key or comment clarifying this is GitHub Actions' default behavior. Readers unfamiliar with Actions defaults may find this ambiguous.

## Solution

Add a brief comment or `working-directory: .` declaration to make intent explicit.
