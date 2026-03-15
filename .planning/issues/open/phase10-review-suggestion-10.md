---
title: "Validate individual scorer details in JSON roundtrip consumption test"
area: tests
priority: low
source: phase-10-pr-review
---

# Validate Individual Scorer Details in JSON Roundtrip Consumption Test

The JSON roundtrip consumption test validates that a `CupelPolicy` survives serialization and deserialization, but only asserts on aggregate counts (e.g., number of scorers). It does not verify that individual scorer types and weights are preserved correctly.

## Suggestion

Extend the roundtrip assertion to check each scorer entry individually — type, weight, and any associated configuration (e.g., tag weights for a Tag scorer). This catches partial corruption where count is preserved but contents are wrong.

Example:

```csharp
// Instead of:
await Assert.That(roundtripped.Scorers).HasCount().EqualTo(2);

// Also assert:
await Assert.That(roundtripped.Scorers[0].ScorerType).IsEqualTo(ScorerType.Recency);
await Assert.That(roundtripped.Scorers[0].Weight).IsEqualTo(0.6);
await Assert.That(roundtripped.Scorers[1].ScorerType).IsEqualTo(ScorerType.Tag);
await Assert.That(roundtripped.Scorers[1].Weight).IsEqualTo(0.4);
```

## Files

- `tests/Wollax.Cupel.Json.Tests/` (JSON roundtrip consumption test file)
