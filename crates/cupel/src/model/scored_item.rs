use crate::model::ContextItem;

/// A value type associating a [`ContextItem`] with its computed relevance score.
#[derive(Debug, Clone)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
#[cfg_attr(feature = "serde", serde(deny_unknown_fields))]
pub struct ScoredItem {
    /// The context item.
    pub item: ContextItem,
    /// The computed relevance score (conventionally in [0.0, 1.0]).
    pub score: f64,
}
