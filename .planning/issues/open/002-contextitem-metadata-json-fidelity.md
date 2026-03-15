---
title: "ContextItem.Metadata object? loses type fidelity through JSON round-trip"
area: core-models
priority: low
source: pr-review-phase-1
---

# Metadata values don't survive JSON round-trip with type fidelity

`IReadOnlyDictionary<string, object?>` serializes fine but deserializes values as `JsonElement` rather than their original types. This is a known System.Text.Json limitation.

**Decision:** Known limitation — documented in STATE.md decisions. The Metadata property is extensible storage; callers who need typed access should cast from `JsonElement`. A strongly-typed metadata system would add complexity disproportionate to the use case.
