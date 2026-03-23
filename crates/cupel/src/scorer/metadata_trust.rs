use crate::error::CupelError;
use crate::model::ContextItem;
use crate::scorer::Scorer;

/// Absolute scorer: reads `cupel:trust` from item metadata and returns it clamped to \[0.0, 1.0\].
///
/// Falls back to `default_score` when:
/// - the `cupel:trust` key is absent from the item's metadata
/// - the value cannot be parsed as an `f64`
/// - the parsed value is non-finite (NaN or ±infinity)
///
/// # Construction
///
/// ```
/// use cupel::{MetadataTrustScorer, Scorer, ContextItemBuilder};
/// use std::collections::HashMap;
///
/// let scorer = MetadataTrustScorer::new(0.5)?;
///
/// let mut meta = HashMap::new();
/// meta.insert("cupel:trust".to_owned(), "0.8".to_owned());
///
/// let item = ContextItemBuilder::new("trusted item", 10)
///     .metadata(meta)
///     .build()?;
///
/// let score = scorer.score(&item, std::slice::from_ref(&item));
/// assert_eq!(score, 0.8);
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Debug, Clone)]
pub struct MetadataTrustScorer {
    default_score: f64,
}

impl MetadataTrustScorer {
    /// Constructs a [`MetadataTrustScorer`] with the given `default_score`.
    ///
    /// # Errors
    ///
    /// Returns [`CupelError::ScorerConfig`] if `default_score` is outside `[0.0, 1.0]`.
    pub fn new(default_score: f64) -> Result<Self, CupelError> {
        if !(0.0..=1.0).contains(&default_score) {
            return Err(CupelError::ScorerConfig(
                "defaultScore must be in [0.0, 1.0]".to_string(),
            ));
        }
        Ok(Self { default_score })
    }
}

impl Scorer for MetadataTrustScorer {
    fn score(&self, item: &ContextItem, _all_items: &[ContextItem]) -> f64 {
        let raw = match item.metadata().get("cupel:trust") {
            Some(v) => v,
            None => return self.default_score,
        };

        let parsed = match raw.parse::<f64>() {
            Ok(v) => v,
            Err(_) => return self.default_score,
        };

        // NaN parses as Ok(NaN) — must check is_finite() after the parse call
        if !parsed.is_finite() {
            return self.default_score;
        }

        parsed.clamp(0.0, 1.0)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::model::ContextItemBuilder;
    use std::collections::HashMap;

    fn item_with_trust(value: &str) -> ContextItem {
        let mut meta = HashMap::new();
        meta.insert("cupel:trust".to_owned(), value.to_owned());
        ContextItemBuilder::new("item", 1)
            .metadata(meta)
            .build()
            .unwrap()
    }

    fn item_no_trust() -> ContextItem {
        ContextItemBuilder::new("item", 1).build().unwrap()
    }

    #[test]
    fn valid_trust_value() {
        let scorer = MetadataTrustScorer::new(0.5).unwrap();
        let item = item_with_trust("0.85");
        assert_eq!(scorer.score(&item, std::slice::from_ref(&item)), 0.85);
    }

    #[test]
    fn key_absent_returns_default() {
        let scorer = MetadataTrustScorer::new(0.5).unwrap();
        let item = item_no_trust();
        assert_eq!(scorer.score(&item, std::slice::from_ref(&item)), 0.5);
    }

    #[test]
    fn unparseable_returns_default() {
        let scorer = MetadataTrustScorer::new(0.5).unwrap();
        let item = item_with_trust("high");
        assert_eq!(scorer.score(&item, std::slice::from_ref(&item)), 0.5);
    }

    #[test]
    fn out_of_range_high_clamped() {
        let scorer = MetadataTrustScorer::new(0.5).unwrap();
        let item = item_with_trust("1.5");
        assert_eq!(scorer.score(&item, std::slice::from_ref(&item)), 1.0);
    }

    #[test]
    fn nan_returns_default() {
        let scorer = MetadataTrustScorer::new(0.5).unwrap();
        let item = item_with_trust("NaN");
        assert_eq!(scorer.score(&item, std::slice::from_ref(&item)), 0.5);
    }

    #[test]
    fn construction_out_of_range_errors() {
        assert!(MetadataTrustScorer::new(-0.1).is_err());
        assert!(MetadataTrustScorer::new(1.1).is_err());
    }

    #[test]
    fn construction_boundary_values_ok() {
        assert!(MetadataTrustScorer::new(0.0).is_ok());
        assert!(MetadataTrustScorer::new(1.0).is_ok());
    }
}
