# Phase 2: Interfaces & Diagnostics Infrastructure - Context

**Gathered:** 2026-03-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Define all pipeline stage contracts (IScorer, ISlicer, IPlacer, IContextSource) and build the tracing infrastructure (ITraceCollector, NullTraceCollector, DiagnosticTraceCollector). Deliver ContextResult as the pipeline return type with optional trace attachment. No implementations of scorers, slicers, or placers — only their interfaces.

</domain>

<decisions>
## Implementation Decisions

### Trace event granularity
- Two-tier verbosity: stage-level by default, item-level opt-in via a detail level on the trace collector
- Wall-clock timing per stage is always captured when tracing is enabled (Stopwatch per stage)
- Intermediate scorer outputs in CompositeScorer trees: Claude's discretion
- Exclusion reason detail level (enum-only vs enum + context data): Claude's discretion

### Interface async patterns
- IScorer sync vs async: Claude's discretion (constrained by zero-allocation hot path requirement)
- ISlicer and IPlacer sync vs async: Claude's discretion
- IContextSource supports both batch (Task<IReadOnlyList<ContextItem>>) and streaming (IAsyncEnumerable<ContextItem>) — either as separate methods or separate interfaces
- CancellationToken strategy: Claude's discretion

### ContextResult & trace attachment
- ContextResult is a sealed record (consistent with ContextItem pattern)
- Result contains selected Items plus an optional SelectionReport (populated only when tracing is enabled) — excluded items with reasons live in the report
- TotalTokens is a computed property summing selected Items
- Trace attachment (nullable vs always-present): Claude's discretion

### Diagnostic consumption model
- DiagnosticTraceCollector supports both buffered event list AND optional callback (Action<TraceEvent>) for real-time consumption
- NullTraceCollector is a static singleton (NullTraceCollector.Instance)
- ILogger integration: Claude's discretion (likely adapter in DI companion package, not core)
- Human-readable trace summary (ToString()): Claude's discretion

### Claude's Discretion
- Scorer trace depth (final composite only vs full scorer tree breakdown)
- Exclusion reason richness (enum only vs enum + contextual numbers)
- Sync/async boundaries on IScorer, ISlicer, IPlacer
- CancellationToken placement
- Trace attachment nullability on ContextResult
- ILogger bridge location (core vs DI package)
- Human-readable trace formatting

</decisions>

<specifics>
## Specific Ideas

- Two-tier verbosity mirrors the philosophy of "no allocations when tracing disabled" — stage-level is cheap, item-level is opt-in for debugging
- IContextSource dual-mode (batch + streaming) aligns with StreamSlice in Phase 6 needing IAsyncEnumerable support
- SelectionReport as optional on ContextResult bridges the gap between Phase 2 (infrastructure) and Phase 7 (full explainability) — the slot exists from day one

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-interfaces-diagnostics-infrastructure*
*Context gathered: 2026-03-11*
