use cupel::SelectionReport;
use cupel::analytics;
use cupel::diagnostics::{ExcludedItem, ExclusionReason, IncludedItem};
use cupel::model::{ContextBudget, ContextKind};

/// A fluent assertion chain for inspecting a [`SelectionReport`].
///
/// Obtain an instance via [`SelectionReportAssertions::should()`](crate::SelectionReportAssertions::should).
/// Assertion methods are chained on this struct and each return `&mut Self` so
/// multiple checks can be composed in a single expression.
pub struct SelectionReportAssertionChain<'a> {
    pub(crate) report: &'a SelectionReport,
}

impl<'a> SelectionReportAssertionChain<'a> {
    pub(crate) fn new(report: &'a SelectionReport) -> Self {
        Self { report }
    }

    // ── Pattern 1: Inclusion ────────────────────────────────────────────────

    /// Asserts that at least one included item has the given `kind`.
    pub fn include_item_with_kind(&mut self, kind: ContextKind) -> &mut Self {
        let included = &self.report.included;
        if !included.iter().any(|i| i.item.kind() == &kind) {
            let kinds: Vec<_> = included
                .iter()
                .map(|i| i.item.kind().as_str().to_string())
                .collect::<std::collections::HashSet<_>>()
                .into_iter()
                .collect();
            let kinds_str = kinds.join(", ");
            panic!(
                "include_item_with_kind({kind}) failed: Included contained 0 items with Kind={kind}. \
                 Included had {count} items with kinds: [{kinds_str}].",
                kind = kind,
                count = included.len(),
            );
        }
        self
    }

    // ── Pattern 2: Inclusion ────────────────────────────────────────────────

    /// Asserts that at least one included item satisfies `predicate`.
    pub fn include_item_matching(
        &mut self,
        predicate: impl Fn(&IncludedItem) -> bool,
    ) -> &mut Self {
        let included = &self.report.included;
        if !included.iter().any(predicate) {
            panic!(
                "include_item_matching failed: no item in Included matched the predicate. \
                 Included had {count} items.",
                count = included.len(),
            );
        }
        self
    }

    // ── Pattern 3: Inclusion ────────────────────────────────────────────────

    /// Asserts that exactly `n` included items have the given `kind`.
    pub fn include_exact_n_items_with_kind(&mut self, kind: ContextKind, n: usize) -> &mut Self {
        let included = &self.report.included;
        let actual = included.iter().filter(|i| i.item.kind() == &kind).count();
        if actual != n {
            panic!(
                "include_exact_n_items_with_kind({kind}, {n}) failed: expected {n} items with Kind={kind} in Included, \
                 but found {actual}. Included had {count} items total.",
                kind = kind,
                n = n,
                actual = actual,
                count = included.len(),
            );
        }
        self
    }

    // ── Pattern 4: Exclusion ────────────────────────────────────────────────

    /// Asserts that at least one excluded item carries the given `reason` variant.
    pub fn exclude_item_with_reason(&mut self, reason: ExclusionReason) -> &mut Self {
        let excluded = &self.report.excluded;
        let found = excluded
            .iter()
            .any(|e| std::mem::discriminant(&e.reason) == std::mem::discriminant(&reason));
        if !found {
            let reasons: Vec<_> = excluded
                .iter()
                .map(|e| format!("{:?}", e.reason))
                .collect::<std::collections::HashSet<_>>()
                .into_iter()
                .collect();
            let reasons_str = reasons.join(", ");
            panic!(
                "exclude_item_with_reason({reason:?}) failed: no excluded item had reason {reason:?}. \
                 Excluded had {count} items with reasons: [{reasons_str}].",
                reason = reason,
                count = excluded.len(),
            );
        }
        self
    }

    // ── Pattern 5: Exclusion ────────────────────────────────────────────────

    /// Asserts that at least one excluded item satisfies `predicate` and has the given `reason` variant.
    pub fn exclude_item_matching_with_reason(
        &mut self,
        predicate: impl Fn(&ExcludedItem) -> bool,
        reason: ExclusionReason,
    ) -> &mut Self {
        let excluded = &self.report.excluded;
        let predicate_matches: Vec<_> = excluded.iter().filter(|e| predicate(e)).collect();
        let found = predicate_matches
            .iter()
            .any(|e| std::mem::discriminant(&e.reason) == std::mem::discriminant(&reason));
        if !found {
            let actual_reasons: Vec<_> = predicate_matches
                .iter()
                .map(|e| format!("{:?}", e.reason))
                .collect::<std::collections::HashSet<_>>()
                .into_iter()
                .collect();
            let actual_reasons_str = actual_reasons.join(", ");
            panic!(
                "exclude_item_matching_with_reason(reason={reason:?}) failed: predicate matched {count} \
                 excluded item(s) but none had reason {reason:?}. Matched items had reasons: [{actual_reasons_str}].",
                reason = reason,
                count = predicate_matches.len(),
            );
        }
        self
    }

    // ── Pattern 6: Exclusion ────────────────────────────────────────────────

    /// Asserts that an excluded item matching `predicate` was excluded due to
    /// `BudgetExceeded` with exactly the given `expected_item_tokens` and
    /// `expected_available_tokens`.
    pub fn have_excluded_item_with_budget_details(
        &mut self,
        predicate: impl Fn(&ExcludedItem) -> bool,
        expected_item_tokens: i64,
        expected_available_tokens: i64,
    ) -> &mut Self {
        let excluded = &self.report.excluded;
        // Find the first predicate-matching BudgetExceeded item.
        let budget_match = excluded
            .iter()
            .find(|e| predicate(e) && matches!(e.reason, ExclusionReason::BudgetExceeded { .. }));
        match budget_match {
            Some(e) => {
                if let ExclusionReason::BudgetExceeded {
                    item_tokens: ait,
                    available_tokens: aat,
                } = e.reason
                {
                    if ait != expected_item_tokens || aat != expected_available_tokens {
                        panic!(
                            "have_excluded_item_with_budget_details failed: expected BudgetExceeded \
                             with item_tokens={eIT}, available_tokens={eAT}, \
                             but found item_tokens={aIT}, available_tokens={aAT}.",
                            eIT = expected_item_tokens,
                            eAT = expected_available_tokens,
                            aIT = ait,
                            aAT = aat,
                        );
                    }
                }
            }
            None => {
                panic!(
                    "have_excluded_item_with_budget_details failed: expected BudgetExceeded \
                     with item_tokens={eIT}, available_tokens={eAT}, \
                     but no matching item had reason BudgetExceeded.",
                    eIT = expected_item_tokens,
                    eAT = expected_available_tokens,
                );
            }
        }
        self
    }

    // ── Pattern 7: Exclusion ────────────────────────────────────────────────

    /// Asserts that no excluded item has the given `kind`.
    pub fn have_no_exclusions_for_kind(&mut self, kind: ContextKind) -> &mut Self {
        let excluded = &self.report.excluded;
        let matching: Vec<_> = excluded.iter().filter(|e| e.item.kind() == &kind).collect();
        if !matching.is_empty() {
            let first = &matching[0];
            panic!(
                "have_no_exclusions_for_kind({kind}) failed: found {count} excluded item(s) with Kind={kind}. \
                 First: score={score:.4}, reason={reason:?}.",
                kind = kind,
                count = matching.len(),
                score = first.score,
                reason = first.reason,
            );
        }
        self
    }

    // ── Pattern 8: Aggregate ────────────────────────────────────────────────

    /// Asserts that the excluded list has at least `n` items.
    pub fn have_at_least_n_exclusions(&mut self, n: usize) -> &mut Self {
        let actual = self.report.excluded.len();
        if actual < n {
            panic!(
                "have_at_least_n_exclusions({n}) failed: expected at least {n} excluded items, \
                 but Excluded had {actual}.",
            );
        }
        self
    }

    // ── Pattern 9: Aggregate ────────────────────────────────────────────────

    /// Asserts that the excluded list is sorted in non-increasing score order.
    pub fn excluded_items_are_sorted_by_score_descending(&mut self) -> &mut Self {
        let excluded = &self.report.excluded;
        for i in 0..excluded.len().saturating_sub(1) {
            let si_prev = excluded[i].score;
            let si = excluded[i + 1].score;
            if si > si_prev {
                panic!(
                    "excluded_items_are_sorted_by_score_descending failed: item at index {next} \
                     (score={si:.6}) is higher than item at index {i} (score={si_prev:.6}). \
                     Expected non-increasing scores.",
                    next = i + 1,
                    i = i,
                    si = si,
                    si_prev = si_prev,
                );
            }
        }
        self
    }

    // ── Pattern 10: Budget ──────────────────────────────────────────────────

    /// Asserts that `sum(included tokens) / budget.max_tokens() >= threshold`.
    pub fn have_budget_utilization_above(
        &mut self,
        threshold: f64,
        budget: &ContextBudget,
    ) -> &mut Self {
        let actual = analytics::budget_utilization(self.report, budget);
        if actual < threshold {
            let included_tokens: i64 = self.report.included.iter().map(|i| i.item.tokens()).sum();
            let max_tokens = budget.max_tokens();
            panic!(
                "have_budget_utilization_above({threshold}) failed: computed utilization was \
                 {actual:.6} (includedTokens={included_tokens}, budget.MaxTokens={max_tokens}).",
            );
        }
        self
    }

    // ── Pattern 11: Coverage ────────────────────────────────────────────────

    /// Asserts that the included list contains at least `n` distinct `ContextKind` values.
    pub fn have_kind_coverage_count(&mut self, n: usize) -> &mut Self {
        let actual = analytics::kind_diversity(self.report);
        if actual < n {
            let kinds: Vec<_> = self
                .report
                .included
                .iter()
                .map(|i| i.item.kind().as_str().to_string())
                .collect::<std::collections::HashSet<_>>()
                .into_iter()
                .collect();
            let actual_kinds = kinds.join(", ");
            panic!(
                "have_kind_coverage_count({n}) failed: expected at least {n} distinct ContextKind \
                 values in Included, but found {actual}: [{actual_kinds}].",
            );
        }
        self
    }

    // ── Pattern 12: Ordering ────────────────────────────────────────────────

    /// Asserts that an included item matching `predicate` is at position 0 or position `count−1`.
    pub fn place_item_at_edge(&mut self, predicate: impl Fn(&IncludedItem) -> bool) -> &mut Self {
        let included = &self.report.included;
        let count = included.len();

        // Find first matching item and its index.
        let found = included
            .iter()
            .enumerate()
            .find(|(_, item)| predicate(item));

        match found {
            None => {
                panic!("place_item_at_edge failed: no item in Included matched the predicate.");
            }
            Some((idx, _)) => {
                let last = count.saturating_sub(1);
                if idx != 0 && idx != last {
                    panic!(
                        "place_item_at_edge failed: item matching predicate was at index {idx} \
                         (not at edge). Edge positions: 0 and {last}. Included had {count} items.",
                    );
                }
            }
        }
        self
    }

    // ── Pattern 13: Ordering ────────────────────────────────────────────────

    /// Asserts that the top-`n` scored included items occupy the `n` outermost edge positions.
    ///
    /// Edge position mapping: 0, count−1, 1, count−2, … (alternating inward from both ends).
    /// `n = 0` always passes. `n > included.len()` panics with a count mismatch message.
    /// Uses index-based approach — no `HashSet<&IncludedItem>` (f64 prevents `Hash`).
    pub fn place_top_n_scored_at_edges(&mut self, n: usize) -> &mut Self {
        if n == 0 {
            return self;
        }
        let count = self.report.included.len();
        if n > count {
            panic!(
                "place_top_n_scored_at_edges({n}) failed: n={n} exceeds Included count={count}.",
            );
        }

        // Collect (score, original_index) pairs and sort by score descending.
        let mut scored: Vec<(f64, usize)> = self
            .report
            .included
            .iter()
            .enumerate()
            .map(|(i, item)| (item.score, i))
            .collect();
        scored.sort_by(|a, b| b.0.total_cmp(&a.0));

        // Top-N entries and the minimum score among them.
        let top_n = &scored[..n];
        let min_top_score = top_n.iter().map(|(s, _)| *s).fold(f64::INFINITY, f64::min);

        // Build the expected edge positions: 0, count-1, 1, count-2, …
        let mut edge_positions: Vec<usize> = Vec::with_capacity(n);
        let mut lo = 0usize;
        let mut hi = count - 1;
        while edge_positions.len() < n {
            edge_positions.push(lo);
            if lo != hi && edge_positions.len() < n {
                edge_positions.push(hi);
            }
            lo += 1;
            hi = hi.saturating_sub(1);
        }

        let edge_set: std::collections::HashSet<usize> = edge_positions.iter().copied().collect();

        // Count top-N items not at expected edge positions.
        let mut fail_count = 0usize;
        for &(score, idx) in top_n {
            // Only count items that are clearly in the top-N (score >= min_top_score).
            if score >= min_top_score && !edge_set.contains(&idx) {
                fail_count += 1;
            }
        }

        if fail_count > 0 {
            let top_items: Vec<_> = top_n
                .iter()
                .map(|&(score, idx)| {
                    let kind = self.report.included[idx].item.kind().as_str().to_string();
                    format!("(kind={kind}, score={score:.6}, idx={idx})")
                })
                .collect();
            let top_items_str = top_items.join(", ");
            let edge_pos_str = edge_positions
                .iter()
                .map(|p| p.to_string())
                .collect::<Vec<_>>()
                .join(", ");
            panic!(
                "place_top_n_scored_at_edges({n}) failed: {fail_count} of the top-{n} scored items \
                 were not at expected edge positions. Top-{n} items (by score): [{top_items_str}]. \
                 Expected edge positions: [{edge_pos_str}].",
            );
        }
        self
    }
}
