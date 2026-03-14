# ChronologicalPlacer

The ChronologicalPlacer orders items by ascending timestamp, preserving natural temporal flow.

## Overview

ChronologicalPlacer is the simplest placer strategy. It sorts items by their `timestamp` field in ascending order (oldest first), producing a context window that reads in chronological order. This is the natural ordering for conversation-style contexts where temporal sequence matters.

### Fields Used

| Field | Source | Purpose |
|---|---|---|
| `timestamp` | [ContextItem](../data-model/context-item.md) | Sort key for temporal ordering |
| `score` | [ScoredItem](../data-model/enumerations.md#scoreditem) | Not used by this placer |

## Algorithm

```text
CHRONOLOGICAL-PLACE(items):
    if length(items) <= 1:
        if length(items) = 0:
            return []
        return [items[0].item]

    // Build sortable array with original indices
    sortable <- new array of (timestamp, index) of length(items)
    for i <- 0 to length(items) - 1:
        sortable[i] <- (items[i].item.timestamp, i)

    // Stable sort: timestamped items ascending, then null-timestamp items, then by index
    STABLE-SORT(sortable, by:
        1. Items with timestamps sort before items without timestamps
        2. Among timestamped items: ascending by timestamp
        3. Tiebreak: ascending by original index
    )

    result <- new array of ContextItem[length(items)]
    for i <- 0 to length(sortable) - 1:
        result[i] <- items[sortable[i].index].item

    return result
```

### Sort Order Detail

The sort comparison between two items `a` and `b`:

```text
CHRONOLOGICAL-COMPARE(a, b):
    aHas <- a.timestamp is not null
    bHas <- b.timestamp is not null

    if aHas and bHas:
        if a.timestamp != b.timestamp:
            return a.timestamp < b.timestamp    // ascending
        return a.index < b.index                // tiebreak

    if aHas:
        return true     // a (with timestamp) sorts before b (without)
    if bHas:
        return false    // b (with timestamp) sorts before a (without)

    return a.index < b.index    // both null: preserve original order
```

## Timestamp Placement Rules

1. **Timestamped items sort first**, in ascending temporal order.
2. **Null-timestamp items sort last**, in original input order.
3. **Tied timestamps** preserve original input order (stable sort).

This means the context window reads oldest-to-newest for timestamped items, with non-timestamped items appended at the end.

## Edge Cases

| Condition | Result |
|---|---|
| Empty input | Empty list |
| Single item | That item returned as-is |
| All items have timestamps | Sorted ascending by timestamp |
| No items have timestamps | Original input order preserved |
| Mixed timestamped and non-timestamped | Timestamped items first (ascending), then non-timestamped (original order) |
| All items have the same timestamp | Original input order preserved (stable sort) |

## Complexity

- **Time:** O(*N* log *N*) — dominated by the sort.
- **Space:** O(*N*) for the sortable array.

## Conformance Notes

- Timestamp comparison is by temporal ordering (the underlying UTC instant), not by string representation or timezone.
- Items without timestamps MUST sort after all timestamped items, not before.
- The sort MUST be stable. When timestamps are equal (or both null), the original input order (index) MUST be preserved.
- Scores are available (items are ScoredItem pairs) but MUST NOT influence the ordering. ChronologicalPlacer is purely timestamp-based.
