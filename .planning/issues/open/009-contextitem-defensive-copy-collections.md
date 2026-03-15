---
title: "Defensive copy of Tags and Metadata in ContextItem init"
area: core-models
priority: low
source: pr-review-phase-1
---

# Defensive copying for collection properties

ContextItem.Tags and Metadata accept caller collections by reference. A caller could mutate the original collection after construction. Consider wrapping in ReadOnlyCollection/ReadOnlyDictionary in init accessors.

**Consideration:** Records don't support custom init logic without a constructor. Would require switching from `{ get; init; }` to constructor parameters, which changes the API significantly. The `sealed record` design already communicates immutability intent. Low risk in practice.
