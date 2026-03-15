//! Slicing strategies that select items to fit within a token budget.
//!
//! A [`Slicer`] receives items pre-sorted by score (descending) from the Sort
//! stage and returns the subset that fits within the [`ContextBudget`]. Different
//! slicers optimize for different trade-offs between speed and optimality.
//!
//! # Strategies
//!
//! - [`GreedySlice`] — Selects by value density (score/token). Fast, good default.
//! - [`KnapsackSlice`] — 0/1 knapsack DP for globally optimal selection. Slower.
//! - [`QuotaSlice`] — Partitions budget by [`ContextKind`](crate::ContextKind) with require/cap quotas,
//!   delegating per-kind selection to an inner slicer.
//!
//! # Example
//!
//! ```
//! use std::collections::HashMap;
//! use cupel::{ContextItemBuilder, ContextBudget, ScoredItem, GreedySlice, Slicer};
//!
//! let items = vec![
//!     ScoredItem {
//!         item: ContextItemBuilder::new("important", 100).build()?,
//!         score: 0.9,
//!     },
//!     ScoredItem {
//!         item: ContextItemBuilder::new("filler", 500).build()?,
//!         score: 0.1,
//!     },
//! ];
//!
//! let budget = ContextBudget::new(1000, 200, 0, HashMap::new(), 0.0)?;
//! let selected = GreedySlice.slice(&items, &budget);
//!
//! assert_eq!(selected.len(), 1);
//! assert_eq!(selected[0].content(), "important");
//! # Ok::<(), cupel::CupelError>(())
//! ```

mod greedy;
mod knapsack;
mod quota;

pub use greedy::GreedySlice;
pub use knapsack::KnapsackSlice;
pub use quota::{QuotaEntry, QuotaSlice};

use crate::model::{ContextBudget, ContextItem, ScoredItem};

/// A slicer selects items from a sorted list to fit within a token budget.
///
/// Slicers receive items pre-sorted by score descending (from the Sort stage)
/// and return the subset of items that fits within the budget.
pub trait Slicer: Send + Sync {
    /// Selects items from `sorted` that fit within `budget`.
    fn slice(&self, sorted: &[ScoredItem], budget: &ContextBudget) -> Vec<ContextItem>;
}
