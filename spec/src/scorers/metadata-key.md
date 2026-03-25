# MetadataKeyScorer

The MetadataKeyScorer returns a configurable multiplier (`boost`) when a metadata key matches a configured value, and `1.0` (a neutral multiplier) otherwise.

## Overview

MetadataKeyScorer is an **absolute scorer** — it examines only the item's `metadata` map. The `allItems` parameter is ignored. This scorer enables callers to boost items that carry a specific metadata signal by returning a multiplier value rather than an absolute score.

**Multiplicative semantics:** Unlike MetadataTrustScorer (which is an absolute passthrough returning a score directly), MetadataKeyScorer returns a multiplier value intended for use in a `CompositeScorer`. A match returns `config.boost` (e.g., `1.5`); a non-match returns `1.0` (the neutral multiplier). Returned values are NOT clamped to [0.0, 1.0].

### Fields Used

| Field | Source | Purpose |
|---|---|---|
| `metadata` | [ContextItem](../data-model/context-item.md) | Key-value map; the scorer checks the configured `key` for the configured `value` |

## Metadata Namespace Reservation

All `ContextItem.metadata` keys with the `"cupel:"` prefix are reserved for library-defined conventions. Callers MUST NOT use this prefix for application-specific keys. This reservation is enforced at the spec level only — there is no runtime validation of key prefixes.

## Conventions

### `cupel:priority`

`cupel:priority` is a caller-provided priority signal for the item.

- **Key:** `"cupel:priority"`
- **RECOMMENDED values:** `"high"`, `"normal"` (or `"low"`)
- **Open string:** implementations MUST NOT validate or reject unknown values. Callers MAY use any string value. Values are not validated at construction time or scoring time.
- **Typical usage:** `MetadataKeyScorer("cupel:priority", "high", 1.5)` boosts high-priority items 1.5× in a composite scorer, leaving all other items at 1.0 (neutral multiplier).

### Other Reserved Conventions

See [MetadataTrustScorer](metadata-trust.md) for the `cupel:trust` and `cupel:source-type` conventions, which are the other reserved metadata keys in this library.

## Configuration

### Constructor Parameters

| Parameter | Type | Constraint | Description |
|---|---|---|---|
| `key` | string | (none) | The metadata key to match |
| `value` | string | (none) | The exact value to match (string-to-string comparison) |
| `boost` | float64 | > 0.0 | The multiplier returned for matching items |

### Validation

- `boost` MUST be greater than 0.0.
- A `boost` value of 0.0 MUST be rejected at construction time with `CupelError::ScorerConfig` (Rust) / `ArgumentException` (not `ArgumentOutOfRangeException`) (.NET).
- A negative `boost` value MUST be rejected at construction time with `CupelError::ScorerConfig` (Rust) / `ArgumentException` (.NET).
- A non-finite `boost` value (NaN or infinity) MUST be rejected at construction time with `CupelError::ScorerConfig` (Rust) / `ArgumentException` (.NET).
- Validation occurs at construction time, not scoring time.

## Algorithm

```text
METADATA-KEY-SCORE(item, allItems, config):
    if item.metadata does not contain config.key:
        return config.defaultMultiplier  // 1.0

    raw <- item.metadata[config.key]
    if raw != config.value:
        return config.defaultMultiplier  // 1.0

    return config.boost
```

`config.defaultMultiplier` is a fixed constant of **1.0**. It is NOT a constructor parameter — the neutral multiplier value is always 1.0. The scorer does NOT receive `item.score` as input; it returns a multiplier value used by the downstream scoring stage.

## Score Interpretation

MetadataKeyScorer uses **multiplicative semantics**: it returns a multiplier, not an absolute score.

- A matching item returns `config.boost` (e.g., `1.5`).
- A non-matching item returns `1.0` (neutral — has no effect when multiplied).
- When used in a `CompositeScorer`, the composite multiplies the contributions of its child scorers. A `MetadataKeyScorer` with `boost=1.5` effectively increases the composite weight for matching items by 1.5× relative to non-matching items.
- Returned values are **NOT** clamped to [0.0, 1.0].

## Edge Cases

| Condition | Result |
|---|---|
| Key absent from `item.metadata` | 1.0 (default multiplier) |
| Key present, value matches `config.value` | `config.boost` |
| Key present, value does not match `config.value` | 1.0 (default multiplier) |
| `boost = 0.0` at construction | Construction error |
| `boost < 0.0` at construction | Construction error |
| `boost` is NaN or Infinity at construction | Construction error |

## Conformance Vector Outlines

The following scenarios outline the expected behavior for conformance testing.

1. **Match → boost applied:** An item with `metadata["cupel:priority"] = "high"`, scorer configured with `key="cupel:priority"`, `value="high"`, `boost=1.5` — the scorer returns `1.5`.

2. **No-match → neutral:** An item with `metadata["cupel:priority"] = "normal"`, scorer configured with `key="cupel:priority"`, `value="high"`, `boost=1.5` — the scorer returns `1.0`.

3. **Missing key → neutral:** An item with no `cupel:priority` key in its metadata, scorer configured for `key="cupel:priority"` — the scorer returns `1.0` regardless of `boost`.

4. **Zero boost → construction error:** Constructing `MetadataKeyScorer("cupel:priority", "high", 0.0)` MUST fail with `CupelError::ScorerConfig` (Rust) / `ArgumentException` (.NET); the scorer MUST NOT be usable.

5. **Negative boost → construction error:** Constructing `MetadataKeyScorer("cupel:priority", "high", -1.0)` MUST fail with `CupelError::ScorerConfig` (Rust) / `ArgumentException` (.NET); the scorer MUST NOT be usable.

## Complexity

- **Time:** O(1) per item (hash map lookup, string comparison).
- **Space:** O(1) auxiliary.

## Conformance Notes

- Value comparison is string-to-string. The `config.value` is compared directly to the raw string stored at `config.key` in `item.metadata`. No parsing, normalization, or type coercion is applied.
- In .NET, `metadata` values stored as non-string types SHOULD be converted to string for comparison. The comparison target is always `config.value`, which is a string.
- `config.defaultMultiplier` is always `1.0` and is NOT a constructor parameter. It is a fixed constant of the scorer, not a configurable default.
- `boost` validation occurs at construction time. Scoring-time inputs (key absence, value mismatch) never produce errors — they return `1.0`.
- The construction error type is `ArgumentException` in .NET — NOT `ArgumentOutOfRangeException` (D178).
