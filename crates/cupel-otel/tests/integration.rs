//! Integration tests for `CupelOtelTraceCollector`.
//!
//! All tests are annotated with `#[serial]` to avoid global tracer provider
//! interference between parallel test runs.
//!
//! **T01 state:** tests compile and run but fail at the assertion level because
//! the stub `CupelOtelTraceCollector::on_pipeline_completed` emits nothing.
//! T02 will implement the span emission and make all tests pass.

use cupel::{
    ChronologicalPlacer, ContextBudget, ContextItemBuilder, GreedySlice, Pipeline, ReflexiveScorer,
};
use cupel_otel::{CupelOtelTraceCollector, CupelVerbosity};
use opentelemetry::global;
use opentelemetry_sdk::export::trace::SpanData;
use opentelemetry_sdk::testing::trace::InMemorySpanExporterBuilder;
use opentelemetry_sdk::trace::TracerProvider as SdkTracerProvider;
use serial_test::serial;
use std::collections::HashMap;

// ── Helper ────────────────────────────────────────────────────────────────────

/// Builds a minimal pipeline, runs it with the given verbosity, and returns
/// the spans captured by the in-memory exporter.
///
/// Uses a generous 1000-token budget so all items fit (no exclusions).
fn run_pipeline_and_capture(verbosity: CupelVerbosity) -> Vec<SpanData> {
    let exporter = InMemorySpanExporterBuilder::new().build();
    let provider = SdkTracerProvider::builder()
        .with_simple_exporter(exporter.clone())
        .build();
    global::set_tracer_provider(provider.clone());

    let pipeline = Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .build()
        .expect("pipeline build should succeed");

    let budget = ContextBudget::new(1000, 900, 0, HashMap::new(), 0.0)
        .expect("budget construction should succeed");

    let items = vec![
        ContextItemBuilder::new("item alpha", 100)
            .build()
            .expect("item build"),
        ContextItemBuilder::new("item beta", 100)
            .build()
            .expect("item build"),
        ContextItemBuilder::new("item gamma", 100)
            .build()
            .expect("item build"),
    ];

    let mut collector = CupelOtelTraceCollector::new(verbosity);
    pipeline
        .run_traced(&items, &budget, &mut collector)
        .expect("pipeline run should succeed");

    let _ = provider.force_flush();
    exporter
        .get_finished_spans()
        .expect("get_finished_spans should succeed")
}

/// Builds a tight-budget pipeline that forces at least one exclusion, runs it
/// with the given verbosity, and returns the captured spans.
///
/// Budget: 60 tokens. Items: 3 × 40 tokens. Items 2 & 3 are excluded.
fn run_pipeline_with_exclusions_and_capture(verbosity: CupelVerbosity) -> Vec<SpanData> {
    let exporter = InMemorySpanExporterBuilder::new().build();
    let provider = SdkTracerProvider::builder()
        .with_simple_exporter(exporter.clone())
        .build();
    global::set_tracer_provider(provider.clone());

    let pipeline = Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .build()
        .expect("pipeline build should succeed");

    // 60-token budget: only 1 item of 40 tokens fits; 2 items are excluded.
    let budget = ContextBudget::new(60, 55, 0, HashMap::new(), 0.0)
        .expect("budget construction should succeed");

    let items = vec![
        ContextItemBuilder::new("item one", 40)
            .build()
            .expect("item build"),
        ContextItemBuilder::new("item two", 40)
            .build()
            .expect("item build"),
        ContextItemBuilder::new("item three", 40)
            .build()
            .expect("item build"),
    ];

    let mut collector = CupelOtelTraceCollector::new(verbosity);
    pipeline
        .run_traced(&items, &budget, &mut collector)
        .expect("pipeline run should succeed");

    let _ = provider.force_flush();
    exporter
        .get_finished_spans()
        .expect("get_finished_spans should succeed")
}

// ── Tests ─────────────────────────────────────────────────────────────────────

/// Every span emitted by the collector must have instrumentation scope name
/// `"cupel"` (matching `SOURCE_NAME`).
///
/// **Fails in T01:** the stub emits 0 spans so the initial "at least 1 span"
/// assertion fires.
#[test]
#[serial]
fn source_name_is_cupel() {
    let spans = run_pipeline_and_capture(CupelVerbosity::StageOnly);

    assert!(
        !spans.is_empty(),
        "expected at least 1 span from the pipeline run, got 0 — stub not yet implemented"
    );

    for span in &spans {
        assert_eq!(
            span.instrumentation_scope.name(),
            "cupel",
            "span '{}' has wrong instrumentation scope name: expected 'cupel', got '{}'",
            span.name,
            span.instrumentation_scope.name()
        );
    }
}

/// A full pipeline run should produce exactly 6 spans: one root `cupel.pipeline`
/// span and five `cupel.stage.*` child spans (classify, score, slice, deduplicate, place).
///
/// **Fails in T01:** the stub emits 0 spans.
#[test]
#[serial]
fn hierarchy_root_and_five_stage_spans() {
    let spans = run_pipeline_and_capture(CupelVerbosity::StageOnly);

    assert_eq!(
        spans.len(),
        6,
        "expected 6 spans (1 root + 5 stage children), got {} — stub not yet implemented",
        spans.len()
    );

    let root_spans: Vec<_> = spans
        .iter()
        .filter(|s| s.name == "cupel.pipeline")
        .collect();
    assert_eq!(
        root_spans.len(),
        1,
        "expected exactly 1 root 'cupel.pipeline' span"
    );

    let stage_spans: Vec<_> = spans
        .iter()
        .filter(|s| s.name.starts_with("cupel.stage."))
        .collect();
    assert_eq!(
        stage_spans.len(),
        5,
        "expected exactly 5 'cupel.stage.*' child spans"
    );

    // All stage spans should have the root span as parent.
    let root_id = root_spans[0].span_context.span_id();
    for stage_span in &stage_spans {
        assert_eq!(
            stage_span.parent_span_id, root_id,
            "stage span '{}' should have root span as parent",
            stage_span.name
        );
    }
}

/// With `StageOnly` verbosity, no span should have any events attached.
///
/// **Fails in T01:** the stub emits 0 spans, so the "6 spans expected"
/// assertion fires before the events check.
#[test]
#[serial]
fn stage_only_no_events() {
    let spans = run_pipeline_and_capture(CupelVerbosity::StageOnly);

    assert_eq!(
        spans.len(),
        6,
        "expected 6 spans (1 root + 5 stage children), got {} — stub not yet implemented",
        spans.len()
    );

    for span in &spans {
        assert!(
            span.events.is_empty(),
            "StageOnly verbosity should emit no events, but span '{}' has {} event(s): {:?}",
            span.name,
            span.events.len(),
            span.events
                .iter()
                .map(|e| e.name.as_ref())
                .collect::<Vec<_>>()
        );
    }
}

/// With `StageAndExclusions` verbosity and a tight-budget pipeline, at least one
/// `cupel.exclusion` event must appear on a stage span.
///
/// **Fails in T01:** the stub emits 0 spans.
#[test]
#[serial]
fn stage_and_exclusions_emits_exclusion_events() {
    let spans = run_pipeline_with_exclusions_and_capture(CupelVerbosity::StageAndExclusions);

    assert!(
        !spans.is_empty(),
        "expected at least 1 span from the pipeline run, got 0 — stub not yet implemented"
    );

    let exclusion_events: Vec<_> = spans
        .iter()
        .flat_map(|s| s.events.iter())
        .filter(|e| e.name == "cupel.exclusion")
        .collect();

    assert!(
        !exclusion_events.is_empty(),
        "expected at least 1 'cupel.exclusion' event on stage spans with StageAndExclusions verbosity"
    );

    // Each exclusion event must carry the required attributes.
    let required_keys = [
        "cupel.exclusion.reason",
        "cupel.exclusion.item_kind",
        "cupel.exclusion.item_tokens",
    ];
    for event in &exclusion_events {
        for key in &required_keys {
            let has_key = event.attributes.iter().any(|kv| kv.key.as_str() == *key);
            assert!(
                has_key,
                "exclusion event is missing required attribute '{}'; got: {:?}",
                key,
                event
                    .attributes
                    .iter()
                    .map(|kv| kv.key.as_str())
                    .collect::<Vec<_>>()
            );
        }
    }
}

/// With `Full` verbosity, `cupel.item.included` events must appear on the
/// `cupel.stage.place` span for every item that was placed.
///
/// **Fails in T01:** the stub emits 0 spans.
#[test]
#[serial]
fn full_emits_included_item_events_on_place() {
    let spans = run_pipeline_and_capture(CupelVerbosity::Full);

    assert!(
        !spans.is_empty(),
        "expected at least 1 span from the pipeline run, got 0 — stub not yet implemented"
    );

    let place_span = spans
        .iter()
        .find(|s| s.name == "cupel.stage.place")
        .expect("expected a 'cupel.stage.place' span");

    let included_events: Vec<_> = place_span
        .events
        .iter()
        .filter(|e| e.name == "cupel.item.included")
        .collect();

    assert!(
        !included_events.is_empty(),
        "expected at least 1 'cupel.item.included' event on 'cupel.stage.place' span with Full verbosity"
    );

    // Each included-item event must carry the required attributes.
    let required_keys = ["cupel.item.kind", "cupel.item.tokens", "cupel.item.score"];
    for event in &included_events {
        for key in &required_keys {
            let has_key = event.attributes.iter().any(|kv| kv.key.as_str() == *key);
            assert!(
                has_key,
                "included-item event is missing required attribute '{}'; got: {:?}",
                key,
                event
                    .attributes
                    .iter()
                    .map(|kv| kv.key.as_str())
                    .collect::<Vec<_>>()
            );
        }
    }
}
