# ContextBudget

A ContextBudget defines the token budget constraints that control how much context the pipeline can select. All fields are validated at construction time — no invalid budget can exist at runtime.

## Fields

| Field | Type | Required | Default | Constraints |
|---|---|---|---|---|
| `maxTokens` | integer | Yes | — | >= 0. Hard ceiling: the model's context window size. |
| `targetTokens` | integer | Yes | — | >= 0, <= `maxTokens`. Soft goal: the slicer aims for this token count. |
| `outputReserve` | integer | No | 0 | >= 0, <= `maxTokens`. Tokens reserved for model output generation, subtracted from available budget. |
| `reservedSlots` | map of [ContextKind](enumerations.md#contextkind) to integer | No | empty map | Minimum guaranteed items per kind. Each value >= 0. Used by [QuotaSlice](../slicers/quota.md). |
| `estimationSafetyMarginPercent` | float64 | No | 0.0 | >= 0.0, <= 100.0. Percentage buffer for token estimation error. |

## Validation Rules

A conforming implementation MUST enforce these validation rules at construction time and reject invalid budgets:

1. **`maxTokens >= 0`** — Negative maximum tokens are invalid.
2. **`targetTokens >= 0`** — Negative target tokens are invalid.
3. **`targetTokens <= maxTokens`** — The soft target cannot exceed the hard ceiling.
4. **`outputReserve >= 0`** — Negative output reserve is invalid.
5. **`outputReserve <= maxTokens`** — The output reserve cannot exceed the context window.
6. **`estimationSafetyMarginPercent >= 0.0 AND <= 100.0`** — Must be a valid percentage.
7. **Each value in `reservedSlots` >= 0** — Negative slot reservations are invalid.

## Effective Budget

The pipeline computes an **effective budget** for the slicing stage by subtracting tokens already committed:

```
effectiveMax    = max(0, maxTokens - outputReserve - pinnedTokens)
effectiveTarget = min(max(0, targetTokens - pinnedTokens), effectiveMax)
```

Where `pinnedTokens` is the sum of `tokens` for all [pinned](context-item.md) items. See [Stage 5: Slice](../pipeline/slice.md) for the full computation.

## Semantics

- **`maxTokens`** is the absolute ceiling. The total tokens of all selected items (pinned + sliced) MUST NOT exceed `maxTokens - outputReserve`, except when pinned items alone exceed this value (which is an error reported during classification).

- **`targetTokens`** is the soft goal. The slicer aims to select items whose total tokens are at most `targetTokens`. The pipeline checks for overflow against `targetTokens` after merging pinned and sliced items.

- **`outputReserve`** carves out tokens for the model's response. It reduces the effective budget available for context items.

- **`reservedSlots`** guarantees minimum representation per ContextKind. This is consumed by QuotaSlice; other slicers ignore it.

- **`estimationSafetyMarginPercent`** provides a buffer for callers whose token counts are estimates rather than exact. This field is available for caller use in budget computation but is not directly consumed by the core pipeline stages.
