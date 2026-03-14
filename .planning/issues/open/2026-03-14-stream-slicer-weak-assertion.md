---
created: 2026-03-14T17:25
title: Stream slicer async test uses weak assertion (> 0 instead of == 3)
area: testing
provenance: github:wollax/cupel#37
files:
  - tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs:688
---

## Problem

`WithPolicy_StreamSlicer_AsyncExecuteStreamWorks` asserts `result.Items.Count > 0` — a one-item result would pass. Given 3 items × 50 tokens well within a 500-token budget, a tighter assertion would verify the slicer actually passes all items through.

## Solution

Change to `IsEqualTo(3)` to verify the slicer passes all items rather than just "something came out."
