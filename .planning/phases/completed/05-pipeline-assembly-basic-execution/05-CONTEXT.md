# Phase 5: Pipeline Assembly & Basic Execution - Context

**Gathered:** 2026-03-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire the fixed pipeline together: Classify → Score → Deduplicate → Slice → Place. Deliver the first end-to-end context selection with GreedySlice, UShapedPlacer, ChronologicalPlacer, and the fluent builder API. This phase does NOT include advanced slicers (KnapsackSlice, QuotaSlice, StreamSlice), explainability (SelectionReport, DryRun), overflow strategies, or the policy system.

</domain>

<decisions>
## Implementation Decisions

### Builder API shape
- Support two mutually exclusive scoring paths: `.WithScorer(IScorer)` for pre-composed scorers and `.AddScorer(IScorer, double weight)` for convenience composition — mixing both causes `Build()` to throw
- Minimum required configuration: Budget + Scorer only — slicer defaults to `GreedySlice`, placer defaults to `ChronologicalPlacer`
- Both `Execute(IReadOnlyList<ContextItem>)` and `Execute(IContextSource)` overloads — collection overload wraps in an internal source

### Deduplication semantics
- Duplicate identity: same `Content` string (regardless of Kind, Source, Tags)
- Resolution: keep the duplicate with the highest composite score — dedup runs after scoring
- On by default, opt-out via `.WithDeduplication(false)` on the builder

### Pinned item behavior
- `IsPinned` bool property on `ContextItem` — pinning is intrinsic to the item
- Pinned items are passed to the Placer with an effective score of 1.0 (max) — placer handles them uniformly alongside scored items
- UShapedPlacer places pinned items at edges (highest score positions); ChronologicalPlacer orders by timestamp

### Classify stage design
- Classify does three things: validate items, enrich from IContextSource defaults (Kind/Source), and partition into pinned vs scoreable
- Pinned items bypass Score/Dedup/Slice and go directly to Placer
- Classify is a fixed internal pipeline stage — not user-extensible

### Claude's Discretion
- Tracing configuration: whether `.WithTraceCollector()` lives on the builder or is per-execution — decide based on Phase 2's ITraceCollector design
- Dedup string comparison strategy: exact ordinal vs normalized — decide based on zero-allocation discipline
- Pinned items budget behavior: whether pinned tokens consume budget or are free — decide based on Phase 1's ContextBudget model
- Pinned overflow handling: throw vs warn when pinned items exceed budget — decide based on project validation patterns
- Classify validation behavior for invalid items: throw vs skip-and-trace — decide based on existing validation patterns

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 05-pipeline-assembly-basic-execution*
*Context gathered: 2026-03-13*
