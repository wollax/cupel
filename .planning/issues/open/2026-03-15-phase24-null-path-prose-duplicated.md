---
created: 2026-03-15T00:00
title: "Phase 24: is_enabled null-path prose duplicated across chapters"
area: docs
provenance: local
files:
  - spec/book/diagnostics.md
  - spec/book/trace-collector.md
---

## Problem

Both `diagnostics.md` and `trace-collector.md` explain the `is_enabled` gate and the zero-cost guarantee in nearly identical prose. This duplication risks the two copies drifting out of sync and increases maintenance burden.

## Solution

`trace-collector.md` should replace its copy of the null-path guarantee prose with a cross-reference to the canonical explanation in `diagnostics.md`.
