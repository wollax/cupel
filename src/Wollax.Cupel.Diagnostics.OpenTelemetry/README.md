# Wollax.Cupel.Diagnostics.OpenTelemetry

OpenTelemetry bridge for [Wollax.Cupel](https://github.com/wollax/cupel). Emits pipeline execution as `System.Diagnostics.Activity` spans and events via the canonical `Wollax.Cupel` `ActivitySource`.

## Quick Start

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;
using Wollax.Cupel.Diagnostics.OpenTelemetry;

// Register the Cupel source with your tracer provider
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddCupelInstrumentation()  // registers ActivitySource("Wollax.Cupel")
    .AddConsoleExporter()
    .Build();

// Use the OTel collector when running pipelines
var collector = new CupelOpenTelemetryTraceCollector(
    CupelOpenTelemetryVerbosity.StageAndExclusions);

var result = pipeline.Execute(items, collector);
```

## ActivitySource Name

The canonical source name is `Wollax.Cupel`. Use `AddCupelInstrumentation()` to register it, or call `AddSource("Wollax.Cupel")` manually.

## Trace Hierarchy

Each pipeline execution emits:

| Activity | Operation Name | Attributes |
|----------|---------------|------------|
| Root | `cupel.pipeline` | `cupel.budget.max_tokens`, `cupel.budget.target_tokens`, `cupel.verbosity`, `cupel.total_candidates`, `cupel.included_count`, `cupel.excluded_count` |
| Stage (×5) | `cupel.stage.{name}` | `cupel.stage.name`, `cupel.stage.item_count_in`, `cupel.stage.item_count_out`, `cupel.stage.duration_ms` |

Stage names: `classify`, `score`, `deduplicate`, `slice`, `place`.

## Verbosity Tiers

| Tier | Events Emitted | Use Case |
|------|---------------|----------|
| `StageOnly` (default) | None | Production — low overhead |
| `StageAndExclusions` | `cupel.exclusion` per excluded item | Production — moderate cardinality |
| `Full` | `cupel.exclusion` + `cupel.item.included` | Debugging only |

### Event Attributes

**`cupel.exclusion`** (on the stage that excluded the item):
- `cupel.exclusion.reason` — exclusion reason (e.g. `BudgetExceeded`, `Deduplicated`)
- `cupel.exclusion.item_kind` — the item's `ContextKind`
- `cupel.exclusion.item_tokens` — the item's token count

**`cupel.item.included`** (on the `place` stage):
- `cupel.item.kind` — the item's `ContextKind`
- `cupel.item.tokens` — the item's token count
- `cupel.item.score` — the item's computed score
- `cupel.item.reason` — inclusion reason (e.g. `Scored`, `Pinned`)

## ⚠️ Cardinality Warning

The `Full` verbosity tier emits one event per included item. In pipelines processing hundreds or thousands of items, this produces high-cardinality traces that can significantly increase storage costs and degrade trace backend performance.

**Recommendation:** Use `StageOnly` or `StageAndExclusions` in production. Reserve `Full` for local debugging or low-volume diagnostic runs.

## Redaction Policy

This package **never** emits:
- Item content (`ContextItem.Content`)
- Raw metadata values (`ContextItem.Metadata`)

Only structural attributes are traced: kind, tokens, score, reason, stage names, budget values, and verbosity level.
