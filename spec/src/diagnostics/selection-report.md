# SelectionReport

## Overview

The `SelectionReport` is the complete diagnostic output from a pipeline run. It contains the event log, lists of included and excluded items with their reasons, and aggregate statistics. It is the primary artifact callers use to understand pipeline decisions — answering questions such as "why was this item excluded?" and "which items competed for the same budget?"

## How to Obtain

```text
OBTAIN-REPORT:
    collector <- CREATE DiagnosticTraceCollector(detail_level)
    result    <- pipeline.RUN(candidates, budget, collector)
    report    <- collector.BUILD-REPORT()
    // result contains the selected items for use in the LLM call
    // report contains the diagnostic data for inspection
```

The report is extracted from the collector after the pipeline run completes. This keeps the pipeline's primary return type unchanged — the report is a side-channel, not part of the main output.

*Rationale: post-call extraction separates diagnostic concerns from the pipeline's functional return value. Callers who do not need diagnostics never interact with report types.*

*Rejected alternative: returning the report as part of the pipeline result — couples diagnostics to every call site, even those that do not need it.*

## SelectionReport Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `events` | list of TraceEvent | Yes | — | All recorded events in insertion (stage) order. See [Events](events.md). |
| `included` | list of IncludedItem | Yes | — | Items selected for the context window, in final placed order. |
| `excluded` | list of ExcludedItem | Yes | — | Items not selected, sorted by score descending (stable by insertion order on ties). |
| `total_candidates` | integer | Yes | — | Total number of items considered by the pipeline. Equals `len(included) + len(excluded)`. |
| `total_tokens_considered` | integer | Yes | — | Sum of `tokens` across all items in both `included` and `excluded` lists. |

Items excluded at any pipeline stage — including pre-scoring exclusions such as `NegativeTokens` at the Classify stage and `Deduplicated` at the Deduplicate stage — appear in the `excluded` list. The `excluded` list is the complete set of items not selected, regardless of which stage excluded them. Items excluded before scoring carry a score of `0.0`.

**Complete JSON example:**

```json
{
  "events": [
    { "stage": "Classify", "duration_ms": 0.3, "item_count": 5 },
    { "stage": "Score", "duration_ms": 8.2, "item_count": 3 },
    { "stage": "Deduplicate", "duration_ms": 0.1, "item_count": 3 },
    { "stage": "Slice", "duration_ms": 1.4, "item_count": 3 },
    { "stage": "Place", "duration_ms": 0.2, "item_count": 2 }
  ],
  "included": [
    {
      "item": { "content": "Recent conversation turn", "tokens": 256, "kind": "Message" },
      "score": 0.92,
      "reason": { "reason": "Scored" }
    },
    {
      "item": { "content": "System prompt", "tokens": 128, "kind": "SystemPrompt" },
      "score": 0.0,
      "reason": { "reason": "Pinned" }
    }
  ],
  "excluded": [
    {
      "item": { "content": "Old tool output", "tokens": 2048, "kind": "ToolOutput" },
      "score": 0.45,
      "reason": { "reason": "BudgetExceeded", "item_tokens": 2048, "available_tokens": 512 }
    },
    {
      "item": { "content": "Duplicate message", "tokens": 256, "kind": "Message" },
      "score": 0.30,
      "reason": { "reason": "Deduplicated", "deduplicated_against": "Recent conversation turn" }
    }
  ],
  "total_candidates": 5,
  "total_tokens_considered": 4736
}
```

## IncludedItem

An `IncludedItem` pairs a context item with the score and reason that led to its inclusion.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `item` | ContextItem | Yes | — | The included context item. See [ContextItem](../data-model/context-item.md). |
| `score` | float64 | Yes | — | The computed relevance score at time of inclusion. `0.0` for pinned and zero-token items. |
| `reason` | InclusionReason | Yes | — | Why this item was included. See [Exclusion Reasons](exclusion-reasons.md#inclusionreason). |

**JSON example:**

```json
{
  "item": { "content": "User's latest message", "tokens": 64, "kind": "Message" },
  "score": 0.95,
  "reason": { "reason": "Scored" }
}
```

## ExcludedItem

An `ExcludedItem` pairs a context item with the score and reason that led to its exclusion.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `item` | ContextItem | Yes | — | The excluded context item. See [ContextItem](../data-model/context-item.md). |
| `score` | float64 | Yes | — | The computed relevance score at time of exclusion. |
| `reason` | ExclusionReason | Yes | — | Why this item was excluded. See [Exclusion Reasons](exclusion-reasons.md#exclusionreason). |

**JSON example — BudgetExceeded:**

```json
{
  "item": { "content": "Large tool output", "tokens": 4096, "kind": "ToolOutput" },
  "score": 0.60,
  "reason": { "reason": "BudgetExceeded", "item_tokens": 4096, "available_tokens": 1024 }
}
```

**JSON example — Deduplicated:**

```json
{
  "item": { "content": "Duplicate content", "tokens": 256, "kind": "Message" },
  "score": 0.40,
  "reason": { "reason": "Deduplicated", "deduplicated_against": "Original message" }
}
```

`deduplicated_against` appears only when `reason` is `Deduplicated`. For all other reasons, the variant's own fields appear instead. Absent fields are omitted — no nulls.

*Rationale: `deduplicated_against` is modelled as a field on the `Deduplicated` reason variant (not as a nullable top-level field on `ExcludedItem`) because the data-carrying variant design mandates that each variant carries its own context fields. `Deduplicated` carries `deduplicated_against`; `BudgetExceeded` carries `item_tokens` and `available_tokens`; and so on. This keeps `ExcludedItem` clean (always 3 fields: `item`, `score`, `reason`) and avoids nullable fields that apply to only one reason.*

*Rejected alternative: nullable `deduplicated_against` on `ExcludedItem` — couples the `ExcludedItem` schema to a specific reason variant, contradicts the data-carrying variant design.*

## Conformance Notes

- The `excluded` list MUST be sorted by score descending. When two items have equal scores, the item excluded earlier in the pipeline run appears first (stable sort by insertion order on ties). This ordering surfaces the highest-value rejected items first, which is the most useful presentation for debugging "why wasn't this included?" questions. *Rejected alternative: insertion order — less useful for diagnosis; the highest-scored excluded item is rarely the first item processed.*
- The `included` list MUST be in final placed order (the order determined by the Placer), not score order or insertion order.
- `total_candidates` MUST equal `len(included) + len(excluded)`.
- `total_tokens_considered` MUST equal the sum of `tokens` across all items in both `included` and `excluded` lists.
- The `score` field on `IncludedItem` and `ExcludedItem` MUST reflect the score at the time the inclusion/exclusion decision was made, not a recalculated value. Items excluded before the Score stage (e.g., `NegativeTokens` at Classify) MUST have a `score` of `0.0`.
