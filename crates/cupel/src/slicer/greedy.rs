use crate::CupelError;
use crate::model::{ContextBudget, ContextItem, ScoredItem};
use crate::slicer::Slicer;

/// Selects items by value density (score per token), greedily filling the budget
/// from highest-density items first.
///
/// Zero-token items have density `f64::MAX` and are always included.
///
/// **Tie-break contract:** When two items have equal value density, the item with
/// the lower original index in the input slice is preferred (original-index ascending).
/// This guarantees deterministic output for identical inputs — a requirement for
/// budget-simulation repeatability. Among zero-token items (all sharing `f64::MAX`
/// density), score values do NOT affect relative order; only the original index matters.
///
/// # Examples
///
/// ```
/// use std::collections::HashMap;
/// use cupel::{ContextItemBuilder, ContextBudget, ScoredItem, GreedySlice, Slicer};
///
/// let items = vec![
///     ScoredItem {
///         item: ContextItemBuilder::new("high value", 50).build()?,
///         score: 0.9,
///     },
///     ScoredItem {
///         item: ContextItemBuilder::new("low value", 200).build()?,
///         score: 0.1,
///     },
/// ];
///
/// let budget = ContextBudget::new(1000, 100, 0, HashMap::new(), 0.0)?;
/// let selected = GreedySlice.slice(&items, &budget)?;
///
/// assert_eq!(selected.len(), 1);
/// assert_eq!(selected[0].content(), "high value");
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub struct GreedySlice;

impl Slicer for GreedySlice {
    fn slice(&self, sorted: &[ScoredItem], budget: &ContextBudget) -> Result<Vec<ContextItem>, CupelError> {
        if sorted.is_empty() || budget.target_tokens() <= 0 {
            return Ok(Vec::new());
        }

        // Step 1: Compute value densities with original indices
        let mut densities: Vec<(f64, usize)> = sorted
            .iter()
            .enumerate()
            .map(|(i, si)| {
                let tokens = si.item.tokens();
                let density = if tokens == 0 {
                    f64::MAX
                } else {
                    si.score / tokens as f64
                };
                (density, i)
            })
            .collect();

        // Step 2: Stable sort by density descending, tiebreak by index ascending
        densities.sort_by(|a, b| b.0.total_cmp(&a.0).then_with(|| a.1.cmp(&b.1)));

        // Step 3: Greedy fill
        let mut remaining = budget.target_tokens();
        let mut selected = Vec::new();

        for &(_, orig_idx) in &densities {
            let item = &sorted[orig_idx].item;
            let tokens = item.tokens();

            if tokens == 0 {
                selected.push(item.clone());
            } else if tokens <= remaining {
                selected.push(item.clone());
                remaining -= tokens;
            }
        }

        Ok(selected)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ContextItemBuilder;
    use std::collections::HashMap;

    fn budget(target: i64) -> ContextBudget {
        ContextBudget::new(target * 2, target, 0, HashMap::new(), 0.0).unwrap()
    }

    fn item(content: &str, tokens: i64) -> ContextItem {
        ContextItemBuilder::new(content, tokens).build().unwrap()
    }

    fn scored(item: ContextItem, score: f64) -> ScoredItem {
        ScoredItem { item, score }
    }

    #[test]
    fn equal_density_preserves_input_order() {
        // All items have density = 0.01 (score / tokens)
        let a = item("A", 100);
        let b = item("B", 50);
        let c = item("C", 200);
        let items = vec![
            scored(a, 1.0),   // density = 1.0/100 = 0.01
            scored(b, 0.5),   // density = 0.5/50  = 0.01
            scored(c, 2.0),   // density = 2.0/200 = 0.01
        ];

        let result = GreedySlice.slice(&items, &budget(400)).unwrap();

        assert_eq!(result.len(), 3);
        assert_eq!(result[0].content(), "A");
        assert_eq!(result[1].content(), "B");
        assert_eq!(result[2].content(), "C");
    }

    #[test]
    fn zero_token_items_tied_preserves_input_order() {
        // All zero-token items share density f64::MAX — tiebreak must be input order
        let x = item("X", 0);
        let y = item("Y", 0);
        let z = item("Z", 0);
        let items = vec![
            scored(x, 0.3),
            scored(y, 0.9),
            scored(z, 0.1),
        ];

        let result = GreedySlice.slice(&items, &budget(50)).unwrap();

        assert_eq!(result.len(), 3);
        // Score must NOT affect order among zero-token items — only original index matters
        assert_eq!(result[0].content(), "X");
        assert_eq!(result[1].content(), "Y");
        assert_eq!(result[2].content(), "Z");
    }

    #[test]
    fn equal_density_budget_constraint_drops_last_in_input_order() {
        // Equal density, budget only fits 2 of 3 -> last in input order is dropped
        let a = item("first", 30);
        let b = item("second", 30);
        let c = item("third", 30);
        let items = vec![
            scored(a, 0.6),   // density = 0.6/30 = 0.02
            scored(b, 0.6),   // density = 0.6/30 = 0.02
            scored(c, 0.6),   // density = 0.6/30 = 0.02
        ];
        // Budget fits 60 tokens = 2 items
        let result = GreedySlice.slice(&items, &budget(60)).unwrap();

        assert_eq!(result.len(), 2);
        assert_eq!(result[0].content(), "first");
        assert_eq!(result[1].content(), "second");
    }

    #[test]
    fn deterministic_tiebreak_is_idempotent() {
        // Run the same equal-density scenario 10 times and confirm identical results
        for _ in 0..10 {
            let a = item("A", 20);
            let b = item("B", 20);
            let c = item("C", 20);
            let items = vec![
                scored(a, 0.4),
                scored(b, 0.4),
                scored(c, 0.4),
            ];
            // Budget fits 2 items (40 tokens)
            let result = GreedySlice.slice(&items, &budget(40)).unwrap();

            assert_eq!(result.len(), 2);
            assert_eq!(result[0].content(), "A");
            assert_eq!(result[1].content(), "B");
        }
    }
}
