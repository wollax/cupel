# Events

## Overview

Two structural event types carry diagnostic information out of the pipeline: `TraceEvent` (produced at every instrumentation point) and `OverflowEvent` (produced only when budget overflow occurs under the `Proceed` strategy). Each pipeline stage fires exactly one stage-level `TraceEvent` after it completes.

## TraceEvent

`TraceEvent` is the universal record produced at each pipeline instrumentation point. Stage-level events capture timing and throughput for a complete stage; item-level events capture decisions for individual items.

### Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `stage` | PipelineStage | Yes | — | The pipeline stage that produced this event. |
| `duration_ms` | float64 | Yes | — | Wall-clock duration of the stage in milliseconds. For item-level events, this is `0.0`. |
| `item_count` | integer | Yes | — | Number of items processed by this stage. For item-level events, this is `1`. |
| `message` | string | No | *(absent)* | Optional diagnostic message. Absent when not provided. |

**Rationale for `duration_ms` as float64:** Milliseconds provide human-readable precision for diagnostic output while remaining portable across all languages. Float64 accommodates sub-millisecond precision without integer overflow concerns and maps cleanly to IEEE 754 double in all target languages.

**Rejected alternative — integer nanoseconds:** Less readable in diagnostic output; requires unit conversion in most display contexts.

**Rejected alternative — ISO 8601 duration strings:** Harder to aggregate and compare programmatically; parsing overhead at query time.

### Stage-Level Event Example

```json
{
  "stage": "Score",
  "duration_ms": 12.4,
  "item_count": 47
}
```

The `message` field is absent because it was not provided. Absent fields are omitted from the JSON representation.

### Item-Level Event Example

```json
{
  "stage": "Slice",
  "duration_ms": 0.0,
  "item_count": 1,
  "message": "BudgetExceeded: 2048 tokens requested, 512 available"
}
```

Item-level events set `duration_ms` to `0.0` because item processing time is not independently measurable from the stage as a whole.

## PipelineStage

`PipelineStage` enumerates the observable stages of the pipeline. Each value corresponds to a stage that emits diagnostic events.

| Value | Description |
|-------|-------------|
| `"Classify"` | Classification and partitioning stage |
| `"Score"` | Scoring stage |
| `"Deduplicate"` | Deduplication stage |
| `"Slice"` | Budget-fitting selection stage |
| `"Place"` | Final ordering and merge stage |

**Sort is omitted.** Sort is an internal ordering step with no user-visible diagnostic boundary and no meaningful duration to report independently. Including it would add a stage that built-in pipelines emit no meaningful diagnostics for, increasing conformance burden without diagnostic value.

**Rejected alternative:** Including `Sort` for completeness — built-in implementations have nothing useful to report for Sort, and requiring a `TraceEvent` for it would produce misleading or empty records.

Implementations may emit events for internal stages not listed here. Callers should handle unknown `PipelineStage` values gracefully when processing diagnostic data (see Conformance Notes below).

## OverflowEvent

`OverflowEvent` is produced when the merged result (pinned + sliced items) exceeds `target_tokens` and `OverflowStrategy` is `Proceed`. See [OverflowStrategy](../data-model/enumerations.md#overflowstrategy).

`OverflowEvent` is a data type only. The spec defines its structure but not its delivery mechanism — implementations choose how to surface the event (callback, return value, collector method, etc.).

**Rationale:** Delivery mechanisms differ across languages and concurrency models. The spec prescribes what information is captured, not how it reaches the caller.

### Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tokens_over_budget` | integer | Yes | — | Number of tokens exceeding the target budget. |
| `overflowing_items` | list of ContextItem | Yes | — | All items present at the time of overflow (pinned + sliced combined). |
| `budget` | ContextBudget | Yes | — | The budget that was exceeded. |

### Example

```json
{
  "tokens_over_budget": 384,
  "overflowing_items": [
    { "content": "...", "tokens": 512, "kind": "message" },
    { "content": "...", "tokens": 1024, "kind": "tool_output" }
  ],
  "budget": {
    "max_tokens": 4096,
    "target_tokens": 3072
  }
}
```

## Conformance Notes

- Stage-level `TraceEvent` records MUST have `duration_ms` set to the wall-clock duration of that stage. Item-level events MUST have `duration_ms` set to `0.0`.
- Each named pipeline stage (`Classify`, `Score`, `Deduplicate`, `Slice`, `Place`) MUST produce exactly one stage-level `TraceEvent` per pipeline run, emitted after the stage completes.
- `OverflowEvent` MUST only be produced when `OverflowStrategy` is `Proceed`. It MUST NOT be produced for `Throw`, `Truncate`, or any other strategy.
- Implementations MUST handle unknown `PipelineStage` values gracefully when deserializing diagnostic data from other implementations.
