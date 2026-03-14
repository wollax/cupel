# FrequencyScorer

The FrequencyScorer scores an item based on the proportion of peer items that share at least one tag with it.

## Overview

FrequencyScorer is a **relative scorer** — it compares the item's tags against every other item's tags to determine how "connected" the item is within the candidate set. Items that share tags with many peers score higher, reflecting thematic relevance within the context.

### Fields Used

| Field | Source | Purpose |
|---|---|---|
| `tags` | [ContextItem](../data-model/context-item.md) | Tag overlap detection with peers |

## Algorithm

```text
FREQUENCY-SCORE(item, allItems):
    if length(item.tags) = 0 or length(allItems) <= 1:
        return 0.0

    matchingItems <- 0

    for i <- 0 to length(allItems) - 1:
        // Skip self (by identity, not by value)
        if allItems[i] is item:
            continue
        if length(allItems[i].tags) = 0:
            continue
        if SHARES-ANY-TAG(item.tags, allItems[i].tags):
            matchingItems <- matchingItems + 1

    return matchingItems / (length(allItems) - 1)
```

### Tag Overlap Detection

```text
SHARES-ANY-TAG(tagsA, tagsB):
    for i <- 0 to length(tagsA) - 1:
        for j <- 0 to length(tagsB) - 1:
            if CASE-INSENSITIVE-EQUAL(tagsA[i], tagsB[j]):
                return true
    return false
```

## Score Interpretation

- A score of 1.0 means every other item in the list shares at least one tag with this item.
- A score of 0.0 means no other item shares any tag with this item (or the item has no tags, or it is the only item).
- Intermediate values represent the fraction of peers with overlapping tags.

## Edge Cases

| Condition | Result |
|---|---|
| Item has no tags | 0.0 |
| Only one item in `allItems` | 0.0 |
| No peer shares any tag | 0.0 |
| All peers share at least one tag | 1.0 |
| Peer has no tags | That peer does not count as matching |

## Self-Exclusion

The item being scored is excluded from the peer count using **reference identity** (object identity), not value equality. This means:

- If the same ContextItem instance appears multiple times in `allItems`, only the reference-identical instance is skipped; other copies with identical content are counted as peers.
- The denominator is always `length(allItems) - 1`, regardless of how many items are skipped.

## Complexity

- **Time:** O(*N* * *T*_a * *T*_b) per item in the worst case, where *N* is the number of items and *T*_a, *T*_b are tag list lengths. O(*N*^2 * *T*^2) total across all items.
- **Space:** O(1) auxiliary per invocation.

## Conformance Notes

- Tag comparison in `SHARES-ANY-TAG` MUST be **case-insensitive** using ASCII case folding. `"Important"` and `"important"` are considered matching tags.
- Self-exclusion MUST use reference identity (the `is` check), not structural equality. This matches the [ContextItem](../data-model/context-item.md) immutability contract — items are compared by identity throughout the pipeline.
- The denominator is `length(allItems) - 1`, which accounts for the self-exclusion. It is not reduced further for tagless peers.
