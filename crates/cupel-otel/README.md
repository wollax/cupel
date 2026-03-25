# cupel-otel

OpenTelemetry trace collector for [`cupel`](https://docs.rs/cupel) selection pipelines.

`cupel-otel` is a companion crate to `cupel` that emits structured OpenTelemetry spans
for every pipeline run. It implements [`TraceCollector`](https://docs.rs/cupel) and
produces a `cupel.pipeline` root span with five `cupel.stage.*` child spans — one per
pipeline stage — each carrying stage-level attributes and, at higher verbosity tiers,
span events for exclusions and included items.

## Quickstart

Add to your crate's dev-dependencies:

```toml
[dev-dependencies]
cupel-otel = { version = "0.1" }
```

Then in your integration tests or application setup:

```rust
use cupel::{ContextBudget, ContextItemBuilder, ContextKind, GreedySlice,
             Pipeline, RecencyScorer, UShapedPlacer};
use cupel_otel::{CupelOtelTraceCollector, CupelVerbosity};
use std::collections::HashMap;

// Configure and install your OpenTelemetry tracer provider before calling the pipeline.
// (provider setup is application-specific and not shown here)

let pipeline = Pipeline::builder()
    .scorer(Box::new(RecencyScorer))
    .slicer(Box::new(GreedySlice))
    .placer(Box::new(UShapedPlacer))
    .build()
    .expect("pipeline build failed");

let items = vec![
    ContextItemBuilder::new("msg", 10)
        .kind(ContextKind::new("Message").unwrap())
        .build()
        .unwrap(),
];

let budget = ContextBudget::new(100, 100, 0, HashMap::new(), 0.0).unwrap();
let mut collector = CupelOtelTraceCollector::new(CupelVerbosity::StageOnly);
pipeline.run_traced(&items, &budget, &mut collector).unwrap();
// Spans are emitted to the globally registered tracer provider on completion.
```

## Verbosity tiers

| Tier | Constant | Recommended for | What is emitted |
|------|----------|-----------------|-----------------|
| Stage-only | `CupelVerbosity::StageOnly` | Production | Root `cupel.pipeline` span + 5 `cupel.stage.*` child spans with stage-level attributes. No span events. |
| Stage + exclusions | `CupelVerbosity::StageAndExclusions` | Staging / QA | All of `StageOnly` plus `cupel.exclusion` events on the stage span where each item was excluded, with reason and token count. |
| Full | `CupelVerbosity::Full` | Development only | All of `StageAndExclusions` plus `cupel.item.included` events on the Place stage span for every included item. High cardinality — do not use in production. |

## Source name

All spans are emitted under the instrumentation source name `"cupel"`. Use this name
when querying traces in your observability backend.

## License

MIT
