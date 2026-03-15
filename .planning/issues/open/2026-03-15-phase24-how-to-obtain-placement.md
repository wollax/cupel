---
created: 2026-03-15T00:00
title: "Phase 24: \"How to Obtain\" pseudocode appears before fields table in selection-report.md"
area: docs
provenance: local
files:
  - spec/book/selection-report.md
---

## Problem

In `selection-report.md`, the "How to Obtain" section with pseudocode appears before the fields table. This is unconventional for a spec — readers need to understand the structure before seeing usage patterns, not the reverse.

## Solution

Move the fields table before the "How to Obtain" / pseudocode section so the structure is defined before usage patterns are shown.
