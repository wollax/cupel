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
//! | [`DecayScorer`] | Absolute | `timestamp` | Decay score by age (exponential, window, or step curve) |
//! | [`MetadataTrustScorer`] | Absolute | `metadata["cupel:trust"]` | Read trust score from item metadata |
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
//! let items = vec![
//!     ContextItemBuilder::new("recent", 1)
//!         .kind(ContextKind::new("Message")?)
//!         .timestamp(Utc::now())
//!         .build()?,
//!     ContextItemBuilder::new("older", 1)
//!         .kind(ContextKind::new("Message")?)
//!         .timestamp(Utc::now() - chrono::Duration::hours(1))
//!         .build()?,
//! ];
//!
//! let recent_score = scorer.score(&items[0], &items);
//! let older_score = scorer.score(&items[1], &items);
//! assert!(recent_score > older_score); // recency weight dominates
//! # Ok::<(), cupel::CupelError>(())
//! ```

mod composite;
mod decay;
mod frequency;
mod kind;
mod metadata_trust;
mod priority;
mod recency;
mod reflexive;
mod scaled;
mod tag;

pub use composite::CompositeScorer;
pub use decay::{DecayCurve, DecayScorer, SystemTimeProvider, TimeProvider};
pub use frequency::FrequencyScorer;
pub use kind::KindScorer;
pub use metadata_trust::MetadataTrustScorer;
pub use priority::PriorityScorer;
pub use recency::RecencyScorer;
pub use reflexive::ReflexiveScorer;
pub use scaled::ScaledScorer;
pub use tag::TagScorer;

use crate::model::ContextItem;

/// A scorer computes a relevance score for a context item relative to all scoreable items.
///
/// Scorers are pure functions: given identical inputs, they must return the same output.
/// Scores are conventionally in \[0.0, 1.0\] but this is not enforced.
///
/// # Examples
///
/// ```
/// use cupel::{ContextItemBuilder, PriorityScorer, Scorer};
///
/// // All built-in scorers implement this trait
/// let scorer: Box<dyn Scorer> = Box::new(PriorityScorer);
///
/// let items = vec![
///     ContextItemBuilder::new("high", 5).priority(100).build()?,
///     ContextItemBuilder::new("low", 5).priority(1).build()?,
/// ];
/// let hi = scorer.score(&items[0], &items);
/// let lo = scorer.score(&items[1], &items);
/// assert!(hi > lo);
/// # Ok::<(), cupel::CupelError>(())
/// ```
pub trait Scorer: Send + Sync {
    /// Computes a relevance score for `item` given the full list of scoreable items.
    fn score(&self, item: &ContextItem, all_items: &[ContextItem]) -> f64;
}
