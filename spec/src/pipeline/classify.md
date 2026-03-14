# Stage 1: Classify

The Classify stage partitions input items into two groups — **pinned** items that bypass scoring and slicing, and **scoreable** items that enter the scoring pipeline — while excluding invalid items.

## Overview

Classification is the entry point of the pipeline. It performs three tasks:

1. **Exclude invalid items.** Items with `tokens < 0` are excluded with reason `NegativeTokens`. These items do not appear in any subsequent stage or in the output.
2. **Partition by pinned status.** Items with `pinned = true` are placed in the pinned list; all others are placed in the scoreable list.
3. **Validate pinned budget.** The total tokens of all pinned items must fit within `maxTokens - outputReserve`. If they do not, the pipeline raises an error.

Pinned items flow directly to the [Place](place.md) stage, skipping Score, Deduplicate, Sort, and Slice entirely. Scoreable items continue to the [Score](score.md) stage.

## Algorithm

```text
CLASSIFY(items, budget):
    pinned    <- empty list
    scoreable <- empty list

    for i <- 0 to length(items) - 1:
        item <- items[i]
        if item.tokens < 0:
            // Exclude: NegativeTokens
            continue
        if item.pinned:
            APPEND(pinned, item)
        else:
            APPEND(scoreable, item)

    // Validate pinned budget
    pinnedTokens <- 0
    for i <- 0 to length(pinned) - 1:
        pinnedTokens <- pinnedTokens + pinned[i].tokens

    availableForPinned <- budget.maxTokens - budget.outputReserve
    if pinnedTokens > availableForPinned:
        ERROR("Pinned items require {pinnedTokens} tokens, but only
               {availableForPinned} are available")

    return (pinned, scoreable)
```

## Edge Cases

- **Empty input.** If `items` is empty, both `pinned` and `scoreable` are empty lists. This is not an error.
- **All items pinned.** If every item is pinned, `scoreable` is empty. The Score, Deduplicate, Sort, and Slice stages operate on an empty list (producing empty output). The Place stage receives only pinned items.
- **All items excluded.** If every item has `tokens < 0`, both lists are empty.
- **Zero-token items.** Items with `tokens = 0` are valid and are classified normally (into pinned or scoreable). They are not excluded.
- **Pinned item with tokens < 0.** The negative-token check runs before the pinned check. A pinned item with `tokens < 0` is excluded, not pinned.

## Conformance Notes

- The exclusion check (`tokens < 0`) MUST be evaluated before the pinned partition. A pinned item with negative tokens is excluded.
- The pinned budget validation MUST compare against `maxTokens - outputReserve`, not `targetTokens`.
- Input order within each partition MUST be preserved. If items A, B, C are scoreable (in that input order), they appear as A, B, C in the scoreable list.
