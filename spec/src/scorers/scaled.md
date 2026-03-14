# ScaledScorer

The ScaledScorer applies min-max normalization to an inner scorer's output across all items, ensuring the resulting scores span the full [0.0, 1.0] range.

## Overview

ScaledScorer is a **composite scorer** — it wraps a single inner scorer and normalizes its output using the formula `(raw - min) / (max - min)`, where `min` and `max` are the inner scorer's minimum and maximum outputs across all items in `allItems`.

This is useful when an inner scorer produces scores clustered in a narrow range (e.g., [0.3, 0.5]). ScaledScorer spreads them to [0.0, 1.0], maximizing the discriminative power of the scorer.

### Fields Used

ScaledScorer uses whatever fields its inner scorer uses.

## Algorithm

```text
SCALED-SCORE(item, allItems, inner):
    if length(allItems) = 0:
        return 0.5

    rawScore <- NaN    // sentinel: not yet computed
    min <- +infinity
    max <- -infinity

    // Single pass: score all items, find min/max, capture this item's raw score
    for i <- 0 to length(allItems) - 1:
        s <- inner.Score(allItems[i], allItems)
        if s < min:
            min <- s
        if s > max:
            max <- s
        if allItems[i] is item:    // reference identity
            rawScore <- s

    // Degenerate case: all scores equal
    if max = min:
        return 0.5

    return (rawScore - min) / (max - min)
```

## Score Distribution

After scaling:

- The item(s) with the inner scorer's minimum score receive 0.0.
- The item(s) with the inner scorer's maximum score receive 1.0.
- All other items are linearly interpolated between 0.0 and 1.0.

## Degenerate Case

When all items receive the same score from the inner scorer (`max = min`), the normalization formula would divide by zero. In this case, ScaledScorer returns **0.5** — the midpoint of the [0.0, 1.0] range. This represents "no information" — all items are equally relevant according to the inner scorer.

Similarly, when `allItems` is empty, the result is 0.5.

## Edge Cases

| Condition | Result |
|---|---|
| Empty `allItems` | 0.5 |
| Single item in `allItems` | 0.5 (max = min) |
| All items receive the same inner score | 0.5 (max = min) |
| Two items with distinct inner scores | One gets 0.0, the other gets 1.0 |
| Inner scorer returns values outside [0.0, 1.0] | Scaling still produces [0.0, 1.0] output |

## Complexity

- **Time:** O(*N*) per item (scores all *N* items to find min/max). O(*N*^2) total across all items in a scoring pass, since each item triggers a full scan.
- **Space:** O(1) auxiliary per invocation.

**Performance note:** Each call to `SCALED-SCORE` invokes the inner scorer *N* times. Across a full scoring pass of *N* items, the inner scorer is invoked *N*^2 times total. Implementations MAY cache the min/max computation across invocations within a single scoring pass, but caching is not required by this specification.

## Conformance Notes

- The item is identified within `allItems` by **reference identity** (the `is` check), not by structural equality.
- The inner scorer is invoked for every item in `allItems` on every call. There is no specification requirement to cache or memoize these results (but implementations are free to do so as an optimization, provided the output is identical).
- The degenerate return value MUST be exactly 0.5, not 0.0 or 1.0.
- ScaledScorer participates in [CompositeScorer](composite.md) cycle detection — a CompositeScorer containing a ScaledScorer will traverse through the ScaledScorer to its inner scorer.
