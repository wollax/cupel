---
created: 2026-03-14T00:00
title: Spec KnapsackSlice floor vs truncation-toward-zero note
area: docs
provenance: local
files:
  - spec/src/slicers/knapsack.md:86
---

## Problem

Spec says `floor(score * 10000)` but C# uses `(int)(score * 10000)` which is truncation-toward-zero. These are equivalent for non-negative scores, but should be noted explicitly.

## Solution

Add a note that floor and truncation-toward-zero are equivalent for non-negative scores.
