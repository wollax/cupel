---
created: 2026-03-14T00:00
title: Spec CompositeScorer pseudocode missing storage assignment
area: docs
provenance: local
files:
  - spec/src/scorers/composite.md:50
---

## Problem

CONSTRUCT-COMPOSITE pseudocode comment says "Store scorers and normalizedWeights" without showing the assignment to named fields. Minor completeness gap.

## Solution

Show explicit assignment to named fields in pseudocode.
