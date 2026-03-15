# TraceCollector

## Overview

The `TraceCollector` is an observer interface that pipeline implementations call at instrumentation points throughout each stage. It has two built-in implementations: `NullTraceCollector` (zero-cost disabled path) and `DiagnosticTraceCollector` (buffered recording with configurable detail level).

The pipeline calls `is_enabled` before constructing event payloads. When `is_enabled` returns `false`, no events are created and no work is done.

## Contract

| Member | Signature (pseudocode) | Description |
|--------|------------------------|-------------|
| `is_enabled` | `-> boolean` | Whether this collector is actively recording. Callers check before constructing event payloads to avoid unnecessary allocations. |
| `record_stage_event` | `(event: TraceEvent) -> void` | Record a stage-level event. Called once per pipeline stage, after the stage completes. |
| `record_item_event` | `(event: TraceEvent) -> void` | Record an item-level event. May be filtered by the collector's configured detail level. |

## NullTraceCollector

`NullTraceCollector` is the disabled implementation. `is_enabled` returns `false`. Both `record_stage_event` and `record_item_event` are no-ops.

A singleton instance is recommended but not required — callers that need a disabled collector can use any `NullTraceCollector` instance.

**Rationale:** The null collector ensures zero overhead when diagnostics are disabled. Because `is_enabled` returns `false`, no event payloads are allocated and no buffers are touched. The cost of a disabled pipeline run is exactly one boolean check per instrumentation point.

## DiagnosticTraceCollector

`DiagnosticTraceCollector` is the enabled implementation. `is_enabled` returns `true`. Events are buffered in insertion order.

### Construction Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `detail_level` | TraceDetailLevel | No | `Stage` | Controls which events are recorded. |

### Behavior

- `record_stage_event`: always records the event, regardless of `detail_level`.
- `record_item_event`: records the event only when `detail_level` is `Item` or higher. When `detail_level` is `Stage`, item events are silently discarded.

After the pipeline run completes, the caller extracts a `SelectionReport` from the collector's buffered events. See [SelectionReport](selection-report.md).

## TraceDetailLevel

`TraceDetailLevel` controls the granularity of events recorded by `DiagnosticTraceCollector`. Values are ordered: a higher ordinal includes all events from lower ordinals.

| Value | Ordinal | Description |
|-------|---------|-------------|
| `Stage` | 0 | Stage-level events only (durations, item counts per stage) |
| `Item` | 1 | Stage-level events plus per-item events (individual scores, exclusion reasons) |

**Rationale:** Two levels provide a meaningful performance/detail trade-off. Stage-level is sufficient for performance profiling and pipeline health checks. Item-level is needed for "why was this item excluded?" debugging. A coarser distinction avoids the ambiguity of intermediate values.

**Rejected alternative:** A continuous verbosity integer — discrete levels are more meaningful to callers and prevent ambiguous intermediate values where behavior would be undefined.

## Observer Callback

Implementations may support an optional observer callback that fires synchronously on each recorded event. The spec defines this as a capability, not a mechanism — implementations choose how to expose the callback (function pointer, closure, delegate, trait object, etc.).

**Rationale:** Synchronous firing ensures events are observable in real time, enabling streaming diagnostic use cases. The callback mechanism is left to implementations because callback abstractions differ fundamentally across languages and concurrency models. The spec prescribes the observable behavior (synchronous, per-event) without constraining the API surface.

## Conformance Notes

- Implementations MUST provide both a null (disabled) and a diagnostic (enabled) collector.
- The null collector MUST return `false` from `is_enabled` and MUST NOT allocate or buffer when `record_stage_event` or `record_item_event` are called.
- `record_item_event` MUST be gated by the configured `detail_level`. When `detail_level` is `Stage`, item events MUST be discarded without recording.
- Implementations MUST preserve event insertion order in the accumulated event list.
