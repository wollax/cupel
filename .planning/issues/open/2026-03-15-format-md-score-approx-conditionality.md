---
created: 2026-03-15T12:00
title: Clarify score_approx field conditionality in format.md
area: conformance
provenance: github:wollax/cupel#78
files:
  - spec/src/conformance/format.md:158-168
---

## Problem

The `score_approx` field is marked `required: yes` in the diagnostics schema table. The Optionality subsection notes all sub-tables are independently optional, but doesn't clarify that fields within a sub-table entry are required only when that sub-table is present. Could confuse implementors.

## Solution

Add clarifying note in schema table or prose: "Fields marked 'yes' are required within each entry when the sub-table is present."
