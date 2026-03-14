# RecencyScorer

The RecencyScorer assigns higher scores to more recent items based on their relative timestamp rank among all scoreable items.

## Overview

RecencyScorer is a **relative scorer** — it compares each item's `timestamp` against all other items' timestamps. The score represents the item's position in the temporal ordering: the most recent item scores 1.0, the oldest scores 0.0, and items in between are linearly interpolated.

Items without a `timestamp` receive a score of 0.0.

### Fields Used

| Field | Source | Purpose |
|---|---|---|
| `timestamp` | [ContextItem](../data-model/context-item.md) | Temporal ordering key |

## Algorithm

For each item, count how many other timestamped items have a strictly earlier timestamp. This count (the item's rank) is divided by the number of timestamped items minus one to produce a normalized score.

```text
RECENCY-SCORE(item, allItems):
    // Items without a timestamp always score 0.0
    if item.timestamp = null:
        return 0.0

    countWithTimestamp <- 0
    rank <- 0

    for i <- 0 to length(allItems) - 1:
        if allItems[i].timestamp = null:
            continue
        countWithTimestamp <- countWithTimestamp + 1
        if allItems[i].timestamp < item.timestamp:
            rank <- rank + 1

    // Single timestamped item is the "most recent" by definition
    if countWithTimestamp <= 1:
        return 1.0

    return rank / (countWithTimestamp - 1)
```

## Score Distribution

Given *N* items with timestamps, all with distinct timestamps:

- The most recent item has rank *N* - 1, scoring 1.0.
- The oldest item has rank 0, scoring 0.0.
- Intermediate items score `rank / (N - 1)`, producing evenly-spaced values.

### Tied Timestamps

When multiple items share the same timestamp, they receive the same rank (the count of items strictly older than them). This means tied items receive identical scores.

## Edge Cases

| Condition | Result |
|---|---|
| `item.timestamp` is null | 0.0 |
| All items have null timestamps | All score 0.0 |
| Only one item has a timestamp | That item scores 1.0 |
| All items share the same timestamp | All score 0.0 (rank = 0 for each, since no item is strictly older) |

**Note:** The single-item case returns 1.0 (not 0.0) because a lone timestamped item is trivially the most recent.

## Complexity

- **Time:** O(*N*) per item, O(*N*^2) total across all items in a scoring pass.
- **Space:** O(1) auxiliary per invocation.

## Conformance Notes

- Timestamp comparison is by temporal ordering (the underlying instant), not by string representation.
- The comparison `allItems[i].timestamp < item.timestamp` is strict — items with equal timestamps do not increment the rank.
- The denominator is `countWithTimestamp - 1`, not `length(allItems) - 1`. Only items that have a timestamp participate in the ranking.
