//! OpenTelemetry trace collector for cupel selection pipelines.
//!
//! Provides [`CupelOtelTraceCollector`] which implements
//! [`TraceCollector`][cupel::TraceCollector] and emits a `cupel.pipeline` root span
//! with five `cupel.stage.*` child spans per pipeline run.

use cupel::{
    ContextBudget, ExclusionReason, PipelineStage, SelectionReport, StageTraceSnapshot,
    TraceCollector, TraceEvent,
};
use opentelemetry::{
    Context, KeyValue, global,
    trace::{Span, TraceContextExt, Tracer},
};

// ── CupelVerbosity ────────────────────────────────────────────────────────────

/// Controls how much detail [`CupelOtelTraceCollector`] records in OpenTelemetry spans.
///
/// * [`StageOnly`][CupelVerbosity::StageOnly] — root `cupel.pipeline` span + 5
///   `cupel.stage.*` child spans with stage-level attributes. No span events.
///
/// * [`StageAndExclusions`][CupelVerbosity::StageAndExclusions] — all of
///   `StageOnly` plus `cupel.exclusion` events on the stage span where each
///   item was excluded.
///
/// * [`Full`][CupelVerbosity::Full] — all of `StageAndExclusions` plus
///   `cupel.item.included` events on the Place stage span for every included item.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum CupelVerbosity {
    /// Stage-level spans only; no events.
    StageOnly,
    /// Stage spans + exclusion events.
    StageAndExclusions,
    /// Stage spans + exclusion events + included-item events on the Place stage.
    Full,
}

// ── CupelOtelTraceCollector ───────────────────────────────────────────────────

/// Instrumentation source name used for all spans emitted by this collector.
pub const SOURCE_NAME: &str = "cupel";

/// An OpenTelemetry [`TraceCollector`][cupel::TraceCollector] that emits a
/// `cupel.pipeline` root span with five `cupel.stage.*` child spans at the end
/// of each pipeline run.
///
/// Verbosity is controlled by [`CupelVerbosity`]:
/// - [`StageOnly`][CupelVerbosity::StageOnly]: no events
/// - [`StageAndExclusions`][CupelVerbosity::StageAndExclusions]: exclusion events
/// - [`Full`][CupelVerbosity::Full]: exclusion + included-item events
///
/// # Example
///
/// ```rust,no_run
/// use cupel_otel::{CupelOtelTraceCollector, CupelVerbosity};
/// // Set up a global tracer provider, then:
/// let mut collector = CupelOtelTraceCollector::new(CupelVerbosity::StageOnly);
/// // pipeline.run_traced(&items, &budget, &mut collector)?;
/// ```
pub struct CupelOtelTraceCollector {
    verbosity: CupelVerbosity,
}

impl CupelOtelTraceCollector {
    /// Creates a new collector with the given verbosity level.
    pub fn new(verbosity: CupelVerbosity) -> Self {
        Self { verbosity }
    }
}

impl TraceCollector for CupelOtelTraceCollector {
    /// Always returns `true` — this collector is always active.
    fn is_enabled(&self) -> bool {
        true
    }

    /// No-op — OTel spans are emitted in [`on_pipeline_completed`][Self::on_pipeline_completed],
    /// not during stage-level events.
    fn record_stage_event(&mut self, _event: TraceEvent) {}

    /// No-op — OTel spans are emitted in [`on_pipeline_completed`][Self::on_pipeline_completed],
    /// not during item-level events.
    fn record_item_event(&mut self, _event: TraceEvent) {}

    /// Emits OTel spans for the completed pipeline run.
    ///
    /// Creates a root `cupel.pipeline` span and five `cupel.stage.*` child spans,
    /// with attributes and events depending on the configured [`CupelVerbosity`].
    fn on_pipeline_completed(
        &mut self,
        report: &SelectionReport,
        budget: &ContextBudget,
        stage_snapshots: &[StageTraceSnapshot],
    ) {
        if stage_snapshots.is_empty() {
            return;
        }

        let tracer = global::tracer(SOURCE_NAME);

        // Create root span and set required attributes.
        let mut root = tracer.start("cupel.pipeline");
        root.set_attribute(KeyValue::new(
            "cupel.budget.max_tokens",
            budget.max_tokens(),
        ));
        root.set_attribute(KeyValue::new(
            "cupel.verbosity",
            verbosity_name(self.verbosity),
        ));

        // Establish root context for child spans.
        let root_cx = Context::current_with_span(root);

        // Create a child span for each stage.
        for snapshot in stage_snapshots {
            let name = stage_name(snapshot.stage);
            let mut span = tracer.start_with_context(format!("cupel.stage.{name}"), &root_cx);

            // Stage-level attributes (always present).
            span.set_attribute(KeyValue::new("cupel.stage.name", name));
            span.set_attribute(KeyValue::new(
                "cupel.stage.item_count_in",
                snapshot.item_count_in as i64,
            ));
            span.set_attribute(KeyValue::new(
                "cupel.stage.item_count_out",
                snapshot.item_count_out as i64,
            ));

            // Exclusion events (StageAndExclusions+).
            if self.verbosity >= CupelVerbosity::StageAndExclusions && !snapshot.excluded.is_empty()
            {
                span.set_attribute(KeyValue::new(
                    "cupel.exclusion.count",
                    snapshot.excluded.len() as i64,
                ));
                for excluded in &snapshot.excluded {
                    span.add_event(
                        "cupel.exclusion",
                        vec![
                            KeyValue::new(
                                "cupel.exclusion.reason",
                                exclusion_reason_name(&excluded.reason),
                            ),
                            KeyValue::new(
                                "cupel.exclusion.item_kind",
                                excluded.item.kind().to_string(),
                            ),
                            KeyValue::new("cupel.exclusion.item_tokens", excluded.item.tokens()),
                        ],
                    );
                }
            }

            // Included-item events (Full tier, Place stage only).
            if self.verbosity == CupelVerbosity::Full && snapshot.stage == PipelineStage::Place {
                for included in &report.included {
                    span.add_event(
                        "cupel.item.included",
                        vec![
                            KeyValue::new("cupel.item.kind", included.item.kind().to_string()),
                            KeyValue::new("cupel.item.tokens", included.item.tokens()),
                            KeyValue::new("cupel.item.score", included.score),
                        ],
                    );
                }
            }

            span.end();
        }

        // End the root span explicitly (opentelemetry 0.27 requires explicit .end()).
        root_cx.span().end();
    }
}

// ── Private helpers ───────────────────────────────────────────────────────────

/// Maps a [`CupelVerbosity`] variant to a stable string for OTel attributes.
fn verbosity_name(v: CupelVerbosity) -> &'static str {
    match v {
        CupelVerbosity::StageOnly => "StageOnly",
        CupelVerbosity::StageAndExclusions => "StageAndExclusions",
        CupelVerbosity::Full => "Full",
    }
}

/// Maps a [`PipelineStage`] to a stable lowercase string for span names.
fn stage_name(stage: PipelineStage) -> &'static str {
    match stage {
        PipelineStage::Classify => "classify",
        PipelineStage::Score => "score",
        PipelineStage::Deduplicate => "deduplicate",
        PipelineStage::Slice => "slice",
        PipelineStage::Place => "place",
        _ => "unknown",
    }
}

/// Extracts the variant name from an [`ExclusionReason`] as a stable string.
///
/// Uses an exhaustive `match` (with `_ => "Unknown"` for `#[non_exhaustive]`)
/// to avoid fragile `Debug` formatting which includes field values.
pub fn exclusion_reason_name(reason: &ExclusionReason) -> &'static str {
    match reason {
        ExclusionReason::BudgetExceeded { .. } => "BudgetExceeded",
        ExclusionReason::Deduplicated { .. } => "Deduplicated",
        ExclusionReason::NegativeTokens { .. } => "NegativeTokens",
        ExclusionReason::PinnedOverride { .. } => "PinnedOverride",
        ExclusionReason::CountCapExceeded { .. } => "CountCapExceeded",
        ExclusionReason::CountRequireCandidatesExhausted { .. } => {
            "CountRequireCandidatesExhausted"
        }
        ExclusionReason::ScoredTooLow { .. } => "ScoredTooLow",
        ExclusionReason::QuotaCapExceeded { .. } => "QuotaCapExceeded",
        ExclusionReason::QuotaRequireDisplaced { .. } => "QuotaRequireDisplaced",
        ExclusionReason::Filtered { .. } => "Filtered",
        _ => "Unknown",
    }
}
