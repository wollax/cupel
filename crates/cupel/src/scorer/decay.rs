//! Time-decay scoring: scores decay as items age.
//!
//! Three decay curves are available:
//!
//! - [`DecayCurve::Exponential`] — continuous half-life decay; a fresh item scores 1.0
//!   and the score halves every `half_life` duration.
//! - [`DecayCurve::Window`] — binary step; items younger than `max_age` score 1.0,
//!   older items score 0.0.
//! - [`DecayCurve::Step`] — piecewise-constant; a list of `(max_age, score)` windows
//!   is walked youngest-first; the first window whose `max_age` exceeds the item's age
//!   is used, falling through to the last window's score otherwise.
//!
//! # Example
//!
//! ```
//! use cupel::{DecayScorer, DecayCurve, SystemTimeProvider};
//! use chrono::Duration;
//!
//! let curve = DecayCurve::exponential(Duration::hours(24))?;
//! let scorer = DecayScorer::new(Box::new(SystemTimeProvider), curve, 0.0)?;
//! # Ok::<(), cupel::CupelError>(())
//! ```

use chrono::{DateTime, Duration, Utc};

use crate::error::CupelError;
use crate::model::ContextItem;
use crate::scorer::Scorer;

// ---------------------------------------------------------------------------
// TimeProvider trait
// ---------------------------------------------------------------------------

/// Provides the current wall-clock time used by [`DecayScorer`].
///
/// Implement this trait with a mock to make tests deterministic.
pub trait TimeProvider: Send + Sync {
    /// Returns the current UTC time.
    fn now(&self) -> DateTime<Utc>;
}

// ---------------------------------------------------------------------------
// SystemTimeProvider
// ---------------------------------------------------------------------------

/// A [`TimeProvider`] that delegates to `chrono::Utc::now()`.
///
/// This is a zero-sized type — it carries no state and is free to construct.
pub struct SystemTimeProvider;

impl TimeProvider for SystemTimeProvider {
    fn now(&self) -> DateTime<Utc> {
        Utc::now()
    }
}

// ---------------------------------------------------------------------------
// DecayCurve
// ---------------------------------------------------------------------------

/// The decay function applied to item age.
///
/// Construct variants via the associated constructors ([`DecayCurve::exponential`],
/// [`DecayCurve::window`], [`DecayCurve::step`]) so that preconditions are
/// enforced at creation time.
pub enum DecayCurve {
    /// Continuous exponential decay with the given half-life.
    ///
    /// `score = 2^(-(age / half_life))`
    Exponential(Duration),

    /// Binary window: items younger than `max_age` score 1.0; older items 0.0.
    Window(Duration),

    /// Piecewise-constant decay defined by `(max_age, score)` windows.
    ///
    /// Windows must be non-empty and each window must have a positive `max_age`.
    /// Windows are searched youngest-first; the last window's score is used as
    /// the fallback for items older than every window.
    Step(Vec<(Duration, f64)>),
}

impl DecayCurve {
    /// Constructs an [`Exponential`](DecayCurve::Exponential) curve.
    ///
    /// # Errors
    ///
    /// Returns [`CupelError::ScorerConfig`] if `half_life` is not strictly positive.
    pub fn exponential(half_life: Duration) -> Result<Self, CupelError> {
        if half_life <= Duration::zero() {
            return Err(CupelError::ScorerConfig(
                "halfLife must be strictly positive".to_string(),
            ));
        }
        Ok(Self::Exponential(half_life))
    }

    /// Constructs a [`Window`](DecayCurve::Window) curve.
    ///
    /// # Errors
    ///
    /// Returns [`CupelError::ScorerConfig`] if `max_age` is not strictly positive.
    pub fn window(max_age: Duration) -> Result<Self, CupelError> {
        if max_age <= Duration::zero() {
            return Err(CupelError::ScorerConfig(
                "maxAge must be strictly positive".to_string(),
            ));
        }
        Ok(Self::Window(max_age))
    }

    /// Constructs a [`Step`](DecayCurve::Step) curve.
    ///
    /// # Errors
    ///
    /// Returns [`CupelError::ScorerConfig`] if `windows` is empty or any entry
    /// has a non-positive `max_age`.
    pub fn step(windows: Vec<(Duration, f64)>) -> Result<Self, CupelError> {
        if windows.is_empty() {
            return Err(CupelError::ScorerConfig(
                "windows must be non-empty".to_string(),
            ));
        }
        for (age, _score) in &windows {
            if *age <= Duration::zero() {
                return Err(CupelError::ScorerConfig(
                    "windows: each maxAge must be strictly positive".to_string(),
                ));
            }
        }
        Ok(Self::Step(windows))
    }

    /// Computes the score for a clamped (non-negative) age in seconds.
    fn compute(&self, age: Duration) -> f64 {
        match self {
            DecayCurve::Exponential(half_life) => {
                let age_secs = age.num_milliseconds() as f64 / 1_000.0;
                let h_secs = half_life.num_milliseconds() as f64 / 1_000.0;
                2_f64.powf(-(age_secs / h_secs))
            }
            DecayCurve::Window(max_age) => {
                if age < *max_age {
                    1.0
                } else {
                    0.0
                }
            }
            DecayCurve::Step(windows) => {
                for (max_age, score) in windows {
                    if age < *max_age {
                        return *score;
                    }
                }
                // Fallthrough: use last window's score
                windows.last().map(|(_, s)| *s).unwrap_or(0.0)
            }
        }
    }
}

// ---------------------------------------------------------------------------
// DecayScorer
// ---------------------------------------------------------------------------

/// Scores items by how recently they were timestamped.
///
/// The scorer uses a pluggable [`TimeProvider`] so tests can inject a fixed
/// clock. Items without a timestamp receive `null_timestamp_score`.
///
/// # Construction
///
/// ```
/// use cupel::{DecayScorer, DecayCurve, SystemTimeProvider};
/// use chrono::Duration;
///
/// let curve = DecayCurve::exponential(Duration::hours(1))?;
/// let scorer = DecayScorer::new(Box::new(SystemTimeProvider), curve, 0.0)?;
/// # Ok::<(), cupel::CupelError>(())
/// ```
pub struct DecayScorer {
    provider: Box<dyn TimeProvider + Send + Sync>,
    curve: DecayCurve,
    null_timestamp_score: f64,
}

impl DecayScorer {
    /// Constructs a [`DecayScorer`].
    ///
    /// # Errors
    ///
    /// Returns [`CupelError::ScorerConfig`] if `null_timestamp_score` is outside
    /// `[0.0, 1.0]`.
    pub fn new(
        provider: Box<dyn TimeProvider + Send + Sync>,
        curve: DecayCurve,
        null_timestamp_score: f64,
    ) -> Result<Self, CupelError> {
        if !(0.0..=1.0).contains(&null_timestamp_score) {
            return Err(CupelError::ScorerConfig(
                "nullTimestampScore must be in [0.0, 1.0]".to_string(),
            ));
        }
        Ok(Self {
            provider,
            curve,
            null_timestamp_score,
        })
    }
}

impl Scorer for DecayScorer {
    fn score(&self, item: &ContextItem, _all_items: &[ContextItem]) -> f64 {
        let ts = match item.timestamp() {
            Some(ts) => ts,
            None => return self.null_timestamp_score,
        };

        let now = self.provider.now();
        let raw_age = now - ts;
        // Clamp before dispatch — no negative ages reach the curve
        let age = raw_age.max(Duration::zero());

        self.curve.compute(age)
    }
}
