use crate::model::{ContextItem, ScoredItem};
use crate::placer::Placer;

/// Positions highest-scored items at both edges of the context window (start and
/// end), with lowest-scored items in the middle.
///
/// Exploits primacy and recency bias in LLMs by placing the most relevant items
/// where they receive the most attention.
///
/// # Examples
///
/// ```
/// use cupel::{ContextItemBuilder, ScoredItem, UShapedPlacer, Placer};
///
/// let items = vec![
///     ScoredItem { item: ContextItemBuilder::new("A", 5).build()?, score: 0.9 },
///     ScoredItem { item: ContextItemBuilder::new("B", 5).build()?, score: 0.1 },
///     ScoredItem { item: ContextItemBuilder::new("C", 5).build()?, score: 0.5 },
/// ];
///
/// let placed = UShapedPlacer.place(&items);
/// // Highest scores at edges, lowest in the middle
/// assert_eq!(placed[0].content(), "A"); // highest at start
/// assert_eq!(placed[2].content(), "C"); // second-highest at end
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub struct UShapedPlacer;

impl Placer for UShapedPlacer {
    fn place(&self, items: &[ScoredItem]) -> Vec<ContextItem> {
        if items.is_empty() {
            return Vec::new();
        }
        if items.len() == 1 {
            return vec![items[0].item.clone()];
        }

        // Step 1: Sort by score descending, tiebreak by index ascending
        let mut scored: Vec<(f64, usize)> = items
            .iter()
            .enumerate()
            .map(|(i, si)| (si.score, i))
            .collect();

        scored.sort_by(|a, b| b.0.total_cmp(&a.0).then_with(|| a.1.cmp(&b.1)));

        // Step 2: Alternate placement — even ranks go left (start), odd ranks go right (end).
        // Right-side items are accumulated in insertion order then reversed so that
        // higher-ranked items end up at the tail of the output.
        let mut left: Vec<ContextItem> = Vec::new();
        let mut right: Vec<ContextItem> = Vec::new();

        for (rank, &(_, orig_idx)) in scored.iter().enumerate() {
            let item = items[orig_idx].item.clone();
            if rank % 2 == 0 {
                left.push(item);
            } else {
                right.push(item);
            }
        }

        // Reverse right so the highest-ranked right item appears last
        right.reverse();
        left.into_iter().chain(right).collect()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::model::ContextItemBuilder;

    fn make_item(content: &str, score: f64) -> ScoredItem {
        ScoredItem {
            item: ContextItemBuilder::new(content, 5).build().unwrap(),
            score,
        }
    }

    #[test]
    fn place_zero_items() {
        let result = UShapedPlacer.place(&[]);
        assert!(result.is_empty());
    }

    #[test]
    fn place_one_item() {
        let items = vec![make_item("A", 0.9)];
        let result = UShapedPlacer.place(&items);
        assert_eq!(result.len(), 1);
        assert_eq!(result[0].content(), "A");
    }

    #[test]
    fn place_two_items() {
        // A(0.9) rank 0 → left[0], B(0.1) rank 1 → right → reversed → [B]
        // Output: [A, B]
        let items = vec![make_item("A", 0.9), make_item("B", 0.1)];
        let result = UShapedPlacer.place(&items);
        assert_eq!(result.len(), 2);
        assert_eq!(result[0].content(), "A"); // highest at start
        assert_eq!(result[1].content(), "B"); // lower at end
    }

    #[test]
    fn place_three_items() {
        // Sorted by score: A(0.9) rank 0, B(0.5) rank 1, C(0.1) rank 2
        // rank 0 (A) → left[0]
        // rank 1 (B) → right[0]
        // rank 2 (C) → left[1]
        // right.reverse() → [B]
        // Output: [A, C, B]
        let items = vec![
            make_item("A", 0.9),
            make_item("B", 0.5),
            make_item("C", 0.1),
        ];
        let result = UShapedPlacer.place(&items);
        assert_eq!(result.len(), 3);
        assert_eq!(result[0].content(), "A"); // rank 0 → left first
        assert_eq!(result[1].content(), "C"); // rank 2 → left second
        assert_eq!(result[2].content(), "B"); // rank 1 → right → end
    }

    #[test]
    fn place_four_items() {
        // Sorted: A(0.9) rank 0, B(0.7) rank 1, C(0.5) rank 2, D(0.1) rank 3
        // rank 0 (A) → left[0]
        // rank 1 (B) → right[0]
        // rank 2 (C) → left[1]
        // rank 3 (D) → right[1]
        // right = [B, D], right.reverse() = [D, B]
        // Output: [A, C, D, B]
        let items = vec![
            make_item("A", 0.9),
            make_item("B", 0.7),
            make_item("C", 0.5),
            make_item("D", 0.1),
        ];
        let result = UShapedPlacer.place(&items);
        assert_eq!(result.len(), 4);
        assert_eq!(result[0].content(), "A"); // highest at start
        assert_eq!(result[3].content(), "B"); // second-highest at end
    }
}
