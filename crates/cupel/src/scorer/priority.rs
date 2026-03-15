use std::any::Any;

use crate::model::ContextItem;
use crate::scorer::Scorer;

/// Rank-based scorer: higher priority values score higher.
///
/// Items without a priority receive 0.0. A single prioritized item scores 1.0.
///
/// # Examples
///
/// ```
/// use cupel::{ContextItemBuilder, PriorityScorer, Scorer};
///
/// let item = ContextItemBuilder::new("important", 5)
///     .priority(10)
///     .build()?;
///
/// let score = PriorityScorer.score(&item, &[item.clone()]);
/// assert_eq!(score, 1.0); // single prioritized item scores 1.0
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Debug, Clone, Copy)]
pub struct PriorityScorer;

impl Scorer for PriorityScorer {
    fn score(&self, item: &ContextItem, all_items: &[ContextItem]) -> f64 {
        let item_priority = match item.priority() {
            Some(p) => p,
            None => return 0.0,
        };

        let mut count_with_priority: usize = 0;
        let mut rank: usize = 0;

        for other in all_items {
            if let Some(p) = other.priority() {
                count_with_priority += 1;
                if p < item_priority {
                    rank += 1;
                }
            }
        }

        if count_with_priority <= 1 {
            return 1.0;
        }

        rank as f64 / (count_with_priority - 1) as f64
    }

    fn as_any(&self) -> &dyn Any {
        self
    }
}
