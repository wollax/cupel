use std::collections::HashMap;

use crate::CupelError;
use crate::model::ContextItem;
use crate::scorer::Scorer;

/// Absolute scorer: weighted tag matching, normalized by total configured weight.
///
/// Tag key lookup is case-sensitive (ordinal) per spec.
///
/// # Examples
///
/// ```
/// use std::collections::HashMap;
/// use cupel::{ContextItemBuilder, TagScorer, Scorer};
///
/// let weights = HashMap::from([
///     ("important".to_string(), 1.0),
///     ("recent".to_string(), 0.5),
/// ]);
/// let scorer = TagScorer::new(weights)?;
///
/// let item = ContextItemBuilder::new("tagged item", 5)
///     .tags(vec!["important".to_string()])
///     .build()?;
///
/// let score = scorer.score(&item, std::slice::from_ref(&item));
/// assert!((score - 0.6666).abs() < 0.01); // 1.0 / 1.5
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Debug, Clone)]
pub struct TagScorer {
    tag_weights: HashMap<String, f64>,
    total_weight: f64,
}

impl TagScorer {
    /// Creates a TagScorer with the given tag-weight map.
    ///
    /// Validates that all weights are non-negative and finite.
    pub fn new(tag_weights: HashMap<String, f64>) -> Result<Self, CupelError> {
        let mut total_weight = 0.0;
        for (tag, &weight) in &tag_weights {
            if weight < 0.0 {
                return Err(CupelError::ScorerConfig(format!(
                    "tag weight for '{}' must be non-negative",
                    tag,
                )));
            }
            if !weight.is_finite() {
                return Err(CupelError::ScorerConfig(format!(
                    "tag weight for '{}' must be finite",
                    tag,
                )));
            }
            total_weight += weight;
        }
        Ok(Self {
            tag_weights,
            total_weight,
        })
    }
}

impl Scorer for TagScorer {
    fn score(&self, item: &ContextItem, _all_items: &[ContextItem]) -> f64 {
        if self.total_weight == 0.0 || item.tags().is_empty() {
            return 0.0;
        }

        let mut matched_sum = 0.0;
        for tag in item.tags() {
            if let Some(&weight) = self.tag_weights.get(tag) {
                matched_sum += weight;
            }
        }

        f64::min(matched_sum / self.total_weight, 1.0)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::model::ContextItemBuilder;

    #[test]
    fn tag_scorer_zero_total_weight() {
        // All weights are 0.0 → total_weight == 0.0 → score always returns 0.0
        let weights = HashMap::from([("important".to_string(), 0.0), ("recent".to_string(), 0.0)]);
        let scorer = TagScorer::new(weights).unwrap();
        let item = ContextItemBuilder::new("item", 5)
            .tags(vec!["important".to_string()])
            .build()
            .unwrap();
        let score = scorer.score(&item, std::slice::from_ref(&item));
        assert_eq!(score, 0.0);
    }

    #[test]
    fn tag_scorer_case_sensitive_no_match() {
        // Weight configured for "Important" (capital I); item has "important" (lowercase)
        // Tag key lookup is case-sensitive → no match → score == 0.0
        let weights = HashMap::from([("Important".to_string(), 1.0)]);
        let scorer = TagScorer::new(weights).unwrap();
        let item = ContextItemBuilder::new("item", 5)
            .tags(vec!["important".to_string()])
            .build()
            .unwrap();
        let score = scorer.score(&item, std::slice::from_ref(&item));
        assert_eq!(score, 0.0);
    }
}
