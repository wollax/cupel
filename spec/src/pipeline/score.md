# Stage 2: Score

The Score stage computes a relevance score for each scoreable item by invoking the configured scorer, producing a list of [ScoredItem](../data-model/enumerations.md#scoreditem) pairs.

## Overview

Scoring is the primary ranking mechanism in the pipeline. For each scoreable item, the scorer receives both the individual item and the full list of all scoreable items. This dual-argument design enables both absolute scorers (that examine only the item) and relative scorers (that compare the item against its peers).

The output is a list of ScoredItem pairs, each containing the original ContextItem and its computed score. The order of the output list matches the order of the input list (input position is preserved).

## Algorithm

```text
SCORE(scoreable, scorer):
    scored <- new array of length(scoreable)

    for i <- 0 to length(scoreable) - 1:
        score <- scorer.Score(scoreable[i], scoreable)
        scored[i] <- ScoredItem(scoreable[i], score)

    return scored
```

### Scorer Interface

A scorer implements a single function:

```text
Score(item: ContextItem, allItems: list of ContextItem) -> float64
```

- `item` is the individual item being scored.
- `allItems` is the complete list of scoreable items (including `item` itself).
- The return value is an IEEE 754 64-bit double.

See the [Scorers](../scorers.md) chapter for all scorer algorithm specifications.

## Score Semantics

- Scores are conventionally in the range [0.0, 1.0], where 0.0 indicates lowest relevance and 1.0 indicates highest relevance.
- The [0.0, 1.0] range is a convention, not an enforced constraint. Individual scorers (e.g., [KindScorer](../scorers/kind.md) with custom weights) may produce values outside this range.
- All scoring computations MUST use IEEE 754 64-bit double-precision floating-point arithmetic (see [Introduction](../introduction.md#numeric-precision)).

## Edge Cases

- **Empty scoreable list.** If `scoreable` is empty, the output is an empty array. The scorer is never invoked.
- **Single item.** The scorer receives a one-element `allItems` list. Relative scorers (e.g., RecencyScorer, PriorityScorer) handle this by returning a defined value (typically 1.0 for the sole item with a non-null field).
- **NaN or infinite scores.** This specification does not mandate rejection of NaN or infinite scores at the Score stage. However, such values may produce implementation-defined behavior in subsequent stages (Sort, Slice). Well-behaved scorers produce finite values.

## Conformance Notes

- The scorer MUST receive the full `scoreable` list as the second argument, not a subset or the original input list.
- Output order MUST match input order: `scored[i]` corresponds to `scoreable[i]`.
- The scorer is invoked exactly once per item per pipeline execution.
