# ContextItem

A ContextItem is an immutable record representing a single piece of context in the pipeline. Every scorer, slicer, and placer operates on ContextItem instances.

## Fields

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `content` | string | Yes | — | The textual content of the item. Non-null and non-empty. |
| `tokens` | integer | Yes | — | The token count for this context item, as provided by the caller. Items with negative token counts are excluded during [classification](../pipeline/classify.md). |
| `kind` | [ContextKind](enumerations.md#contextkind) | No | `"Message"` | The kind of context item. Used by [KindScorer](../scorers/kind.md) and [QuotaSlice](../slicers/quota.md). |
| `source` | [ContextSource](enumerations.md#contextsource) | No | `"Chat"` | The origin of this context item. |
| `priority` | integer or null | No | null | Optional priority override. Higher values indicate greater importance. Used by [PriorityScorer](../scorers/priority.md). |
| `tags` | list of strings | No | empty list | Descriptive tags for filtering and scoring. May be empty. Tag comparison is case-insensitive (ASCII fold). Used by [TagScorer](../scorers/tag.md) and [FrequencyScorer](../scorers/frequency.md). |
| `metadata` | map of string to any | No | empty map | Arbitrary key-value metadata. Opaque to the pipeline — not read or modified by any pipeline stage. Preserved on output for caller use. |
| `timestamp` | datetime (UTC) or null | No | null | When this context item was created or observed. Used by [RecencyScorer](../scorers/recency.md) and [ChronologicalPlacer](../placers/chronological.md). |
| `futureRelevanceHint` | float64 or null | No | null | Hint for future relevance scoring, conventionally in the range [0.0, 1.0]. Used by [ReflexiveScorer](../scorers/reflexive.md). |
| `pinned` | boolean | No | false | Whether this item is pinned. Pinned items bypass scoring and slicing — they are always included in the output regardless of score or budget. |
| `originalTokens` | integer or null | No | null | The original token count before any external summarization or truncation. Not used by the pipeline; preserved for caller diagnostics. |

## Constraints

1. **`content` MUST be a non-null, non-empty string.** Implementations SHOULD reject construction of a ContextItem with null or empty content.

2. **`tokens` is caller-provided.** The pipeline does not tokenize content; it trusts the caller's token count. The pipeline excludes items with `tokens < 0` during classification (see [Stage 1: Classify](../pipeline/classify.md)).

3. **`tags` uses case-insensitive comparison.** When comparing tags (e.g., in TagScorer or FrequencyScorer), implementations MUST use case-insensitive ASCII comparison. `"Important"` and `"important"` are considered equal.

4. **`metadata` is opaque.** The pipeline MUST NOT read, interpret, or modify metadata values. Implementations MUST preserve metadata through the pipeline so that selected items retain their original metadata on output.

5. **`timestamp` represents a point in time with UTC semantics.** Implementations may use any datetime representation that preserves UTC instant precision (e.g., ISO 8601 with timezone, Unix epoch milliseconds). Comparison is by temporal ordering.

6. **Immutability.** Once constructed, a ContextItem MUST NOT be modified. Pipeline stages MUST NOT mutate any field of any ContextItem they receive.
