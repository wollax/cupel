use std::collections::HashMap;

use crate::model::ScoredItem;

/// Removes items with duplicate content, keeping the highest-scoring instance.
///
/// Returns a `(survivors, excluded)` tuple. `excluded` contains the removed
/// duplicates so callers can record them for diagnostics.
///
/// Uses byte-exact ordinal comparison (no Unicode normalization, no case folding).
/// When scores are equal, the item with the lower original index wins.
/// If deduplication is disabled, returns the input unchanged with an empty excluded list.
pub(crate) fn deduplicate(
    scored: Vec<ScoredItem>,
    enabled: bool,
) -> (Vec<ScoredItem>, Vec<ScoredItem>) {
    if !enabled || scored.is_empty() {
        return (scored, vec![]);
    }

    // Map: content string -> index of best item
    let mut best_by_content: HashMap<String, usize> = HashMap::new();

    for (i, si) in scored.iter().enumerate() {
        let content = si.item.content().to_owned();
        match best_by_content.get(&content) {
            Some(&existing_idx) => {
                if si.score > scored[existing_idx].score {
                    best_by_content.insert(content, i);
                }
                // Equal score: keep lower index (existing), no action
            }
            None => {
                best_by_content.insert(content, i);
            }
        }
    }

    // Split into survivors (in original order) and excluded
    let (survivors, excluded): (Vec<_>, Vec<_>) = scored
        .into_iter()
        .enumerate()
        .partition(|(i, si)| best_by_content.get(si.item.content()) == Some(i));

    (
        survivors.into_iter().map(|(_, si)| si).collect(),
        excluded.into_iter().map(|(_, si)| si).collect(),
    )
}
