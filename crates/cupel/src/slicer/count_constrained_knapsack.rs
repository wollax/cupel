//! Count-constrained knapsack slicer combining count quotas with knapsack-optimal selection.
//!
//! [`CountConstrainedKnapsackSlice`] implements a 3-phase pre-processing algorithm:
//!
//! - **Phase 1 (Count-Satisfy):** For each kind with `require_count > 0`, commits the top-N
//!   candidates by score descending. This mirrors `CountQuotaSlice`'s Phase 1 exactly.
//!
//! - **Phase 2 (Knapsack-Distribute):** The remaining candidates are passed to a stored-by-value
//!   [`KnapsackSlice`] with the residual token budget. This differs from `CountQuotaSlice`
//!   which calls `self.inner.slice()` on a `Box<dyn Slicer>` — here `self.knapsack.slice()`
//!   is called on the value directly (KnapsackSlice is `Copy`).
//!
//! - **Phase 3 (Cap Enforcement):** Phase 2 output is filtered: items of a kind exceeding
//!   `cap_count` are excluded. `selected_count` from Phase 1 is the starting state so the
//!   cap is correctly tracked across both phases.
//!
//! # Design references
//!
//! - D174: Pre-processing path (5A) — no full constrained-DP
//! - D175: Re-use `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall`
//! - D176: `is_count_quota() → true`; `is_knapsack()` default stays `false`

use std::collections::HashMap;

use crate::CupelError;
use crate::diagnostics::CountRequirementShortfall;
use crate::model::{ContextBudget, ContextItem, ContextKind, ScoredItem};
use crate::slicer::count_quota::{CountQuotaEntry, ScarcityBehavior};
use crate::slicer::{KnapsackSlice, QuotaConstraint, QuotaConstraintMode, QuotaPolicy, Slicer};

// ── CountConstrainedKnapsackSlice ─────────────────────────────────────────────

/// A slicer that enforces count requirements and caps per [`ContextKind`] while using
/// knapsack-optimal selection for the residual budget.
///
/// # Algorithm
///
/// **Phase 1 — Count-Satisfy:** For each [`CountQuotaEntry`] with `require_count > 0`,
/// selects the top-N candidates of that kind by score descending and pre-commits them.
/// Their token cost is subtracted from the budget before Phase 2.
///
/// **Phase 2 — Knapsack-Distribute:** The stored [`KnapsackSlice`] runs on the remaining
/// candidates with the residual budget, finding the globally optimal subset.
///
/// **Phase 3 — Cap Enforcement:** Items of a kind exceeding `cap_count` are excluded
/// from Phase 2 output. `selected_count` from Phase 1 is the starting state.
///
/// # Scarcity
///
/// When the candidate pool has fewer items than `require_count`, behavior is
/// controlled by [`ScarcityBehavior`]:
/// - [`Degrade`](ScarcityBehavior::Degrade) (default): select all available; record shortfall.
/// - [`Throw`](ScarcityBehavior::Throw): return [`CupelError::SlicerConfig`].
///
/// # Examples
///
/// ```
/// use std::collections::HashMap;
/// use cupel::{
///     ContextItemBuilder, ContextBudget, ContextKind,
///     CountQuotaEntry, CountConstrainedKnapsackSlice, KnapsackSlice, ScoredItem, Slicer,
///     ScarcityBehavior,
/// };
///
/// let entries = vec![
///     CountQuotaEntry::new(ContextKind::new("tool")?, 2, 4)?,
/// ];
/// let knapsack = KnapsackSlice::new(100)?;
/// let slicer = CountConstrainedKnapsackSlice::new(entries, knapsack, Default::default())?;
///
/// let items = vec![
///     ScoredItem {
///         item: ContextItemBuilder::new("tool-a", 100)
///             .kind(ContextKind::new("tool")?)
///             .build()?,
///         score: 0.9,
///     },
///     ScoredItem {
///         item: ContextItemBuilder::new("tool-b", 100)
///             .kind(ContextKind::new("tool")?)
///             .build()?,
///         score: 0.7,
///     },
/// ];
///
/// let budget = ContextBudget::new(1000, 1000, 0, HashMap::new(), 0.0)?;
/// let selected = slicer.slice(&items, &budget)?;
/// assert_eq!(selected.len(), 2);
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Clone)]
pub struct CountConstrainedKnapsackSlice {
    entries: Vec<CountQuotaEntry>,
    knapsack: KnapsackSlice,
    scarcity: ScarcityBehavior,
}

impl std::fmt::Debug for CountConstrainedKnapsackSlice {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("CountConstrainedKnapsackSlice")
            .field("entries", &self.entries)
            .field("knapsack", &self.knapsack)
            .field("scarcity", &self.scarcity)
            .finish()
    }
}

impl CountConstrainedKnapsackSlice {
    /// Creates a new `CountConstrainedKnapsackSlice`.
    ///
    /// No `is_knapsack()` guard is applied here — `CountConstrainedKnapsackSlice` IS
    /// the knapsack wrapper. The guard in `CountQuotaSlice::new` does not apply.
    ///
    /// # Errors
    ///
    /// Returns [`CupelError::SlicerConfig`] only if an entry fails validation
    /// (see [`CountQuotaEntry::new`]). The entries are pre-validated by the caller.
    pub fn new(
        entries: Vec<CountQuotaEntry>,
        knapsack: KnapsackSlice,
        scarcity: ScarcityBehavior,
    ) -> Result<Self, CupelError> {
        Ok(Self {
            entries,
            knapsack,
            scarcity,
        })
    }

    /// Returns the configured quota entries.
    pub fn entries(&self) -> &[CountQuotaEntry] {
        &self.entries
    }

    /// Returns the configured scarcity behavior.
    pub fn scarcity(&self) -> ScarcityBehavior {
        self.scarcity
    }

    /// Builds lookup maps for require_count and cap_count keyed by kind.
    fn build_policy_maps(&self) -> (HashMap<ContextKind, usize>, HashMap<ContextKind, usize>) {
        let mut require_map: HashMap<ContextKind, usize> = HashMap::new();
        let mut cap_map: HashMap<ContextKind, usize> = HashMap::new();
        for entry in &self.entries {
            require_map.insert(entry.kind().clone(), entry.require_count());
            cap_map.insert(entry.kind().clone(), entry.cap_count());
        }
        (require_map, cap_map)
    }
}

impl Slicer for CountConstrainedKnapsackSlice {
    /// Runs the 3-phase COUNT-KNAPSACK-CAP algorithm.
    ///
    /// Returns the union of Phase 1 committed items and Phase 2 knapsack-selected results
    /// (with cap enforcement applied to Phase 2 output).
    fn slice(
        &self,
        sorted: &[ScoredItem],
        budget: &ContextBudget,
    ) -> Result<Vec<ContextItem>, CupelError> {
        if sorted.is_empty() || budget.target_tokens() <= 0 {
            return Ok(Vec::new());
        }

        let (require_map, cap_map) = self.build_policy_maps();
        let target_tokens = budget.target_tokens();

        // ── Phase 1: Count-Satisfy ────────────────────────────────────────────
        //
        // For each kind with require_count > 0, partition candidates by kind,
        // sort descending by score, commit the top-N, and accumulate token cost.

        // Partition all sorted items by kind, preserving score order.
        let mut partitions: HashMap<ContextKind, Vec<&ScoredItem>> = HashMap::new();
        for si in sorted {
            partitions
                .entry(si.item.kind().clone())
                .or_default()
                .push(si);
        }
        // Each partition is already in the caller's score-descending order (sorted input),
        // but we sort explicitly for safety.
        for items in partitions.values_mut() {
            items.sort_by(|a, b| b.score.total_cmp(&a.score));
        }

        // Track committed items, counts, and pre-allocated token cost.
        let mut committed: Vec<ContextItem> = Vec::new();
        let mut selected_count: HashMap<ContextKind, usize> = HashMap::new();
        let mut pre_alloc_tokens: i64 = 0;

        // Track which ScoredItems were committed so we can exclude them from Phase 2.
        // These pointers are safe: `sorted` lives for the duration of this call.
        let mut committed_ids: std::collections::HashSet<*const ScoredItem> =
            std::collections::HashSet::new();

        // Record shortfalls for reporting.
        let mut shortfalls: Vec<CountRequirementShortfall> = Vec::new();

        // Sort kinds for deterministic iteration.
        let mut sorted_kinds: Vec<&ContextKind> = partitions.keys().collect();
        sorted_kinds.sort_by_key(|k| k.as_str().to_ascii_lowercase());

        for kind in &sorted_kinds {
            let req_count = require_map.get(*kind).copied().unwrap_or(0);
            if req_count == 0 {
                continue;
            }

            let candidates = &partitions[*kind];
            let mut satisfied = 0usize;

            for &si in candidates.iter() {
                if satisfied >= req_count {
                    break;
                }
                committed.push(si.item.clone());
                committed_ids.insert(si as *const ScoredItem);
                pre_alloc_tokens += si.item.tokens();
                satisfied += 1;
            }

            selected_count.insert((*kind).clone(), satisfied);

            if satisfied < req_count {
                // Scarcity: fewer candidates than required.
                match self.scarcity {
                    ScarcityBehavior::Degrade => {
                        shortfalls.push(CountRequirementShortfall {
                            kind: kind.as_str().to_owned(),
                            required_count: req_count,
                            satisfied_count: satisfied,
                        });
                    }
                    ScarcityBehavior::Throw => {
                        return Err(CupelError::SlicerConfig(format!(
                            "CountConstrainedKnapsackSlice: kind {:?} requires {req_count} items \
                             but only {satisfied} candidates are available",
                            kind.as_str(),
                        )));
                    }
                }
            }
        }

        // ── Phase 2: Knapsack-Distribute ─────────────────────────────────────
        //
        // Build residual candidate pool (omit committed items) and run the stored
        // KnapsackSlice with the residual budget.

        let residual_budget_tokens = (target_tokens - pre_alloc_tokens).max(0);

        // Build remaining candidates (not committed in Phase 1), preserving original order.
        let remaining: Vec<ScoredItem> = sorted
            .iter()
            .filter(|si| !committed_ids.contains(&(*si as *const ScoredItem)))
            .cloned()
            .collect();

        // Build a score lookup so Phase 3 can process Phase 2 output in score-descending
        // order. KnapsackSlice returns items in DP-reconstruction order (not score order),
        // so we need to re-sort before cap enforcement to ensure the highest-scoring items
        // survive the cap.
        let score_by_content: HashMap<String, f64> = remaining
            .iter()
            .map(|si| (si.item.content().to_owned(), si.score))
            .collect();

        let mut phase2_selected: Vec<ContextItem> =
            if residual_budget_tokens > 0 && !remaining.is_empty() {
                // Create a sub-budget for the knapsack slicer.
                let sub_budget = ContextBudget::new(
                    residual_budget_tokens,
                    residual_budget_tokens,
                    0,
                    HashMap::new(),
                    0.0,
                )
                .expect("residual budget is non-negative");

                // Call knapsack by value — KnapsackSlice is Copy.
                let mut selected = self.knapsack.slice(&remaining, &sub_budget)?;

                // Sort by score descending so Phase 3 cap enforcement preserves the
                // highest-scoring items when the cap is binding.
                selected.sort_by(|a, b| {
                    let sa = score_by_content.get(a.content()).copied().unwrap_or(0.0);
                    let sb = score_by_content.get(b.content()).copied().unwrap_or(0.0);
                    sb.total_cmp(&sa)
                });

                selected
            } else {
                Vec::new()
            };

        // ── Phase 3: Cap Enforcement ──────────────────────────────────────────
        //
        // Filter Phase 2 output: items of a kind exceeding cap_count are excluded.
        // selected_count already reflects Phase 1 committed items — this is the correct
        // starting state. Starting from zero would incorrectly allow cap+1 items.

        let mut filtered_phase2: Vec<ContextItem> = Vec::new();
        for item in phase2_selected.drain(..) {
            let kind = item.kind();
            let cap = cap_map.get(kind).copied();
            let current = selected_count.entry(kind.clone()).or_insert(0);

            match cap {
                Some(cap_count) if *current >= cap_count => {
                    // Exceeds cap — exclude.
                }
                _ => {
                    filtered_phase2.push(item);
                    *current += 1;
                }
            }
        }

        // Suppress unused variable warning for shortfalls (recorded but not surfaced at
        // slicer level — pipeline-level SelectionReport is the correct surface).
        let _ = shortfalls;

        // Final result: Phase 1 committed + Phase 2 cap-enforced.
        let mut result = committed;
        result.extend(filtered_phase2);
        Ok(result)
    }

    fn is_count_quota(&self) -> bool {
        true
    }

    fn count_cap_map(&self) -> std::collections::HashMap<ContextKind, usize> {
        self.entries
            .iter()
            .map(|e| (e.kind().clone(), e.cap_count()))
            .collect()
    }
}

impl QuotaPolicy for CountConstrainedKnapsackSlice {
    fn quota_constraints(&self) -> Vec<QuotaConstraint> {
        self.entries
            .iter()
            .map(|e| QuotaConstraint {
                kind: e.kind().clone(),
                mode: QuotaConstraintMode::Count,
                require: e.require_count() as f64,
                cap: e.cap_count() as f64,
            })
            .collect()
    }
}
