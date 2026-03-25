//! Integration tests for `TraceCollector::on_pipeline_completed`.
//!
//! T01 writes these tests in a **failing** state — the hook is not yet wired
//! into `run_with_components`. T02 will make the integration test pass by
//! calling `on_pipeline_completed` at the end of the pipeline run.

use std::collections::HashMap;

use cupel::{
    ChronologicalPlacer, ContextBudget, ContextItem, ContextItemBuilder, GreedySlice,
    NullTraceCollector, Pipeline, PipelineStage, ReflexiveScorer, SelectionReport,
    StageTraceSnapshot, TraceCollector, TraceEvent,
};

// ── SpyCollector ──────────────────────────────────────────────────────────────

/// A minimal `TraceCollector` that records whether `on_pipeline_completed` was
/// called and captures the stage snapshots it received.
struct SpyCollector {
    /// Number of times `on_pipeline_completed` has been called.
    pub called: u32,
    /// Clones of the stage snapshots received by the last call.
    pub snapshots: Vec<StageTraceSnapshot>,
}

impl SpyCollector {
    fn new() -> Self {
        Self {
            called: 0,
            snapshots: Vec::new(),
        }
    }
}

impl TraceCollector for SpyCollector {
    fn is_enabled(&self) -> bool {
        true
    }

    fn record_stage_event(&mut self, _event: TraceEvent) {}

    fn record_item_event(&mut self, _event: TraceEvent) {}

    fn on_pipeline_completed(
        &mut self,
        _report: &SelectionReport,
        _budget: &ContextBudget,
        stage_snapshots: &[StageTraceSnapshot],
    ) {
        self.called += 1;
        self.snapshots = stage_snapshots.to_vec();
    }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

fn make_pipeline() -> Pipeline {
    Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .build()
        .unwrap()
}

fn make_budget() -> ContextBudget {
    ContextBudget::new(10_000, 8_000, 0, HashMap::new(), 0.0).unwrap()
}

fn make_items() -> Vec<ContextItem> {
    vec![
        ContextItemBuilder::new("item one", 100).build().unwrap(),
        ContextItemBuilder::new("item two", 100).build().unwrap(),
        ContextItemBuilder::new("item three", 100).build().unwrap(),
    ]
}

// ── Integration tests ─────────────────────────────────────────────────────────

/// After `run_traced` completes, `on_pipeline_completed` must be called exactly
/// once with exactly 5 stage snapshots (one per pipeline stage), and the first
/// snapshot's `stage` must be `PipelineStage::Classify`.
///
/// **This test FAILS in T01** because the hook is not yet wired in
/// `run_with_components`. T02 will wire the call and make this test pass.
#[test]
fn on_pipeline_completed_called_once_with_five_snapshots() {
    let pipeline = make_pipeline();
    let budget = make_budget();
    let items = make_items();
    let mut spy = SpyCollector::new();

    pipeline
        .run_traced(&items, &budget, &mut spy)
        .expect("run_traced should succeed");

    assert_eq!(
        spy.called, 1,
        "on_pipeline_completed must be called exactly once"
    );
    assert_eq!(
        spy.snapshots.len(),
        5,
        "must receive exactly 5 stage snapshots (one per pipeline stage)"
    );
    assert_eq!(
        spy.snapshots[0].stage,
        PipelineStage::Classify,
        "first snapshot must be for the Classify stage"
    );
}

/// `run_traced` with `NullTraceCollector` must not panic — the defaulted no-op
/// is confirmed by a zero-cost ZST that does nothing.
#[test]
fn on_pipeline_completed_not_called_for_null_collector() {
    let pipeline = make_pipeline();
    let budget = make_budget();
    let items = make_items();

    // Should not panic; the defaulted no-op does nothing.
    pipeline
        .run_traced(&items, &budget, &mut NullTraceCollector::default())
        .expect("run_traced with NullTraceCollector should succeed");
}
