# Enumerations

## ContextKind

ContextKind is an **extensible string enumeration** that classifies the type of a context item. Implementations MUST support arbitrary string values beyond the well-known set.

### Well-Known Values

| Value | Description |
|---|---|
| `"Message"` | A conversational message (default) |
| `"Document"` | A document or file content |
| `"ToolOutput"` | Output from a tool invocation |
| `"Memory"` | A stored memory or fact |
| `"SystemPrompt"` | A system-level instruction |

### Comparison Semantics

ContextKind comparison MUST be **case-insensitive** using ASCII case folding. The following are all considered equal:

- `"Message"`, `"message"`, `"MESSAGE"`, `"mEsSaGe"`

This case-insensitivity applies everywhere ContextKind is compared:
- [KindScorer](../scorers/kind.md) weight lookups
- [QuotaSlice](../slicers/quota.md) kind partitioning
- [ContextBudget](context-budget.md) `reservedSlots` key lookups

### Construction

A ContextKind value MUST be a non-null, non-whitespace-only string. Implementations SHOULD reject empty or whitespace-only values at construction time.

---

## ContextSource

ContextSource is an **extensible string enumeration** that identifies the origin of a context item. Implementations MUST support arbitrary string values beyond the well-known set.

### Well-Known Values

| Value | Description |
|---|---|
| `"Chat"` | From a user chat interaction (default) |
| `"Tool"` | From a tool or function call |
| `"Rag"` | From a retrieval-augmented generation source |

### Comparison Semantics

ContextSource comparison MUST be **case-insensitive** using ASCII case folding. The following are all considered equal:

- `"Chat"`, `"chat"`, `"CHAT"`

### Construction

A ContextSource value MUST be a non-null, non-whitespace-only string.

---

## OverflowStrategy

OverflowStrategy is a **closed enumeration** (not extensible) that controls pipeline behavior when selected items exceed the token budget after merging pinned and sliced items.

### Values

| Value | Behavior |
|---|---|
| `Throw` | Raise an error (exception) when selected items exceed `targetTokens`. This is the default strategy. |
| `Truncate` | Remove lowest-priority items from the selection until the total fits within `targetTokens`. Pinned items are never removed by truncation. Items are removed from the tail of the merged list (lowest-scored non-pinned items first). |
| `Proceed` | Accept the over-budget selection and report the overflow to an observer. No items are removed. |

### Overflow Detection

Overflow is detected after the [Place](../pipeline/place.md) stage merges pinned items (assigned score 1.0) with sliced items. The total tokens of the merged set are compared against `targetTokens`:

```
mergedTokens = sum of tokens for all items in merged set
if mergedTokens > targetTokens:
    apply OverflowStrategy
```

See [Stage 6: Place](../pipeline/place.md) for the full overflow handling algorithm.

---

## ScoredItem

ScoredItem is a **value type** (pair/tuple) that associates a [ContextItem](context-item.md) with its computed relevance score.

### Fields

| Field | Type | Description |
|---|---|---|
| `item` | [ContextItem](context-item.md) | The context item |
| `score` | float64 | The computed relevance score, conventionally in the range [0.0, 1.0] |

### Semantics

- The `score` field is an IEEE 754 64-bit double.
- Scores are conventionally in [0.0, 1.0] but this is not enforced by the type. Individual scorers may produce values outside this range (e.g., KindScorer with custom weights).
- ScoredItem is produced by the [Score](../pipeline/score.md) stage and consumed by [Deduplicate](../pipeline/deduplicate.md), [Sort](../pipeline/sort.md), [Slice](../pipeline/slice.md), and [Place](../pipeline/place.md).
- Pinned items are assigned a score of 1.0 when merged into the scored item list during the [Place](../pipeline/place.md) stage.
