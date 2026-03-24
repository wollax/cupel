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
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::model::ContextItemBuilder;

    #[test]
    fn priority_scorer_scores_in_range() {
        // 5 items with priorities 1..=5; all scores in [0.0, 1.0];
        // highest priority (5) → 1.0, lowest priority (1) → 0.0
        let items: Vec<ContextItem> = (1..=5)
            .map(|p| {
                ContextItemBuilder::new(p.to_string().as_str(), 5)
                    .priority(p)
                    .build()
                    .unwrap()
            })
            .collect();

        for item in &items {
            let score = PriorityScorer.score(item, &items);
            assert!(
                (0.0..=1.0).contains(&score),
                "score {score} out of range for priority {:?}",
                item.priority()
            );
        }

        // Item with priority 5 should score 1.0
        let top = PriorityScorer.score(&items[4], &items);
        assert_eq!(top, 1.0);

        // Item with priority 1 should score 0.0
        let bottom = PriorityScorer.score(&items[0], &items);
        assert_eq!(bottom, 0.0);
    }

    #[test]
    fn priority_scorer_item_without_priority() {
        // Item with no priority → score == 0.0
        let item = ContextItemBuilder::new("no-priority", 5).build().unwrap();
        let score = PriorityScorer.score(&item, std::slice::from_ref(&item));
        assert_eq!(score, 0.0);
    }
}
