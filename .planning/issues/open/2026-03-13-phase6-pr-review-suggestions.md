---
created: 2026-03-13T22:30
title: Phase 6 PR review suggestions
area: testing
provenance: github:wollax/cupel#18
files:
  - tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs
  - tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs
  - tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs
  - tests/Wollax.Cupel.Tests/Pipeline/CupelPipelineTests.cs
  - src/Wollax.Cupel/Slicing/QuotaBuilder.cs
  - benchmarks/Wollax.Cupel.Benchmarks/SlicerBenchmark.cs
---

## Problem

Phase 6 PR review surfaced 7 suggestions (non-blocking) that improve test quality and code hygiene:

1. **KnapsackSliceTests.BucketSize_DefaultIs100** doesn't actually verify the default is 100 — test passes for any bucket size since the item fits well within budget regardless.

2. **Missing QuotaSlice constructor null-argument tests** — `QuotaSlice(null, quotas)` and `QuotaSlice(slicer, null)` have `ThrowIfNull` guards but no test coverage.

3. **Missing KnapsackSlice negative-token items test** — The `Tokens > 0` guard at line 69 skips negative-token items but no test covers this path.

4. **QuotaBuilder floating-point epsilon** — `totalRequired > 100` check at line 88 could fail spuriously for three kinds at 33.333...% each due to floating-point accumulation. Should use `> 100 + 1e-9`.

5. **SlicerBenchmark async iterator** — `ToAsyncEnumerable` helper uses `await Task.CompletedTask` to suppress CS1998 warning. Use `#pragma warning disable` instead.

6. **Inconsistent Throws vs ThrowsExactly** — QuotaBuilderTests and PipelineBuilderTests use `Throws<T>` while KnapsackSliceTests and StreamSliceTests use `ThrowsExactly<T>`. Standardize on `ThrowsExactly<T>`.

7. **Duplicate test helpers** — `DelegateScorer`, `DelegateSlicer`, `DelegatePlacer` are defined identically in both PipelineBuilderTests and CupelPipelineTests. Extract to shared test helper.

## Solution

TBD — batch fix in a future cleanup pass.
