# Stage 3: Deduplicate

The Deduplicate stage removes items with duplicate content, keeping only the highest-scoring instance of each unique content string. Deduplication is enabled by default but can be disabled.

## Overview

Deduplication prevents the same content from appearing multiple times in the context window. This is important because retrieval-augmented generation (RAG) sources and tool outputs may produce identical content from different sources or at different times.

**Identity semantics:** Two items are considered duplicates if and only if their `content` fields are byte-exact equal. This specification mandates **ordinal (byte-exact) comparison** with no Unicode normalization, no case folding, and no whitespace normalization. Items that differ by a single byte — including trailing whitespace, BOM markers, or normalization form — are considered distinct.

> **Pitfall (P3): Deduplication Identity.** Implementations MUST use byte-exact ordinal comparison for content equality. Using Unicode-normalized or case-insensitive comparison would produce different deduplication results and fail conformance tests.

## Algorithm

```text
DEDUPLICATE(scored, enabled):
    if not enabled or length(scored) = 0:
        return scored

    // Map: content string -> index of best item in scored array
    bestByContent <- empty map (ordinal string keys)

    for i <- 0 to length(scored) - 1:
        content <- scored[i].item.content
        if content IN bestByContent:
            existingIndex <- bestByContent[content]
            if scored[i].score > scored[existingIndex].score:
                bestByContent[content] <- i
            // Tiebreak: when scores are equal, keep the lower index
            // (the existing entry already has the lower index, so no action needed)
        else:
            bestByContent[content] <- i

    // Collect survivors in original order
    deduped <- empty list
    for i <- 0 to length(scored) - 1:
        if bestByContent[scored[i].item.content] = i:
            APPEND(deduped, scored[i])

    return deduped
```

## Tiebreaking

When two duplicate items have the **same score**, the item with the **lower original index** (earlier in the input) is kept. This is achieved naturally by the algorithm: the first occurrence is stored in the map, and subsequent items with equal scores do not replace it.

## Edge Cases

- **Deduplication disabled.** When disabled, the input is returned unchanged. No content comparison occurs.
- **Empty input.** Returns an empty list.
- **No duplicates.** All items are unique; the output is identical to the input.
- **All items identical.** All items have the same content. Only one survives: the one with the highest score (or lowest index if scores are equal).
- **Duplicate content, different metadata.** Only `content` is compared. Items with the same content but different `kind`, `source`, `tags`, or other fields are considered duplicates. The surviving item retains all its original fields.

## Conformance Notes

- Content comparison MUST be byte-exact ordinal. No Unicode normalization (NFC, NFD, NFKC, NFKD), no case folding, no whitespace normalization.
- The survivor for each content group is the item with the highest score. On score ties, the item with the lowest original index (position in the `scored` input array) wins.
- The output MUST preserve the relative order of surviving items from the input. If items at indices 0, 3, and 7 survive, they appear in that order in the output.
