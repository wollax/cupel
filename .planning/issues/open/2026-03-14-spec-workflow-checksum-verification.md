---
created: 2026-03-14T00:00
title: Spec GitHub Actions workflow missing checksum verification
area: docs
provenance: local
files:
  - .github/workflows/spec.yml:30
---

## Problem

mdBook binary is downloaded via hardcoded URL with no checksum verification. Supply chain risk.

## Solution

Add sha256sum verification after download, or use a maintained action like `peaceiris/actions-mdbook`.
