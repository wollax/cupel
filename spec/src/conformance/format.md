# Test Vector Format

All conformance test vectors are [TOML](https://toml.io/) files. Each file contains a single test case with inputs, configuration, and expected outputs. This chapter documents the schema for each stage type.

## Common Structure

Every test vector contains a `[test]` table that identifies the test:

```toml
[test]
name = "Human-readable test name"
stage = "scoring"    # one of: scoring, slicing, placing, pipeline
```

The `stage` field determines which additional tables are expected.

## Scoring Vectors

Scoring vectors test individual scorer algorithms in isolation.

### Schema

| Table | Field | Type | Description |
|---|---|---|---|
| `[test]` | `name` | string | Test name |
| `[test]` | `stage` | string | `"scoring"` |
| `[test]` | `scorer` | string | Scorer type: `"recency"`, `"priority"`, `"kind"`, `"tag"`, `"frequency"`, `"reflexive"`, `"composite"`, `"scaled"` |
| `[[items]]` | `content` | string | Item content (used as identifier in assertions) |
| `[[items]]` | `tokens` | integer | Token count |
| `[[items]]` | `timestamp` | datetime (optional) | UTC timestamp |
| `[[items]]` | `priority` | integer (optional) | Numeric priority value |
| `[[items]]` | `kind` | string (optional) | ContextKind value |
| `[[items]]` | `tags` | array of string (optional) | Item tags |
| `[[items]]` | `futureRelevanceHint` | float (optional) | Caller-provided relevance hint |
| `[[expected]]` | `content` | string | Item content to match |
| `[[expected]]` | `score_approx` | float | Expected score (compared with epsilon tolerance) |
| `[tolerance]` | `score_epsilon` | float | Maximum allowed absolute difference between actual and expected score (default: `1e-9`) |

### Scorer-Specific Configuration

Some scorers require additional configuration:

| Table | Field | Type | Applies To |
|---|---|---|---|
| `[config]` | `use_default_weights` | boolean | KindScorer — when `true`, use default weight map |
| `[[config.weights]]` | `kind` | string | KindScorer — custom weight kind |
| `[[config.weights]]` | `weight` | float | KindScorer — custom weight value |
| `[[config.tag_weights]]` | `tag` | string | TagScorer — configured tag name |
| `[[config.tag_weights]]` | `weight` | float | TagScorer — configured tag weight |
| `[[config.scorers]]` | `type` | string | CompositeScorer — child scorer type |
| `[[config.scorers]]` | `weight` | float | CompositeScorer — child weight |
| `[config]` | `inner_scorer` | string | ScaledScorer — inner scorer type |

### Score Comparison

Score assertions use **epsilon tolerance** comparison:

```
abs(actual_score - expected_score) < score_epsilon
```

This addresses floating-point representation differences across languages and platforms. The default epsilon of `1e-9` is sufficient for all algorithms in this specification. Test vectors that require different tolerances specify a custom `[tolerance]` table.

## Slicing Vectors

Slicing vectors test slicer algorithms with pre-scored input.

### Schema

| Table | Field | Type | Description |
|---|---|---|---|
| `[test]` | `name` | string | Test name |
| `[test]` | `stage` | string | `"slicing"` |
| `[test]` | `slicer` | string | Slicer type: `"greedy"`, `"knapsack"`, `"quota"` |
| `[budget]` | `target_tokens` | integer | Token budget for selection |
| `[[scored_items]]` | `content` | string | Item content |
| `[[scored_items]]` | `tokens` | integer | Token count |
| `[[scored_items]]` | `score` | float | Pre-computed relevance score |
| `[[scored_items]]` | `kind` | string (optional) | ContextKind (used by QuotaSlice) |
| `[expected]` | `selected_contents` | array of string | Content values of selected items (**set comparison** — order does not matter) |

### Slicer-Specific Configuration

| Table | Field | Type | Applies To |
|---|---|---|---|
| `[config]` | `bucket_size` | integer | KnapsackSlice — discretization bucket size (default: 100) |
| `[config]` | `inner_slicer` | string | QuotaSlice — inner slicer type |
| `[[config.quotas]]` | `kind` | string | QuotaSlice — ContextKind |
| `[[config.quotas]]` | `require` | float | QuotaSlice — minimum percentage |
| `[[config.quotas]]` | `cap` | float | QuotaSlice — maximum percentage |

### Set Comparison

Slicer output is compared as a **set** — the order of items in `selected_contents` does not matter. An implementation passes if the set of selected item contents exactly matches the expected set. This is because slicers select items but do not determine presentation order (that is the placer's responsibility).

## Placing Vectors

Placing vectors test placer algorithms with pre-scored input.

### Schema

| Table | Field | Type | Description |
|---|---|---|---|
| `[test]` | `name` | string | Test name |
| `[test]` | `stage` | string | `"placing"` |
| `[test]` | `placer` | string | Placer type: `"chronological"`, `"u-shaped"` |
| `[[items]]` | `content` | string | Item content |
| `[[items]]` | `tokens` | integer | Token count |
| `[[items]]` | `score` | float | Pre-computed relevance score |
| `[[items]]` | `timestamp` | datetime (optional) | UTC timestamp (used by ChronologicalPlacer) |
| `[expected]` | `ordered_contents` | array of string | Content values in expected output order (**ordered comparison**) |

### Ordered Comparison

Placer output is compared as an **ordered list** — the position of each item matters. An implementation passes if the output items, in order, match the expected `ordered_contents` exactly.

## Pipeline Vectors

Pipeline vectors test the full 6-stage pipeline end-to-end.

### Schema

| Table | Field | Type | Description |
|---|---|---|---|
| `[test]` | `name` | string | Test name |
| `[test]` | `stage` | string | `"pipeline"` |
| `[budget]` | `max_tokens` | integer | Maximum token capacity |
| `[budget]` | `target_tokens` | integer | Target token budget for selection |
| `[budget]` | `output_reserve` | integer | Tokens reserved for output (default: 0) |
| `[config]` | `slicer` | string | Slicer type |
| `[config]` | `placer` | string | Placer type |
| `[config]` | `deduplication` | boolean | Whether deduplication is enabled |
| `[config]` | `overflow_strategy` | string (optional) | `"throw"`, `"truncate"`, or `"proceed"` (default: `"throw"`) |
| `[[config.scorers]]` | `type` | string | Scorer type |
| `[[config.scorers]]` | `weight` | float | Scorer weight (used for CompositeScorer weighting) |
| `[[items]]` | `content` | string | Item content |
| `[[items]]` | `tokens` | integer | Token count |
| `[[items]]` | `kind` | string (optional) | ContextKind |
| `[[items]]` | `timestamp` | datetime (optional) | UTC timestamp |
| `[[items]]` | `priority` | integer (optional) | Numeric priority |
| `[[items]]` | `tags` | array of string (optional) | Item tags |
| `[[items]]` | `futureRelevanceHint` | float (optional) | Relevance hint |
| `[[items]]` | `pinned` | boolean (optional) | Whether item is pinned (default: false) |
| `[[expected_output]]` | `content` | string | Content values in expected output order (**ordered comparison**) |

### Ordered Comparison

Pipeline output is compared as an **ordered list** — both the selected items and their presentation order must match. An implementation passes if the output items, in order, match the `expected_output` entries exactly.

## Field Types

| Type | TOML Representation | Notes |
|---|---|---|
| string | `"text"` | UTF-8 string |
| integer | `42` | Signed 64-bit integer |
| float | `0.5` | IEEE 754 double-precision |
| boolean | `true` / `false` | |
| datetime | `2024-06-15T12:00:00Z` | RFC 3339, always UTC |
| array of string | `["a", "b"]` | |

## Extensibility

Future versions of the conformance suite may add new fields to existing tables. Implementations SHOULD ignore unknown fields in test vector files rather than raising errors. This enables forward compatibility as the specification evolves.
