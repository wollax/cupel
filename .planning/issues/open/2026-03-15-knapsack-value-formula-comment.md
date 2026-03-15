---
created: 2026-03-15T12:00
title: Add inline value formula to knapsack-basic.toml comment
area: conformance
provenance: github:wollax/cupel#77
files:
  - conformance/required/slicing/knapsack-basic.toml:10-12
---

## Problem

The knapsack-basic.toml comment shows `score=0.7 → value=7000` without explaining the `floor(score * 10000)` scaling formula. A reader looking only at the test comment could mistake 7000 for an arbitrary label. The formula is defined in `spec/src/slicers/knapsack.md:39` but not referenced inline.

## Solution

Add brief inline note — e.g., `value=floor(0.7*10000)=7000` — to make the comment self-contained. Apply to all 3 conformance copies.
