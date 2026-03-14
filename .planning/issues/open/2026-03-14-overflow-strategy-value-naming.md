---
created: 2026-03-14T17:25
title: OverflowStrategyValue naming inconsistent with other accessor properties
area: api
provenance: github:wollax/cupel#41
files:
  - src/Wollax.Cupel/CupelPipeline.cs:27-32
---

## Problem

All other internal accessors match their field names: `Scorer`, `Slicer`, `Placer`, `AsyncSlicer`, `DeduplicationEnabled`. But `OverflowStrategyValue` has a `Value` suffix to avoid collision with the `OverflowStrategy` enum type. This is a leaky naming concession.

## Solution

Rename to `OverflowStrategy` — C# allows property names to shadow their type (e.g., `CupelPolicy.OverflowStrategy` already uses this pattern). This is a minor internal-only change.
