---
created: 2026-03-15T00:00
title: "Phase 24: ExcludedItem rationale in selection-report.md repeats exclusion-reasons.md"
area: docs
provenance: local
files:
  - spec/book/selection-report.md
  - spec/book/exclusion-reasons.md
---

## Problem

The rationale block for `deduplicated_against` placement in `selection-report.md` is approximately 90 words that restate content already present in `exclusion-reasons.md`. This duplication increases maintenance burden and risks the two copies diverging.

## Solution

Replace the inline rationale block with a back-reference to the relevant section of `exclusion-reasons.md`.
