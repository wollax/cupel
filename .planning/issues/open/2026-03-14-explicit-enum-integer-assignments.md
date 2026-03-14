---
created: 2026-03-14T17:25
title: Add explicit integer assignments to Scaled and Stream enum values
area: api
provenance: github:wollax/cupel#35
files:
  - src/Wollax.Cupel/ScorerType.cs:36
  - src/Wollax.Cupel/SlicerType.cs:20
---

## Problem

`ScorerType.Scaled` and `SlicerType.Stream` rely on implicit enum value assignment from their position. `PublicAPI.Unshipped.txt` declares them as `Scaled = 6` and `Stream = 2`, but the source doesn't anchor these values explicitly. A future insertion could silently renumber them.

## Solution

Add explicit `= 6` and `= 2` assignments in source to match the PublicAPI declarations, preventing accidental renumbering.
