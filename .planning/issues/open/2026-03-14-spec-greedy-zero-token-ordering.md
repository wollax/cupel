---
created: 2026-03-14T00:00
title: Spec GreedySlice zero-token item ordering note
area: docs
provenance: local
files:
  - spec/src/slicers/greedy.md:56
---

## Problem

Spec notes zero-token items get MAX_FLOAT density but doesn't clarify that among zero-token items, ordering is by index tiebreaker only (not score). A zero-score zero-token item sorts identically to a score=1.0 zero-token item.

## Solution

Add explicit note about zero-token item ordering.
