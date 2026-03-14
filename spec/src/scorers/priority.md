# PriorityScorer

The PriorityScorer assigns higher scores to items with higher numeric priority values, based on their relative rank among all scoreable items.

## Overview

PriorityScorer is a **relative scorer** with the same rank-based algorithm as [RecencyScorer](recency.md), but applied to the `priority` field instead of `timestamp`. Higher numeric priority values produce higher scores.

Items without a `priority` receive a score of 0.0.

### Fields Used

| Field | Source | Purpose |
|---|---|---|
| `priority` | [ContextItem](../data-model/context-item.md) | Numeric importance ranking key |

## Algorithm

For each item, count how many other prioritized items have a strictly lower priority value. This count (the item's rank) is divided by the number of prioritized items minus one to produce a normalized score.

```text
PRIORITY-SCORE(item, allItems):
    // Items without a priority always score 0.0
    if item.priority = null:
        return 0.0

    countWithPriority <- 0
    rank <- 0

    for i <- 0 to length(allItems) - 1:
        if allItems[i].priority = null:
            continue
        countWithPriority <- countWithPriority + 1
        if allItems[i].priority < item.priority:
            rank <- rank + 1

    // Single prioritized item scores 1.0
    if countWithPriority <= 1:
        return 1.0

    return rank / (countWithPriority - 1)
```

## Score Distribution

Given *N* items with priorities, all with distinct priority values:

- The highest-priority item has rank *N* - 1, scoring 1.0.
- The lowest-priority item has rank 0, scoring 0.0.
- Intermediate items score `rank / (N - 1)`, producing evenly-spaced values.

### Tied Priorities

When multiple items share the same priority value, they receive the same rank (the count of items with strictly lower priority). Tied items receive identical scores.

## Edge Cases

| Condition | Result |
|---|---|
| `item.priority` is null | 0.0 |
| All items have null priorities | All score 0.0 |
| Only one item has a priority | That item scores 1.0 |
| All items share the same priority | All score 0.0 (no item has strictly lower priority) |

## Complexity

- **Time:** O(*N*) per item, O(*N*^2) total across all items in a scoring pass.
- **Space:** O(1) auxiliary per invocation.

## Conformance Notes

- Higher numeric priority values produce higher scores. `priority = 10` scores higher than `priority = 5`.
- The comparison `allItems[i].priority < item.priority` is strict — items with equal priority do not increment the rank.
- The denominator is `countWithPriority - 1`, not `length(allItems) - 1`. Only items that have a priority participate in the ranking.
- The rank-based algorithm is identical to [RecencyScorer](recency.md), substituting `priority` for `timestamp`.
