use crate::error::CupelError;
use crate::model::ContextItem;
use crate::scorer::Scorer;

/// Absolute scorer: returns a configurable multiplier (`boost`) when a metadata key matches
/// a configured value, and `1.0` (the neutral multiplier) otherwise.
///
/// **Multiplicative semantics:** Unlike [`MetadataTrustScorer`](crate::MetadataTrustScorer)
/// (which returns an absolute score), `MetadataKeyScorer` returns a multiplier value intended
/// for use in a [`CompositeScorer`](crate::CompositeScorer). A match returns `config.boost`
/// (e.g., `1.5`); a non-match returns `1.0`. Returned values are NOT clamped to \[0.0, 1.0\].
///
/// # Construction
///
/// ```
/// use cupel::{MetadataKeyScorer, Scorer, ContextItemBuilder};
/// use std::collections::HashMap;
///
/// let scorer = MetadataKeyScorer::new("cupel:priority", "high", 1.5)?;
///
/// let mut meta = HashMap::new();
/// meta.insert("cupel:priority".to_owned(), "high".to_owned());
///
/// let item = ContextItemBuilder::new("priority item", 10)
///     .metadata(meta)
///     .build()?;
///
/// let score = scorer.score(&item, std::slice::from_ref(&item));
/// assert_eq!(score, 1.5);
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Debug, Clone)]
pub struct MetadataKeyScorer {
    key: String,
    value: String,
    boost: f64,
}

impl MetadataKeyScorer {
    /// Constructs a [`MetadataKeyScorer`] that returns `boost` for items where
    /// `metadata[key] == value`, and `1.0` for all other items.
    ///
    /// # Errors
    ///
    /// Returns [`CupelError::ScorerConfig`] if `boost` is not positive or is non-finite
    /// (i.e., `boost <= 0.0`, `boost` is NaN, or `boost` is infinite).
    pub fn new(
        key: impl Into<String>,
        value: impl Into<String>,
        boost: f64,
    ) -> Result<Self, CupelError> {
        if !boost.is_finite() || boost <= 0.0 {
            return Err(CupelError::ScorerConfig(
                "boost must be a finite value greater than 0.0".to_string(),
            ));
        }
        Ok(Self {
            key: key.into(),
            value: value.into(),
            boost,
        })
    }
}

impl Scorer for MetadataKeyScorer {
    fn score(&self, item: &ContextItem, _all_items: &[ContextItem]) -> f64 {
        match item.metadata().get(&self.key) {
            Some(v) if v == &self.value => self.boost,
            _ => 1.0,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::model::ContextItemBuilder;
    use std::collections::HashMap;

    fn item_with_meta(key: &str, value: &str) -> ContextItem {
        let mut meta = HashMap::new();
        meta.insert(key.to_owned(), value.to_owned());
        ContextItemBuilder::new("item", 1)
            .metadata(meta)
            .build()
            .unwrap()
    }

    fn item_no_meta() -> ContextItem {
        ContextItemBuilder::new("item", 1).build().unwrap()
    }

    #[test]
    fn match_returns_boost() {
        let scorer = MetadataKeyScorer::new("cupel:priority", "high", 1.5).unwrap();
        let item = item_with_meta("cupel:priority", "high");
        assert_eq!(scorer.score(&item, std::slice::from_ref(&item)), 1.5);
    }

    #[test]
    fn value_mismatch_returns_neutral() {
        let scorer = MetadataKeyScorer::new("cupel:priority", "high", 1.5).unwrap();
        let item = item_with_meta("cupel:priority", "normal");
        assert_eq!(scorer.score(&item, std::slice::from_ref(&item)), 1.0);
    }

    #[test]
    fn key_absent_returns_neutral() {
        let scorer = MetadataKeyScorer::new("cupel:priority", "high", 1.5).unwrap();
        let item = item_no_meta();
        assert_eq!(scorer.score(&item, std::slice::from_ref(&item)), 1.0);
    }

    #[test]
    fn zero_boost_errors() {
        assert!(MetadataKeyScorer::new("cupel:priority", "high", 0.0).is_err());
    }

    #[test]
    fn negative_boost_errors() {
        assert!(MetadataKeyScorer::new("cupel:priority", "high", -1.0).is_err());
    }

    #[test]
    fn nan_boost_errors() {
        assert!(MetadataKeyScorer::new("cupel:priority", "high", f64::NAN).is_err());
    }

    #[test]
    fn infinite_boost_errors() {
        assert!(MetadataKeyScorer::new("cupel:priority", "high", f64::INFINITY).is_err());
    }
}
