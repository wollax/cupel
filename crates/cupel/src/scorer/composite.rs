use crate::CupelError;
use crate::model::ContextItem;
use crate::scorer::Scorer;

/// Composite scorer: weighted average of child scorers with normalized weights.
///
/// Validates at construction: at least one entry, all weights positive and finite.
///
/// Cycles are structurally impossible: children are stored as owned `Box<dyn Scorer>`,
/// so no two instances can share a child via reference — a scorer cannot reference its
/// own ancestor.
///
/// # Examples
///
/// ```
/// use cupel::{
///     ContextItemBuilder, ContextKind,
///     CompositeScorer, RecencyScorer, KindScorer, Scorer,
/// };
/// use chrono::Utc;
///
/// let scorer = CompositeScorer::new(vec![
///     (Box::new(RecencyScorer), 2.0),
///     (Box::new(KindScorer::with_default_weights()), 1.0),
/// ])?;
///
/// let items = vec![
///     ContextItemBuilder::new("recent", 5)
///         .kind(ContextKind::new("Message")?)
///         .timestamp(Utc::now())
///         .build()?,
///     ContextItemBuilder::new("older", 5)
///         .kind(ContextKind::new("Message")?)
///         .timestamp(Utc::now() - chrono::Duration::hours(1))
///         .build()?,
/// ];
///
/// let recent = scorer.score(&items[0], &items);
/// let older = scorer.score(&items[1], &items);
/// assert!(recent > older); // recency weight differentiates
/// # Ok::<(), cupel::CupelError>(())
/// ```
pub struct CompositeScorer {
    scorers: Vec<Box<dyn Scorer>>,
    normalized_weights: Vec<f64>,
}

impl CompositeScorer {
    /// Creates a new CompositeScorer from a list of (scorer, weight) pairs.
    ///
    /// Weights are normalized to sum to 1.0.
    pub fn new(entries: Vec<(Box<dyn Scorer>, f64)>) -> Result<Self, CupelError> {
        if entries.is_empty() {
            return Err(CupelError::ScorerConfig(
                "at least one scorer entry is required".to_owned(),
            ));
        }

        let mut total_weight = 0.0;
        for (_, weight) in &entries {
            if *weight <= 0.0 {
                return Err(CupelError::ScorerConfig(
                    "weight must be positive".to_owned(),
                ));
            }
            if !weight.is_finite() {
                return Err(CupelError::ScorerConfig("weight must be finite".to_owned()));
            }
            total_weight += weight;
        }

        let normalized_weights: Vec<f64> = entries.iter().map(|(_, w)| w / total_weight).collect();
        let scorers: Vec<Box<dyn Scorer>> = entries.into_iter().map(|(s, _)| s).collect();

        Ok(Self {
            scorers,
            normalized_weights,
        })
    }
}

impl Scorer for CompositeScorer {
    fn score(&self, item: &ContextItem, all_items: &[ContextItem]) -> f64 {
        let mut result = 0.0;
        for i in 0..self.scorers.len() {
            result += self.scorers[i].score(item, all_items) * self.normalized_weights[i];
        }
        result
    }
}

impl std::fmt::Debug for CompositeScorer {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("CompositeScorer")
            .field("num_scorers", &self.scorers.len())
            .field("normalized_weights", &self.normalized_weights)
            .finish()
    }
}
