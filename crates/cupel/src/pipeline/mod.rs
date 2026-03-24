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

use std::time::Instant;

use crate::CupelError;
use crate::diagnostics::{
    ExclusionReason, InclusionReason, PipelineStage, SelectionReport, TraceEvent,
};
use crate::diagnostics::trace_collector::{
    DiagnosticTraceCollector, TraceCollector, TraceDetailLevel,
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
        collector.set_candidates(items.len(), items.iter().map(|i| i.tokens()).sum());

        // Stage 1: Classify
        let t = Instant::now();
        let (pinned, scoreable, neg_items) = classify::classify(items, budget)?;
        if collector.is_enabled() {
            for item in &neg_items {
                collector.record_excluded(
                    item.clone(),
                    0.0,
                    ExclusionReason::NegativeTokens { tokens: item.tokens() },
                );
            }
            collector.record_stage_event(TraceEvent {
                stage: PipelineStage::Classify,
                duration_ms: t.elapsed().as_secs_f64() * 1000.0,
                item_count: pinned.len() + scoreable.len(),
                message: None,
            });
        }

        // Stage 2: Score
        let t = Instant::now();
        let scored = score::score_items(&scoreable, self.scorer.as_ref());
        if collector.is_enabled() {
            collector.record_stage_event(TraceEvent {
                stage: PipelineStage::Score,
                duration_ms: t.elapsed().as_secs_f64() * 1000.0,
                item_count: scored.len(),
                message: None,
            });
        }

        // Stage 3: Deduplicate
        let t = Instant::now();
        let (deduped, ded_excluded) = deduplicate::deduplicate(scored, self.deduplication);
        if collector.is_enabled() {
            for si in &ded_excluded {
                collector.record_excluded(
                    si.item.clone(),
                    si.score,
                    ExclusionReason::Deduplicated {
                        deduplicated_against: si.item.content().to_owned(),
                    },
                );
            }
            collector.record_stage_event(TraceEvent {
                stage: PipelineStage::Deduplicate,
                duration_ms: t.elapsed().as_secs_f64() * 1000.0,
                item_count: deduped.len(),
                message: None,
            });
        }

        // Stage 4: Sort
        let sorted = sort::sort_scored(deduped);

        // Build score lookup for inclusion recording later
        let score_lookup: std::collections::HashMap<&str, f64> =
            sorted.iter().map(|si| (si.item.content(), si.score)).collect();

        // Compute effective budget parameters needed for PinnedOverride detection
        let pinned_tokens: i64 = pinned.iter().map(|i: &ContextItem| i.tokens()).sum();
        let effective_budget = slice::compute_effective_budget(budget, pinned_tokens);
        let effective_target = effective_budget.target_tokens();

        // Stage 5: Slice
        let t = Instant::now();
        let sliced = slice::slice_items(&sorted, budget, pinned_tokens, self.slicer.as_ref())?;
        if collector.is_enabled() {
            let sliced_total: i64 = sliced.iter().map(|i| i.tokens()).sum();
            let available_tokens = effective_target - sliced_total;

            // Track which sliced items have been "consumed" when matching sorted items
            let mut sliced_count: std::collections::HashMap<&str, usize> =
                std::collections::HashMap::new();
            for item in &sliced {
                *sliced_count.entry(item.content()).or_insert(0) += 1;
            }

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
                } else {
                    ExclusionReason::BudgetExceeded {
                        item_tokens: si.item.tokens(),
                        available_tokens,
                    }
                };
                collector.record_excluded(si.item.clone(), si.score, reason);
            }

            collector.record_stage_event(TraceEvent {
                stage: PipelineStage::Slice,
                duration_ms: t.elapsed().as_secs_f64() * 1000.0,
                item_count: sliced.len(),
                message: None,
            });
        }

        // Stage 6: Place
        let t = Instant::now();
        let (result, truncated) = place::place_items(
            &pinned,
            &sliced,
            &sorted,
            budget,
            self.overflow_strategy,
            self.placer.as_ref(),
        )?;
        if collector.is_enabled() {
            for (item, score) in &truncated {
                let available_tokens = budget.target_tokens()
                    - result.iter().map(|i| i.tokens()).sum::<i64>();
                collector.record_excluded(
                    item.clone(),
                    *score,
                    ExclusionReason::BudgetExceeded {
                        item_tokens: item.tokens(),
                        available_tokens,
                    },
                );
            }
            collector.record_stage_event(TraceEvent {
                stage: PipelineStage::Place,
                duration_ms: t.elapsed().as_secs_f64() * 1000.0,
                item_count: result.len(),
                message: None,
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
}

impl std::fmt::Debug for Pipeline {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Pipeline")
            .field("deduplication", &self.deduplication)
            .field("overflow_strategy", &self.overflow_strategy)
            .finish()
    }
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
        assert!(result.is_empty(), "expected empty result; got {}", result.len());
    }
}
