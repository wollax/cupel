# OpenTelemetry Integration

The `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package bridges the core pipeline's `ITraceCollector`/`TraceCollector` abstraction to .NET `ActivitySource`, emitting structured telemetry that traces context-window construction.

## Overview

`Wollax.Cupel.Diagnostics.OpenTelemetry` is a **separate NuGet assembly**. The core `Wollax.Cupel` assembly has zero dependency on OpenTelemetry (R032/D039). Callers who do not need telemetry incur no transitive dependency on the OpenTelemetry SDK.

The companion registers a single `ActivitySource` with the canonical name `"Wollax.Cupel"`. To receive traces, callers add this name in their OpenTelemetry configuration:

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Wollax.Cupel")
        // ... exporters ...
    );
```

## Pre-Stability Disclaimer

> **Warning:** All `cupel.*` attribute names documented in this chapter are **pre-stable**. The OpenTelemetry LLM semantic conventions are under active development, and `cupel.*` names may be renamed, merged, or removed as the conventions stabilize.
>
> Do not build alert rules, dashboards, or automation that hard-code specific `cupel.*` attribute names. Use the attribute names only for ad-hoc debugging and observability during development and staging.

## Activity Hierarchy

A single pipeline run produces a tree of Activities:

```
cupel.pipeline                            ← root; spans the full pipeline run
├── cupel.stage.classify
├── cupel.stage.score
├── cupel.stage.deduplicate
├── cupel.stage.slice
└── cupel.stage.place
```

There are exactly **five** stage Activities, one per diagnostic stage. Stage names in `cupel.stage.{name}` attribute values use lowercase only, matching the `PipelineStage` enum value lowercasing.

**Sort is omitted.** Sort is an internal ordering step with no diagnostic boundary and no meaningful duration to report independently. It does not receive its own Activity, consistent with the `PipelineStage` enum omission documented in [Events](../diagnostics/events.md).

## Verbosity Tiers

The `CupelVerbosity` enum controls how much diagnostic data is emitted per pipeline run. Choose the tier appropriate for the target environment.

### `StageOnly` — Production-safe

Emits the root Activity and five stage Activities with item-count boundaries only. No per-item events are recorded.

**Root Activity (`cupel.pipeline`) attributes:**

| Attribute | Type | Description |
|---|---|---|
| `cupel.budget.max_tokens` | `int` | The pipeline's maximum token budget |
| `cupel.verbosity` | `string` | The verbosity tier name (`"StageOnly"`) |

**Each stage Activity (`cupel.stage.{name}`) attributes:**

| Attribute | Type | Description |
|---|---|---|
| `cupel.stage.name` | `string` | Stage name in lowercase (e.g., `"classify"`, `"score"`) |
| `cupel.stage.item_count_in` | `int` | Number of items entering this stage |
| `cupel.stage.item_count_out` | `int` | Number of items leaving this stage |

> **Note:** No `duration_ms` attribute is present. Stage duration is provided by the Activity's own start and end timestamps; recording it as an attribute would duplicate information already available in the trace span.

### `StageAndExclusions` — Staging environments

Emits all `StageOnly` attributes plus per-exclusion Events on each stage Activity where an exclusion occurred.

**Additions over `StageOnly`:**

On each stage Activity, for every item excluded during that stage, one `cupel.exclusion` Event is recorded:

| Event Attribute | Type | Description |
|---|---|---|
| `cupel.exclusion.reason` | `string` | `ExclusionReason` variant name (e.g., `"BudgetExceeded"`) |
| `cupel.exclusion.item_kind` | `string` | The kind of the excluded item |
| `cupel.exclusion.item_tokens` | `int` | Token count of the excluded item |

Additionally, a summary attribute is added to the stage Activity:

| Attribute | Type | Description |
|---|---|---|
| `cupel.exclusion.count` | `int` | Total number of exclusions in this stage |

> **Note:** `cupel.exclusion.reason` values are **open-ended**. New `ExclusionReason` variants may appear in future spec versions without a schema change. Trace backends and dashboards MUST NOT hard-code the set of expected reason values.

### `Full` — Development only

Emits all `StageAndExclusions` attributes plus per-included-item Events recorded after the Place stage completes.

**Additions over `StageAndExclusions`:**

After the Place stage, for every item included in the final output, one `cupel.item.included` Event is recorded:

| Event Attribute | Type | Description |
|---|---|---|
| `cupel.item.kind` | `string` | The kind of the included item |
| `cupel.item.tokens` | `int` | Token count of the included item |
| `cupel.item.score` | `float64` | The item's final composite score |

> **Note:** No placement-position attribute is recorded. Position is a derived property of ordering and not a stable attribute of the item itself.

## Cardinality

| Verbosity | Events / Pipeline Run | Recommended Environment |
|---|---|---|
| `StageOnly` | ~10 (5 stage Activities + root) | Production |
| `StageAndExclusions` | ~10 + 0–300 (depends on exclusion count) | Staging |
| `Full` | ~10 + 0–1000 (depends on item count) | Development only |

> **Warning:** `Full` verbosity can produce high-cardinality traces with large item sets. Do not enable `Full` in production without a sampling strategy that caps trace volume.

## Attribute Reference

Complete flat table of all `cupel.*` attributes and events for quick lookup.

| Name | Type | Tier(s) | Carrier | Description |
|---|---|---|---|---|
| `cupel.budget.max_tokens` | `int` | All | Root Activity | Pipeline maximum token budget |
| `cupel.verbosity` | `string` | All | Root Activity | Verbosity tier name |
| `cupel.stage.name` | `string` | All | Stage Activity | Stage name, lowercase |
| `cupel.stage.item_count_in` | `int` | All | Stage Activity | Items entering the stage |
| `cupel.stage.item_count_out` | `int` | All | Stage Activity | Items leaving the stage |
| `cupel.exclusion.count` | `int` | `StageAndExclusions`, `Full` | Stage Activity | Total exclusions in the stage |
| `cupel.exclusion.reason` | `string` | `StageAndExclusions`, `Full` | `cupel.exclusion` Event | ExclusionReason variant name |
| `cupel.exclusion.item_kind` | `string` | `StageAndExclusions`, `Full` | `cupel.exclusion` Event | Kind of the excluded item |
| `cupel.exclusion.item_tokens` | `int` | `StageAndExclusions`, `Full` | `cupel.exclusion` Event | Token count of the excluded item |
| `cupel.item.kind` | `string` | `Full` | `cupel.item.included` Event | Kind of the included item |
| `cupel.item.tokens` | `int` | `Full` | `cupel.item.included` Event | Token count of the included item |
| `cupel.item.score` | `float64` | `Full` | `cupel.item.included` Event | Final composite score of the included item |

## Conformance Notes

- **`cupel.exclusion.reason` mapping:** Values MUST be the canonical `ExclusionReason` variant name string (e.g., `"BudgetExceeded"`, `"Deduplicated"`). Implementations MUST NOT use numeric codes, display strings, or locale-specific representations. New `ExclusionReason` variants in future spec versions will appear as new attribute values — trace backends must not hard-code the set.
- **ActivitySource name:** Implementations MUST register the ActivitySource with the name `"Wollax.Cupel"` exactly. Callers configure their OpenTelemetry pipeline using this string; any deviation breaks trace collection silently.
- **Zero-dependency core:** The `Wollax.Cupel` assembly MUST have no compile-time or runtime dependency on any OpenTelemetry SDK assembly. The companion package provides the bridge; the core pipeline operates through `ITraceCollector` only.
- **Stage Activity names:** Stage Activity names MUST follow the pattern `cupel.stage.{name}` where `{name}` is the lowercase stage name. The five canonical values are `classify`, `score`, `deduplicate`, `slice`, `place`.
