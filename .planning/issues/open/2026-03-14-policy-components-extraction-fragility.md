---
created: 2026-03-14T17:25
title: PolicyComponents extraction via throwaway pipeline is fragile
area: api
provenance: github:wollax/cupel#40
files:
  - src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs:66-90
---

## Problem

The DI singleton factory builds a full `CupelPipeline` then extracts component references via internal accessor properties. This creates implicit coupling: if `Build()` ever requires budget-dependent component construction, the bootstrap approach silently produces wrong singletons.

## Solution

Consider a dedicated `PipelineBuilder.BuildComponents(CupelPolicy)` internal factory method that returns a `PolicyComponents`-equivalent directly, without requiring a throwaway `CupelPipeline` and `ContextBudget`.
