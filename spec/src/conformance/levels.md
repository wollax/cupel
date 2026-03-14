# Conformance Levels

The Cupel conformance suite defines two tiers of test vectors: **Required** and **Optional**. Together, they provide a comprehensive validation of an implementation's correctness.

## Required

An implementation MUST pass all Required test vectors to claim Cupel conformance. These vectors cover the core algorithm behavior that every conforming implementation must exhibit.

### Covered Algorithms

| Category | Algorithms |
|---|---|
| Scorers | RecencyScorer, PriorityScorer, KindScorer, TagScorer, FrequencyScorer, ReflexiveScorer, CompositeScorer, ScaledScorer |
| Slicers | GreedySlice, KnapsackSlice |
| Placers | ChronologicalPlacer, UShapedPlacer |
| Pipeline | End-to-end scenarios combining scoring, slicing, and placing |

Required vectors test the fundamental contracts of each algorithm:

- Correct score computation for all 8 scorer types
- Correct item selection for GreedySlice and KnapsackSlice
- Correct ordering for both placers
- End-to-end pipeline behavior with pinned items, multiple scorer/slicer/placer combinations

## Optional

An implementation MAY pass Optional test vectors for full conformance. These vectors cover edge cases and advanced features. Passing all Optional vectors demonstrates robustness in boundary conditions.

### Covered Scenarios

| Category | Scenarios |
|---|---|
| Scoring edge cases | Single-item recency, all-null timestamps, degenerate ScaledScorer, nested CompositeScorer |
| Slicing edge cases | Empty input, QuotaSlice with Require and Cap constraints |
| Pipeline edge cases | Empty input, all-pinned items, content deduplication, overflow with Truncate strategy |

## Claiming Conformance

An implementation may claim one of two conformance levels:

- **Cupel Conformant**: All Required test vectors pass.
- **Cupel Fully Conformant**: All Required and all Optional test vectors pass.

Implementations SHOULD document which conformance level they have achieved and the version of the test vector suite used for validation.
