---
created: 2026-03-14T17:25
title: Unbounded Scaled nesting depth — add doc warning
area: api
provenance: github:wollax/cupel#42
files:
  - src/Wollax.Cupel/ScorerEntry.cs
---

## Problem

There is no depth limit on `Scaled` wrapping `Scaled`. `ScaledScorer` has O(N²) scoring cost; deeply nested chains compound to O(N^(depth+1)). Likely acceptable for v1.0 since policies are human-authored, but could be a performance trap.

## Solution

Add a note in the `ScorerEntry.InnerScorer` XML doc warning about compounding performance cost of deep nesting. Optionally, add a max-depth guard (e.g., depth > 8 → `ArgumentException`).
