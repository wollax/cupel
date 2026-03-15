use std::fmt;
use std::hash::{Hash, Hasher};

#[cfg(feature = "serde")]
use serde::{Deserialize, Deserializer, Serialize, Serializer};

use crate::CupelError;

/// An extensible string enumeration classifying the type of a context item.
///
/// Comparison is case-insensitive using ASCII case folding.
///
/// # Examples
///
/// ```
/// use cupel::ContextKind;
///
/// // Use well-known constants
/// let system = ContextKind::new(ContextKind::SYSTEM_PROMPT)?;
/// assert_eq!(system.as_str(), "SystemPrompt");
///
/// // Custom kinds are supported
/// let custom = ContextKind::new("Embedding")?;
/// assert_eq!(custom.as_str(), "Embedding");
///
/// // Comparison is case-insensitive
/// let a = ContextKind::new("message")?;
/// let b = ContextKind::new("Message")?;
/// assert_eq!(a, b);
///
/// // Default is "Message"
/// assert_eq!(ContextKind::default().as_str(), "Message");
/// # Ok::<(), cupel::CupelError>(())
/// ```
#[derive(Debug, Clone)]
pub struct ContextKind(String);

impl ContextKind {
    /// Well-known kind: conversational message (default).
    pub const MESSAGE: &str = "Message";
    /// Well-known kind: document or file content.
    pub const DOCUMENT: &str = "Document";
    /// Well-known kind: output from a tool invocation.
    pub const TOOL_OUTPUT: &str = "ToolOutput";
    /// Well-known kind: stored memory or fact.
    pub const MEMORY: &str = "Memory";
    /// Well-known kind: system-level instruction.
    pub const SYSTEM_PROMPT: &str = "SystemPrompt";

    /// Creates a new `ContextKind` from the given string.
    ///
    /// Rejects empty or whitespace-only strings.
    pub fn new(value: impl Into<String>) -> Result<Self, CupelError> {
        let s = value.into();
        if s.trim().is_empty() {
            return Err(CupelError::EmptyKind);
        }
        Ok(Self(s))
    }

    /// Creates a `ContextKind` from a well-known constant, bypassing validation.
    /// Only used internally for statically-known non-empty strings.
    pub(crate) fn from_static(value: &str) -> Self {
        Self(value.to_owned())
    }

    /// Returns the underlying string value.
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl Default for ContextKind {
    fn default() -> Self {
        Self(Self::MESSAGE.to_owned())
    }
}

impl PartialEq for ContextKind {
    fn eq(&self, other: &Self) -> bool {
        self.0.eq_ignore_ascii_case(&other.0)
    }
}

impl Eq for ContextKind {}

impl Hash for ContextKind {
    fn hash<H: Hasher>(&self, state: &mut H) {
        for byte in self.0.bytes() {
            state.write_u8(byte.to_ascii_lowercase());
        }
    }
}

impl fmt::Display for ContextKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.0)
    }
}

#[cfg(feature = "serde")]
impl Serialize for ContextKind {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        serializer.serialize_str(&self.0)
    }
}

#[cfg(feature = "serde")]
impl<'de> Deserialize<'de> for ContextKind {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        let s = String::deserialize(deserializer)?;
        ContextKind::new(s).map_err(serde::de::Error::custom)
    }
}
