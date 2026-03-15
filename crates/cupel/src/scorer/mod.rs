//! Scoring strategies that compute relevance for context items.
//!
//! A [`Scorer`] is a pure function: given identical inputs it must produce the same
//! output. Scores are conventionally in \[0.0, 1.0\] but this is not enforced —
//! [`ScaledScorer`] can be used to normalize arbitrary ranges.
//!
//! # Scorer comparison
//!
//! | Scorer | Strategy | Input | Use Case |
//! |--------|----------|-------|----------|
//! | [`RecencyScorer`] | Rank | `timestamp` | Favor recent conversation turns |
//! | [`PriorityScorer`] | Rank | `priority` | Honor explicit priority overrides |
//! | [`KindScorer`] | Absolute | `kind` | Weight system prompts above messages |
//! | [`TagScorer`] | Absolute | `tags` | Boost items matching configured tags |
//! | [`FrequencyScorer`] | Relative | `tags` | Promote items with common themes |
//! | [`ReflexiveScorer`] | Absolute | `future_relevance_hint` | Pass through external relevance signals |
//! | [`CompositeScorer`] | Weighted avg | child scorers | Combine multiple strategies |
//! | [`ScaledScorer`] | Min-max norm | inner scorer | Normalize scores to \[0, 1\] |
//!
//! **Rank** scorers compare each item against all peers (output depends on the full
//! set). **Absolute** scorers read a single field (output is independent of peers).
//! **Relative** scorers compute a proportion across the full set.
//!
//! # Example
//!
//! ```
//! use cupel::{
//!     ContextItemBuilder, ContextKind,
//!     CompositeScorer, RecencyScorer, KindScorer, Scorer,
//! };
//! use chrono::Utc;
//!
//! let scorer = CompositeScorer::new(vec![
//!     (Box::new(RecencyScorer), 2.0),
//!     (Box::new(KindScorer::with_default_weights()), 1.0),
//! ])?;
//!
//! let item = ContextItemBuilder::new("hello", 1)
//!     .kind(ContextKind::new("Message")?)
//!     .timestamp(Utc::now())
//!     .build()?;
//!
//! let score = scorer.score(&item, &[item.clone()]);
//! assert!(score >= 0.0);
//! # Ok::<(), cupel::CupelError>(())
//! ```

mod composite;
mod frequency;
mod kind;
mod priority;
mod recency;
mod reflexive;
mod scaled;
mod tag;

pub use composite::CompositeScorer;
pub use frequency::FrequencyScorer;
pub use kind::KindScorer;
pub use priority::PriorityScorer;
pub use recency::RecencyScorer;
pub use reflexive::ReflexiveScorer;
pub use scaled::ScaledScorer;
pub use tag::TagScorer;

use std::any::Any;

use crate::model::ContextItem;

/// A scorer computes a relevance score for a context item relative to all scoreable items.
///
/// Scorers are pure functions: given identical inputs, they must return the same output.
/// Scores are conventionally in [0.0, 1.0] but this is not enforced.
pub trait Scorer: Any + Send + Sync {
    /// Computes a relevance score for `item` given the full list of scoreable items.
    fn score(&self, item: &ContextItem, all_items: &[ContextItem]) -> f64;

    /// Returns `self` as `&dyn Any` for downcasting (used by cycle detection).
    #[doc(hidden)]
    fn as_any(&self) -> &dyn Any;
}
