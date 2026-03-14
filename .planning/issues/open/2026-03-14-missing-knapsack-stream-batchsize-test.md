---
created: 2026-03-14T17:25
title: No test for StreamBatchSize with KnapsackSlicer
area: testing
provenance: github:wollax/cupel#38
files:
  - tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs
---

## Problem

`Validation_StreamBatchSizeWithGreedySlicer_Throws` tests the cross-field validation, but there is no equivalent test for `Validation_StreamBatchSizeWithKnapsackSlicer_Throws`. The guard applies to all non-Stream slicers, so Knapsack should be tested symmetrically.

## Solution

Add `Validation_StreamBatchSizeWithKnapsackSlicer_Throws` test case.
