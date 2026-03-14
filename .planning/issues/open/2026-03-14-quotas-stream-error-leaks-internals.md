---
created: 2026-03-14T17:25
title: Quotas+Stream error message leaks internal type names
area: api
provenance: github:wollax/cupel#33
files:
  - src/Wollax.Cupel/CupelPolicy.cs:134
---

## Problem

The error message for the Quotas+Stream mutual exclusion exposes internal implementation type names: `"QuotaSlice wraps ISlicer (sync) and cannot wrap StreamSlice (IAsyncSlicer)."` For a public API exception message, coupling to internal class names (`QuotaSlice`, `ISlicer`, `IAsyncSlicer`) is undesirable.

## Solution

Replace with a caller-facing message like: `"Quotas cannot be combined with SlicerType.Stream (stream slicing is asynchronous and does not support synchronous quota wrapping)."` — communicates the constraint without leaking type names.
