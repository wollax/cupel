//! Diagnostic types for pipeline observability.
//!
//! Re-exports: [`TraceDetailLevel`], [`TraceCollector`], [`NullTraceCollector`],
//! [`DiagnosticTraceCollector`] from the [`trace_collector`] submodule.
//!
//! These types form the explainability core of the pipeline — they answer
//! "what happened during a run?" and "why was this item included or excluded?".
//! The primary entry point is [`SelectionReport`], which is produced by a
//! `DiagnosticTraceCollector` after a pipeline run completes.

use crate::model::{ContextBudget, ContextItem};

// ── PipelineStage ─────────────────────────────────────────────────────────────

/// A stage in the fixed five-stage pipeline.
///
/// Stages execute in the order listed: `Classify` → `Score` → `Deduplicate` →
/// `Slice` → `Place`. [`TraceEvent`] records use this enum to identify which
/// stage produced the event.
#[non_exhaustive]
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub enum PipelineStage {
    /// Validates items and attaches computed metadata (e.g., rejects negative-token items).
    Classify,
    /// Computes a relevance score for each candidate item.
    Score,
    /// Removes byte-exact duplicate content from the candidate set.
    Deduplicate,
    /// Selects the highest-value subset of items that fits within the token budget.
    Slice,
    /// Orders the selected items into their final context-window positions.
    Place,
}

// ── TraceEvent ────────────────────────────────────────────────────────────────

/// A single timing and count record emitted by one pipeline stage.
///
/// Events are collected in insertion order and surfaced on
/// [`SelectionReport::events`]. Together they provide a stage-by-stage view of
/// how long each stage ran and how many items it processed.
#[non_exhaustive]
#[derive(Debug, Clone)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub struct TraceEvent {
    /// The pipeline stage that emitted this event.
    pub stage: PipelineStage,
    /// Wall-clock duration of the stage in milliseconds.
    pub duration_ms: f64,
    /// Number of items present at the end of this stage.
    pub item_count: usize,
    /// Optional free-text annotation attached by the stage.
    pub message: Option<String>,
}

// ── OverflowEvent ─────────────────────────────────────────────────────────────

/// Emitted when selected items exceed the token budget under the `Proceed` overflow strategy.
///
/// Callers that configure the pipeline with `Proceed` overflow handling receive
/// this event to signal that the context window is over-budget. The event
/// identifies how many tokens are over, which items are responsible, and what
/// the original budget was.
#[non_exhaustive]
#[derive(Debug, Clone)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub struct OverflowEvent {
    /// How many tokens the selection exceeds the budget by (always positive).
    pub tokens_over_budget: i64,
    /// The items that collectively caused the overflow.
    pub overflowing_items: Vec<ContextItem>,
    /// The budget that was exceeded.
    pub budget: ContextBudget,
}

// ── ExclusionReason ───────────────────────────────────────────────────────────

/// Why the pipeline did not select an item for the context window.
///
/// Each variant is data-carrying: its fields provide the context needed to
/// programmatically inspect the exclusion decision without parsing message
/// strings. See the spec in `spec/src/diagnostics/exclusion-reasons.md` for
/// the full rationale.
///
/// **Reserved variants** (`ScoredTooLow`, `QuotaCapExceeded`,
/// `QuotaRequireDisplaced`, `Filtered`) are defined for forward-compatibility
/// with future specification versions. They are not currently emitted by any
/// built-in pipeline stage. Custom stage implementations may emit them.
///
/// # Serialization
///
/// The wire format uses an adjacent-tagged envelope:
/// `{ "reason": "<VariantName>", ...fields }`. A custom serde implementation
/// is planned for S04; this type carries `cfg_attr` stubs only.
// custom serde impl in S04 — adjacent-tagged wire format
#[non_exhaustive]
#[derive(Debug, Clone, PartialEq)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub enum ExclusionReason {
    /// Item did not fit within the remaining token budget.
    ///
    /// Emitted by the Slice stage and by the Place stage under truncation overflow handling.
    BudgetExceeded {
        /// Token cost of the item that did not fit.
        item_tokens: i64,
        /// Tokens remaining in the budget at the time of exclusion.
        available_tokens: i64,
    },

    /// Item scored below the selection threshold.
    ///
    /// **Reserved** — defined for forward-compatibility; not currently emitted
    /// by any built-in pipeline stage.
    ScoredTooLow {
        /// The computed score of the item.
        score: f64,
        /// The minimum score required for inclusion.
        threshold: f64,
    },

    /// Byte-exact content duplicate removed from the candidate set.
    ///
    /// Emitted by the Deduplicate stage.
    Deduplicated {
        /// Content identifier of the item this one duplicated.
        deduplicated_against: String,
    },

    /// Item's kind exceeded its configured quota cap.
    ///
    /// **Reserved** — defined for forward-compatibility; not currently emitted
    /// by any built-in pipeline stage.
    QuotaCapExceeded {
        /// The kind that exceeded its cap.
        kind: String,
        /// The maximum number of items of this kind allowed.
        cap: i64,
        /// The actual number of items of this kind present.
        actual: i64,
    },

    /// Item was displaced to satisfy another kind's quota requirement.
    ///
    /// **Reserved** — defined for forward-compatibility; not currently emitted
    /// by any built-in pipeline stage.
    QuotaRequireDisplaced {
        /// The kind whose quota requirement caused this item to be displaced.
        displaced_by_kind: String,
    },

    /// Item has a negative token count, which is invalid.
    ///
    /// Emitted by the Classify stage.
    NegativeTokens {
        /// The invalid negative token count.
        tokens: i64,
    },

    /// Item was displaced by a pinned item during truncation overflow handling.
    ///
    /// Emitted by the Place stage under truncation overflow handling.
    PinnedOverride {
        /// Content identifier of the pinned item that caused this displacement.
        displaced_by: String,
    },

    /// Item was excluded by a user-defined filter predicate.
    ///
    /// **Reserved** — defined for forward-compatibility; not currently emitted
    /// by any built-in pipeline stage.
    Filtered {
        /// Name of the filter predicate that excluded this item.
        filter_name: String,
    },
}

// ── InclusionReason ───────────────────────────────────────────────────────────

/// Why the pipeline selected an item for the context window.
///
/// Inclusion reasons are fieldless — the quantitative detail is carried by
/// [`IncludedItem::score`]. Together with the score, these variants answer
/// "how did this item get in?" at a glance.
#[non_exhaustive]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub enum InclusionReason {
    /// Included based on its computed relevance score within the token budget.
    Scored,
    /// Bypassed scoring and slicing due to its pinned status.
    Pinned,
    /// Included at no budget cost because its token count is zero.
    ZeroToken,
}

// ── IncludedItem ──────────────────────────────────────────────────────────────

/// A context item that was selected for the context window, with its score and
/// inclusion reason.
///
/// The `included` list on [`SelectionReport`] is in final placed order —
/// the order determined by the Placer, not score order.
#[non_exhaustive]
#[derive(Debug, Clone)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub struct IncludedItem {
    /// The selected context item.
    pub item: ContextItem,
    /// The computed relevance score at time of inclusion. `0.0` for pinned and
    /// zero-token items.
    pub score: f64,
    /// Why this item was included.
    pub reason: InclusionReason,
}

// ── ExcludedItem ──────────────────────────────────────────────────────────────

/// A context item that was not selected for the context window, with its score
/// and exclusion reason.
///
/// Items excluded before the Score stage (e.g., `NegativeTokens` at Classify,
/// `Deduplicated` at Deduplicate) carry a `score` of `0.0`. The `excluded`
/// list on [`SelectionReport`] is sorted by `score` descending, surfacing the
/// highest-value rejected items first.
#[non_exhaustive]
#[derive(Debug, Clone)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub struct ExcludedItem {
    /// The excluded context item.
    pub item: ContextItem,
    /// The computed relevance score at time of exclusion. Pre-scoring exclusions
    /// receive `0.0`.
    pub score: f64,
    /// Why this item was excluded.
    pub reason: ExclusionReason,
}

// ── SelectionReport ───────────────────────────────────────────────────────────

/// The complete diagnostic output from a single pipeline run.
///
/// Produced by a `DiagnosticTraceCollector` after the pipeline completes.
/// The report answers "what happened?" (`events`), "what was selected?"
/// (`included`), and "what was rejected and why?" (`excluded`).
///
/// `excluded` is sorted by score descending, stable by insertion order on
/// ties. This surfaces the highest-value rejected items first, which is the
/// most useful ordering for debugging "why wasn't this included?" questions.
///
/// `total_candidates` equals `included.len() + excluded.len()`.
/// `total_tokens_considered` equals the sum of `tokens` across all items in
/// both `included` and `excluded`.
#[non_exhaustive]
#[derive(Debug, Clone)]
pub struct SelectionReport {
    /// All recorded trace events in insertion (stage) order.
    pub events: Vec<TraceEvent>,
    /// Items selected for the context window, in final placed order.
    pub included: Vec<IncludedItem>,
    /// Items not selected, sorted by score descending (stable by insertion
    /// order on ties).
    pub excluded: Vec<ExcludedItem>,
    /// Total number of items considered by the pipeline.
    /// Equals `included.len() + excluded.len()`.
    pub total_candidates: usize,
    /// Sum of `tokens` across all items in both `included` and `excluded`.
    pub total_tokens_considered: i64,
}

pub mod trace_collector;
pub use trace_collector::{TraceDetailLevel, TraceCollector, NullTraceCollector, DiagnosticTraceCollector};
