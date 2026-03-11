# Phase 3: Individual Scorers - Context

**Gathered:** 2026-03-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Implement all six built-in scorers as pure, stateless functions. Each scorer implements `IScorer` and produces output conventionally in 0.0–1.0. Tests assert ordinal relationships, not exact values. No LINQ, closure captures, or boxing in any `Score()` method — verified by benchmark.

Scorers: RecencyScorer, PriorityScorer, KindScorer, TagScorer, FrequencyScorer, ReflexiveScorer.

</domain>

<decisions>
## Implementation Decisions

### Scorer configuration

- **KindScorer** takes `IReadOnlyDictionary<ContextKind, double>` via constructor
- Default weight map covers the 5 well-known kinds: SystemPrompt=1.0, Memory=0.8, ToolOutput=0.6, Document=0.4, Message=0.2
- Parameterless constructor uses the default map; overload accepts a custom map
- **TagScorer** takes `IReadOnlyDictionary<string, double>` mapping tags to weights (required, no default)
- Multiple matching tags → sum of matched weights / total weight sum (normalized to 0–1)
- **Unknown keys** (kind or tag not in the configured map) → 0.0 score — "if you didn't configure it, it gets no boost"
- Scorers do not throw on unknown keys — they rank, they don't validate

### Missing data behavior

- Uniform rule: if the optional field a scorer depends on is absent, the item scores **0.0**
- RecencyScorer: `Timestamp` is null → 0.0
- PriorityScorer: `Priority` is null → 0.0
- ReflexiveScorer: `FutureRelevanceHint` is null → 0.0
- TagScorer: `Tags` is empty → 0.0
- Rationale: CompositeScorer (Phase 4) combines multiple signals — a single missing signal doesn't eliminate an item, other scorers compensate

### FrequencyScorer signal source

- Derives frequency from **tag co-occurrence** across the candidate set
- For a given item, counts how many other items in `allItems` share at least one tag
- Score = (items sharing ≥1 tag) / (allItems.Count - 1), naturally producing 0–1
- Items with no tags → 0.0 (consistent with missing data rule)
- Distinct from TagScorer: TagScorer matches against *user-specified* target tags, FrequencyScorer measures *candidate set density*
- Uses the `allItems` parameter meaningfully (like RecencyScorer does for relative ranking)

### RecencyScorer ranking

- Uses relative timestamp ranking within the input set (per ROADMAP.md success criteria)
- Not absolute distance from `DateTime.Now` — position-based within the candidate set
- Most recent item → 1.0, oldest → 0.0, others linearly interpolated by rank

### PriorityScorer ranking

- Uses relative ranking within the input set (consistent with RecencyScorer)
- Highest priority value → 1.0, lowest → 0.0, others linearly interpolated
- Higher `Priority` int value = more important

### ReflexiveScorer passthrough

- Directly returns the `FutureRelevanceHint` value (already conventionally 0–1)
- Clamps to 0.0–1.0 if caller provides out-of-range hint
- Simplest scorer — pure passthrough with null handling

### Claude's Discretion

- Exact linear interpolation formula for rank-based scorers (RecencyScorer, PriorityScorer)
- Tag comparison case sensitivity in FrequencyScorer (recommend case-insensitive, consistent with ContextKind)
- Internal data structures for allocation-free scoring (e.g., stackalloc, ArrayPool)
- Test fixture design and helper utilities
- Benchmark structure and scenario selection

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 03-individual-scorers*
*Context gathered: 2026-03-11*
