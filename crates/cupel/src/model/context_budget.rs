use std::collections::HashMap;

#[cfg(feature = "serde")]
use serde::{Deserialize, Deserializer, Serialize, Serializer, ser::SerializeStruct};

use crate::CupelError;
use crate::model::ContextKind;

/// Token budget constraints that control how much context the pipeline can select.
///
/// All fields are validated at construction time — no invalid budget can exist at runtime.
///
/// # Examples
///
/// ```
/// use std::collections::HashMap;
/// use cupel::ContextBudget;
///
/// // A simple budget: 4096 max, 3000 target, 1024 reserved for output
/// let budget = ContextBudget::new(4096, 3000, 1024, HashMap::new(), 5.0)?;
///
/// assert_eq!(budget.max_tokens(), 4096);
/// assert_eq!(budget.target_tokens(), 3000);
/// assert_eq!(budget.output_reserve(), 1024);
///
/// // Invalid budgets are rejected at construction time
/// let err = ContextBudget::new(100, 200, 0, HashMap::new(), 0.0); // target 200 > max 100
/// assert!(err.is_err());
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Debug, Clone)]
pub struct ContextBudget {
    max_tokens: i64,
    target_tokens: i64,
    output_reserve: i64,
    reserved_slots: HashMap<ContextKind, i64>,
    estimation_safety_margin_percent: f64,
}

impl ContextBudget {
    /// Creates a new `ContextBudget`, validating all spec constraints.
    ///
    /// # Validation Rules
    /// 1. `max_tokens >= 0`
    /// 2. `target_tokens >= 0`
    /// 3. `target_tokens <= max_tokens`
    /// 4. `output_reserve >= 0`
    /// 5. `output_reserve <= max_tokens`
    /// 6. `estimation_safety_margin_percent` in `[0.0, 100.0]`
    /// 7. All `reserved_slots` values `>= 0`
    pub fn new(
        max_tokens: i64,
        target_tokens: i64,
        output_reserve: i64,
        reserved_slots: HashMap<ContextKind, i64>,
        estimation_safety_margin_percent: f64,
    ) -> Result<Self, CupelError> {
        if max_tokens < 0 {
            return Err(CupelError::InvalidBudget(format!(
                "max_tokens ({max_tokens}) must be >= 0"
            )));
        }
        if target_tokens < 0 {
            return Err(CupelError::InvalidBudget(format!(
                "target_tokens ({target_tokens}) must be >= 0"
            )));
        }
        if target_tokens > max_tokens {
            return Err(CupelError::InvalidBudget(format!(
                "target_tokens ({target_tokens}) must be <= max_tokens ({max_tokens})"
            )));
        }
        if output_reserve < 0 {
            return Err(CupelError::InvalidBudget(format!(
                "output_reserve ({output_reserve}) must be >= 0"
            )));
        }
        if output_reserve > max_tokens {
            return Err(CupelError::InvalidBudget(format!(
                "output_reserve ({output_reserve}) must be <= max_tokens ({max_tokens})"
            )));
        }
        if !(0.0..=100.0).contains(&estimation_safety_margin_percent) {
            return Err(CupelError::InvalidBudget(format!(
                "estimation_safety_margin_percent ({estimation_safety_margin_percent}) must be in [0.0, 100.0]"
            )));
        }
        for (kind, &count) in &reserved_slots {
            if count < 0 {
                return Err(CupelError::InvalidBudget(format!(
                    "reserved slot count for kind '{}' must be >= 0",
                    kind,
                )));
            }
        }

        Ok(Self {
            max_tokens,
            target_tokens,
            output_reserve,
            reserved_slots,
            estimation_safety_margin_percent,
        })
    }

    /// Hard ceiling: the model's context window size.
    pub fn max_tokens(&self) -> i64 {
        self.max_tokens
    }

    /// Soft goal: the slicer aims for this token count.
    pub fn target_tokens(&self) -> i64 {
        self.target_tokens
    }

    /// Tokens reserved for model output generation.
    pub fn output_reserve(&self) -> i64 {
        self.output_reserve
    }

    /// Minimum guaranteed items per kind.
    pub fn reserved_slots(&self) -> &HashMap<ContextKind, i64> {
        &self.reserved_slots
    }

    /// Percentage buffer for token estimation error.
    pub fn estimation_safety_margin_percent(&self) -> f64 {
        self.estimation_safety_margin_percent
    }
}

#[cfg(feature = "serde")]
impl Serialize for ContextBudget {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        let mut state = serializer.serialize_struct("ContextBudget", 5)?;
        state.serialize_field("max_tokens", &self.max_tokens)?;
        state.serialize_field("target_tokens", &self.target_tokens)?;
        state.serialize_field("output_reserve", &self.output_reserve)?;
        state.serialize_field("reserved_slots", &self.reserved_slots)?;
        state.serialize_field("estimation_safety_margin_percent", &self.estimation_safety_margin_percent)?;
        state.end()
    }
}

#[cfg(feature = "serde")]
impl<'de> Deserialize<'de> for ContextBudget {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        #[derive(Deserialize)]
        #[serde(deny_unknown_fields)]
        struct Raw {
            max_tokens: i64,
            target_tokens: i64,
            output_reserve: i64,
            #[serde(default)]
            reserved_slots: HashMap<ContextKind, i64>,
            estimation_safety_margin_percent: f64,
        }

        let raw = Raw::deserialize(deserializer)?;
        ContextBudget::new(
            raw.max_tokens,
            raw.target_tokens,
            raw.output_reserve,
            raw.reserved_slots,
            raw.estimation_safety_margin_percent,
        )
        .map_err(serde::de::Error::custom)
    }
}
