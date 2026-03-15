# Phase 20: Serde Feature Flag - Context

**Gathered:** 2026-03-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Add optional `serde` feature flag gating `Serialize`/`Deserialize` derives on all public data types. Requires careful custom deserializers to maintain constructor validation invariants. Crate re-published as minor version bump.

</domain>

<decisions>
## Implementation Decisions

### Type coverage boundary
- **Data types only** — derive serde on pure data types, skip all trait-object wrappers
- In scope: ContextBudget, ContextItem, ContextKind, ContextSource, OverflowStrategy, QuotaEntry
- Out of scope: Pipeline, PipelineBuilder, CompositeScorer, ScaledScorer, KindScorer, TagScorer, QuotaSlice, KnapsackSlice, GreedySlice, ChronologicalPlacer, UShapedPlacer (all contain or are trait-object-based runtime constructs)
- Unit structs (FrequencyScorer, PriorityScorer, etc.) excluded — they're runtime strategy objects, not data

### Validation-on-deserialize
- **All validating types deserialize through their constructors** — no bypassing validation via serde
- ContextBudget: custom deserializer calling `ContextBudget::new(...)`
- ContextKind: custom deserializer calling `ContextKind::new(value)`
- ContextSource: custom deserializer calling `ContextSource::new(value)`
- ContextItem: custom deserializer routing through `ContextItemBuilder::new().build()`
- QuotaEntry: custom deserializer calling `QuotaEntry::new(kind, require, cap)`
- Impossible to create invalid state via serde, period

### Wire format representation
- JSON keys use **snake_case** matching Rust field names (`future_relevance_hint`, `original_tokens`)
- Natural for Rust consumers, matches serde defaults

### Claude's Discretion
- ScoredItem: include or exclude based on what's pragmatic
- CupelError: include or exclude based on what's pragmatic
- ContextItemBuilder: likely exclude (serialize the built item, not the builder)
- Newtype representation for ContextKind/ContextSource (bare string recommended, Claude decides)
- OverflowStrategy enum variant casing convention
- ContextBudget reserved_slots map key serialization (preserve case vs normalize)
- Error messages on deserialization failure (reuse CupelError text vs serde-specific)
- Unknown field handling (deny vs ignore)
- Optional field defaults on ContextItem (missing = builder defaults vs explicit)

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches. The .NET v1.0 has a JSON serialization package that could serve as a reference for field naming alignment, but Claude has discretion on Rust-idiomatic choices.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 20-serde-feature-flag*
*Context gathered: 2026-03-15*
