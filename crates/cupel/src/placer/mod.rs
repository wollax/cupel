//! Placement strategies that determine final item ordering.
//!
//! A [`Placer`] receives the merged set of pinned and sliced items (with their
//! scores) and produces the final ordered list for the context window. Placement
//! is the last stage of the pipeline.
//!
//! # Strategies
//!
//! - [`ChronologicalPlacer`] — Orders items by ascending timestamp, preserving
//!   natural temporal flow. Best for chat-style contexts.
//! - [`UShapedPlacer`] — Places highest-scored items at both edges (start and end)
//!   of the context window, exploiting primacy/recency bias in LLMs.
//!
//! # Example
//!
//! ```
//! use cupel::{ContextItemBuilder, ScoredItem, ChronologicalPlacer, Placer};
//! use chrono::Utc;
//!
//! let items = vec![
//!     ScoredItem {
//!         item: ContextItemBuilder::new("first", 5)
//!             .timestamp(Utc::now())
//!             .build()?,
//!         score: 0.5,
//!     },
//! ];
//!
//! let placed = ChronologicalPlacer.place(&items);
//! assert_eq!(placed.len(), 1);
//! # Ok::<(), cupel::CupelError>(())
//! ```

mod chronological;
mod u_shaped;

pub use chronological::ChronologicalPlacer;
pub use u_shaped::UShapedPlacer;

use crate::model::{ContextItem, ScoredItem};

/// A placer determines the final presentation order of selected items.
///
/// Placers receive merged items (pinned + sliced) with their scores and
/// produce the final ordered list of context items.
///
/// # Examples
///
/// ```
/// use cupel::{ChronologicalPlacer, UShapedPlacer, Placer};
///
/// // All built-in placers implement this trait
/// let _: Box<dyn Placer> = Box::new(ChronologicalPlacer);
/// let _: Box<dyn Placer> = Box::new(UShapedPlacer);
/// ```
pub trait Placer: Send + Sync {
    /// Orders the given items for final presentation in the context window.
    fn place(&self, items: &[ScoredItem]) -> Vec<ContextItem>;
}
