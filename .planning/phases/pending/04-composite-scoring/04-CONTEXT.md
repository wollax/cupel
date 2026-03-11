# Phase 4: Composite Scoring - Context

**Gathered:** 2026-03-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Enable multi-signal scoring through composition. `CompositeScorer` combines multiple `IScorer` instances with weighted averaging and normalizes weights internally. `ScaledScorer` wraps any scorer and normalizes its output to 0–1 range. Stable sort with secondary tiebreaking ensures deterministic ordering. No new scorer types — only composition machinery.

</domain>

<decisions>
## Implementation Decisions

### Weight semantics
- Claude's discretion on whether to use relative ratios or absolute percentages
- Claude's discretion on zero-weight and negative-weight handling
- Claude's discretion on default weight value (or whether weight is required)

### Tiebreaking strategy
- Claude's discretion on tiebreaker signal (timestamp, insertion order, etc.)
- Claude's discretion on determinism guarantees (cross-run vs within-run)
- Claude's discretion on whether tiebreaker is configurable or fixed
- Claude's discretion on whether tiebreaking lives in CompositeScorer or the pipeline

### ScaledScorer behavior
- Claude's discretion on normalization approach (min-max rescale vs clamp)
- Claude's discretion on degenerate case (all identical scores)
- Claude's discretion on whether ScaledScorer is standalone wrapper vs CompositeScorer option
- Claude's discretion on single-pass vs two-pass implementation within IScorer contract

### Composition depth
- **Cycle detection required** — CompositeScorer must detect cycles (A contains B contains A) at construction time and throw
- Claude's discretion on maximum nesting depth (cap vs unlimited)
- Claude's discretion on validation timing (construction vs lazy)
- Claude's discretion on minimum child count (1 vs 2)
- Claude's discretion on aggregation strategy (fixed WeightedAverage vs pluggable)

### Claude's Discretion
The user has delegated nearly all implementation decisions to Claude. The guiding constraints are:
- Phase 4 success criteria from ROADMAP.md (WeightedAverage aggregation, relative weight normalization, nested composites, ScaledScorer 0–1 normalization, stable tiebreaking)
- Zero-allocation discipline from Phase 3 (no LINQ/closures/boxing in Score() methods)
- Existing patterns: ContextBudget validates at construction, scorers are pure/stateless
- The one explicit decision: **cycle detection is required at construction time**

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches that align with the existing codebase patterns and success criteria.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 04-composite-scoring*
*Context gathered: 2026-03-11*
