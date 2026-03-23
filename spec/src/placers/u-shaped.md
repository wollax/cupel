# UShapedPlacer

The UShapedPlacer positions the highest-scored items at both edges of the context window (start and end), with the lowest-scored items in the middle.

## Overview

UShapedPlacer exploits the **primacy and recency bias** observed in large language models — LLMs tend to pay more attention to content at the beginning and end of the context window, with less attention to content in the middle. By placing the most relevant items at both edges, UShapedPlacer maximizes the chance that important context is attended to.

The placement pattern forms a "U" shape when score is plotted against position: high at the edges, low in the center.

### Fields Used

| Field | Source | Purpose |
|---|---|---|
| `score` | [ScoredItem](../data-model/enumerations.md#scoreditem) | Determines placement priority |

## Algorithm

```text
U-SHAPED-PLACE(items):
    if length(items) <= 1:
        if length(items) = 0:
            return []
        return [items[0].item]

    // Step 1: Sort by score descending, tiebreak by index ascending
    scored <- new array of (score, index) of length(items)
    for i <- 0 to length(items) - 1:
        scored[i] <- (items[i].score, i)

    STABLE-SORT(scored, by score descending, then by index ascending)

    // Step 2: Alternate placement — even ranks to left edge, odd ranks to right edge
    result <- new array of ContextItem[length(items)]
    left  <- 0                      // next available left position
    right <- length(items) - 1      // next available right position

    for i <- 0 to length(scored) - 1:
        originalIndex <- scored[i].index
        item <- items[originalIndex].item

        if i mod 2 = 0:             // even rank (0, 2, 4, ...)
            result[left] <- item
            left <- left + 1
        else:                       // odd rank (1, 3, 5, ...)
            result[right] <- item
            right <- right - 1

    return result
```

## Placement Pattern

Items are sorted by score descending and assigned a rank (0, 1, 2, ...). The rank determines placement:

| Rank | Position | Edge |
|---|---|---|
| 0 (highest score) | Left edge (index 0) | Start |
| 1 (2nd highest) | Right edge (index N-1) | End |
| 2 (3rd highest) | Left edge (index 1) | Start |
| 3 (4th highest) | Right edge (index N-2) | End |
| 4 (5th highest) | Left edge (index 2) | Start |
| ... | ... | ... |

### Visual Example

Given 7 items sorted by score `[A=0.9, B=0.8, C=0.7, D=0.6, E=0.5, F=0.4, G=0.3]`:

```
Position:  [  0  ] [  1  ] [  2  ] [  3  ] [  4  ] [  5  ] [  6  ]
Item:      [  A  ] [  C  ] [  E  ] [  G  ] [  F  ] [  D  ] [  B  ]
Score:     [ 0.9 ] [ 0.7 ] [ 0.5 ] [ 0.3 ] [ 0.4 ] [ 0.6 ] [ 0.8 ]

Score plot:  ___                                               ___
            |   \                                           /  |
            |    \___                                 ___/     |
            |        \___                       ___/           |
            |            \___             ___/                 |
            |                \___   ___/                       |
            |                    \_/                           |
           start                middle                       end
```

The highest scores (A, B) are at the edges. The lowest score (G) is in the center.

## Sort Stability

The sort MUST be stable or use a composite sort key. When two items have the same score, the item with the lower original index is ranked first. This ensures deterministic placement when scores are tied.

## Edge Cases

| Condition | Result |
|---|---|
| Empty input | Empty list |
| Single item | That item returned as-is |
| Two items | Higher-scored at index 0, lower-scored at index 1 |
| All items have the same score | Original input order preserved (stable sort); alternating placement still applies |
| Pinned items (score 1.0) | Not special-cased by UShapedPlacer; pinned items arrive with score 1.0 from the pipeline and naturally rank highly, sorting to edges. |

## Complexity

- **Time:** O(*N* log *N*) — dominated by the sort.
- **Space:** O(*N*) for the scored array and result array.

## Conformance Notes

- The sort is **descending by score**, with **ascending index** as the tiebreaker.
- Even-ranked items (0, 2, 4, ...) fill from the **left** (start of the array).
- Odd-ranked items (1, 3, 5, ...) fill from the **right** (end of the array).
- The `left` pointer advances forward; the `right` pointer advances backward. They meet in the middle.
- Pinned items arrive with score 1.0 (see [Stage 6: Place](../pipeline/place.md#pinned-item-score)). They will be ranked highly and placed at the edges, which is the intended behavior — pinned items should receive maximum attention.
