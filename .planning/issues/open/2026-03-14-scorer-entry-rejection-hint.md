---
created: 2026-03-14T17:25
title: ScorerEntry rejection message should hint at the fix
area: api
provenance: github:wollax/cupel#34
files:
  - src/Wollax.Cupel/ScorerEntry.cs:86-90
---

## Problem

The error message `"InnerScorer must be null when Type is not Scaled."` is accurate but doesn't guide the caller toward a fix.

## Solution

Change to: `"InnerScorer is only valid for ScorerType.Scaled. Remove it or change the type to Scaled."` — consistent with how TagWeights error messages provide corrective guidance.
