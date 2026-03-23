use crate::model::ContextItem;
use crate::scorer::Scorer;

/// Absolute scorer: passes through `futureRelevanceHint`, clamped to \[0.0, 1.0\].
///
/// Returns 0.0 for null or non-finite hints. Finiteness is checked before clamping.
///
/// # Examples
///
/// ```
/// use cupel::{ContextItemBuilder, ReflexiveScorer, Scorer};
///
/// let item = ContextItemBuilder::new("relevant context", 10)
///     .future_relevance_hint(0.75)
///     .build()?;
///
/// let score = ReflexiveScorer.score(&item, std::slice::from_ref(&item));
/// assert_eq!(score, 0.75);
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Debug, Clone, Copy)]
pub struct ReflexiveScorer;

impl Scorer for ReflexiveScorer {
    fn score(&self, item: &ContextItem, _all_items: &[ContextItem]) -> f64 {
        match item.future_relevance_hint() {
            None => 0.0,
            Some(value) => {
                if !value.is_finite() {
                    return 0.0;
                }
                value.clamp(0.0, 1.0)
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::model::ContextItemBuilder;

    #[test]
    fn reflexive_scorer_nan_hint() {
        // NaN is not finite → implementation returns 0.0 (finiteness guard)
        let item = ContextItemBuilder::new("nan-item", 5)
            .future_relevance_hint(f64::NAN)
            .build()
            .unwrap();
        let score = ReflexiveScorer.score(&item, std::slice::from_ref(&item));
        assert_eq!(score, 0.0);
    }

    #[test]
    fn reflexive_scorer_large_hint_clamped() {
        // hint = 2.0 → clamped to 1.0
        let item = ContextItemBuilder::new("large-hint", 5)
            .future_relevance_hint(2.0)
            .build()
            .unwrap();
        let score = ReflexiveScorer.score(&item, std::slice::from_ref(&item));
        assert_eq!(score, 1.0);
    }
}
