# Phase 8: Policy System & Named Presets - Context

**Gathered:** 2026-03-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Declarative `CupelPolicy` config objects that fully specify a pipeline recipe (scorers + weights, slicer, placer, quotas, overflow, dedup). 7+ named presets for common LLM context management use cases marked `[Experimental]`. `CupelOptions` provides intent-based policy lookup. Budget is NOT part of the policy — it's per-invocation.

</domain>

<decisions>
## Implementation Decisions

### Policy-to-builder relationship
- Policy feeds into builder via `.WithPolicy(CupelPolicy)` on `PipelineBuilder` — no `policy.CreatePipeline()` method
- Policy is pure data (no behavior) — the builder resolves it into pipeline components
- Budget stays outside the policy; user must still call `.WithBudget()` after `.WithPolicy()`
- CupelPipeline constructor remains internal — only builder creates pipelines

### Override, layering, and scorer merge semantics
- Claude's Discretion — pick what fits existing builder patterns (last-write-wins is the natural fit for the current builder style)

### Named preset philosophy
- Claude's Discretion on final preset list (roadmap suggests: chat, code-review, rag, document-qa, tool-use, long-running, debugging)
- Claude's Discretion on preset exposure pattern (static properties on CupelPolicy vs separate CupelPresets class)
- Claude's Discretion on preset depth (whether presets include quotas or just scorers + slicer + placer)
- Per-preset `[Experimental]` attribute with individual diagnostic IDs (e.g., `CUPEL001`, `CUPEL002`) — allows graduating individual presets independently

### Intent-based lookup model
- Claude's Discretion on whether `CupelOptions` lives in core library (Phase 8) or defers to DI package (Phase 10)
- Claude's Discretion on intent key design (free-form strings vs constrained)
- Claude's Discretion on fallback/default policy behavior

### Policy type design and surface
- Claude's Discretion on record vs sealed class
- Claude's Discretion on direct instances vs config objects for scorer/slicer/placer references — should consider Phase 9 serialization needs
- Claude's Discretion on metadata (name/description) inclusion
- Claude's Discretion on whether overflow strategy and dedup are part of policy or builder-only

### Claude's Discretion
Significant latitude given across all areas except:
- Policy feeds into builder (locked)
- Budget stays outside policy (locked)
- Per-preset [Experimental] with individual diagnostic IDs (locked)

For all discretionary decisions, prioritize:
1. Consistency with existing codebase patterns (sealed class + validation, zero-allocation discipline, etc.)
2. Phase 9 serialization readiness (config objects likely better than direct instances)
3. Simplicity — don't over-abstract for 7 presets

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches. The existing `PipelineBuilder` and `ContextBudget` patterns are the primary style reference.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 08-policy-system-named-presets*
*Context gathered: 2026-03-13*
