---
created: 2026-03-15T00:00
title: "Phase 24: diagnostics.md summary table \"Defined in\" column header misleading"
area: docs
provenance: local
files:
  - spec/book/diagnostics.md
---

## Problem

The summary table in `diagnostics.md` has a "Defined in" column header, which implies a file location. However, the column values are type-name links, not file paths. This creates a mismatch between header label and content.

## Solution

Rename the column header to "Spec page" or "Chapter" to accurately reflect that the values link to spec chapters or type definitions rather than source file locations.
