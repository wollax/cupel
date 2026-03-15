# Exclusion Reasons

## Overview

Every item considered by the pipeline receives either an inclusion reason or an exclusion reason. These reasons form the explainability core of the diagnostics system — they answer "why was this item included or excluded?" Reasons are attached to items in the [SelectionReport](selection-report.md), where each `IncludedItem` carries an `InclusionReason` and each `ExcludedItem` carries an `ExclusionReason`.

## ExclusionReason

`ExclusionReason` describes why the pipeline did not select an item for the context window. Each variant is **data-carrying**: it includes associated fields that provide context for the exclusion decision. This is a deliberate design choice — data-carrying variants enable callers to programmatically inspect exclusion details without parsing message strings.

*Rejected alternative: fieldless enum variants — simpler wire format, but forces callers to reconstruct context from other diagnostic data, which is fragile and error-prone.*

| Variant | Description | Fields | Emitted by | Status |
|---------|-------------|--------|------------|--------|
| `BudgetExceeded` | Item did not fit within the token budget | `item_tokens`: integer, `available_tokens`: integer | Slice stage, Place stage (Truncate) | Active |
| `ScoredTooLow` | Item scored below the selection threshold | `score`: float64, `threshold`: float64 | — | Reserved |
| `Deduplicated` | Byte-exact content duplicate removed | `deduplicated_against`: string (content identifier) | Deduplicate stage | Active |
| `QuotaCapExceeded` | Kind exceeded its quota cap | `kind`: string, `cap`: integer, `actual`: integer | — | Reserved |
| `QuotaRequireDisplaced` | Displaced to satisfy another kind's quota requirement | `displaced_by_kind`: string | — | Reserved |
| `NegativeTokens` | Item has a negative token count | `tokens`: integer | Classify stage | Active |
| `PinnedOverride` | Displaced by a pinned item during truncation overflow handling | `displaced_by`: string (content identifier) | Place stage (Truncate) | Active |
| `Filtered` | Excluded by a user-defined filter predicate | `filter_name`: string | — | Reserved |

Reserved variants are defined but not emitted by any built-in pipeline stage. They are reserved for future specification versions and custom stage implementations. Implementations must include these variants in their type definitions to ensure forward compatibility.

*Rationale: defining reserved variants now ensures that implementations allocate space in their type system for future extensions, avoiding breaking changes when these variants become active.*

**JSON example — BudgetExceeded:**

```json
{
  "reason": "BudgetExceeded",
  "item_tokens": 2048,
  "available_tokens": 512
}
```

**JSON example — Deduplicated:**

```json
{
  "reason": "Deduplicated",
  "deduplicated_against": "tool_output_abc123"
}
```

**JSON example — NegativeTokens:**

```json
{
  "reason": "NegativeTokens",
  "tokens": -1
}
```

Wire format: the `reason` field is a string discriminator. Variant-specific fields appear as sibling fields alongside `reason`. Fields that belong to other variants are omitted — absent fields are never represented as nulls.

## InclusionReason

`InclusionReason` describes why the pipeline selected an item for the context window. Inclusion reasons are not data-carrying — they carry no additional fields beyond the variant name. The score itself (carried on `IncludedItem`) provides the quantitative detail.

*Rationale: inclusion reasons are simple status indicators. Data-carrying variants would add complexity without diagnostic value.*

| Variant | Description | Emitted by |
|---------|-------------|------------|
| `Scored` | Included based on computed score within budget | Place stage |
| `Pinned` | Bypassed scoring and slicing due to pinned status | Classify stage / Place stage |
| `ZeroToken` | Included at no budget cost (zero-token item) | Place stage |

**JSON example:**

```json
{
  "reason": "Scored"
}
```

## Conformance Notes

- Implementations MUST define all 8 `ExclusionReason` variants, including reserved variants, to ensure forward compatibility.
- Implementations MUST handle unknown `ExclusionReason` variants gracefully when deserializing diagnostic data from other implementations or future specification versions.
- The `reason` field in the JSON wire format MUST be the string name of the variant (e.g., `"BudgetExceeded"`, not an integer code).
- Data-carrying fields MUST be present when the variant is emitted. For example, `BudgetExceeded` MUST include both `item_tokens` and `available_tokens`.
- Reserved variants MUST NOT be emitted by built-in pipeline stages. Custom stage implementations may emit reserved variants.
