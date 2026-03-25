# S02: Implement CupelOtelTraceCollector (all 3 verbosity tiers) — Research

**Date:** 2026-03-24
**Domain:** Rust OpenTelemetry crate API, span hierarchy, in-memory test exporter
**Confidence:** HIGH

## Summary

S02 implements `CupelOtelTraceCollector` in a new `crates/cupel-otel/` crate. The collector overrides `TraceCollector::on_pipeline_completed` (delivered in S01) and emits the `cupel.pipeline` root span + 5 `cupel.stage.*` child spans with correct attributes and events for each verbosity tier.

The canonical reference implementation is `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs`. The Rust port diverges in one important way: the .NET version re-scans `report.Excluded` in `GetExclusionsForStage()`, but the Rust version can use `StageTraceSnapshot.excluded` directly (stage-scoped exclusions already attributed to the correct stage by `run_with_components`). This is cleaner and removes the need for any stage-to-reason mapping in `cupel-otel`.

**Pin to `opentelemetry = "0.27"`** in `[dependencies]`. The latest is 0.31, but 0.27 is the most widely used stable version in the ecosystem. The 0.27 API surface is stable: `global::tracer()`, `Tracer::start()`, `Tracer::start_with_context()`, `Span::set_attribute()`, `Span::add_event()`, `Span::end()`. Integration tests use `opentelemetry_sdk = { version = "0.27", features = ["testing", "trace"] }` in `[dev-dependencies]`.

The `Span` trait is not dyn-compatible in Rust OTel (D164 already captures this). The `on_pipeline_completed` structured-handoff design fully sidesteps it: no live spans are stored on the struct; all spans are created, attributed, and ended inline within `on_pipeline_completed`. No dyn-compatibility is needed.

## Recommendation

Port the .NET `CupelOpenTelemetryTraceCollector` to Rust. Key differences from the .NET implementation:

1. **Use `StageTraceSnapshot.excluded` directly** — no `GetExclusionsForStage` needed; stage-scoped exclusions are already the right scope
2. **Source name is `"cupel"`** (not `"Wollax.Cupel"`) per D163
3. **Verbosity is `CupelVerbosity`** (not `CupelOpenTelemetryVerbosity`) — Rust uses the crate name convention
4. **Integration tests use `serial_test` or process isolation** — `global::set_tracer_provider` is process-global; tests that touch it must run serially
5. **`ExclusionReason` variant names** require a `match` helper function since `Debug` output includes field values, not just the variant name

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Span creation, attribute setting, event emission | `opentelemetry` 0.27 API directly | `global::tracer("cupel")` → `Tracer::start` / `start_with_context` → `Span::set_attribute` / `add_event` |
| In-memory span capture for tests | `opentelemetry_sdk` 0.27 `InMemorySpanExporter` (requires feature `testing`) | Captures all exported spans; `exporter.get_finished_spans()` returns `Vec<SpanData>`; same pattern as .NET in-memory exporter |
| Test serialization (global provider) | `serial_test = "0.9"` in `[dev-dependencies]` | `#[serial]` attribute on each integration test prevents global tracer provider from leaking between tests; avoids `[NotInParallel]` equivalent needing a custom mechanism |
| Crate layout | Follow `crates/cupel-testing/` as template | Same structure: minimal `Cargo.toml`, `src/lib.rs`, `tests/` directory, `cupel = { version = "1.1", path = "../cupel" }` dep |

## Existing Code and Patterns

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — canonical implementation to port; `OnPipelineCompleted` is the entry point; study `EmitExclusionEvents`, `EmitIncludedItemEvents`; the .NET re-scans `report.Excluded` via `GetExclusionsForStage` — Rust uses `snapshot.excluded` directly instead
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryVerbosity.cs` — verbosity enum shape to mirror as `CupelVerbosity`
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — 7 integration tests using `Sdk.CreateTracerProviderBuilder().AddInMemoryExporter().Build()`; use as integration test blueprint; each test covers hierarchy, attributes, and events per tier
- `crates/cupel-testing/Cargo.toml` — companion crate template: `edition = "2024"`, `rust-version = "1.85"`, minimal `include`, `cupel = { version = "1.1", path = "../cupel" }` pattern
- `crates/cupel/tests/on_pipeline_completed.rs` — `SpyCollector` pattern; shows exactly how to implement `TraceCollector` externally
- `crates/cupel/src/diagnostics/mod.rs` — `StageTraceSnapshot`, `ExclusionReason`, `IncludedItem`, `PipelineStage` all exported from `cupel` crate root via `lib.rs`
- `crates/cupel/src/lib.rs` — confirms all types needed are already `pub use`d: `StageTraceSnapshot`, `ExclusionReason`, `IncludedItem`, `PipelineStage`, `SelectionReport`, `TraceCollector`, `ContextBudget`

## Constraints

- `crates/cupel` must have **zero `opentelemetry` in `[dependencies]`** — all OTel deps are in `cupel-otel` only (D161, R032)
- Source name `"cupel"` (not `"Wollax.Cupel"`) per D163
- `ExclusionReason` is `#[non_exhaustive]` — variant name extraction must have a `_ => "Unknown"` arm
- `opentelemetry 0.27`: `Tracer` trait is not dyn-compatible; `global::tracer("cupel")` returns `BoxedTracer` which IS usable (it wraps the non-dyn-safe trait)
- `Span::set_attribute` takes `KeyValue` (not named params); `KeyValue::new(key, value)` where key is `&'static str` and value converts via `Into<Value>` (supports `i64`, `f64`, `String`, `bool`, `&'static str`)
- `Span::add_event` takes `(name: impl Into<Cow<'static, str>>, attributes: Vec<KeyValue>)` — must pass `Vec<KeyValue>`, not a map
- `cargo package --dry-run` will fail if `cupel` dep has path only and no version — always include `version = "1.1"` with path (D135 pattern)
- `PipelineStage` derives `Debug` but has no `Display`; use `format!("{:?}", stage).to_lowercase()` for the stage name string in span names

## Common Pitfalls

- **`global::set_tracer_provider` is process-global** — tests run in parallel will interfere with each other. Use `#[serial]` from `serial_test` crate. Without this, test output is non-deterministic and may contain spans from other tests' runs.
- **`ExclusionReason::Debug` includes field values** — `format!("{:?}", ExclusionReason::BudgetExceeded { ... })` produces `"BudgetExceeded { item_tokens: 100, available_tokens: 50 }"`, not `"BudgetExceeded"`. Need a `match` helper that returns just the variant name string.
- **`Span::end()` must be called explicitly** — unlike .NET's `Activity` (which ends on `Dispose`), Rust `Span` does NOT auto-end on drop in 0.27. Call `.end()` explicitly after setting all attributes and adding all events. Forgetting this means the span never appears in the exporter.
- **`i64` not `usize` or `i32` for token attributes** — `KeyValue::new("key", value as i64)` is required; `usize` and `i32` don't implement `Into<Value>` in opentelemetry 0.27; cast explicitly
- **Span hierarchy requires explicit context threading** — create root span, wrap in `Context::current_with_span(root)`, then create each stage span with `tracer.start_with_context("cupel.stage.classify", &root_cx)`. Without the explicit context, stage spans are root-level (no parent).
- **`InMemorySpanExporter` requires `opentelemetry_sdk` feature `testing`** — `[dev-dependencies] opentelemetry_sdk = { version = "0.27", features = ["testing", "trace"] }` exactly. The feature name is `testing` not `test`.
- **`TracerProvider` needs explicit flush in tests** — call `provider.force_flush()` (returns `Vec<Result>`) before reading from the exporter; otherwise fast-completing spans may not be exported yet. With `SimpleSpanProcessor` (not batch), this is less critical but still good practice.
- **`is_recording()` vs listener check** — .NET uses `Source.HasListeners()` to skip work when no subscriber is attached. In Rust 0.27, the equivalent is `global::tracer_provider()` check or checking `span.is_recording()` after creating it. The simplest approach: always create spans via `global::tracer("cupel")` — if no provider is set, it returns a NoopTracer whose spans are no-ops. No explicit `is_recording` check needed for correctness; noop path is zero-allocation.

## Open Risks

- **API churn between 0.27 and 0.31**: We pin to 0.27 but the opentelemetry Rust crate has had breaking changes across minor versions. The 0.28 and 0.29 releases removed `once_cell` dependency and changed some initialization APIs. Pinning `= "0.27"` (exact minor, Cargo semver) protects against this. Document the pinned version in README.
- **Test isolation via `serial_test`**: Adding `serial_test` as a dev-dependency is an extra crate. Alternative is using `Mutex<()>` locally. `serial_test` is cleaner and widely used in OTel Rust ecosystem.
- **`SdkTracerProvider` type name**: In `opentelemetry_sdk = "0.27"`, the builder is `opentelemetry_sdk::trace::TracerProvider::builder()`. Check the exact import path — it may be `opentelemetry_sdk::trace::SdkTracerProvider` or just `TracerProvider` depending on the feature flags.
- **`add_cupel_instrumentation` helper**: The spec implies an optional `TracerProviderBuilder` extension method equivalent. In Rust, this would be a trait extension on `opentelemetry_sdk::trace::Builder` or a free function. This is low-priority for S02 (verify in planning whether it's needed or is a S03 concern).

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| opentelemetry (Rust) | none | none found |

## Sources

- opentelemetry 0.27 Tracer trait docs: `start`, `start_with_context`, `span_builder`, `in_span` — all confirmed at docs.rs/opentelemetry/0.27.0
- opentelemetry 0.27 Span trait: `set_attribute`, `add_event`, `end`, `is_recording` — all confirmed; `end()` must be explicit
- opentelemetry_sdk 0.27 `InMemorySpanExporter`: `InMemorySpanExporterBuilder::new().build()`, `get_finished_spans()`, `reset()` — confirmed in testing module
- .NET test reference: `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — 7 tests; `RunPipelineAndCapture` helper pattern; integration test structure to mirror
- .NET reference implementation: `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — stage-span creation, attribute names, event names, verbosity gating
- `crates.io` latest versions confirmed: `opentelemetry = "0.31.0"` (newest), `opentelemetry_sdk = "0.31.0"` — pinning to `"0.27"` intentionally for stability

## Appendix: Key API Cheatsheet (opentelemetry 0.27)

```rust
use opentelemetry::{global, trace::{Span, Tracer, TraceContextExt}, Context, KeyValue};

// Get tracer
let tracer = global::tracer("cupel");

// Root span (no parent)
let mut root = tracer.start("cupel.pipeline");
root.set_attribute(KeyValue::new("cupel.budget.max_tokens", budget.max_tokens() as i64));
root.set_attribute(KeyValue::new("cupel.verbosity", "StageOnly"));

// Wrap in context for children to reference
let root_cx = Context::current_with_span(root);

// Stage child span
let mut stage = tracer.start_with_context(format!("cupel.stage.{}", stage_name), &root_cx);
stage.set_attribute(KeyValue::new("cupel.stage.name", stage_name));
stage.set_attribute(KeyValue::new("cupel.stage.item_count_in", snapshot.item_count_in as i64));
stage.set_attribute(KeyValue::new("cupel.stage.item_count_out", snapshot.item_count_out as i64));

// Event
stage.add_event("cupel.exclusion", vec![
    KeyValue::new("cupel.exclusion.reason", exclusion_reason_name(&excluded.reason)),
    KeyValue::new("cupel.exclusion.item_kind", excluded.item.kind().to_string()),
    KeyValue::new("cupel.exclusion.item_tokens", excluded.item.tokens() as i64),
]);

stage.end(); // MUST call explicitly
// root_cx drops here → root span ends

// Variant name extraction (non_exhaustive safe):
fn exclusion_reason_name(r: &ExclusionReason) -> &'static str {
    match r {
        ExclusionReason::BudgetExceeded { .. } => "BudgetExceeded",
        ExclusionReason::Deduplicated { .. } => "Deduplicated",
        ExclusionReason::NegativeTokens { .. } => "NegativeTokens",
        ExclusionReason::PinnedOverride { .. } => "PinnedOverride",
        ExclusionReason::CountCapExceeded { .. } => "CountCapExceeded",
        ExclusionReason::CountRequireCandidatesExhausted { .. } => "CountRequireCandidatesExhausted",
        ExclusionReason::ScoredTooLow { .. } => "ScoredTooLow",
        ExclusionReason::QuotaCapExceeded { .. } => "QuotaCapExceeded",
        ExclusionReason::QuotaRequireDisplaced { .. } => "QuotaRequireDisplaced",
        ExclusionReason::Filtered { .. } => "Filtered",
        _ => "Unknown",
    }
}
```

```toml
# Test setup (dev-dependencies):
opentelemetry_sdk = { version = "0.27", features = ["testing", "trace"] }
serial_test = "0.9"

# InMemorySpanExporter setup in tests:
use opentelemetry_sdk::testing::trace::InMemorySpanExporterBuilder;
use opentelemetry_sdk::trace::TracerProvider as SdkTracerProvider;

let exporter = InMemorySpanExporterBuilder::new().build();
let provider = SdkTracerProvider::builder()
    .with_simple_exporter(exporter.clone())
    .build();
let _prev = global::set_tracer_provider(provider.clone());
```
