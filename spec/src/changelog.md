# Changelog

All notable changes to the Cupel Specification are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.3.0] — 2026-03-23

### Added

- **Scorers**: [DecayScorer](scorers/decay.md) with Exponential, Step, and Window curve factories; [MetadataTrustScorer](scorers/metadata-trust.md) for caller-provided `cupel:trust` metadata passthrough
- **Slicers**: [CountQuotaSlice](slicers/count-quota.md) decorator slicer enforcing absolute item-count require/cap per ContextKind, with ScarcityBehavior (Degrade/Throw) and CountRequirementShortfall reporting
- **Analytics**: [Budget simulation](analytics/budget-simulation.md) extension methods on CupelPipeline (.NET only): `GetMarginalItems(items, budget, slackTokens)` and `FindMinBudgetFor(items, targetItem, searchCeiling)` with reference-equality diff semantics and binary search
- **Analytics**: `BudgetUtilization(budget)`, `KindDiversity()`, and `TimestampCoverage()` extension methods on SelectionReport (both .NET and Rust)
- **Testing**: `Wollax.Cupel.Testing` NuGet package with `SelectionReport.Should()` assertion chains (13 assertion patterns from the testing vocabulary spec)
- **Diagnostics**: `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package bridging Cupel pipelines to OpenTelemetry ActivitySource with three verbosity tiers (StageOnly, StageAndExclusions, Full)

### Changed

- **GreedySlice**: [Deterministic tie-break contract](slicers/greedy.md#deterministic-tie-break-contract) explicitly specified — equal-density items are ordered by original index ascending. This was implicit behavior in 1.0.0 but is now a spec-committed contract that budget simulation depends on.

### Specification Decisions

- DecayScorer requires caller-injected TimeProvider with no default — makes time dependency visible and enables deterministic testing (D042)
- DecayScorer is not Clone in Rust; stores `Box<dyn TimeProvider + Send + Sync>` (D047)
- MetadataTrustScorer in .NET accepts both `string` and `double` for `cupel:trust` metadata value (D059)
- CountQuotaSlice rejects KnapsackSlice as inner slicer at construction time; defers CountConstrainedKnapsackSlice to a future release
- Budget simulation API scoped to .NET in v1.3; Rust parity deferred to a future milestone
- `SweepBudget` (exhaustive budget sweep) assigned to Smelt project, not Cupel

---

## [1.0.0] — 2026-03-14

Initial specification release.

### Added

- **Data Model**: ContextItem, ContextBudget, and all enumerations (ContextKind, OverflowStrategy, ExclusionReason)
- **Pipeline**: 6-stage fixed pipeline (Classify, Score, Deduplicate, Sort, Slice, Place)
- **Scorers**: RecencyScorer, PriorityScorer, KindScorer, TagScorer, FrequencyScorer, ReflexiveScorer, CompositeScorer, ScaledScorer
- **Slicers**: GreedySlice, KnapsackSlice, QuotaSlice
- **Placers**: ChronologicalPlacer, UShapedPlacer
- **Conformance suite**: 37 TOML test vectors (28 required, 9 optional) covering all algorithms and edge cases
  - 13 required scoring vectors (all 8 scorer types)
  - 6 required slicing vectors (GreedySlice, KnapsackSlice, QuotaSlice)
  - 4 required placing vectors (ChronologicalPlacer, UShapedPlacer)
  - 5 required pipeline vectors (multiple slicer/placer combinations, pinned items)
  - 4 optional scoring vectors (edge cases)
  - 1 optional slicing vector (empty input)
  - 4 optional pipeline vectors (empty input, all-pinned, deduplication, overflow)

### Specification Decisions

- Epsilon tolerance (`1e-9`) for all floating-point score comparisons (P2)
- Byte-exact ordinal comparison for content deduplication (P3)
- Case-insensitive ASCII case folding for ContextKind comparison (P4)
- KnapsackSlice discretization parameters are implementation-defined; conformance tests use score differences >= 0.01 (P5)
- QuotaSlice budget distribution uses floor truncation for all percentage-to-token conversions (P6)
- Stable sort mandated for all sorting operations with index-based tiebreaking (P1)
