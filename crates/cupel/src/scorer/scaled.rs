use crate::model::ContextItem;
use crate::scorer::Scorer;

/// Wrapping scorer: min-max normalization of an inner scorer across all items.
///
/// Returns 0.5 for degenerate cases (empty allItems, all scores equal).
/// Self-identification uses reference identity (`std::ptr::eq`).
///
/// # Examples
///
/// ```
/// use cupel::{ContextItemBuilder, ScaledScorer, PriorityScorer, Scorer};
///
/// let scorer = ScaledScorer::new(Box::new(PriorityScorer));
///
/// let lo = ContextItemBuilder::new("low", 5).priority(1).build()?;
/// let hi = ContextItemBuilder::new("high", 5).priority(10).build()?;
/// let items = vec![lo.clone(), hi.clone()];
///
/// let lo_score = scorer.score(&items[0], &items);
/// let hi_score = scorer.score(&items[1], &items);
/// assert_eq!(lo_score, 0.0); // min
/// assert_eq!(hi_score, 1.0); // max
/// # Ok::<(), cupel::CupelError>(())
/// ```
pub struct ScaledScorer {
    inner: Box<dyn Scorer>,
}

impl ScaledScorer {
    /// Creates a new ScaledScorer wrapping the given inner scorer.
    pub fn new(inner: Box<dyn Scorer>) -> Self {
        Self { inner }
    }
}

impl Scorer for ScaledScorer {
    fn score(&self, item: &ContextItem, all_items: &[ContextItem]) -> f64 {
        if all_items.is_empty() {
            return 0.5;
        }

        let mut raw_score = f64::NAN;
        let mut min = f64::INFINITY;
        let mut max = f64::NEG_INFINITY;

        for i in 0..all_items.len() {
            let s = self.inner.score(&all_items[i], all_items);
            if s < min {
                min = s;
            }
            if s > max {
                max = s;
            }
            if std::ptr::eq(item, &all_items[i]) {
                raw_score = s;
            }
        }

        // If item was not found via reference identity, return neutral score
        if !raw_score.is_finite() {
            return 0.5;
        }

        if max == min {
            return 0.5;
        }

        (raw_score - min) / (max - min)
    }
}

impl std::fmt::Debug for ScaledScorer {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("ScaledScorer").finish()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::model::ContextItemBuilder;

    /// A scorer that always returns the same constant value.
    struct ConstantScorer(f64);
    impl Scorer for ConstantScorer {
        fn score(&self, _item: &ContextItem, _all_items: &[ContextItem]) -> f64 {
            self.0
        }
    }

    #[test]
    fn scaled_scorer_degenerate_all_equal_scores() {
        // All inner scores equal → max == min → returns 0.5
        let scorer = ScaledScorer::new(Box::new(ConstantScorer(0.7)));
        let items: Vec<ContextItem> = (0..3)
            .map(|i| {
                ContextItemBuilder::new(i.to_string().as_str(), 5)
                    .build()
                    .unwrap()
            })
            .collect();

        for item in &items {
            let score = scorer.score(item, &items);
            assert_eq!(score, 0.5, "expected 0.5 for degenerate all-equal case");
        }
    }

    #[test]
    fn scaled_scorer_item_not_in_list() {
        // Item not found via reference identity → returns 0.5
        let scorer = ScaledScorer::new(Box::new(ConstantScorer(0.5)));
        let other = ContextItemBuilder::new("other", 5).build().unwrap();
        let external = ContextItemBuilder::new("external", 5).build().unwrap();

        // `external` is not in the slice passed to score()
        let score = scorer.score(&external, &[other]);
        assert_eq!(score, 0.5);
    }
}
