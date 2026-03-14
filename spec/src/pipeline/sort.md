# Stage 4: Sort

The Sort stage orders scored items by descending score using a **stable sort** with a prescribed tiebreaking rule.

## Overview

After deduplication, items must be sorted so that the highest-scored items appear first. This ordering is consumed by the [Slice](slice.md) stage, which greedily or optimally selects items from the front of the sorted list.

Sort stability is critical: when two items have the same score, their relative order from the input must be preserved. This ensures deterministic, reproducible output across all conforming implementations.

> **Pitfall (P1): Sort Stability.** Implementations MUST use a stable sort algorithm or equivalently sort by the composite key `(score descending, originalIndex ascending)`. An unstable sort (e.g., quicksort without index tiebreaking) can produce different orderings for equal-scored items, causing conformance test failures.

## Algorithm

```text
SORT(deduped):
    // Build composite sort keys
    keys <- new array of (score: float64, index: int) of length(deduped)
    for i <- 0 to length(deduped) - 1:
        keys[i] <- (deduped[i].score, i)

    // Sort by score descending, then by index ascending (tiebreak)
    SORT-BY(keys, comparator):
        comparator(a, b):
            if a.score != b.score:
                return COMPARE-DESCENDING(a.score, b.score)
            else:
                return COMPARE-ASCENDING(a.index, b.index)

    // Reconstruct sorted array
    sorted <- new array of length(deduped)
    for i <- 0 to length(keys) - 1:
        sorted[i] <- deduped[keys[i].index]

    return sorted
```

### Implementation Pattern

The algorithm builds an array of `(score, index)` pairs, sorts this auxiliary array using the composite comparator, then reconstructs the ScoredItem array by following the index references. This pattern works with any sort algorithm (stable or unstable) because the index component of the composite key provides a deterministic tiebreak.

Alternatively, implementations may use a natively stable sort and sort only by score descending, relying on the sort's stability guarantee to preserve input order for equal scores. Both approaches produce identical results.

## Edge Cases

- **Empty input.** Returns an empty array.
- **Single item.** Returns a single-element array (no sorting needed).
- **All equal scores.** Output preserves input order entirely (the index tiebreak produces ascending index order).
- **NaN scores.** Behavior with NaN scores is implementation-defined. IEEE 754 comparisons involving NaN return false for all relational operators, which may cause items with NaN scores to appear in an unpredictable position. Conforming implementations are not required to handle NaN scores in any particular way. Well-behaved scorers do not produce NaN.

## Conformance Notes

- The sort MUST be stable, or equivalently MUST use the composite key `(score descending, originalIndex ascending)`.
- "Original index" refers to the item's position in the input to the Sort stage (i.e., the deduplication output), not the item's position in the original candidate list.
- Score comparison uses standard IEEE 754 floating-point ordering (total order for non-NaN values).
