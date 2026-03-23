# Wollax.Cupel.Diagnostics.OpenTelemetry

OpenTelemetry bridge for the [Cupel](https://github.com/wollax/cupel) context-window construction pipeline. Emits structured `System.Diagnostics.Activity` traces from pipeline runs without taking a NuGet dependency on the OpenTelemetry SDK — the companion uses BCL `ActivitySource` exclusively.

## Pre-Stability Disclaimer

> **Warning:** All `cupel.*` attribute names in this package are **pre-stable**. The OpenTelemetry LLM semantic conventions are under active development, and `cupel.*` names may be renamed, merged, or removed as the conventions stabilize.
>
> Do not build alert rules, dashboards, or automation that hard-code specific `cupel.*` attribute names. Use these attributes only for ad-hoc debugging and observability during development and staging.

## Overview

`Wollax.Cupel.Diagnostics.OpenTelemetry` is a **separate NuGet assembly**. The core `Wollax.Cupel` package has zero dependency on OpenTelemetry — callers who do not need telemetry incur no transitive overhead.

The package registers a single `ActivitySource` with the canonical name `"Wollax.Cupel"`. To receive traces, register the source via the `AddCupelInstrumentation()` extension method:

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddCupelInstrumentation()  // registers "Wollax.Cupel" ActivitySource
        // ... exporters ...
    );
```

This is equivalent to calling `.AddSource(CupelActivitySource.SourceName)` manually, but avoids hard-coding the source name string.

## Usage

```csharp
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics.OpenTelemetry;

// 1. Create the tracer with the desired verbosity tier
var tracer = new CupelOpenTelemetryTraceCollector(CupelVerbosity.StageOnly);

// 2. Use DryRun (not Execute) to obtain a non-null SelectionReport
//    Execute() returns a null report — StageAndExclusions and Full tiers
//    require a non-null report to emit per-item events.
var budget = new ContextBudget(maxTokens: 4096);
var result = await pipeline.DryRun(items, tracer);

// 3. Flush Activities — Complete() creates Activities retroactively
//    from the buffered stage data collected during DryRun.
tracer.Complete(result.Report!, budget);

// 4. Dispose when done with the pipeline session
tracer.Dispose();
```

> **Important:** Always use `DryRun()`, not `Execute()`, when you need per-item events (StageAndExclusions or Full verbosity). `Execute()` returns a null `SelectionReport`; `Complete()` gracefully degrades to StageOnly-level output when the report is null, but no exclusion or inclusion events will be emitted.

## Verbosity Tiers

| Tier | Events / Run | Recommended Environment |
|---|---|---|
| `StageOnly` | ~10 (root + 5 stage Activities) | Production |
| `StageAndExclusions` | ~10 + 0–300 (depends on exclusions) | Staging |
| `Full` | ~10 + 0–1000 (depends on item count) | Development only |

### `StageOnly`

Emits the root `cupel.pipeline` Activity and five stage Activities (`cupel.stage.classify` through `cupel.stage.place`). Records item-count boundaries only — no per-item data.

### `StageAndExclusions`

All `StageOnly` output plus one `cupel.exclusion` Event per excluded item on each stage Activity, and a `cupel.exclusion.count` summary attribute per stage.

### `Full`

All `StageAndExclusions` output plus one `cupel.item.included` Event per included item on the Place stage Activity with `cupel.item.kind`, `cupel.item.tokens`, and `cupel.item.score`.

## Cardinality Warning

> **Warning:** `Full` verbosity can emit up to ~1000 Events per pipeline run with large item sets. **Do not enable `Full` in production** without a sampling strategy that caps trace volume. Use `Full` only in development and local debugging scenarios.

## Registering with `AddSource`

```csharp
// Preferred: use the convenience extension method
.AddCupelInstrumentation()

// Or use the exported constant directly:
.AddSource(CupelActivitySource.SourceName)  // == "Wollax.Cupel"
```

Do not hard-code the string `"Wollax.Cupel"` in production dashboards or alert rules — attribute names are pre-stable and may change.

## Dispose Behaviour

`CupelOpenTelemetryTraceCollector.Dispose()` disposes the underlying `ActivitySource`. Callers who create multiple tracer instances should **not** call `Dispose()` until they are done with all instances, as the `ActivitySource` is shared at the class level.

## Activity Hierarchy

```
cupel.pipeline                    ← root (budget + verbosity attributes)
├── cupel.stage.classify
├── cupel.stage.score
├── cupel.stage.deduplicate
├── cupel.stage.slice
└── cupel.stage.place
```

Stage Activities are named `cupel.stage.{name}` where `{name}` is the lowercase pipeline stage identifier.
