use crate::model::{ContextItem, ScoredItem};
use crate::placer::Placer;

/// Orders items by ascending timestamp, preserving natural temporal flow.
///
/// Timestamped items sort before null-timestamp items. Among timestamped items,
/// ordering is ascending by timestamp. Tiebreak is by original index (stable).
///
/// # Examples
///
/// ```
/// use cupel::{ContextItemBuilder, ScoredItem, ChronologicalPlacer, Placer};
/// use chrono::{Utc, Duration};
///
/// let now = Utc::now();
/// let items = vec![
///     ScoredItem {
///         item: ContextItemBuilder::new("later", 5)
///             .timestamp(now)
///             .build()?,
///         score: 0.5,
///     },
///     ScoredItem {
///         item: ContextItemBuilder::new("earlier", 5)
///             .timestamp(now - Duration::hours(1))
///             .build()?,
///         score: 0.9,
///     },
/// ];
///
/// let placed = ChronologicalPlacer.place(&items);
/// assert_eq!(placed[0].content(), "earlier");
/// assert_eq!(placed[1].content(), "later");
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub struct ChronologicalPlacer;

impl Placer for ChronologicalPlacer {
    fn place(&self, items: &[ScoredItem]) -> Vec<ContextItem> {
        if items.is_empty() {
            return Vec::new();
        }
        if items.len() == 1 {
            return vec![items[0].item.clone()];
        }

        // Build sortable array with original indices
        let mut sortable: Vec<usize> = (0..items.len()).collect();

        sortable.sort_by(|&a, &b| {
            let a_ts = items[a].item.timestamp();
            let b_ts = items[b].item.timestamp();

            match (a_ts, b_ts) {
                (Some(at), Some(bt)) => at.cmp(&bt).then_with(|| a.cmp(&b)),
                (Some(_), None) => std::cmp::Ordering::Less,
                (None, Some(_)) => std::cmp::Ordering::Greater,
                (None, None) => a.cmp(&b),
            }
        });

        sortable.iter().map(|&i| items[i].item.clone()).collect()
    }
}
