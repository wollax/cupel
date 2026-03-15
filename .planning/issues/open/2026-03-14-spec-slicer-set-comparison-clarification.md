---
created: 2026-03-14T00:00
title: Spec conformance format slicer set comparison clarification
area: docs
provenance: local
files:
  - spec/src/conformance/format.md:80
---

## Problem

Slicer schema uses `selected_contents` with "set comparison" but doesn't clarify this applies to all slicers including QuotaSlice. Ordering is the placer's responsibility, not the slicer's.

## Solution

Add note clarifying set comparison applies to all slicers since ordering is the placer's job.
