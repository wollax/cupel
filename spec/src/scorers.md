# Scorers

Scorers compute relevance scores for context items. They are the primary ranking mechanism in the Cupel pipeline, invoked during [Stage 2: Score](pipeline/score.md).

## Scorer Interface

A scorer implements a single pure function:

```text
Score(item: ContextItem, allItems: list of ContextItem) -> float64
```

- **`item`** is the individual [ContextItem](data-model/context-item.md) being scored.
- **`allItems`** is the complete list of scoreable items (including `item` itself). This enables relative scoring — scorers that rank an item against its peers.
- The return value is an IEEE 754 64-bit double-precision floating-point number.

### Contract

1. **Pure function.** Scorers MUST NOT mutate state between calls, perform I/O, or produce side effects. Given identical inputs, a scorer MUST return the same output.
2. **Conventional range.** Scores are conventionally in the range [0.0, 1.0], where 0.0 indicates lowest relevance and 1.0 indicates highest relevance. This range is a convention, not an enforced constraint — custom [KindScorer](scorers/kind.md) weights may produce values outside this range.
3. **IEEE 754 arithmetic.** All scoring computations MUST use 64-bit double-precision floating-point arithmetic (see [Numeric Precision](introduction.md#numeric-precision)).

## Scorer Summary

| Scorer | Input Fields Used | Output Range | Description |
|---|---|---|---|
| [RecencyScorer](scorers/recency.md) | `timestamp` | [0.0, 1.0] | Rank-based: more recent items score higher |
| [PriorityScorer](scorers/priority.md) | `priority` | [0.0, 1.0] | Rank-based: higher priority values score higher |
| [KindScorer](scorers/kind.md) | `kind` | [0.0, max weight] | Dictionary lookup by item kind |
| [TagScorer](scorers/tag.md) | `tags` | [0.0, 1.0] | Weighted tag matching, normalized |
| [FrequencyScorer](scorers/frequency.md) | `tags` | [0.0, 1.0] | Proportion of peers sharing a tag |
| [ReflexiveScorer](scorers/reflexive.md) | `futureRelevanceHint` | [0.0, 1.0] | Passthrough of caller-provided hint |
| [CompositeScorer](scorers/composite.md) | (delegates to children) | [0.0, 1.0] | Weighted average of child scorers |
| [ScaledScorer](scorers/scaled.md) | (delegates to inner) | [0.0, 1.0] | Min-max normalization of an inner scorer |
| [MetadataTrustScorer](scorers/metadata-trust.md) | `metadata["cupel:trust"]` | [0.0, 1.0] | Passthrough of caller-provided trust value from metadata |
| [DecayScorer](scorers/decay.md) | `timestamp` | [0.0, 1.0] | Time-based decay with configurable curve (Exponential, Step, Window) |
| [MetadataKeyScorer](scorers/metadata-key.md) | `metadata[key]` | {1.0, boost} | Multiplicative boost for items where metadata key matches a configured value |

## Scorer Categories

### Absolute Scorers

Absolute scorers examine only the item being scored; the `allItems` parameter is ignored. These scorers produce the same score for an item regardless of what other items are present.

- **KindScorer** — dictionary lookup
- **TagScorer** — weighted tag matching
- **ReflexiveScorer** — passthrough of `futureRelevanceHint`
- **MetadataTrustScorer** — passthrough of caller-provided trust value from `metadata["cupel:trust"]`
- **DecayScorer** — time-based decay from item timestamp against a reference time (Exponential, Step, or Window curve)
- **MetadataKeyScorer** — multiplicative boost for items where a metadata key matches a configured value

### Relative Scorers

Relative scorers compare the item against its peers. The same item may receive different scores depending on the composition of `allItems`.

- **RecencyScorer** — rank among timestamped items
- **PriorityScorer** — rank among prioritized items
- **FrequencyScorer** — proportion of peers sharing a tag

### Composite Scorers

Composite scorers delegate to other scorers and transform or combine their outputs.

- **CompositeScorer** — weighted average of multiple child scorers
- **ScaledScorer** — min-max normalization wrapper for any inner scorer
