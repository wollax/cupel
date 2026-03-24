//! [`TraceCollector`] trait and its two built-in implementations.
//!
//! A `TraceCollector` is the recording surface handed to each pipeline stage so
//! it can emit events and item-level decisions without knowing whether the
//! caller cares about diagnostics at all.
//!
//! Two implementations ship out of the box:
//!
//! * [`NullTraceCollector`] — a zero-sized type that compiles away entirely via
//!   Rust monomorphization. Use this when you do not need diagnostics.
//! * [`DiagnosticTraceCollector`] — buffers all events and item records in
//!   memory and converts them into a [`SelectionReport`] via
//!   [`DiagnosticTraceCollector::into_report`].

use super::{
    ExcludedItem, ExclusionReason, IncludedItem, InclusionReason, SelectionReport, TraceEvent,
};
use crate::model::ContextItem;

// ── TraceDetailLevel ──────────────────────────────────────────────────────────

/// Controls how much detail a [`DiagnosticTraceCollector`] records.
///
/// * [`Stage`][TraceDetailLevel::Stage] — records only stage-level
///   [`TraceEvent`]s (timing and counts). Item-level events (individual
///   inclusion/exclusion records) are **not** captured. This is the
///   lower-overhead option when you only need a pipeline timing summary.
///
/// * [`Item`][TraceDetailLevel::Item] — records everything: stage-level
///   [`TraceEvent`]s **and** item-level events
///   ([`record_included`][TraceCollector::record_included],
///   [`record_excluded`][TraceCollector::record_excluded],
///   [`record_item_event`][TraceCollector::record_item_event]). Use this when
///   you need the full "why was each item included or excluded?" picture.
#[non_exhaustive]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub enum TraceDetailLevel {
    /// Record stage-level events only. Item-level events are discarded.
    Stage,
    /// Record both stage-level and item-level events.
    Item,
}

// ── TraceCollector ────────────────────────────────────────────────────────────

/// The recording surface used by each pipeline stage to emit diagnostic events.
///
/// Implementors buffer or discard events as appropriate. The trait is **not**
/// thread-safe; a single collector instance is used by the pipeline on one
/// thread from start to finish.
///
/// Before constructing a diagnostic payload, callers should check
/// [`is_enabled`][TraceCollector::is_enabled]. When `false`, event
/// construction can be skipped entirely, which is the mechanism
/// [`NullTraceCollector`] uses to achieve zero-overhead recording.
pub trait TraceCollector {
    /// Returns `true` if this collector will record any events.
    ///
    /// Callers may guard expensive payload construction behind this check:
    ///
    /// ```rust,ignore
    /// if collector.is_enabled() {
    ///     collector.record_stage_event(build_expensive_event());
    /// }
    /// ```
    ///
    /// [`NullTraceCollector`] always returns `false`.
    /// [`DiagnosticTraceCollector`] always returns `true`.
    fn is_enabled(&self) -> bool;

    /// Records a stage-level event (timing, item count, optional message).
    ///
    /// Stage events are always recorded regardless of
    /// [`TraceDetailLevel`] when the collector is enabled.
    fn record_stage_event(&mut self, event: TraceEvent);

    /// Records an item-level event associated with a specific pipeline stage.
    ///
    /// Item-level events are only recorded when
    /// [`TraceDetailLevel::Item`] is active. Stage-only collectors discard
    /// these events.
    fn record_item_event(&mut self, event: TraceEvent);

    /// Records an item that was selected for the context window.
    ///
    /// **No-op default.** [`DiagnosticTraceCollector`] overrides this to push
    /// an [`IncludedItem`] onto its internal buffer. [`NullTraceCollector`]
    /// relies on this no-op via monomorphization — no allocation occurs.
    fn record_included(&mut self, _item: ContextItem, _score: f64, _reason: InclusionReason) {}

    /// Records an item that was excluded from the context window.
    ///
    /// **No-op default.** [`DiagnosticTraceCollector`] overrides this to push
    /// an [`ExcludedItem`] (plus its insertion index for stable sort) onto its
    /// internal buffer. [`NullTraceCollector`] relies on this no-op via
    /// monomorphization — no allocation occurs.
    fn record_excluded(&mut self, _item: ContextItem, _score: f64, _reason: ExclusionReason) {}

    /// Records the total candidate count and total tokens considered before
    /// slicing.
    ///
    /// **No-op default.** [`DiagnosticTraceCollector`] overrides this to store
    /// the values for later inclusion in the [`SelectionReport`].
    /// [`NullTraceCollector`] relies on this no-op via monomorphization — no
    /// allocation occurs.
    fn set_candidates(&mut self, _total: usize, _total_tokens: i64) {}
}

// ── NullTraceCollector ────────────────────────────────────────────────────────

/// A zero-sized [`TraceCollector`] that discards all events.
///
/// `NullTraceCollector` is a zero-sized type (ZST): it occupies no memory at
/// runtime (`size_of::<NullTraceCollector>() == 0`). When a pipeline is
/// parameterised as `Pipeline<NullTraceCollector>`, the Rust compiler
/// monomorphizes all event-construction branches away during optimization —
/// **no allocations occur** and the diagnostic path vanishes from the generated
/// code entirely. Use this type when you do not need pipeline diagnostics.
#[non_exhaustive]
#[derive(Debug, Clone, Copy, Default)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub struct NullTraceCollector;

impl TraceCollector for NullTraceCollector {
    /// Always returns `false`. Callers can skip event construction entirely.
    #[inline]
    fn is_enabled(&self) -> bool {
        false
    }

    /// No-op. Events are discarded immediately.
    #[inline]
    fn record_stage_event(&mut self, _event: TraceEvent) {}

    /// No-op. Events are discarded immediately.
    #[inline]
    fn record_item_event(&mut self, _event: TraceEvent) {}

    // record_included, record_excluded, and set_candidates use the trait
    // defaults, which are already no-ops.
}

// ── DiagnosticTraceCollector serde helpers ────────────────────────────────────

#[cfg(feature = "serde")]
fn ser_excluded_items<S: serde::Serializer>(
    items: &Vec<(ExcludedItem, usize)>,
    serializer: S,
) -> Result<S::Ok, S::Error> {
    use serde::ser::SerializeSeq;
    let mut seq = serializer.serialize_seq(Some(items.len()))?;
    for (item, _) in items {
        seq.serialize_element(item)?;
    }
    seq.end()
}

#[cfg(feature = "serde")]
fn de_excluded_items<'de, D: serde::Deserializer<'de>>(
    deserializer: D,
) -> Result<Vec<(ExcludedItem, usize)>, D::Error> {
    use serde::Deserialize;
    let items = Vec::<ExcludedItem>::deserialize(deserializer)?;
    Ok(items
        .into_iter()
        .enumerate()
        .map(|(i, item)| (item, i))
        .collect())
}

// ── DiagnosticTraceCollector ──────────────────────────────────────────────────

/// Callback type invoked synchronously after each recorded [`TraceEvent`].
type TraceEventCallback = Box<dyn Fn(&TraceEvent)>;

/// A buffering [`TraceCollector`] that records all events and produces a
/// [`SelectionReport`] on completion.
///
/// One instance should be created per pipeline execution. It is **not**
/// thread-safe — do not share across threads. If the optional callback panics,
/// the panic unwinds normally; no `catch_unwind` is applied.
///
/// Call [`into_report`][DiagnosticTraceCollector::into_report] after the
/// pipeline completes to consume the collector and obtain the final
/// [`SelectionReport`].
///
/// # Serde note
///
/// Serde support is complete as of S04. The `callback` field is always
/// skipped — callbacks cannot be serialized.
#[non_exhaustive]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub struct DiagnosticTraceCollector {
    events: Vec<TraceEvent>,
    included: Vec<IncludedItem>,
    /// Each entry pairs an `ExcludedItem` with its insertion index, which is
    /// used to implement a stable descending-score sort in `into_report`.
    #[cfg_attr(
        feature = "serde",
        serde(
            serialize_with = "ser_excluded_items",
            deserialize_with = "de_excluded_items"
        )
    )]
    excluded: Vec<(ExcludedItem, usize)>,
    total_candidates: usize,
    total_tokens_considered: i64,
    detail_level: TraceDetailLevel,
    #[cfg_attr(feature = "serde", serde(skip))]
    callback: Option<TraceEventCallback>,
}

impl std::fmt::Debug for DiagnosticTraceCollector {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("DiagnosticTraceCollector")
            .field("events", &self.events)
            .field("included", &self.included)
            .field("excluded", &self.excluded)
            .field("total_candidates", &self.total_candidates)
            .field("total_tokens_considered", &self.total_tokens_considered)
            .field("detail_level", &self.detail_level)
            .field("callback", &self.callback.as_ref().map(|_| "<callback>"))
            .finish()
    }
}

impl DiagnosticTraceCollector {
    /// Creates a new collector with the given detail level and no callback.
    pub fn new(detail_level: TraceDetailLevel) -> Self {
        Self {
            events: Vec::new(),
            included: Vec::new(),
            excluded: Vec::new(),
            total_candidates: 0,
            total_tokens_considered: 0,
            detail_level,
            callback: None,
        }
    }

    /// Creates a new collector with the given detail level and an event
    /// callback.
    ///
    /// The callback is invoked synchronously after each call to
    /// [`record_stage_event`][TraceCollector::record_stage_event] and
    /// [`record_item_event`][TraceCollector::record_item_event] (when
    /// detail-level gating permits). If the callback panics, the panic unwinds
    /// normally.
    pub fn with_callback(detail_level: TraceDetailLevel, callback: TraceEventCallback) -> Self {
        Self {
            events: Vec::new(),
            included: Vec::new(),
            excluded: Vec::new(),
            total_candidates: 0,
            total_tokens_considered: 0,
            detail_level,
            callback: Some(callback),
        }
    }

    /// Consumes the collector and returns a [`SelectionReport`].
    ///
    /// The `excluded` list is sorted by score descending, stable by insertion
    /// order on ties (i.e., items with equal scores appear in the order they
    /// were recorded). [`f64::total_cmp`] is used so `NaN` values sort
    /// deterministically rather than causing undefined ordering.
    pub fn into_report(mut self) -> SelectionReport {
        // Sort excluded by score descending, stable by insertion index on ties.
        self.excluded
            .sort_by(|(a, ai), (b, bi)| b.score.total_cmp(&a.score).then_with(|| ai.cmp(bi)));
        let excluded: Vec<ExcludedItem> = self.excluded.into_iter().map(|(item, _)| item).collect();

        SelectionReport {
            events: self.events,
            included: self.included,
            excluded,
            total_candidates: self.total_candidates,
            total_tokens_considered: self.total_tokens_considered,
            count_requirement_shortfalls: Vec::new(),
        }
    }

    fn invoke_callback(&self, event: &TraceEvent) {
        if let Some(cb) = &self.callback {
            cb(event);
        }
    }
}

impl TraceCollector for DiagnosticTraceCollector {
    /// Always returns `true`.
    #[inline]
    fn is_enabled(&self) -> bool {
        true
    }

    /// Pushes the event onto the internal buffer and invokes the callback if
    /// one is set.
    fn record_stage_event(&mut self, event: TraceEvent) {
        self.invoke_callback(&event);
        self.events.push(event);
    }

    /// Pushes the event onto the internal buffer if
    /// [`TraceDetailLevel::Item`] is active; otherwise discards it.
    /// Invokes the callback (when active) before discarding.
    fn record_item_event(&mut self, event: TraceEvent) {
        if !matches!(self.detail_level, TraceDetailLevel::Item) {
            return;
        }
        self.invoke_callback(&event);
        self.events.push(event);
    }

    /// Pushes an [`IncludedItem`] onto the internal buffer.
    fn record_included(&mut self, item: ContextItem, score: f64, reason: InclusionReason) {
        self.included.push(IncludedItem {
            item,
            score,
            reason,
        });
    }

    /// Pushes an [`ExcludedItem`] (with its insertion index) onto the internal
    /// buffer. The index is used by [`into_report`][DiagnosticTraceCollector::into_report]
    /// to produce a stable sort.
    fn record_excluded(&mut self, item: ContextItem, score: f64, reason: ExclusionReason) {
        let idx = self.excluded.len();
        self.excluded.push((
            ExcludedItem {
                item,
                score,
                reason,
            },
            idx,
        ));
    }

    /// Stores the total candidate count and total tokens considered.
    fn set_candidates(&mut self, total: usize, total_tokens: i64) {
        self.total_candidates = total;
        self.total_tokens_considered = total_tokens;
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;
    use crate::diagnostics::{ExclusionReason, InclusionReason, PipelineStage, TraceEvent};
    use crate::model::ContextItemBuilder;
    use std::sync::{
        Arc,
        atomic::{AtomicU32, Ordering},
    };

    /// Creates a minimal `ContextItem` for use in tests.
    fn make_item(content: &str, tokens: i64) -> ContextItem {
        ContextItemBuilder::new(content, tokens).build().unwrap()
    }

    /// Creates a minimal `TraceEvent` for use in tests.
    fn make_event(stage: PipelineStage) -> TraceEvent {
        TraceEvent {
            stage,
            duration_ms: 1.0,
            item_count: 0,
            message: None,
        }
    }

    // ── NullTraceCollector ────────────────────────────────────────────────────

    #[test]
    fn null_is_zst() {
        assert_eq!(std::mem::size_of::<NullTraceCollector>(), 0);
    }

    #[test]
    fn null_is_not_enabled() {
        assert!(!NullTraceCollector.is_enabled());
    }

    #[test]
    fn null_record_methods_are_noop() {
        let mut c = NullTraceCollector;
        // None of these should panic — not panicking is the contract.
        let _ = c.is_enabled();
        c.record_stage_event(make_event(PipelineStage::Classify));
        c.record_item_event(make_event(PipelineStage::Score));
        c.record_included(make_item("inc", 5), 1.0, InclusionReason::Scored);
        c.record_excluded(
            make_item("exc", 5),
            0.5,
            ExclusionReason::BudgetExceeded {
                item_tokens: 5,
                available_tokens: 0,
            },
        );
        c.set_candidates(2, 10);
    }

    // ── DiagnosticTraceCollector — is_enabled ─────────────────────────────────

    #[test]
    fn diagnostic_is_enabled() {
        assert!(DiagnosticTraceCollector::new(TraceDetailLevel::Stage).is_enabled());
        assert!(DiagnosticTraceCollector::new(TraceDetailLevel::Item).is_enabled());
    }

    // ── DiagnosticTraceCollector — stage-level gating ─────────────────────────

    #[test]
    fn stage_level_only_records_stage_events() {
        let mut c = DiagnosticTraceCollector::new(TraceDetailLevel::Stage);
        c.record_stage_event(make_event(PipelineStage::Classify));
        c.record_item_event(make_event(PipelineStage::Score));
        let report = c.into_report();
        assert_eq!(report.events.len(), 1);
    }

    #[test]
    fn item_level_records_both() {
        let mut c = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
        c.record_stage_event(make_event(PipelineStage::Classify));
        c.record_item_event(make_event(PipelineStage::Score));
        let report = c.into_report();
        assert_eq!(report.events.len(), 2);
    }

    // ── DiagnosticTraceCollector — callback ───────────────────────────────────

    #[test]
    fn callback_invoked_on_stage_event() {
        let counter = Arc::new(AtomicU32::new(0));
        let counter_clone = Arc::clone(&counter);
        let mut c = DiagnosticTraceCollector::with_callback(
            TraceDetailLevel::Stage,
            Box::new(move |_event| {
                counter_clone.fetch_add(1, Ordering::SeqCst);
            }),
        );
        c.record_stage_event(make_event(PipelineStage::Classify));
        assert_eq!(counter.load(Ordering::SeqCst), 1);
    }

    #[test]
    fn callback_not_invoked_when_item_event_filtered() {
        let counter = Arc::new(AtomicU32::new(0));
        let counter_clone = Arc::clone(&counter);
        // Stage-level collector: item events are filtered before the callback.
        let mut c = DiagnosticTraceCollector::with_callback(
            TraceDetailLevel::Stage,
            Box::new(move |_event| {
                counter_clone.fetch_add(1, Ordering::SeqCst);
            }),
        );
        c.record_item_event(make_event(PipelineStage::Score));
        assert_eq!(counter.load(Ordering::SeqCst), 0);
    }

    // ── DiagnosticTraceCollector — into_report sort contract ──────────────────

    #[test]
    fn into_report_sort_score_desc() {
        let mut c = DiagnosticTraceCollector::new(TraceDetailLevel::Stage);
        c.record_excluded(
            make_item("low", 5),
            2.0,
            ExclusionReason::BudgetExceeded {
                item_tokens: 5,
                available_tokens: 0,
            },
        );
        c.record_excluded(
            make_item("high", 5),
            5.0,
            ExclusionReason::BudgetExceeded {
                item_tokens: 5,
                available_tokens: 0,
            },
        );
        let report = c.into_report();
        assert_eq!(report.excluded.len(), 2);
        assert_eq!(report.excluded[0].score, 5.0);
    }

    #[test]
    fn into_report_sort_stable_on_tie() {
        let mut c = DiagnosticTraceCollector::new(TraceDetailLevel::Stage);
        c.record_excluded(
            make_item("first", 5),
            3.0,
            ExclusionReason::BudgetExceeded {
                item_tokens: 5,
                available_tokens: 0,
            },
        );
        c.record_excluded(
            make_item("second", 5),
            3.0,
            ExclusionReason::BudgetExceeded {
                item_tokens: 5,
                available_tokens: 0,
            },
        );
        let report = c.into_report();
        // Stable sort: equal scores preserve insertion order.
        assert_eq!(report.excluded[0].item.content(), "first");
        assert_eq!(report.excluded[1].item.content(), "second");
    }

    // ── DiagnosticTraceCollector — item-recording populates SelectionReport ───

    #[test]
    fn item_recording_populates_report_fields() {
        let mut c = DiagnosticTraceCollector::new(TraceDetailLevel::Stage);
        c.record_included(make_item("item_a", 30), 4.0, InclusionReason::Scored);
        c.record_excluded(
            make_item("item_b", 100),
            1.0,
            ExclusionReason::BudgetExceeded {
                item_tokens: 100,
                available_tokens: 50,
            },
        );
        c.set_candidates(2, 60);
        let report = c.into_report();
        assert_eq!(report.included.len(), 1);
        assert_eq!(report.excluded.len(), 1);
        assert_eq!(report.total_candidates, 2);
        assert_eq!(report.total_tokens_considered, 60);
    }

    #[test]
    fn into_report_events_in_insertion_order() {
        let mut c = DiagnosticTraceCollector::new(TraceDetailLevel::Stage);
        c.record_stage_event(make_event(PipelineStage::Classify));
        c.record_stage_event(make_event(PipelineStage::Score));
        let report = c.into_report();
        assert_eq!(report.events[0].stage, PipelineStage::Classify);
        assert_eq!(report.events[1].stage, PipelineStage::Score);
    }
}
