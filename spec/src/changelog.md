# Changelog

All notable changes to the Cupel Specification are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.0.0] — 2026-03-14

Initial specification release.

### Added

- **Data Model**: ContextItem, ContextBudget, and all enumerations (ContextKind, OverflowStrategy, ExclusionReason)
- **Pipeline**: 6-stage fixed pipeline (Classify, Score, Deduplicate, Sort, Slice, Place)
- **Scorers**: RecencyScorer, PriorityScorer, KindScorer, TagScorer, FrequencyScorer, ReflexiveScorer, CompositeScorer, ScaledScorer
- **Slicers**: GreedySlice, KnapsackSlice, QuotaSlice
- **Placers**: ChronologicalPlacer, UShapedPlacer
- **Conformance suite**: 37 TOML test vectors (27 required, 10 optional) covering all algorithms and edge cases
  - 13 required scoring vectors (all 8 scorer types)
  - 5 required slicing vectors (GreedySlice, KnapsackSlice)
  - 4 required placing vectors (ChronologicalPlacer, UShapedPlacer)
  - 5 required pipeline vectors (multiple slicer/placer combinations, pinned items)
  - 4 optional scoring vectors (edge cases)
  - 2 optional slicing vectors (empty input, QuotaSlice)
  - 4 optional pipeline vectors (empty input, all-pinned, deduplication, overflow)

### Specification Decisions

- Epsilon tolerance (`1e-9`) for all floating-point score comparisons (P2)
- Byte-exact ordinal comparison for content deduplication (P3)
- Case-insensitive ASCII case folding for ContextKind comparison (P4)
- KnapsackSlice discretization parameters are implementation-defined; conformance tests use score differences >= 0.01 (P5)
- QuotaSlice budget distribution uses floor truncation for all percentage-to-token conversions (P6)
- Stable sort mandated for all sorting operations with index-based tiebreaking (P1)
