---
created: 2026-03-14T17:25
title: Duplicate transient ITraceCollector test
area: testing
provenance: github:wollax/cupel#39
files:
  - tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs:287
---

## Problem

`AddCupelTracing_IsTransient_DifferentInstancesPerResolve` is functionally identical to `AddCupelTracing_RegistersTransientTraceCollector` (line 155) which already asserts `ReferenceEquals(...).IsFalse()`. No additional coverage.

## Solution

Remove the duplicate test.
