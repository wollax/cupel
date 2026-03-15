---
created: 2026-03-14T00:00
title: Spec UShapedPlacer pinned items edge case misleading
area: docs
provenance: local
files:
  - spec/src/placers/u-shaped.md:98
---

## Problem

Edge case table says pinned items are "Placed at edges alongside other high-scored items." UShapedPlacer has no special pinned-item behavior — it receives merged ScoredItems and doesn't distinguish pinned vs non-pinned. This describes emergent pipeline behavior, not UShapedPlacer behavior.

## Solution

Remove the row or clarify: "Not special-cased here; pinned items arrive with score 1.0 from the pipeline and naturally sort to edges."
