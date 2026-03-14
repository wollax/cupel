# Phase 14: Policy Type Completeness - Context

**Gathered:** 2026-03-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Make `ScaledScorer` and `StreamSlice` reachable from the declarative policy, JSON serialization, and DI paths — closing the integration gap where these components are only usable via manual builder. Align DI lifetimes to the documented singleton specification. No new algorithmic capabilities; this phase wires existing implementations into existing configuration surfaces.

</domain>

<decisions>
## Implementation Decisions

### ScaledScorer nesting
- Claude's discretion on ScorerEntry representation (InnerScorer property vs reusing Scorers list) — pick whichever fits the existing ScorerEntry shape and JSON ergonomics best
- Claude's discretion on whether ScaledScorer supports explicit min/max bounds in policy vs auto-detect only
- Claude's discretion on whether ScaledScorer can wrap any scorer type (including Composite/Scaled) vs leaf-only — mirror what the builder API already supports
- Claude's discretion on JSON shape for nested inner scorer — should be consistent with existing JSON patterns

### StreamSlice config
- Claude's discretion on whether BatchSize is configurable in policy or default-only
- Claude's discretion on behavior when StreamSlice is declared but sync Execute is called — pick approach consistent with existing pipeline validation
- Claude's discretion on whether existing presets get streaming variants or stay as-is
- Claude's discretion on whether QuotaSlice can wrap StreamSlice in the declarative path — mirror builder API capabilities

### DI singleton scope
- Pipeline remains transient (new instance per resolve) — user decision
- Scorers, slicers, and placers are singletons shared across pipeline instances — user decision
- ITraceCollector remains transient (per-request) — user decision
- Claude's discretion on whether composed scorer trees (CompositeScorer with children, ScaledScorer with inner) are registered as single singleton trees or independently

### Enum addition strategy
- Unknown ScorerType/SlicerType values fail hard with JsonException — user decision
- Built-in scorer type names detection refactored to derive from enum values instead of hardcoded array — user decision, eliminates future drift
- Claude's discretion on JSON member names for new enum values — pick names consistent with existing patterns
- Claude's discretion on PublicAPI file placement (Shipped vs Unshipped)

### Claude's Discretion
- ScaledScorer nesting representation in ScorerEntry
- ScaledScorer bounds configuration (explicit vs auto-detect)
- ScaledScorer wrapping depth restrictions
- StreamSlice BatchSize exposure in policy
- StreamSlice sync/async mismatch handling
- Preset updates for streaming
- QuotaSlice + StreamSlice composition in policy
- Composed scorer DI registration strategy
- JSON member names for new enum values
- PublicAPI file placement

</decisions>

<specifics>
## Specific Ideas

- Derive known scorer type names from enum to prevent hardcoded array drift — refactor the existing detection mechanism
- Pipeline transient + components singleton matches the ROADMAP specification exactly

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 14-policy-type-completeness*
*Context gathered: 2026-03-14*
