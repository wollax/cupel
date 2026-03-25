//! The fixed 6-stage context selection pipeline.
//!
//! [`Pipeline`] executes six stages in order to select and arrange context items
//! that fit within a token budget. Stages cannot be reordered, skipped, or
//! inserted between.
//!
//! # Stage flow
//!
//! 1. **Classify** — Separates pinned items (which bypass scoring/slicing) from
//!    scoreable candidates. Validates that pinned token totals fit the budget.
//! 2. **Score** — Applies the configured [`Scorer`] to every
//!    scoreable item, producing [`ScoredItem`](crate::ScoredItem) records.
//! 3. **Deduplicate** — Removes content-identical items, keeping the
//!    highest-scored copy (optional, enabled by default).
//! 4. **Sort** — Orders scored items by score descending, with stable tiebreaks.
//! 5. **Slice** — Applies the configured [`Slicer`] to select
//!    items that fit within the remaining token budget (after pinned items).
//! 6. **Place** — Merges pinned and sliced items, then applies the configured
//!    [`Placer`] to determine final presentation order.
//!
//! # Example
//!
//! ```
//! # use std::collections::HashMap;
//! use cupel::{
//!     Pipeline, ContextItemBuilder, ContextBudget,
//!     RecencyScorer, GreedySlice, ChronologicalPlacer,
//! };
//! use chrono::Utc;
//!
//! let pipeline = Pipeline::builder()
//!     .scorer(Box::new(RecencyScorer))
//!     .slicer(Box::new(GreedySlice))
//!     .placer(Box::new(ChronologicalPlacer))
//!     .build()?;
//!
//! let items = vec![
//!     ContextItemBuilder::new("recent message", 10)
//!         .timestamp(Utc::now())
//!         .build()?,
//!     ContextItemBuilder::new("huge filler doc", 5000)
//!         .timestamp(Utc::now() - chrono::Duration::hours(1))
//!         .build()?,
//! ];
//! let budget = ContextBudget::new(4096, 200, 0, HashMap::new(), 0.0)?;
//!
//! let result = pipeline.run(&items, &budget)?;
//! assert_eq!(result.len(), 1); // filler excluded — doesn't fit budget
//! assert_eq!(result[0].content(), "recent message");
//! # Ok::<(), cupel::CupelError>(())
//! ```

mod classify;
mod deduplicate;
mod place;
mod score;
mod slice;
mod sort;

use std::sync::Arc;
use std::time::Instant;

use crate::CupelError;
use crate::diagnostics::trace_collector::{
    DiagnosticTraceCollector, TraceCollector, TraceDetailLevel,
};
use crate::diagnostics::{
    ExcludedItem, ExclusionReason, IncludedItem, InclusionReason, PipelineStage, SelectionReport,
    StageTraceSnapshot, TraceEvent,
};
use crate::model::{ContextBudget, ContextItem, OverflowStrategy};
use crate::placer::Placer;
use crate::scorer::Scorer;
use crate::slicer::Slicer;

/// A fixed 6-stage pipeline that selects and orders context items within a token budget.
///
/// Stages execute in order: Classify, Score, Deduplicate, Sort, Slice, Place.
/// Stages cannot be reordered, skipped, or inserted between.
///
/// # Examples
///
/// ```
/// use std::collections::HashMap;
/// use cupel::{
///     Pipeline, ContextItemBuilder, ContextBudget,
///     RecencyScorer, GreedySlice, ChronologicalPlacer,
/// };
/// use chrono::Utc;
///
/// let pipeline = Pipeline::builder()
///     .scorer(Box::new(RecencyScorer))
///     .slicer(Box::new(GreedySlice))
///     .placer(Box::new(ChronologicalPlacer))
///     .build()?;
///
/// let items = vec![
///     ContextItemBuilder::new("user message", 10)
///         .timestamp(Utc::now())
///         .build()?,
/// ];
/// let budget = ContextBudget::new(4096, 3000, 1024, HashMap::new(), 0.0)?;
///
/// let result = pipeline.run(&items, &budget)?;
/// assert_eq!(result.len(), 1);
/// # Ok::<(), cupel::CupelError>(())
/// ```
pub struct Pipeline {
    scorer: Box<dyn Scorer>,
    slicer: Box<dyn Slicer>,
    placer: Box<dyn Placer>,
    deduplication: bool,
    overflow_strategy: OverflowStrategy,
}

impl Pipeline {
    /// Creates a new `PipelineBuilder`.
    pub fn builder() -> PipelineBuilder {
        PipelineBuilder {
            scorer: None,
            slicer: None,
            placer: None,
            deduplication: true,
            overflow_strategy: OverflowStrategy::default(),
        }
    }

    /// Executes the 6-stage pipeline on the given candidate items and budget.
    ///
    /// Stages: Classify -> Score -> Deduplicate -> Sort -> Slice -> Place
    pub fn run(
        &self,
        items: &[ContextItem],
        budget: &ContextBudget,
    ) -> Result<Vec<ContextItem>, CupelError> {
        // Stage 1: Classify
        let (pinned, scoreable, _) = classify::classify(items, budget)?;

        // Stage 2: Score
        let scored = score::score_items(&scoreable, self.scorer.as_ref());

        // Stage 3: Deduplicate
        let (deduped, _) = deduplicate::deduplicate(scored, self.deduplication);

        // Stage 4: Sort
        let sorted = sort::sort_scored(deduped);

        // Stage 5: Slice
        let pinned_tokens: i64 = pinned.iter().map(|i: &ContextItem| i.tokens()).sum();
        let sliced = slice::slice_items(&sorted, budget, pinned_tokens, self.slicer.as_ref())?;

        // Stage 6: Place
        let (result, _) = place::place_items(
            &pinned,
            &sliced,
            &sorted,
            budget,
            self.overflow_strategy,
            self.placer.as_ref(),
        )?;
        Ok(result)
    }

    /// Executes the pipeline and records each stage's timing, item counts, inclusion
    /// and exclusion decisions into `collector`.
    ///
    /// `run_traced` is the primary observability surface. It is equivalent to [`Pipeline::run`]
    /// but emits five [`TraceEvent`]s (one per stage) and per-item
    /// [`record_included`](crate::diagnostics::trace_collector::TraceCollector::record_included) /
    /// [`record_excluded`](crate::diagnostics::trace_collector::TraceCollector::record_excluded)
    /// calls into `collector`.
    ///
    /// Use a [`DiagnosticTraceCollector`] to capture a full [`SelectionReport`], or a
    /// [`NullTraceCollector`](crate::diagnostics::trace_collector::NullTraceCollector)
    /// to run with zero diagnostic overhead (the compiler optimises all instrumentation away).
    ///
    /// See also [`dry_run`](Pipeline::dry_run) for the common case of "run and return a report".
    ///
    /// # Errors
    ///
    /// Returns the same errors as [`Pipeline::run`].
    ///
    /// # Examples
    ///
    /// ```
    /// use std::collections::HashMap;
    /// use cupel::{
    ///     Pipeline, ContextItemBuilder, ContextBudget,
    ///     RecencyScorer, GreedySlice, ChronologicalPlacer,
    /// };
    /// use cupel::diagnostics::trace_collector::{DiagnosticTraceCollector, TraceDetailLevel};
    /// use chrono::Utc;
    ///
    /// let pipeline = Pipeline::builder()
    ///     .scorer(Box::new(RecencyScorer))
    ///     .slicer(Box::new(GreedySlice))
    ///     .placer(Box::new(ChronologicalPlacer))
    ///     .build()?;
    ///
    /// let items = vec![
    ///     ContextItemBuilder::new("hello world", 10)
    ///         .timestamp(Utc::now())
    ///         .build()?,
    /// ];
    /// let budget = ContextBudget::new(4096, 3000, 1024, HashMap::new(), 0.0)?;
    ///
    /// let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
    /// let result = pipeline.run_traced(&items, &budget, &mut collector)?;
    /// let report = collector.into_report();
    ///
    /// assert_eq!(report.included.len(), 1);
    /// assert_eq!(report.excluded.len(), 0);
    /// # Ok::<(), cupel::CupelError>(())
    /// ```
    pub fn run_traced<C: TraceCollector>(
        &self,
        items: &[ContextItem],
        budget: &ContextBudget,
        collector: &mut C,
    ) -> Result<Vec<ContextItem>, CupelError> {
        self.run_with_components(
            items,
            budget,
            self.scorer.as_ref(),
            self.slicer.as_ref(),
            self.placer.as_ref(),
            self.deduplication,
            self.overflow_strategy,
            collector,
        )
    }

    /// Private helper that executes the 6-stage pipeline with injected strategy components.
    ///
    /// This allows `dry_run_with_policy` to override scorer/slicer/placer/flags without
    /// duplicating the stage logic. `run_traced` delegates here using `self.*` fields.
    #[allow(clippy::too_many_arguments)]
    fn run_with_components<C: TraceCollector>(
        &self,
        items: &[ContextItem],
        budget: &ContextBudget,
        scorer: &dyn Scorer,
        slicer: &dyn Slicer,
        placer: &dyn Placer,
        deduplication: bool,
        overflow_strategy: OverflowStrategy,
        collector: &mut C,
    ) -> Result<Vec<ContextItem>, CupelError> {
        let total_tokens_considered: i64 = if collector.is_enabled() {
            items.iter().map(|i| i.tokens()).sum()
        } else {
            0
        };
        collector.set_candidates(items.len(), items.iter().map(|i| i.tokens()).sum());
        let mut stage_snapshots: Vec<StageTraceSnapshot> = if collector.is_enabled() {
            Vec::with_capacity(5)
        } else {
            Vec::new()
        };

        // Stage 1: Classify
        let t = Instant::now();
        let (pinned, scoreable, neg_items) = classify::classify(items, budget)?;
        if collector.is_enabled() {
            for item in &neg_items {
                collector.record_excluded(
                    item.clone(),
                    0.0,
                    ExclusionReason::NegativeTokens {
                        tokens: item.tokens(),
                    },
                );
            }
            let classify_ms = t.elapsed().as_secs_f64() * 1000.0;
            collector.record_stage_event(TraceEvent {
                stage: PipelineStage::Classify,
                duration_ms: classify_ms,
                item_count: pinned.len() + scoreable.len(),
                message: None,
            });
            stage_snapshots.push(StageTraceSnapshot {
                stage: PipelineStage::Classify,
                item_count_in: items.len(),
                item_count_out: pinned.len() + scoreable.len(),
                duration_ms: classify_ms,
                excluded: neg_items
                    .iter()
                    .map(|item| ExcludedItem {
                        item: item.clone(),
                        score: 0.0,
                        reason: ExclusionReason::NegativeTokens {
                            tokens: item.tokens(),
                        },
                    })
                    .collect(),
            });
        }

        // Stage 2: Score
        let t = Instant::now();
        let scored = score::score_items(&scoreable, scorer);
        if collector.is_enabled() {
            let score_ms = t.elapsed().as_secs_f64() * 1000.0;
            collector.record_stage_event(TraceEvent {
                stage: PipelineStage::Score,
                duration_ms: score_ms,
                item_count: scored.len(),
                message: None,
            });
            stage_snapshots.push(StageTraceSnapshot {
                stage: PipelineStage::Score,
                item_count_in: scoreable.len(),
                item_count_out: scored.len(),
                duration_ms: score_ms,
                excluded: vec![],
            });
        }

        // Stage 3: Deduplicate
        let t = Instant::now();
        let scored_len = scored.len();
        let (deduped, ded_excluded) = deduplicate::deduplicate(scored, deduplication);
        if collector.is_enabled() {
            let ded_snapshot_excluded: Vec<ExcludedItem> = ded_excluded
                .iter()
                .map(|si| ExcludedItem {
                    item: si.item.clone(),
                    score: si.score,
                    reason: ExclusionReason::Deduplicated {
                        deduplicated_against: si.item.content().to_owned(),
                    },
                })
                .collect();
            for exc in &ded_snapshot_excluded {
                collector.record_excluded(exc.item.clone(), exc.score, exc.reason.clone());
            }
            let ded_ms = t.elapsed().as_secs_f64() * 1000.0;
            collector.record_stage_event(TraceEvent {
                stage: PipelineStage::Deduplicate,
                duration_ms: ded_ms,
                item_count: deduped.len(),
                message: None,
            });
            stage_snapshots.push(StageTraceSnapshot {
                stage: PipelineStage::Deduplicate,
                item_count_in: scored_len,
                item_count_out: deduped.len(),
                duration_ms: ded_ms,
                excluded: ded_snapshot_excluded,
            });
        }

        // Stage 4: Sort
        let sorted = sort::sort_scored(deduped);

        // Build score lookup for inclusion recording later
        let score_lookup: std::collections::HashMap<&str, f64> = sorted
            .iter()
            .map(|si| (si.item.content(), si.score))
            .collect();

        // Compute effective budget parameters needed for PinnedOverride detection
        let pinned_tokens: i64 = pinned.iter().map(|i: &ContextItem| i.tokens()).sum();
        let effective_budget = slice::compute_effective_budget(budget, pinned_tokens);
        let effective_target = effective_budget.target_tokens();

        // Stage 5: Slice
        let t = Instant::now();
        let sliced = slice::slice_items(&sorted, budget, pinned_tokens, slicer)?;
        if collector.is_enabled() {
            let sliced_total: i64 = sliced.iter().map(|i| i.tokens()).sum();
            let available_tokens = effective_target - sliced_total;

            // Track which sliced items have been "consumed" when matching sorted items
            let mut sliced_count: std::collections::HashMap<&str, usize> =
                std::collections::HashMap::new();
            for item in &sliced {
                *sliced_count.entry(item.content()).or_insert(0) += 1;
            }

            // For CountQuotaSlice: reconstruct per-kind selected counts from the
            // actual slice output (mirrors .NET D141 pattern). Used to classify
            // excluded items as CountCapExceeded instead of BudgetExceeded when the
            // item fits the budget but the kind's cap was reached.
            let count_cap_map = if slicer.is_count_quota() {
                slicer.count_cap_map()
            } else {
                std::collections::HashMap::new()
            };
            let mut selected_kind_counts: std::collections::HashMap<
                &crate::model::ContextKind,
                usize,
            > = std::collections::HashMap::new();
            if !count_cap_map.is_empty() {
                for item in &sliced {
                    *selected_kind_counts.entry(item.kind()).or_insert(0) += 1;
                }
            }

            let mut slice_snapshot_excluded: Vec<ExcludedItem> = Vec::new();
            for si in &sorted {
                let content = si.item.content();
                if let Some(count) = sliced_count.get_mut(content) {
                    if *count > 0 {
                        *count -= 1;
                        continue;
                    }
                }
                // This item was excluded by the slice stage
                let reason = if pinned_tokens > 0
                    && si.item.tokens() > effective_target
                    && si.item.tokens() <= budget.target_tokens() - budget.output_reserve()
                {
                    ExclusionReason::PinnedOverride {
                        displaced_by: pinned
                            .first()
                            .map(|p| p.content().to_owned())
                            .unwrap_or_default(),
                    }
                } else if !count_cap_map.is_empty() {
                    // Check whether this kind's cap is saturated and the item fits budget.
                    // If so, classify as CountCapExceeded rather than BudgetExceeded.
                    let kind = si.item.kind();
                    if let Some(&cap) = count_cap_map.get(kind) {
                        let current = selected_kind_counts.get(kind).copied().unwrap_or(0);
                        if current >= cap && si.item.tokens() <= effective_target {
                            // Kind cap saturated and item fits budget → count-cap exclusion.
                            // Increment the counter so subsequent items of same kind
                            // also carry CountCapExceeded (count reflects cap at time of exclusion).
                            ExclusionReason::CountCapExceeded {
                                kind: kind.as_str().to_owned(),
                                cap,
                                count: current,
                            }
                        } else {
                            ExclusionReason::BudgetExceeded {
                                item_tokens: si.item.tokens(),
                                available_tokens,
                            }
                        }
                    } else {
                        ExclusionReason::BudgetExceeded {
                            item_tokens: si.item.tokens(),
                            available_tokens,
                        }
                    }
                } else {
                    ExclusionReason::BudgetExceeded {
                        item_tokens: si.item.tokens(),
                        available_tokens,
                    }
                };
                let exc = ExcludedItem {
                    item: si.item.clone(),
                    score: si.score,
                    reason,
                };
                collector.record_excluded(exc.item.clone(), exc.score, exc.reason.clone());
                slice_snapshot_excluded.push(exc);
            }

            let slice_ms = t.elapsed().as_secs_f64() * 1000.0;
            collector.record_stage_event(TraceEvent {
                stage: PipelineStage::Slice,
                duration_ms: slice_ms,
                item_count: sliced.len(),
                message: None,
            });
            stage_snapshots.push(StageTraceSnapshot {
                stage: PipelineStage::Slice,
                item_count_in: sorted.len(),
                item_count_out: sliced.len(),
                duration_ms: slice_ms,
                excluded: slice_snapshot_excluded,
            });
        }

        // Stage 6: Place
        let t = Instant::now();
        let (result, truncated) =
            place::place_items(&pinned, &sliced, &sorted, budget, overflow_strategy, placer)?;
        if collector.is_enabled() {
            let mut place_snapshot_excluded: Vec<ExcludedItem> = Vec::new();
            for (item, score) in &truncated {
                let available_tokens =
                    budget.target_tokens() - result.iter().map(|i| i.tokens()).sum::<i64>();
                let exc = ExcludedItem {
                    item: item.clone(),
                    score: *score,
                    reason: ExclusionReason::BudgetExceeded {
                        item_tokens: item.tokens(),
                        available_tokens,
                    },
                };
                collector.record_excluded(exc.item.clone(), exc.score, exc.reason.clone());
                place_snapshot_excluded.push(exc);
            }
            let place_ms = t.elapsed().as_secs_f64() * 1000.0;
            collector.record_stage_event(TraceEvent {
                stage: PipelineStage::Place,
                duration_ms: place_ms,
                item_count: result.len(),
                message: None,
            });
            stage_snapshots.push(StageTraceSnapshot {
                stage: PipelineStage::Place,
                item_count_in: pinned.len() + sliced.len(),
                item_count_out: result.len(),
                duration_ms: place_ms,
                excluded: place_snapshot_excluded,
            });
            for item in &result {
                let (score, reason) = if item.pinned() {
                    (1.0, InclusionReason::Pinned)
                } else if item.tokens() == 0 {
                    (
                        score_lookup.get(item.content()).copied().unwrap_or(0.0),
                        InclusionReason::ZeroToken,
                    )
                } else {
                    (
                        score_lookup.get(item.content()).copied().unwrap_or(0.0),
                        InclusionReason::Scored,
                    )
                };
                collector.record_included(item.clone(), score, reason);
            }

            // Call on_pipeline_completed with synthetic report and snapshots.
            if !stage_snapshots.is_empty() {
                let synthetic_included: Vec<IncludedItem> = result
                    .iter()
                    .map(|item| {
                        let (score, reason) = if item.pinned() {
                            (1.0, InclusionReason::Pinned)
                        } else if item.tokens() == 0 {
                            (
                                score_lookup.get(item.content()).copied().unwrap_or(0.0),
                                InclusionReason::ZeroToken,
                            )
                        } else {
                            (
                                score_lookup.get(item.content()).copied().unwrap_or(0.0),
                                InclusionReason::Scored,
                            )
                        };
                        IncludedItem {
                            item: item.clone(),
                            score,
                            reason,
                        }
                    })
                    .collect();
                let synthetic_excluded: Vec<ExcludedItem> = stage_snapshots
                    .iter()
                    .flat_map(|s| s.excluded.iter().cloned())
                    .collect();
                let total_candidates = synthetic_included.len() + synthetic_excluded.len();
                let synthetic_report = SelectionReport {
                    events: vec![],
                    included: synthetic_included,
                    excluded: synthetic_excluded,
                    total_candidates,
                    total_tokens_considered,
                    count_requirement_shortfalls: vec![],
                };
                collector.on_pipeline_completed(&synthetic_report, budget, &stage_snapshots);
            }
        }

        Ok(result)
    }

    /// Runs the pipeline and returns a full [`SelectionReport`] without side effects.
    ///
    /// Equivalent to calling [`run_traced`](Pipeline::run_traced) with a
    /// [`DiagnosticTraceCollector`] at [`TraceDetailLevel::Item`] and discarding the
    /// `Vec<ContextItem>`. This is the most convenient entry point when you want to
    /// inspect the pipeline's decisions (inclusion/exclusion reasons, stage timings,
    /// per-item scores) without needing the selected items directly.
    ///
    /// # Errors
    ///
    /// Returns the same errors as [`Pipeline::run`].
    ///
    /// # Examples
    ///
    /// ```
    /// use std::collections::HashMap;
    /// use cupel::{
    ///     Pipeline, ContextItemBuilder, ContextBudget,
    ///     RecencyScorer, GreedySlice, ChronologicalPlacer,
    /// };
    /// use chrono::Utc;
    ///
    /// let pipeline = Pipeline::builder()
    ///     .scorer(Box::new(RecencyScorer))
    ///     .slicer(Box::new(GreedySlice))
    ///     .placer(Box::new(ChronologicalPlacer))
    ///     .build()?;
    ///
    /// let items = vec![
    ///     ContextItemBuilder::new("message a", 10)
    ///         .timestamp(Utc::now())
    ///         .build()?,
    ///     ContextItemBuilder::new("message b", 5000)
    ///         .timestamp(Utc::now() - chrono::Duration::hours(1))
    ///         .build()?,
    /// ];
    /// let budget = ContextBudget::new(4096, 200, 0, HashMap::new(), 0.0)?;
    ///
    /// let report = pipeline.dry_run(&items, &budget)?;
    /// assert_eq!(report.total_candidates, 2);
    /// assert_eq!(report.included.len(), 1);
    /// assert_eq!(report.excluded.len(), 1);
    /// # Ok::<(), cupel::CupelError>(())
    /// ```
    pub fn dry_run(
        &self,
        items: &[ContextItem],
        budget: &ContextBudget,
    ) -> Result<SelectionReport, CupelError> {
        let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
        self.run_traced(items, budget, &mut collector)?;
        Ok(collector.into_report())
    }

    /// Identifies items included in a full-budget run but excluded when the budget
    /// is reduced by `slack_tokens`.
    ///
    /// This is useful for understanding which items are "on the margin" — items that
    /// would be dropped if the budget were slightly smaller.
    ///
    /// # Monotonicity guard
    ///
    /// Returns [`CupelError::PipelineConfig`] if the pipeline's slicer is a
    /// [`QuotaSlice`](crate::QuotaSlice), which produces non-monotonic inclusion as
    /// budget changes shift percentage allocations.
    ///
    /// # Short-circuit
    ///
    /// Returns an empty vec immediately if `slack_tokens == 0`.
    ///
    /// # Errors
    ///
    /// - [`CupelError::PipelineConfig`] if the slicer is a `QuotaSlice`.
    /// - Any error propagated from [`dry_run`](Pipeline::dry_run) or [`ContextBudget::new`].
    pub fn get_marginal_items(
        &self,
        items: &[ContextItem],
        budget: &ContextBudget,
        slack_tokens: i32,
    ) -> Result<Vec<ContextItem>, CupelError> {
        if self.slicer.is_quota() {
            return Err(CupelError::PipelineConfig(
                "GetMarginalItems requires monotonic item inclusion. QuotaSlice produces \
                 non-monotonic inclusion as budget changes shift percentage allocations."
                    .to_owned(),
            ));
        }

        if slack_tokens == 0 {
            return Ok(vec![]);
        }

        // Full-budget dry run
        let primary_report = self.dry_run(items, budget)?;

        // Reduced-budget dry run
        let reduced_budget = ContextBudget::new(
            budget.max_tokens() - slack_tokens as i64,
            budget.target_tokens() - slack_tokens as i64,
            budget.output_reserve(),
            std::collections::HashMap::new(),
            0.0,
        )?;
        let margin_report = self.dry_run(items, &reduced_budget)?;

        // Build a set of content strings from the reduced run's included items
        let mut margin_content: std::collections::HashMap<&str, usize> =
            std::collections::HashMap::new();
        for inc in &margin_report.included {
            *margin_content.entry(inc.item.content()).or_insert(0) += 1;
        }

        // Diff: items in primary but not in margin (content-based matching)
        let mut marginal = Vec::new();
        for inc in &primary_report.included {
            let content = inc.item.content();
            if let Some(count) = margin_content.get_mut(content) {
                if *count > 0 {
                    *count -= 1;
                    continue;
                }
            }
            marginal.push(inc.item.clone());
        }

        Ok(marginal)
    }

    /// Finds the minimum token budget (within a search ceiling) at which `target`
    /// would be included in the selection result. Uses binary search over real dry runs.
    ///
    /// Returns `Ok(Some(budget))` if the target is included at that budget, or `Ok(None)`
    /// if the target is not selectable within the ceiling.
    ///
    /// # Monotonicity guard
    ///
    /// Returns [`CupelError::PipelineConfig`] if the pipeline's slicer is a
    /// [`QuotaSlice`](crate::QuotaSlice) or [`CountQuotaSlice`](crate::CountQuotaSlice),
    /// which produce non-monotonic inclusion as budget changes shift allocations.
    ///
    /// # Preconditions
    ///
    /// - `target` must be present in `items` (matched by content).
    /// - `search_ceiling` must be `>= target.tokens()`.
    ///
    /// # Errors
    ///
    /// - [`CupelError::PipelineConfig`] if the slicer is `QuotaSlice` or `CountQuotaSlice`.
    /// - [`CupelError::InvalidBudget`] if `target` is not in `items` or `search_ceiling` is
    ///   below the target's token count.
    pub fn find_min_budget_for(
        &self,
        items: &[ContextItem],
        budget: &ContextBudget,
        target: &ContextItem,
        search_ceiling: i32,
    ) -> Result<Option<i32>, CupelError> {
        if self.slicer.is_quota() || self.slicer.is_count_quota() {
            return Err(CupelError::PipelineConfig(
                "FindMinBudgetFor requires monotonic item inclusion. QuotaSlice and \
                 CountQuotaSlice produce non-monotonic inclusion as budget changes shift \
                 allocations. Use a GreedySlice or KnapsackSlice inner slicer for budget \
                 simulation."
                    .to_owned(),
            ));
        }

        // Precondition: target must be in items (by content)
        let target_found = items.iter().any(|i| i.content() == target.content());
        if !target_found {
            return Err(CupelError::InvalidBudget(
                "target item must be an element of items (matched by content)".to_owned(),
            ));
        }

        if (search_ceiling as i64) < target.tokens() {
            return Err(CupelError::InvalidBudget(format!(
                "search_ceiling ({search_ceiling}) must be >= target.tokens() ({})",
                target.tokens()
            )));
        }

        let _ = budget; // explicit budget param per D069 — not used in binary search

        // Binary search over [target.tokens(), search_ceiling]
        let mut low = target.tokens() as i32;
        let mut high = search_ceiling;

        while high - low > 1 {
            let mid = low + (high - low) / 2;
            let mid_budget = ContextBudget::new(
                mid as i64,
                mid as i64,
                0,
                std::collections::HashMap::new(),
                0.0,
            )?;
            let report = self.dry_run(items, &mid_budget)?;

            if Self::contains_item_by_content(&report, target) {
                high = mid;
            } else {
                low = mid;
            }
        }

        // Check low first, then high (matching .NET behavior)
        let low_budget = ContextBudget::new(
            low as i64,
            low as i64,
            0,
            std::collections::HashMap::new(),
            0.0,
        )?;
        let low_report = self.dry_run(items, &low_budget)?;
        if Self::contains_item_by_content(&low_report, target) {
            return Ok(Some(low));
        }

        let high_budget = ContextBudget::new(
            high as i64,
            high as i64,
            0,
            std::collections::HashMap::new(),
            0.0,
        )?;
        let high_report = self.dry_run(items, &high_budget)?;
        if Self::contains_item_by_content(&high_report, target) {
            return Ok(Some(high));
        }

        Ok(None)
    }

    /// Checks whether the report's included items contain the target by content comparison.
    fn contains_item_by_content(report: &SelectionReport, target: &ContextItem) -> bool {
        report
            .included
            .iter()
            .any(|inc| inc.item.content() == target.content())
    }

    /// Runs the pipeline stages using the strategy components from `policy` instead of
    /// the pipeline's own scorer/slicer/placer/flags, and returns a [`SelectionReport`].
    ///
    /// This is the primary entry point for fork diagnostics: callers build a [`Policy`]
    /// via [`PolicyBuilder`] and call this method to observe how a different combination
    /// of strategies would have selected from the same items and budget, without
    /// constructing a separate full [`Pipeline`].
    ///
    /// # Errors
    ///
    /// Returns the same errors as [`Pipeline::run`]. The error surface is identical to
    /// [`dry_run`](Pipeline::dry_run) — only the strategy components differ.
    pub fn dry_run_with_policy(
        &self,
        items: &[ContextItem],
        budget: &ContextBudget,
        policy: &Policy,
    ) -> Result<SelectionReport, CupelError> {
        let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
        self.run_with_components(
            items,
            budget,
            policy.scorer.as_ref(),
            policy.slicer.as_ref(),
            policy.placer.as_ref(),
            policy.deduplication,
            policy.overflow_strategy,
            &mut collector,
        )?;
        Ok(collector.into_report())
    }
}

impl std::fmt::Debug for Pipeline {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Pipeline")
            .field("deduplication", &self.deduplication)
            .field("overflow_strategy", &self.overflow_strategy)
            .finish()
    }
}

/// Run a [`Policy`] against `items` and `budget` without requiring an existing [`Pipeline`] instance.
///
/// Constructs a minimal dummy pipeline whose own scorer/slicer/placer are fully overridden by the
/// policy, so the dummy's own components don't affect the result. This is a `pub(crate)` helper
/// for [`crate::analytics`] functions that operate on policies directly.
pub(crate) fn run_policy(
    items: &[ContextItem],
    budget: &ContextBudget,
    policy: &Policy,
) -> Result<SelectionReport, CupelError> {
    use crate::placer::ChronologicalPlacer;
    use crate::scorer::ReflexiveScorer;
    use crate::slicer::GreedySlice;
    let dummy = Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .build()
        .expect("dummy pipeline always valid");
    dummy.dry_run_with_policy(items, budget, policy)
}

/// Builder for constructing a [`Pipeline`] with required and optional configuration.
///
/// # Examples
///
/// ```
/// use cupel::{Pipeline, RecencyScorer, GreedySlice, UShapedPlacer, OverflowStrategy};
///
/// let pipeline = Pipeline::builder()
///     .scorer(Box::new(RecencyScorer))
///     .slicer(Box::new(GreedySlice))
///     .placer(Box::new(UShapedPlacer))
///     .deduplication(false)
///     .overflow_strategy(OverflowStrategy::Truncate)
///     .build()?;
/// # Ok::<(), cupel::CupelError>(())
/// ```
pub struct PipelineBuilder {
    scorer: Option<Box<dyn Scorer>>,
    slicer: Option<Box<dyn Slicer>>,
    placer: Option<Box<dyn Placer>>,
    deduplication: bool,
    overflow_strategy: OverflowStrategy,
}

impl std::fmt::Debug for PipelineBuilder {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("PipelineBuilder")
            .field("scorer", &self.scorer.is_some())
            .field("slicer", &self.slicer.is_some())
            .field("placer", &self.placer.is_some())
            .field("deduplication", &self.deduplication)
            .field("overflow_strategy", &self.overflow_strategy)
            .finish()
    }
}

impl PipelineBuilder {
    /// Sets the scorer strategy (required).
    pub fn scorer(mut self, scorer: Box<dyn Scorer>) -> Self {
        self.scorer = Some(scorer);
        self
    }

    /// Sets the slicer strategy (required).
    pub fn slicer(mut self, slicer: Box<dyn Slicer>) -> Self {
        self.slicer = Some(slicer);
        self
    }

    /// Sets the placer strategy (required).
    pub fn placer(mut self, placer: Box<dyn Placer>) -> Self {
        self.placer = Some(placer);
        self
    }

    /// Enables or disables deduplication (default: enabled).
    pub fn deduplication(mut self, enabled: bool) -> Self {
        self.deduplication = enabled;
        self
    }

    /// Sets the overflow strategy (default: `Throw`).
    pub fn overflow_strategy(mut self, strategy: OverflowStrategy) -> Self {
        self.overflow_strategy = strategy;
        self
    }

    /// Builds the pipeline, validating that all required components are set.
    ///
    /// # Errors
    ///
    /// Returns an error if scorer, slicer, or placer is not set.
    pub fn build(self) -> Result<Pipeline, CupelError> {
        let scorer = self
            .scorer
            .ok_or_else(|| CupelError::PipelineConfig("scorer is required".to_owned()))?;
        let slicer = self
            .slicer
            .ok_or_else(|| CupelError::PipelineConfig("slicer is required".to_owned()))?;
        let placer = self
            .placer
            .ok_or_else(|| CupelError::PipelineConfig("placer is required".to_owned()))?;

        Ok(Pipeline {
            scorer,
            slicer,
            placer,
            deduplication: self.deduplication,
            overflow_strategy: self.overflow_strategy,
        })
    }
}

/// A set of strategy components (scorer, slicer, placer, flags) that can be injected
/// into an existing [`Pipeline`] via [`Pipeline::dry_run_with_policy`].
///
/// Use [`PolicyBuilder`] to construct a `Policy`. Fields are `pub(crate)` to allow
/// the pipeline to access components directly without trait method dispatch overhead
/// on the builder side.
///
/// # Examples
///
/// ```
/// use std::sync::Arc;
/// use cupel::{PolicyBuilder, GreedySlice, ChronologicalPlacer, ReflexiveScorer, OverflowStrategy};
///
/// let policy = PolicyBuilder::new()
///     .scorer(Arc::new(ReflexiveScorer))
///     .slicer(Arc::new(GreedySlice))
///     .placer(Arc::new(ChronologicalPlacer))
///     .overflow_strategy(OverflowStrategy::Truncate)
///     .build()?;
/// # Ok::<(), cupel::CupelError>(())
/// ```
pub struct Policy {
    pub(crate) scorer: Arc<dyn Scorer>,
    pub(crate) slicer: Arc<dyn Slicer>,
    pub(crate) placer: Arc<dyn Placer>,
    pub(crate) deduplication: bool,
    pub(crate) overflow_strategy: OverflowStrategy,
}

impl std::fmt::Debug for Policy {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Policy")
            .field("scorer", &"<dyn Scorer>")
            .field("slicer", &"<dyn Slicer>")
            .field("placer", &"<dyn Placer>")
            .field("deduplication", &self.deduplication)
            .field("overflow_strategy", &self.overflow_strategy)
            .finish()
    }
}

/// Builder for constructing a [`Policy`] with required and optional configuration.
///
/// Mirrors [`PipelineBuilder`] but accepts `Arc<dyn Trait>` instead of `Box<dyn Trait>`
/// so that strategy components can be shared across multiple policies without cloning.
///
/// # Examples
///
/// ```
/// use std::sync::Arc;
/// use cupel::{PolicyBuilder, PriorityScorer, KnapsackSlice, ChronologicalPlacer};
///
/// let policy = PolicyBuilder::new()
///     .scorer(Arc::new(PriorityScorer))
///     .slicer(Arc::new(KnapsackSlice::with_default_bucket_size()))
///     .placer(Arc::new(ChronologicalPlacer))
///     .build()?;
/// # Ok::<(), cupel::CupelError>(())
/// ```
pub struct PolicyBuilder {
    scorer: Option<Arc<dyn Scorer>>,
    slicer: Option<Arc<dyn Slicer>>,
    placer: Option<Arc<dyn Placer>>,
    deduplication: bool,
    overflow_strategy: OverflowStrategy,
}

impl std::fmt::Debug for PolicyBuilder {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("PolicyBuilder")
            .field("scorer", &self.scorer.is_some())
            .field("slicer", &self.slicer.is_some())
            .field("placer", &self.placer.is_some())
            .field("deduplication", &self.deduplication)
            .field("overflow_strategy", &self.overflow_strategy)
            .finish()
    }
}

impl PolicyBuilder {
    /// Creates a new `PolicyBuilder` with defaults matching [`PipelineBuilder`]:
    /// deduplication enabled, overflow strategy set to the default ([`OverflowStrategy::default()`]).
    pub fn new() -> Self {
        Self {
            scorer: None,
            slicer: None,
            placer: None,
            deduplication: true,
            overflow_strategy: OverflowStrategy::default(),
        }
    }

    /// Sets the scorer strategy (required).
    pub fn scorer(mut self, scorer: Arc<dyn Scorer>) -> Self {
        self.scorer = Some(scorer);
        self
    }

    /// Sets the slicer strategy (required).
    pub fn slicer(mut self, slicer: Arc<dyn Slicer>) -> Self {
        self.slicer = Some(slicer);
        self
    }

    /// Sets the placer strategy (required).
    pub fn placer(mut self, placer: Arc<dyn Placer>) -> Self {
        self.placer = Some(placer);
        self
    }

    /// Enables or disables deduplication (default: enabled).
    pub fn deduplication(mut self, enabled: bool) -> Self {
        self.deduplication = enabled;
        self
    }

    /// Sets the overflow strategy (default: `Throw`).
    pub fn overflow_strategy(mut self, strategy: OverflowStrategy) -> Self {
        self.overflow_strategy = strategy;
        self
    }

    /// Builds the policy, validating that all required components are set.
    ///
    /// # Errors
    ///
    /// Returns an error if scorer, slicer, or placer is not set.
    pub fn build(self) -> Result<Policy, CupelError> {
        let scorer = self
            .scorer
            .ok_or_else(|| CupelError::PipelineConfig("scorer is required".to_owned()))?;
        let slicer = self
            .slicer
            .ok_or_else(|| CupelError::PipelineConfig("slicer is required".to_owned()))?;
        let placer = self
            .placer
            .ok_or_else(|| CupelError::PipelineConfig("placer is required".to_owned()))?;

        Ok(Policy {
            scorer,
            slicer,
            placer,
            deduplication: self.deduplication,
            overflow_strategy: self.overflow_strategy,
        })
    }
}

impl Default for PolicyBuilder {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use super::*;
    use crate::model::ContextItemBuilder;
    use crate::placer::ChronologicalPlacer;
    use crate::scorer::RecencyScorer;
    use crate::slicer::GreedySlice;

    fn minimal_pipeline() -> Pipeline {
        Pipeline::builder()
            .scorer(Box::new(RecencyScorer))
            .slicer(Box::new(GreedySlice))
            .placer(Box::new(ChronologicalPlacer))
            .build()
            .unwrap()
    }

    #[test]
    fn pipeline_single_item() {
        let pipeline = minimal_pipeline();
        let item = ContextItemBuilder::new("only item", 10).build().unwrap();
        let budget = ContextBudget::new(4096, 200, 0, HashMap::new(), 0.0).unwrap();

        let result = pipeline.run(&[item], &budget).unwrap();
        assert_eq!(result.len(), 1);
        assert_eq!(result[0].content(), "only item");
    }

    #[test]
    fn pipeline_all_negative_token_items() {
        // Items with negative token counts are filtered out at the Classify stage
        let pipeline = minimal_pipeline();
        let items = vec![
            ContextItemBuilder::new("neg-a", -1).build().unwrap(),
            ContextItemBuilder::new("neg-b", -5).build().unwrap(),
        ];
        let budget = ContextBudget::new(4096, 200, 0, HashMap::new(), 0.0).unwrap();

        let result = pipeline.run(&items, &budget).unwrap();
        assert!(
            result.is_empty(),
            "expected empty result; got {}",
            result.len()
        );
    }
}
