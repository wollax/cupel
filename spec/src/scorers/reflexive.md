# ReflexiveScorer

The ReflexiveScorer passes through the item's `futureRelevanceHint` as the score, clamped to [0.0, 1.0].

## Overview

ReflexiveScorer is an **absolute scorer** — it examines only the item's `futureRelevanceHint` field. The `allItems` parameter is ignored. This scorer enables callers (or upstream systems like an LLM) to inject relevance signals directly into the scoring pipeline.

### Fields Used

| Field | Source | Purpose |
|---|---|---|
| `futureRelevanceHint` | [ContextItem](../data-model/context-item.md) | Caller-provided relevance signal |

## Algorithm

```text
REFLEXIVE-SCORE(item, allItems):
    if item.futureRelevanceHint = null:
        return 0.0

    value <- item.futureRelevanceHint

    if value is not finite:    // NaN, +infinity, -infinity
        return 0.0

    return clamp(value, 0.0, 1.0)
```

Where `clamp(x, lo, hi)` returns `lo` if `x < lo`, `hi` if `x > hi`, and `x` otherwise.

## Score Interpretation

- The score is a direct passthrough of the caller-provided hint, clamped to the conventional [0.0, 1.0] range.
- A hint of 0.0 means the caller considers the item irrelevant for future context.
- A hint of 1.0 means the caller considers the item highly relevant for future context.
- Values outside [0.0, 1.0] are clamped, not rejected.

## Edge Cases

| Condition | Result |
|---|---|
| `futureRelevanceHint` is null | 0.0 |
| `futureRelevanceHint` is NaN | 0.0 |
| `futureRelevanceHint` is +infinity | 0.0 |
| `futureRelevanceHint` is -infinity | 0.0 |
| `futureRelevanceHint` is 0.5 | 0.5 |
| `futureRelevanceHint` is -0.3 | 0.0 (clamped) |
| `futureRelevanceHint` is 1.7 | 1.0 (clamped) |

## Complexity

- **Time:** O(1) per item.
- **Space:** O(1) auxiliary.

## Conformance Notes

- Non-finite values (NaN, positive infinity, negative infinity) MUST return 0.0, not be clamped to a range boundary.
- The check for finiteness MUST occur before the clamp. A NaN value must not pass through `clamp` (which may have implementation-defined behavior for NaN in some languages).
- The clamping order is: null check, then finiteness check, then clamp to [0.0, 1.0].
