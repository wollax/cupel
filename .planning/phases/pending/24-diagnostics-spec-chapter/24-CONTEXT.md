# Phase 24: Diagnostics Spec Chapter - Context

**Gathered:** 2026-03-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Author a language-agnostic diagnostics spec chapter in `/spec/` that resolves all load-bearing API decisions before any implementation PR opens. The chapter must be complete enough that a spec reviewer can answer questions like "what variant does `BudgetExceeded` carry?" without consulting implementation files. This phase blocks Phases 25-29.

</domain>

<decisions>
## Implementation Decisions

### Chapter structure
- Sub-page structure mirroring the Pipeline chapter pattern (not a single monolithic file)
- Top-level placement: sibling of Pipeline under `# Specification` — diagnostics observes the pipeline but isn't part of it
- Section ordering follows data flow: TraceCollector → Events → ExclusionReasons → SelectionReport
- Diagnostic conformance vectors documented in the existing `# Conformance` section, not within the diagnostics chapter

### Spec prescriptiveness
- Inline rationale: brief "why" sentence after each decision, co-located with the decision itself
- MUST keyword is permitted in Conformance Notes sections only, consistent with existing spec chapters (classify.md, slice.md). Informal prose elsewhere — no RFC 2119 keywords outside Conformance Notes.
- Rejected alternatives mentioned: one sentence per load-bearing decision to prevent re-litigation
- Pure contract level — no implementation hints, the spec defines *what*, implementations decide *how*

### Wire format
- Each diagnostic type gets both a field/type table (quick reference) and one complete JSON example
- Prescribe snake_case for JSON wire format — canonical, matches existing conformance vector style
- Absent fields are omitted (no nulls) — clean wire format, no "not recorded" vs "explicitly null" ambiguity
- Prose + examples only, no JSON Schema fragments — conformance vectors serve as the machine-verifiable layer

### Cross-referencing implementations
- Spec is the source of truth — .NET is a conforming implementation, not the reference implementation
- Language-agnostic only: prose, JSON, and pseudocode in examples. No C# or Rust syntax
- Spec covers .NET feature parity: TraceDetailLevel, OverflowEvent, etc. all get spec'd where they generalize
- Spec can diverge from .NET if a choice doesn't generalize well — .NET gets a follow-up issue to conform. Spec leads, implementations follow

### Claude's Discretion
- Exact sub-page breakdown (which concepts share a page vs. get their own)
- Prose style and section headings within each sub-page
- How to handle pseudocode notation for type definitions
- Amount of cross-referencing to other spec chapters (Pipeline, Data Model)

</decisions>

<specifics>
## Specific Ideas

- Structure should mirror the Pipeline chapter's sub-page pattern for consistency
- Wire format examples should look like existing conformance vectors
- The existing .NET diagnostics implementation (14 files in `src/Wollax.Cupel/Diagnostics/`) provides the feature surface to spec, but the spec may improve on .NET's design choices where they don't generalize

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 24-diagnostics-spec-chapter*
*Context gathered: 2026-03-15*
