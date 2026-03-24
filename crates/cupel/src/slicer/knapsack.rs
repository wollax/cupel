use crate::CupelError;
use crate::model::{ContextBudget, ContextItem, ScoredItem};
use crate::slicer::Slicer;

/// Selects items using 0/1 knapsack dynamic programming, finding the combination
/// that maximizes total score within the token budget.
///
/// Discretizes weights and capacity using a configurable bucket size for
/// feasible DP table sizes.
///
/// # Examples
///
/// ```
/// use std::collections::HashMap;
/// use cupel::{ContextItemBuilder, ContextBudget, ScoredItem, KnapsackSlice, Slicer};
///
/// let slicer = KnapsackSlice::with_default_bucket_size();
///
/// let items = vec![
///     ScoredItem {
///         item: ContextItemBuilder::new("high value", 100).build()?,
///         score: 0.9,
///     },
///     ScoredItem {
///         item: ContextItemBuilder::new("medium value", 100).build()?,
///         score: 0.5,
///     },
/// ];
///
/// let budget = ContextBudget::new(1000, 150, 0, HashMap::new(), 0.0)?;
/// let selected = slicer.slice(&items, &budget)?;
///
/// assert_eq!(selected.len(), 1);
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct KnapsackSlice {
    bucket_size: i64,
}

impl KnapsackSlice {
    /// Creates a new `KnapsackSlice` with the given bucket size.
    ///
    /// # Errors
    ///
    /// Returns `CupelError::SlicerConfig` if `bucket_size` is not positive.
    pub fn new(bucket_size: i64) -> Result<Self, CupelError> {
        if bucket_size <= 0 {
            return Err(CupelError::SlicerConfig(
                "bucket_size must be > 0".to_owned(),
            ));
        }
        Ok(Self { bucket_size })
    }

    /// Creates a new `KnapsackSlice` with the default bucket size of 100.
    pub fn with_default_bucket_size() -> Self {
        Self { bucket_size: 100 }
    }
}

impl Slicer for KnapsackSlice {
    fn is_knapsack(&self) -> bool {
        true
    }

    fn slice(
        &self,
        sorted: &[ScoredItem],
        budget: &ContextBudget,
    ) -> Result<Vec<ContextItem>, CupelError> {
        if sorted.is_empty() || budget.target_tokens() <= 0 {
            return Ok(Vec::new());
        }

        // Step 1: Pre-filter zero-token items (always included)
        let mut zero_token_items: Vec<ContextItem> = Vec::new();
        let mut candidates: Vec<&ScoredItem> = Vec::new();

        for si in sorted {
            match si.item.tokens().cmp(&0) {
                std::cmp::Ordering::Equal => zero_token_items.push(si.item.clone()),
                std::cmp::Ordering::Greater => candidates.push(si),
                std::cmp::Ordering::Less => {}
            }
        }

        if candidates.is_empty() {
            return Ok(zero_token_items);
        }

        let n = candidates.len();

        // Step 2: Build parallel arrays
        let mut weights = Vec::with_capacity(n);
        let mut values = Vec::with_capacity(n);

        for c in &candidates {
            weights.push(c.item.tokens());
            let v = (c.score * 10000.0).floor() as i64;
            values.push(v.max(0));
        }

        // Step 3: Discretize capacity and weights
        let capacity = (budget.target_tokens() / self.bucket_size) as usize;
        if capacity == 0 {
            return Ok(zero_token_items);
        }

        // Guard: reject table sizes that would cause OOM
        let cells = (capacity as u64) * (n as u64);
        if cells > 50_000_000 {
            return Err(CupelError::TableTooLarge {
                candidates: n,
                capacity,
                cells,
            });
        }

        let discretized_weights: Vec<usize> = weights
            .iter()
            .map(|&w| ((w + self.bucket_size - 1) / self.bucket_size) as usize)
            .collect();

        // Step 4: DP with 1D value array + flat keep table
        let mut dp = vec![0i64; capacity + 1];
        let stride = capacity + 1;
        let mut keep = vec![false; n * stride];

        for i in 0..n {
            let dw = discretized_weights[i];
            let dv = values[i];
            for w in (dw..=capacity).rev() {
                let with_item = dp[w - dw] + dv;
                if with_item > dp[w] {
                    dp[w] = with_item;
                    keep[i * stride + w] = true;
                }
            }
        }

        // Step 5: Reconstruct solution
        let mut selected: Vec<ContextItem> = Vec::new();
        let mut remaining_capacity = capacity;

        for i in (0..n).rev() {
            if keep[i * stride + remaining_capacity] {
                selected.push(candidates[i].item.clone());
                remaining_capacity -= discretized_weights[i];
            }
        }

        // Step 6: Combine zero-token items with selected items
        let mut result = zero_token_items;
        result.extend(selected);
        Ok(result)
    }
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use crate::model::{ContextBudget, ScoredItem};
    use crate::{ContextItemBuilder, CupelError};

    use super::KnapsackSlice;
    use crate::slicer::Slicer;

    #[test]
    fn knapsack_table_too_large() {
        // bucket_size=1 means capacity = target_tokens directly
        let slicer = KnapsackSlice::new(1).expect("bucket_size=1 is valid");

        // 1001 items × capacity 50_001 = 50_051_001 > 50_000_000
        let items: Vec<ScoredItem> = (0..1001)
            .map(|i| ScoredItem {
                item: ContextItemBuilder::new(format!("item-{i}"), 1)
                    .build()
                    .expect("valid item"),
                score: 0.5,
            })
            .collect();

        let budget =
            ContextBudget::new(100_000, 50_001, 0, HashMap::new(), 0.0).expect("valid budget");

        let result = slicer.slice(&items, &budget);
        match result {
            Err(CupelError::TableTooLarge {
                candidates,
                capacity,
                cells,
            }) => {
                assert_eq!(candidates, 1001);
                assert_eq!(capacity, 50_001);
                assert!(cells > 50_000_000, "cells={cells} should exceed limit");
            }
            other => panic!("expected Err(TableTooLarge), got {other:?}"),
        }
    }
}
