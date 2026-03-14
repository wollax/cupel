# TagScorer

The TagScorer assigns a score based on how well an item's tags match a configured set of weighted tags.

## Overview

TagScorer is an **absolute scorer** — it examines only the item's `tags` field against a preconfigured tag-weight map. The `allItems` parameter is ignored. The score is the sum of matched tag weights divided by the total configured weight, clamped to [0.0, 1.0].

### Fields Used

| Field | Source | Purpose |
|---|---|---|
| `tags` | [ContextItem](../data-model/context-item.md) | Tags to match against configured weights |

## Algorithm

```text
TAG-SCORE(item, allItems, tagWeights, totalWeight):
    // tagWeights: map of string -> float64
    // totalWeight: sum of all values in tagWeights (precomputed at construction)

    if totalWeight = 0.0 or length(item.tags) = 0:
        return 0.0

    matchedSum <- 0.0
    for i <- 0 to length(item.tags) - 1:
        if tagWeights contains item.tags[i]:    // case-sensitive lookup (default)
            matchedSum <- matchedSum + tagWeights[item.tags[i]]

    return min(matchedSum / totalWeight, 1.0)
```

## Construction

TagScorer is constructed with a tag-weight map. At construction time:

1. The `totalWeight` is precomputed as the sum of all weight values.
2. Each weight MUST be non-negative (`>= 0.0`).
3. Each weight MUST be finite (not NaN, not positive/negative infinity).

```text
CONSTRUCT-TAG-SCORER(tagWeights):
    totalWeight <- 0.0
    for each (tag, weight) in tagWeights:
        if weight < 0.0:
            ERROR("Tag weight must be non-negative")
        if weight is not finite:
            ERROR("Tag weight must be finite")
        totalWeight <- totalWeight + weight
    // Store tagWeights and totalWeight for use in scoring
```

## Score Interpretation

- A score of 1.0 means the item's matched tags account for 100% or more of the total configured weight.
- A score of 0.0 means no tags matched (or the item has no tags, or no weights are configured).
- The `min(..., 1.0)` clamp prevents scores from exceeding 1.0 when an item matches all configured tags or when duplicate tag entries would cause the sum to exceed the total.

## Edge Cases

| Condition | Result |
|---|---|
| Item has no tags | 0.0 |
| `totalWeight` is 0.0 (all weights are zero) | 0.0 |
| Item has tags but none match configured weights | 0.0 |
| Item matches all configured tags | `min(sum of matched / total, 1.0)` = 1.0 if exactly all matched |
| Item has duplicate tags matching the same weight | Each occurrence adds to `matchedSum`; result clamped to 1.0 |

## Complexity

- **Time:** O(*T*) per item, where *T* is the number of tags on the item (hash map lookup per tag).
- **Space:** O(*W*) where *W* is the number of configured tag weights.

## Conformance Notes

- Tag key lookup in the weight map uses the **string comparer provided at construction**, defaulting to **ordinal (case-sensitive)** comparison. This is distinct from the case-insensitive tag comparison used by [FrequencyScorer](frequency.md).
- The `totalWeight` denominator is the sum of **all** configured weights, not just the weights of matched tags.
- The result is clamped to `min(result, 1.0)`. It is never clamped to a minimum of 0.0 because `matchedSum` can only be non-negative (all weights are non-negative).
