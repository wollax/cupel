# Placers

Placers determine the final ordering of selected items in the context window. They are invoked during [Stage 6: Place](pipeline/place.md).

## Placer Interface

A placer implements a single function:

```text
Place(items: list of ScoredItem) -> list of ContextItem
```

- **`items`** is the merged list of [ScoredItem](data-model/enumerations.md#scoreditem) pairs — pinned items (with score 1.0) followed by sliced items (with their computed scores). See [Stage 6: Place](pipeline/place.md) for the merge process.
- The return value is the final ordered list of [ContextItem](data-model/context-item.md) instances.

### Contract

1. **Reorder only.** The placer MUST return the same items it receives, in a potentially different order. It MUST NOT add, remove, modify, or duplicate items.
2. **Deterministic.** Given the same input, the placer MUST produce the same output order.
3. **Score-aware.** The placer receives scores (via ScoredItem) and may use them to determine ordering. Pinned items have score 1.0.

## Placer Summary

| Placer | Algorithm | Use Case |
|---|---|---|
| [ChronologicalPlacer](placers/chronological.md) | Stable sort ascending by timestamp | Natural conversation flow; preserves temporal ordering |
| [UShapedPlacer](placers/u-shaped.md) | Highest scores at edges, lowest in middle | Exploits LLM primacy + recency attention bias |
