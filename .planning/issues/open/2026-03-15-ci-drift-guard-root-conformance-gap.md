---
created: 2026-03-15T12:00
title: CI drift guard does not cover root conformance/ directory
area: ci
provenance: github:wollax/cupel#80
files:
  - .github/workflows/ci-rust.yml:36-52
  - conformance/required/
---

## Problem

The repo has three identical conformance copies: `conformance/` (canonical), `spec/conformance/` (mdBook), and `crates/cupel/conformance/` (Rust tests). The CI drift guard only checks `spec/` vs `crates/`. If someone updates the canonical `conformance/` but forgets `spec/`, CI stays green and drift goes undetected. Pre-existing architectural gap, not introduced by Phase 25.

## Solution

Either extend the drift guard to also check `conformance/` vs `spec/conformance/`, or document the pre-commit hook as the canonical↔crates guard and consider whether the three-copy architecture should be simplified.
