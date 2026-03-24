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
//! let selected = GreedySlice.slice(&items, &budget)?;
//!
//! assert_eq!(selected.len(), 1);
//! assert_eq!(selected[0].content(), "important");
//! # Ok::<(), cupel::CupelError>(())
//! ```

mod count_quota;
mod greedy;
mod knapsack;
mod quota;

pub use count_quota::{CountQuotaEntry, CountQuotaSlice, ScarcityBehavior};
pub use greedy::GreedySlice;
pub use knapsack::KnapsackSlice;
pub use quota::{QuotaEntry, QuotaSlice};

use crate::CupelError;
use crate::model::{ContextBudget, ContextItem, ContextKind, ScoredItem};

// ── QuotaPolicy abstraction ──────────────────────────────────────────────────

/// Whether a quota constraint is expressed as a percentage of the token budget
/// or as an absolute item count.
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum QuotaConstraintMode {
    /// Constraint values are percentages of the token budget (0.0–100.0).
    Percentage,
    /// Constraint values are absolute item counts (as `f64` for uniformity).
    Count,
}

/// A single per-kind quota constraint describing the require and cap thresholds.
///
/// For [`QuotaConstraintMode::Percentage`], `require` and `cap` are percentages (0–100).
/// For [`QuotaConstraintMode::Count`], they are absolute item counts (as `f64`).
#[derive(Debug, Clone, PartialEq)]
pub struct QuotaConstraint {
    /// The context kind this constraint applies to.
    pub kind: ContextKind,
    /// Whether the constraint is percentage-based or count-based.
    pub mode: QuotaConstraintMode,
    /// Minimum threshold (percentage or count).
    pub require: f64,
    /// Maximum threshold (percentage or count).
    pub cap: f64,
}

/// A trait for slicers that expose per-kind quota constraints.
///
/// Implemented by [`QuotaSlice`] (percentage-based) and [`CountQuotaSlice`]
/// (count-based). The returned constraints are consumed by analytics functions
/// such as `quota_utilization` to compute how fully each kind's quota is used.
pub trait QuotaPolicy {
    /// Returns all per-kind constraints configured on this slicer.
    fn quota_constraints(&self) -> Vec<QuotaConstraint>;
}

/// A slicer selects items from a sorted list to fit within a token budget.
///
/// Slicers receive items pre-sorted by score descending (from the Sort stage)
/// and return the subset of items that fits within the budget.
///
/// # Examples
///
/// ```
/// use cupel::{GreedySlice, KnapsackSlice, Slicer};
///
/// // All built-in slicers implement this trait
/// let _: Box<dyn Slicer> = Box::new(GreedySlice);
/// let _: Box<dyn Slicer> = Box::new(KnapsackSlice::new(1)?);
/// # Ok::<(), cupel::CupelError>(())
/// ```
pub trait Slicer: Send + Sync {
    /// Selects items from `sorted` that fit within `budget`.
    fn slice(
        &self,
        sorted: &[ScoredItem],
        budget: &ContextBudget,
    ) -> Result<Vec<ContextItem>, CupelError>;

    /// Returns `true` if this slicer is a [`KnapsackSlice`].
    ///
    /// Used by [`CountQuotaSlice`] to reject knapsack inner slicers at construction
    /// time, since the two-phase count algorithm is incompatible with knapsack's
    /// global optimization.
    ///
    /// The default implementation returns `false`.
    fn is_knapsack(&self) -> bool {
        false
    }

    /// Returns `true` if this slicer is a [`QuotaSlice`].
    ///
    /// Used by budget simulation methods to reject non-monotonic slicers.
    /// `QuotaSlice` produces non-monotonic inclusion as budget changes shift
    /// percentage allocations.
    ///
    /// The default implementation returns `false`.
    fn is_quota(&self) -> bool {
        false
    }

    /// Returns `true` if this slicer is a [`CountQuotaSlice`].
    ///
    /// Used by budget simulation methods to reject non-monotonic slicers.
    /// `CountQuotaSlice` produces non-monotonic inclusion as budget changes
    /// shift count allocations.
    ///
    /// The default implementation returns `false`.
    fn is_count_quota(&self) -> bool {
        false
    }

    /// Returns the per-kind count caps configured on this slicer, or an empty map
    /// if this slicer does not enforce count caps.
    ///
    /// Used by the pipeline's Slice stage to emit
    /// [`ExclusionReason::CountCapExceeded`](crate::ExclusionReason::CountCapExceeded)
    /// for items excluded due to a count cap rather than budget exhaustion.
    ///
    /// The default implementation returns an empty map.
    fn count_cap_map(&self) -> std::collections::HashMap<ContextKind, usize> {
        std::collections::HashMap::new()
    }
}
