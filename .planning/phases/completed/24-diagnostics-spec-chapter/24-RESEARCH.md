# Phase 24 Research: Diagnostics Spec Chapter

**Phase:** 24 — Diagnostics Spec Chapter
**Date:** 2026-03-15
**Researcher mode:** ecosystem (spec-writing focus)

---

## Standard Stack

The "standard stack" for this phase is mdBook + Markdown:

- **Tool:** mdBook (spec/book.toml configured with `src = "src"`)
- **Adding a chapter:** Two steps only:
  1. Create `spec/src/diagnostics.md` and `spec/src/diagnostics/<sub-page>.md` files
  2. Add entries to `spec/src/SUMMARY.md` under `# Specification` as a sibling of `- [Pipeline](pipeline.md)`
- **No book.toml changes needed** — mdBook infers everything from SUMMARY.md
- **SUMMARY.md entry format:**
  ```
  - [Diagnostics](diagnostics.md)
    - [TraceCollector](diagnostics/trace-collector.md)
    - [Events](diagnostics/events.md)
    - [Exclusion Reasons](diagnostics/exclusion-reasons.md)
    - [SelectionReport](diagnostics/selection-report.md)
  ```
- **Mermaid diagrams:** supported by the book configuration (Pipeline chapter uses flowchart TD)
- **Pseudocode blocks:** use ` ```text ` fenced blocks (consistent with all Pipeline stage pages)
- **JSON examples:** use ` ```json ` fenced blocks

---

## Architecture Patterns

### Spec Chapter Structure (from existing chapters)

Every spec chapter follows this pattern:

**Top-level page (e.g., `pipeline.md`, `data-model.md`):**
- One-sentence definition of the concept
- Invariants section (numbered list of inviolable guarantees)
- Data-flow or overview table/diagram
- Brief summary table (type, role, description)
- Cross-references to sub-pages

**Sub-pages (e.g., `pipeline/classify.md`, `data-model/context-item.md`):**
- `# Title` heading (concept name, not "Stage N:" prefix)
- Overview prose (2-4 sentences on purpose)
- Fields table (for types) OR Algorithm block (for stages)
- Edge cases section
- Conformance Notes section (behavioral mandates for implementors)

### Diagnostics Chapter Sub-Page Breakdown (recommended)

Four sub-pages following the data-flow ordering mandated in CONTEXT.md:

| Sub-page | File | Covers |
|----------|------|--------|
| Top-level | `diagnostics.md` | Overview, ownership model, null-path guarantee, data flow |
| TraceCollector | `diagnostics/trace-collector.md` | Contract, IsEnabled, RecordStageEvent, RecordItemEvent, NullTraceCollector, DiagnosticTraceCollector, TraceDetailLevel |
| Events | `diagnostics/events.md` | TraceEvent fields, PipelineStage enum, OverflowEvent |
| Exclusion Reasons | `diagnostics/exclusion-reasons.md` | ExclusionReason enum (all 8 variants with emission stage), InclusionReason enum (all 3 variants) |
| SelectionReport | `diagnostics/selection-report.md` | SelectionReport fields, IncludedItem, ExcludedItem, sort order, how to obtain the report |

**Rationale for splitting ExclusionReasons from Events:** Reason enums are the load-bearing API surface with the most variant-level decisions (data-carrying vs. fieldless, reserved-but-not-emitted variants). They warrant their own page to keep Events focused on structural types.

**Rationale for keeping TraceCollector and TraceDetailLevel together:** TraceDetailLevel only exists to control DiagnosticTraceCollector's filter behavior — it has no meaning in isolation.

### Section Headings Pattern

From `pipeline/classify.md` and `data-model/context-item.md`, use exactly:

```
# Title
## Overview           ← prose explanation of purpose
## Fields             ← for data types (table format)
## Algorithm          ← for behavioural specs (```text pseudocode)
## Edge Cases         ← numbered list
## Conformance Notes  ← behavioral mandates for implementors
```

For the top-level diagnostics.md, use:
```
# Diagnostics
## Overview
## Ownership Model
## Data Flow
## Null Path Guarantee
## Summary
```

---

## Complete .NET Type Catalogue

Every type, field, method, and enum variant from `src/Wollax.Cupel/Diagnostics/`:

### ITraceCollector (interface → spec: TraceCollector contract)

| Member | Type | Notes |
|--------|------|-------|
| `IsEnabled` | bool (get) | Callers check before constructing payloads — avoid allocations on disabled path |
| `RecordStageEvent(traceEvent)` | void | Always recorded when IsEnabled is true |
| `RecordItemEvent(traceEvent)` | void | May be filtered by detail level |

### NullTraceCollector

- Singleton pattern (`Instance` static field)
- `IsEnabled` → `false`
- Both Record methods are no-ops
- Zero-cost: callers can short-circuit entirely via `IsEnabled`

### DiagnosticTraceCollector

| Member | Type | Notes |
|--------|------|-------|
| Constructor | `(TraceDetailLevel detailLevel = Stage, Action<TraceEvent>? callback = null)` | callback is optional; fires synchronously on each recorded event |
| `IsEnabled` | bool | always `true` |
| `Events` | `IReadOnlyList<TraceEvent>` | recorded events in insertion order |
| `RecordStageEvent` | void | always records |
| `RecordItemEvent` | void | filtered by `detailLevel < Item` |

**Note:** The callback (`Action<TraceEvent>`) is a .NET-specific feature. The spec should describe the callback pattern in terms of observer semantics, not as a language-specific delegate. Implementors may choose their callback mechanism.

### TraceDetailLevel (enum)

| Variant | Value | Meaning |
|---------|-------|---------|
| `Stage` | 0 | Stage-level events only (durations, item counts) |
| `Item` | 1 | Stage-level plus per-item events (individual scores, exclusion reasons) |

### TraceEvent (record struct)

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Stage` | PipelineStage | Yes | The stage that produced this event |
| `Duration` | TimeSpan | Yes | Wall-clock duration of the stage; `TimeSpan.Zero` for item-level events |
| `ItemCount` | int | Yes | Number of items processed |
| `Message` | string? | No | Optional diagnostic message; absent fields omitted in wire format |

**Wire format name:** `stage`, `duration_ms` (recommend milliseconds as float for language-agnostic portability), `item_count`, `message`

### PipelineStage (enum)

| Variant | .NET has | Notes |
|---------|----------|-------|
| `Classify` | Yes | — |
| `Score` | Yes | — |
| `Deduplicate` | Yes | — |
| `Sort` | **No** | .NET omits Sort; the spec SHOULD omit Sort for language-agnostic alignment. Sort is internal, no user-visible duration boundary. |
| `Slice` | Yes | — |
| `Place` | Yes | — |

**Load-bearing decision:** The spec must decide whether to include Sort as a PipelineStage. Recommendation: **omit Sort** — consistent with .NET, reduces conformance test burden, Sort is O(n log n) and not a meaningful diagnostic boundary.

### ExclusionReason (enum)

| Variant | Currently emitted? | Stage | Notes |
|---------|-------------------|-------|-------|
| `BudgetExceeded` | Yes | Slice / Place(Truncate) | Item didn't fit in token budget |
| `ScoredTooLow` | **Reserved** (not emitted) | — | For future minimum-score filter |
| `Deduplicated` | Yes | Deduplicate | Byte-exact content duplicate removed |
| `QuotaCapExceeded` | **Reserved** | — | Kind exceeded quota cap |
| `QuotaRequireDisplaced` | **Reserved** | — | Displaced for another kind's quota |
| `NegativeTokens` | Yes | Classify | `tokens < 0` |
| `PinnedOverride` | Yes | Place(Truncate) | Displaced by pinned item during truncation |
| `Filtered` | **Reserved** | — | Future filter predicate |

**Load-bearing decision:** The spec must define whether ExclusionReason variants carry data. The prior research (PITFALLS-RUST-DIAGNOSTICS.md §3.2) recommends data-carrying variants. However, the .NET reference uses a fieldless enum, and the CONTEXT.md says "Spec can diverge from .NET." Recommendation: **spec fieldless variants for now** with explicit note that future versions may add associated data. This avoids specifying a wire format for variant payloads before the use cases are understood. Mark the enum as extensible (implementations must handle unknown variants gracefully).

### InclusionReason (enum)

| Variant | Stage | Notes |
|---------|-------|-------|
| `Scored` | Place | Included based on computed score within budget |
| `Pinned` | Classify/Place | Bypassed scoring and slicing |
| `ZeroToken` | Place | Included at no budget cost |

### ExcludedItem (record)

| Field | Type | Notes |
|-------|------|-------|
| `Item` | ContextItem | The excluded context item |
| `Score` | double | Computed score at time of exclusion |
| `Reason` | ExclusionReason | Why excluded |
| `DeduplicatedAgainst` | ContextItem? | Only present when Reason = Deduplicated |

**Wire format:** `item`, `score`, `reason`, `deduplicated_against` (absent when not Deduplicated)

### IncludedItem (record)

| Field | Type | Notes |
|-------|------|-------|
| `Item` | ContextItem | The included context item |
| `Score` | double | Computed score |
| `Reason` | InclusionReason | Why included |

**Wire format:** `item`, `score`, `reason`

### SelectionReport (record)

| Field | Type | Notes |
|-------|------|-------|
| `Events` | list of TraceEvent | In insertion (stage) order |
| `Included` | list of IncludedItem | In final placed order |
| `Excluded` | list of ExcludedItem | Sorted by score descending; stable by insertion order on ties |
| `TotalCandidates` | int | Total items considered by pipeline (before any exclusion) |
| `TotalTokensConsidered` | int | Sum of tokens across all candidates |

**Sort invariant for Excluded:** Score descending, stable (insertion order) tiebreak. This is a conformance-testable invariant.

### OverflowEvent (record)

| Field | Type | Notes |
|-------|------|-------|
| `TokensOverBudget` | int | Tokens over the budget limit |
| `OverflowingItems` | list of ContextItem | All items at time of overflow (pinned + sliced combined) |
| `Budget` | ContextBudget | The budget that was exceeded |

**Spec decision:** OverflowEvent is produced when OverflowStrategy = Proceed. The spec should define how implementations expose OverflowEvent. The .NET approach uses a callback; the spec can describe it as "the observer receives an OverflowEvent" without prescribing the callback mechanism.

### ReportBuilder (internal)

Internal accumulator — not part of the public contract. The spec should describe the accumulation semantics abstractly (the collector accumulates decisions throughout the run; the report is obtained after the run completes).

---

## Load-Bearing Decisions (from Prior Research)

These decisions must be resolved in the spec chapter. Each is marked with confidence in the recommended answer:

### D1: PipelineStage Sort variant

**Decision:** Omit Sort from `PipelineStage`. Sort is an internal O(n log n) step with no user-visible diagnostic boundary. Consistent with .NET.
**Confidence:** HIGH — verified against .NET source; both prior research files agree.

### D2: ExclusionReason — fieldless vs. data-carrying

**Decision:** Spec fieldless variants. Prior research (PITFALLS §3.2) argues for data-carrying, but CONTEXT.md mandates language-agnostic spec with no implementation hints. Data-carrying variants force the spec to define variant payload types and wire format before real use cases are known. Spec fieldless + mark extensible.
**Confidence:** MEDIUM — defensible; the planner should note this explicitly so the spec author can make a final call.

### D3: How SelectionReport is obtained

**Decision:** Post-call extraction from the collector (not as pipeline return value). Caller creates a DiagnosticTraceCollector, runs the pipeline, then calls a method on the collector to obtain the SelectionReport. This keeps the pipeline's primary return type unchanged.
**Confidence:** HIGH — mandated by CONTEXT.md decisions; consistent with .NET design; both research files agree.

### D4: Reserved-but-not-emitted ExclusionReason variants

**Decision:** All 8 variants appear in the spec. Variants marked Reserved are documented as "defined but not emitted by any built-in pipeline stage; reserved for future use." This is a contract boundary: implementations that emit these variants in custom stages are conforming; callers must handle them.
**Confidence:** HIGH — .NET source has explicit `<remarks>Reserved for future use</remarks>` on these variants.

### D5: OverflowEvent observer pattern

**Decision:** Spec describes OverflowEvent as an output type but leaves the delivery mechanism to implementations ("when Proceed strategy is active, the implementation delivers an OverflowEvent to the observer"). This is language-agnostic.
**Confidence:** HIGH — pure contract spec; no implementation hint needed.

### D6: Absent fields in JSON (no nulls)

**Decision:** LOCKED in CONTEXT.md. `deduplicated_against` absent when not Deduplicated. `message` absent when no message. Do not emit `null` values.
**Confidence:** HIGH.

### D7: Duration wire format

**Decision:** Recommend `duration_ms` as a float64 (milliseconds). The .NET uses TimeSpan; the spec should prescribe a concrete wire format. Milliseconds as float64 is portable across all languages. Item-level events have `duration_ms: 0.0`.
**Confidence:** MEDIUM — could also be `duration_ns` (integer) or ISO 8601 duration string. Milliseconds float is the most readable for diagnostic purposes.

### D8: Callback observer for DiagnosticTraceCollector

**Decision:** Spec the capability but do not prescribe the mechanism. "Implementations may support an observer callback that fires synchronously on each recorded event." This is a Claude's Discretion area per CONTEXT.md.
**Confidence:** HIGH.

---

## Don't Hand-Roll

The spec author must not invent:

- **A new chapter layout** — mirror the Pipeline chapter sub-page structure exactly (one top-level `.md` + sub-page directory)
- **New prose conventions** — use the existing informal-but-precise style of the Pipeline chapter, not RFC 2119 keywords
- **A different field naming convention** — use snake_case for all JSON wire format field names, consistent with existing conformance vectors
- **A new table format** — use the same `| Field | Type | Required | Default | Description |` column order as `context-item.md`
- **A custom diagram syntax** — use Mermaid flowchart TD if a data-flow diagram is needed (same as pipeline.md)

---

## Common Pitfalls

Ordered by severity for spec authors:

### P1: Omitting the "reserved but not emitted" distinction [HIGH]

If ExclusionReason variants like `ScoredTooLow`, `Filtered`, `QuotaCapExceeded`, `QuotaRequireDisplaced` are omitted from the spec, implementations won't know they exist. If they're listed without the "not emitted by built-in stages" note, readers assume they can observe them from standard pipelines. The spec must explicitly mark each reserved variant.

### P2: Conflating Stage-level and Item-level events [HIGH]

The spec must clearly distinguish:
- **Stage events** — one per pipeline stage, carry duration and item count
- **Item events** — zero or more per stage (only when `TraceDetailLevel.Item`), carry per-item data

If the prose conflates these, implementations will not know when to gate on `TraceDetailLevel`.

### P3: Specifying how SelectionReport is stored/produced [HIGH]

The spec must define the *contract* (what fields SelectionReport contains, what their types and invariants are) without specifying *how* the collector accumulates data or whether a ReportBuilder type exists. The internal accumulation mechanism is an implementation detail. The spec should only describe the output structure and how to obtain it (post-call extraction).

### P4: Omitting the sort invariant for Excluded [MEDIUM]

The `excluded` list in SelectionReport is sorted by score descending (stable by insertion order). This is a conformance-testable invariant that must appear in the spec. If omitted, implementations will produce different orderings, breaking any conformance vectors that check the excluded list.

### P5: Undefined behavior for TraceEvent.duration on item events [MEDIUM]

The spec must explicitly state: "For item-level events, `duration_ms` is `0.0`." Without this, implementations will either omit the field (breaking the schema) or measure per-item duration (wrong semantic).

### P6: Missing wire format for duration [MEDIUM]

`std::time::Duration` (Rust) and `TimeSpan` (.NET) have no universal wire format. The spec must prescribe one. Omitting this causes implementations to emit incompatible formats (nanoseconds, milliseconds, ISO 8601 strings, etc.).

### P7: OverflowEvent not tied to OverflowStrategy [MEDIUM]

OverflowEvent is only produced when `OverflowStrategy = Proceed`. If the spec does not make this link explicit, readers will wonder when OverflowEvents appear.

---

## Code Examples (Spec Prose Patterns)

These are load-bearing patterns from existing spec chapters that the spec author should replicate.

### Pattern 1: Field table format (from context-item.md)

```markdown
## Fields

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `content` | string | Yes | — | The textual content... |
| `tokens` | integer | Yes | — | The token count... |
| `priority` | integer or null | No | null | Optional priority override... |
```

Apply this pattern to: TraceEvent, ExcludedItem, IncludedItem, SelectionReport, OverflowEvent.

### Pattern 2: Enum value table (from enumerations.md)

```markdown
## Values

| Value | Description |
|---|---|
| `"Classify"` | Classification stage |
| `"Score"` | Scoring stage |
```

Apply this pattern to: PipelineStage, ExclusionReason, InclusionReason, TraceDetailLevel.

For ExclusionReason, add an "Emitted by" column:

```markdown
| Value | Description | Emitted by |
|---|---|---|
| `"BudgetExceeded"` | Item did not fit within the token budget | Slice stage, Place stage (Truncate) |
| `"ScoredTooLow"` | Item scored below the selection threshold | Reserved — not emitted by built-in stages |
```

### Pattern 3: JSON example (required — one complete example per type per CONTEXT.md)

From context-item.md style (not present there but established by CONTEXT.md decision):

```markdown
## Example

```json
{
  "stage": "Score",
  "duration_ms": 12.4,
  "item_count": 47
}
```
```

Note: `message` is absent because it is optional and absent fields are omitted.

### Pattern 4: Algorithm pseudocode (from pipeline/classify.md)

```markdown
## Algorithm

```text
OBTAIN-REPORT(collector):
    // After pipeline.run() completes:
    report <- collector.BuildReport()
    return report
```
```

Use `ALL-CAPS` verb names, lowercase variable names, `<-` for assignment, `//` for comments — consistent with all pipeline stage pages.

### Pattern 5: Conformance Notes section (from pipeline/classify.md)

```markdown
## Conformance Notes

- The `excluded` list MUST be sorted by score descending. When two items have equal
  scores, the item that was excluded earlier in the pipeline run appears first (stable sort).
- `duration_ms` for item-level events MUST be `0.0`. Per-item processing is not
  independently timed.
- Implementations MUST handle unknown `ExclusionReason` values gracefully. New reason
  values may be added in future versions of the specification.
```

**Important:** The existing spec uses informal mandates without RFC 2119 keywords ("MUST"). However, looking at the actual spec pages (classify.md, slice.md), they DO use "MUST" in Conformance Notes. The CONTEXT.md says "No RFC 2119 keywords — stay consistent with the existing informal-but-precise spec style." But the existing spec DOES use "MUST" in Conformance Notes sections. The planner should resolve this: the existing spec uses MUST precisely; CONTEXT.md may have been referring to the overview sections only. Recommendation: use MUST only in Conformance Notes sections, consistent with existing chapters.

### Pattern 6: Inline rationale (required per CONTEXT.md)

```markdown
`excluded` items are sorted by score descending (stable by insertion order on ties). This
ordering surfaces the highest-value rejected items first, which is the most useful
presentation for debugging "why wasn't this included?" questions.

*Rejected alternative: Insertion order. Stable insertion order is less useful for
diagnosis — the highest-scored excluded item is rarely the first item processed.*
```

---

## Sub-Page File Map

```
spec/src/
  SUMMARY.md          ← add Diagnostics entry after Pipeline section
  diagnostics.md      ← top-level overview page
  diagnostics/
    trace-collector.md
    events.md
    exclusion-reasons.md
    selection-report.md
```

SUMMARY.md addition (after the `- [Placers]` block, before `# Conformance`):

```markdown
- [Diagnostics](diagnostics.md)
  - [TraceCollector](diagnostics/trace-collector.md)
  - [Events](diagnostics/events.md)
  - [Exclusion Reasons](diagnostics/exclusion-reasons.md)
  - [SelectionReport](diagnostics/selection-report.md)
```

---

## Content Map per Sub-Page

### diagnostics.md (top-level)

- One-sentence definition: "The Cupel diagnostics system exposes the pipeline's internal selection decisions as structured, inspectable data without affecting the pipeline's output or performance when disabled."
- Ownership model: trace collector is passed at call time (not stored on pipeline), so it is per-invocation. One collector instance per pipeline execution.
- Null path guarantee: when diagnostics are disabled, no events are constructed and no performance overhead is incurred. Implementations achieve this by checking `is_enabled` before constructing event payloads.
- Data flow diagram: input → pipeline (annotated with trace calls) → output + collector → SelectionReport
- Summary table of types and their roles

### diagnostics/trace-collector.md

- TraceCollector contract: `is_enabled`, `record_stage_event`, `record_item_event`
- NullTraceCollector: zero-cost disabled implementation; `is_enabled = false`; singleton recommended but not required
- DiagnosticTraceCollector: buffered collector; `is_enabled = true`; respects TraceDetailLevel; exposes accumulated events; produces SelectionReport after run completes
- TraceDetailLevel enum: Stage (0) and Item (1)
- Observer callback: optional capability; implementations may support a per-event callback that fires synchronously

### diagnostics/events.md

- TraceEvent: fields table + JSON example (with message) + JSON example (item-level, no message, duration_ms: 0.0)
- PipelineStage enum: Classify, Score, Deduplicate, Slice, Place (no Sort)
- When each stage event fires: one stage event per named stage, after the stage completes
- OverflowEvent: fields table + JSON example; only produced when OverflowStrategy = Proceed

### diagnostics/exclusion-reasons.md

- ExclusionReason: table with Variant, Description, Emitted by, Reserved status
- All 8 variants
- InclusionReason: table with Variant, Description, Emitted by
- All 3 variants
- Cross-reference: "See [Stage 1: Classify](../pipeline/classify.md) for when NegativeTokens is recorded"

### diagnostics/selection-report.md

- SelectionReport fields table + complete JSON example
- IncludedItem fields table + JSON example
- ExcludedItem fields table + JSON examples (with and without deduplicated_against)
- How to obtain: create collector → run pipeline → extract report
- Sort invariant for excluded items (load-bearing conformance note)
- TotalCandidates and TotalTokensConsidered definitions

---

## Cross-References to Other Spec Chapters

The diagnostics chapter should link to:
- `../pipeline/classify.md` — NegativeTokens exclusion
- `../pipeline/deduplicate.md` — Deduplicated exclusion
- `../pipeline/slice.md` — BudgetExceeded exclusion
- `../pipeline/place.md` — BudgetExceeded and PinnedOverride during Truncate; OverflowEvent during Proceed
- `../data-model/enumerations.md#overflowstrategy` — OverflowStrategy → OverflowEvent connection
- `../data-model/context-item.md` — ContextItem (used in ExcludedItem, IncludedItem, OverflowEvent)

---

## Confidence Levels

| Area | Level | Reason |
|------|-------|--------|
| .NET type catalogue (all fields, all variants) | HIGH | Direct source code inspection of all 14 files |
| Existing spec chapter structure and conventions | HIGH | Read pipeline.md, classify.md, slice.md, place.md, context-item.md, enumerations.md |
| mdBook SUMMARY.md entry format | HIGH | Direct inspection of existing SUMMARY.md + book.toml |
| Sub-page breakdown recommendation | HIGH | Derived from data-flow ordering (CONTEXT.md locked) + type cohesion analysis |
| ExclusionReason fieldless vs. data-carrying | MEDIUM | Prior research argues data-carrying; CONTEXT.md constrains to pure contract level; both are defensible |
| duration_ms as float64 wire format | MEDIUM | Reasoned choice; no existing conformance vector sets precedent |
| Sort variant omission from PipelineStage | HIGH | Consistent with .NET; both research files agree; Sort has no user-visible boundary |
| MUST usage in Conformance Notes | MEDIUM | Existing spec DOES use MUST in Conformance Notes despite CONTEXT.md saying "no RFC 2119 keywords"; planner must confirm intent |

---

## Open Questions for Planner

1. **MUST usage:** Conformance Notes in existing chapters use "MUST" (e.g., classify.md: "MUST be evaluated before the pinned partition"). CONTEXT.md says "No RFC 2119 keywords." Which takes precedence? Recommendation: use MUST in Conformance Notes only (consistent with existing chapters), not in overview prose.

2. **ExclusionReason data carriers:** The spec can define fieldless variants now and defer data-carrying to a later spec revision. Or it can spec data-carrying variants from day one (e.g., `BudgetExceeded` carrying `available_tokens` and `item_tokens`). Since the spec leads implementations, defining data-carrying now prevents an incompatible change later. Recommend the planner note this as a decision the spec author must make explicitly.

3. **OverflowEvent delivery mechanism:** The spec can say "when Proceed strategy is active, the collector receives an OverflowEvent" (making it part of the TraceCollector contract) OR "implementations may expose OverflowEvent through a separate observer mechanism." The first is simpler and more prescriptive; the second is more flexible. The .NET implementation exposes it via `ITraceCollector` indirectly (the pipeline emits it through the collector). Recommend: OverflowEvent is a data type defined in the spec; delivery is left to implementations.

4. **Sort sub-page structure:** The diagnostics chapter has 4 sub-pages. Is this enough or should OverflowEvent get its own sub-page? Recommendation: keep OverflowEvent on `events.md` — it is one record type, not a complex enough concept to warrant a dedicated page.

5. **PipelineStage Sort:** Should the spec define Sort as a stage for implementations that want to emit timing for it? Or should the spec silence this entirely? Recommendation: omit Sort from the spec; add a note "implementations may emit additional events for internal stages not listed here; callers must handle unknown stage values gracefully." This is consistent with the extensibility principle.

---

## Ready for Planning

All source material catalogued. Planner can create PLAN.md files for:
- PLAN-01: Create spec directory structure and top-level diagnostics.md
- PLAN-02: Write diagnostics/trace-collector.md
- PLAN-03: Write diagnostics/events.md
- PLAN-04: Write diagnostics/exclusion-reasons.md
- PLAN-05: Write diagnostics/selection-report.md
- PLAN-06: Update SUMMARY.md to register the new chapter

*Research completed 2026-03-15. Sources: All 14 .NET Diagnostics source files; spec/src/pipeline.md, pipeline/classify.md, pipeline/slice.md, pipeline/place.md, data-model.md, data-model/context-item.md, data-model/enumerations.md, data-model/context-budget.md; ARCHITECTURE-RUST-DIAGNOSTICS.md; PITFALLS-RUST-DIAGNOSTICS.md; FEATURES.md; SUMMARY.md; book.toml.*
