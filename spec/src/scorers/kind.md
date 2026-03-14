# KindScorer

The KindScorer assigns a score to each item based on its [ContextKind](../data-model/enumerations.md#contextkind), using a configurable weight map.

## Overview

KindScorer is an **absolute scorer** — it examines only the item's `kind` field. The `allItems` parameter is ignored. The score is a direct lookup in a weight dictionary: if the item's kind has a configured weight, that weight is returned; otherwise, the score is 0.0.

### Fields Used

| Field | Source | Purpose |
|---|---|---|
| `kind` | [ContextItem](../data-model/context-item.md) | Dictionary lookup key |

## Default Weights

When no custom weights are provided, the following defaults are used:

| ContextKind | Weight |
|---|---|
| `"SystemPrompt"` | 1.0 |
| `"Memory"` | 0.8 |
| `"ToolOutput"` | 0.6 |
| `"Document"` | 0.4 |
| `"Message"` | 0.2 |

These defaults reflect a common heuristic: system prompts and memories are typically more important to preserve than messages.

## Algorithm

```text
KIND-SCORE(item, allItems, weights):
    // weights is a map of ContextKind -> float64

    if weights contains item.kind:    // case-insensitive lookup
        return weights[item.kind]
    else:
        return 0.0
```

## Construction Validation

Custom weight maps MUST be validated at construction time:

1. Each weight value MUST be non-negative (`>= 0.0`).
2. Each weight value MUST be finite (not NaN, not positive/negative infinity).
3. Weight values are not required to be in [0.0, 1.0] — custom weights may exceed 1.0.

```text
VALIDATE-KIND-WEIGHTS(weights):
    for each (kind, weight) in weights:
        if weight < 0.0:
            ERROR("Weight for kind must be non-negative")
        if weight is not finite:
            ERROR("Weight for kind must be finite")
```

## Edge Cases

| Condition | Result |
|---|---|
| Item's kind is in the weight map | The corresponding weight value |
| Item's kind is not in the weight map | 0.0 |
| Custom weight map is empty | All items score 0.0 |
| Custom weight exceeds 1.0 | The weight value is returned as-is (no clamping) |

## Complexity

- **Time:** O(1) per item (hash map lookup).
- **Space:** O(*K*) where *K* is the number of configured kinds.

## Conformance Notes

- ContextKind comparison MUST be **case-insensitive** using ASCII case folding (see [ContextKind](../data-model/enumerations.md#contextkind)). A weight configured for `"Message"` matches items with kind `"message"`, `"MESSAGE"`, etc.
- The default weight map MUST contain exactly the five well-known ContextKind values listed above, with the specified weights. Implementations MAY use a different internal representation (e.g., frozen dictionary, immutable map) but MUST produce the same lookup behavior.
- Custom weights are not clamped to [0.0, 1.0]. An implementation using KindScorer with weights > 1.0 should be aware that composite scoring may produce scores outside the conventional range.
