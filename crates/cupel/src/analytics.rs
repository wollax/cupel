//! Analytics functions for [`SelectionReport`].
//!
//! These are pure computation functions — no side effects, no allocation beyond
//! the temporary `HashSet` used by [`kind_diversity`]. All three return primitive
//! types and are safe to call on empty reports.

use std::collections::HashMap;

use crate::diagnostics::SelectionReport;
use crate::error::CupelError;
use crate::model::{ContextBudget, ContextItem, ContextKind};
use crate::pipeline::Pipeline;
use crate::slicer::{QuotaConstraintMode, QuotaPolicy};

/// Fraction of the token budget consumed by the selected items.
///
/// Returns `tokens_included / budget.max_tokens()`. The result can exceed `1.0`
/// when the pipeline runs under `OverflowStrategy::Proceed`. The caller is
/// responsible for interpreting values > 1.
///
/// `budget.max_tokens()` is guaranteed to be >= 0 by [`ContextBudget`]
/// construction, and the pipeline enforces `max_tokens > 0` before placement,
/// so no division-by-zero guard is needed here.
pub fn budget_utilization(report: &SelectionReport, budget: &ContextBudget) -> f64 {
    report
        .included
        .iter()
        .map(|i| i.item.tokens() as f64)
        .sum::<f64>()
        / budget.max_tokens() as f64
}

/// Number of distinct context kinds among the included items.
///
/// Returns `0` when `included` is empty. The count is computed by collecting
/// kind references into a `HashSet` and returning its length.
pub fn kind_diversity(report: &SelectionReport) -> usize {
    report
        .included
        .iter()
        .map(|i| i.item.kind())
        .collect::<std::collections::HashSet<_>>()
        .len()
}

/// Fraction of included items that carry a timestamp.
///
/// Returns `0.0` when `included` is empty (avoids division by zero and NaN).
/// A value of `1.0` means every included item has a timestamp; `0.0` means
/// none do.
pub fn timestamp_coverage(report: &SelectionReport) -> f64 {
    if report.included.is_empty() {
        return 0.0;
    }
    report
        .included
        .iter()
        .filter(|i| i.item.timestamp().is_some())
        .count() as f64
        / report.included.len() as f64
}

// ── Quota utilization ─────────────────────────────────────────────────────────

/// Per-kind utilization of a quota constraint, computed from a
/// [`SelectionReport`] against a [`QuotaPolicy`].
///
/// For [`QuotaConstraintMode::Percentage`] mode, `actual` is the percentage of
/// `target_tokens` consumed by items of this kind. For
/// [`QuotaConstraintMode::Count`] mode, `actual` is the number of included
/// items of this kind (as `f64`).
///
/// `utilization` is `actual / cap`, clamped to `[0.0, 1.0]`. When `cap` is
/// zero, `utilization` is `0.0`.
#[derive(Debug, Clone, PartialEq)]
pub struct KindQuotaUtilization {
    /// The context kind this utilization applies to.
    pub kind: ContextKind,
    /// Whether the constraint is percentage-based or count-based.
    pub mode: QuotaConstraintMode,
    /// Minimum threshold from the policy constraint.
    pub require: f64,
    /// Maximum threshold from the policy constraint.
    pub cap: f64,
    /// Actual value achieved: percentage of target_tokens for percentage mode,
    /// item count for count mode.
    pub actual: f64,
    /// `actual / cap`, clamped to `[0.0, 1.0]`. `0.0` when `cap` is zero.
    pub utilization: f64,
}

/// Compute per-kind quota utilization from a selection report against a quota
/// policy.
///
/// Returns one [`KindQuotaUtilization`] per constraint in the policy, sorted by
/// kind for determinism.
///
/// For percentage-mode constraints, `actual` is
/// `sum(tokens for kind) / target_tokens * 100.0`. For count-mode constraints,
/// `actual` is the count of included items of that kind.
pub fn quota_utilization(
    report: &SelectionReport,
    policy: &dyn QuotaPolicy,
    budget: &ContextBudget,
) -> Vec<KindQuotaUtilization> {
    let constraints = policy.quota_constraints();

    // Pre-aggregate included items by kind: (token_sum, count).
    let mut kind_stats: HashMap<&ContextKind, (i64, usize)> = HashMap::new();
    for inc in &report.included {
        let entry = kind_stats.entry(inc.item.kind()).or_insert((0, 0));
        entry.0 += inc.item.tokens();
        entry.1 += 1;
    }

    let target_tokens = budget.target_tokens() as f64;

    let mut results: Vec<KindQuotaUtilization> = constraints
        .iter()
        .map(|c| {
            let (token_sum, count) = kind_stats
                .get(&c.kind)
                .copied()
                .unwrap_or((0, 0));

            let actual = match c.mode {
                QuotaConstraintMode::Percentage => {
                    if target_tokens == 0.0 {
                        0.0
                    } else {
                        token_sum as f64 / target_tokens * 100.0
                    }
                }
                QuotaConstraintMode::Count => count as f64,
            };

            let utilization = if c.cap == 0.0 {
                0.0
            } else {
                (actual / c.cap).clamp(0.0, 1.0)
            };

            KindQuotaUtilization {
                kind: c.kind.clone(),
                mode: c.mode,
                require: c.require,
                cap: c.cap,
                actual,
                utilization,
            }
        })
        .collect();

    results.sort_by(|a, b| a.kind.as_str().cmp(b.kind.as_str()));
    results
}

// ── Policy sensitivity ────────────────────────────────────────────────────────

/// Whether an item was included or excluded by a particular pipeline variant.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ItemStatus {
    Included,
    Excluded,
}

/// A single item that changed inclusion status across pipeline variants.
///
/// `content` is the item's content string (used as the join key).
/// `statuses` holds one `(variant_label, status)` pair per variant, in the same
/// order as the variants were supplied.
#[derive(Debug, Clone, PartialEq)]
pub struct PolicySensitivityDiffEntry {
    pub content: String,
    pub statuses: Vec<(String, ItemStatus)>,
}

/// Result of running multiple pipeline configurations over the same item set.
///
/// `variants` holds the labeled `SelectionReport` for each configuration.
/// `diffs` contains only items whose inclusion status differs across at least
/// two variants — the interesting items that "swing".
#[derive(Debug, Clone, PartialEq)]
pub struct PolicySensitivityReport {
    pub variants: Vec<(String, SelectionReport)>,
    pub diffs: Vec<PolicySensitivityDiffEntry>,
}

/// Run multiple pipeline configurations over the same item set and compute a
/// structured diff showing which items changed inclusion status.
///
/// Each entry in `variants` is a `(label, pipeline)` pair. The function calls
/// [`Pipeline::dry_run`] on each, then builds a content-keyed diff retaining
/// only items where at least two variants disagree on inclusion.
pub fn policy_sensitivity(
    items: &[ContextItem],
    budget: &ContextBudget,
    variants: &[(impl AsRef<str>, &Pipeline)],
) -> Result<PolicySensitivityReport, CupelError> {
    let mut results: Vec<(String, SelectionReport)> = Vec::with_capacity(variants.len());
    for (label, pipeline) in variants {
        let report = pipeline.dry_run(items, budget)?;
        results.push((label.as_ref().to_string(), report));
    }

    // Build content → Vec<(label, status)> map.
    let mut status_map: HashMap<String, Vec<(String, ItemStatus)>> = HashMap::new();

    for (label, report) in &results {
        for inc in &report.included {
            status_map
                .entry(inc.item.content().to_string())
                .or_default()
                .push((label.clone(), ItemStatus::Included));
        }
        for exc in &report.excluded {
            status_map
                .entry(exc.item.content().to_string())
                .or_default()
                .push((label.clone(), ItemStatus::Excluded));
        }
    }

    // Keep only entries where not all statuses are the same.
    let diffs: Vec<PolicySensitivityDiffEntry> = status_map
        .into_iter()
        .filter(|(_, statuses)| {
            let first = statuses.first().map(|(_, s)| *s);
            statuses.iter().any(|(_, s)| Some(*s) != first)
        })
        .map(|(content, statuses)| PolicySensitivityDiffEntry { content, statuses })
        .collect();

    Ok(PolicySensitivityReport {
        variants: results,
        diffs,
    })
}

// ── Unit tests ────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;
    use crate::diagnostics::{IncludedItem, InclusionReason, SelectionReport};
    use crate::model::{ContextBudget, ContextItemBuilder, ContextKind};
    use chrono::Utc;

    fn empty_report() -> SelectionReport {
        SelectionReport {
            events: vec![],
            included: vec![],
            excluded: vec![],
            total_candidates: 0,
            total_tokens_considered: 0,
            count_requirement_shortfalls: vec![],
        }
    }

    fn make_included(tokens: i64, kind: &str, with_timestamp: bool) -> IncludedItem {
        let mut builder = ContextItemBuilder::new("content", tokens)
            .kind(ContextKind::new(kind).unwrap());
        if with_timestamp {
            builder = builder.timestamp(Utc::now());
        }
        IncludedItem {
            item: builder.build().unwrap(),
            score: 1.0,
            reason: InclusionReason::Scored,
        }
    }

    fn budget(max: i64) -> ContextBudget {
        ContextBudget::new(max, max, 0, Default::default(), 0.0).unwrap()
    }

    // ── budget_utilization ────────────────────────────────────────────────────

    #[test]
    fn budget_utilization_empty_is_zero() {
        let report = empty_report();
        let b = budget(1000);
        assert_eq!(budget_utilization(&report, &b), 0.0);
    }

    #[test]
    fn budget_utilization_full_budget() {
        let item = make_included(500, "Message", false);
        let report = SelectionReport {
            total_candidates: 1,
            total_tokens_considered: 500,
            included: vec![item],
            excluded: vec![],
            events: vec![],
            count_requirement_shortfalls: vec![],
        };
        let b = budget(1000);
        let util = budget_utilization(&report, &b);
        assert!((util - 0.5).abs() < f64::EPSILON, "expected 0.5, got {util}");
    }

    // ── kind_diversity ────────────────────────────────────────────────────────

    #[test]
    fn kind_diversity_empty_is_zero() {
        let report = empty_report();
        assert_eq!(kind_diversity(&report), 0);
    }

    #[test]
    fn kind_diversity_counts_distinct_kinds() {
        let report = SelectionReport {
            included: vec![
                make_included(10, "SystemPrompt", false),
                make_included(20, "Message", false),
                make_included(30, "Message", false), // duplicate kind
            ],
            excluded: vec![],
            events: vec![],
            total_candidates: 3,
            total_tokens_considered: 60,
            count_requirement_shortfalls: vec![],
        };
        assert_eq!(kind_diversity(&report), 2);
    }

    // ── timestamp_coverage ────────────────────────────────────────────────────

    #[test]
    fn timestamp_coverage_empty_is_zero() {
        let report = empty_report();
        assert_eq!(timestamp_coverage(&report), 0.0);
    }

    #[test]
    fn timestamp_coverage_all_have_timestamps() {
        let report = SelectionReport {
            included: vec![
                make_included(10, "Message", true),
                make_included(20, "Message", true),
            ],
            excluded: vec![],
            events: vec![],
            total_candidates: 2,
            total_tokens_considered: 30,
            count_requirement_shortfalls: vec![],
        };
        assert_eq!(timestamp_coverage(&report), 1.0);
    }

    #[test]
    fn timestamp_coverage_partial() {
        let report = SelectionReport {
            included: vec![
                make_included(10, "Message", true),
                make_included(20, "Message", false),
            ],
            excluded: vec![],
            events: vec![],
            total_candidates: 2,
            total_tokens_considered: 30,
            count_requirement_shortfalls: vec![],
        };
        let cov = timestamp_coverage(&report);
        assert!((cov - 0.5).abs() < f64::EPSILON, "expected 0.5, got {cov}");
    }
}
