//! Data types that flow through the cupel pipeline.
//!
//! This module defines the core value types used at every stage of the context
//! selection pipeline. Items enter as [`ContextItem`] instances, are scored into
//! [`ScoredItem`] records, and selected under a [`ContextBudget`].
//!
//! # Key types
//!
//! | Type | Purpose |
//! |------|---------|
//! | [`ContextItem`] | An immutable record representing a single piece of context |
//! | [`ContextItemBuilder`] | Builder for constructing `ContextItem` instances |
//! | [`ContextBudget`] | Token budget constraints for the pipeline |
//! | [`ContextKind`] | Extensible classification of context type (e.g. Message, Document) |
//! | [`ContextSource`] | Extensible identification of context origin (e.g. Chat, Tool) |
//! | [`OverflowStrategy`] | Controls behavior when selected items exceed the budget |
//! | [`ScoredItem`] | Associates a `ContextItem` with its computed relevance score |
//!
//! Timestamps use [`chrono::DateTime<Utc>`] for timezone-aware ordering.
//!
//! # Example
//!
//! ```
//! # use std::collections::HashMap;
//! use cupel::{ContextItemBuilder, ContextBudget, ContextKind};
//!
//! // Build a context item
//! let item = ContextItemBuilder::new("You are a helpful assistant.", 8)
//!     .kind(ContextKind::new("SystemPrompt")?)
//!     .build()?;
//!
//! assert_eq!(item.tokens(), 8);
//!
//! // Create a budget
//! let budget = ContextBudget::new(4096, 3000, 1024, HashMap::new(), 5.0)?;
//! assert_eq!(budget.max_tokens(), 4096);
//! # Ok::<(), cupel::CupelError>(())
//! ```

mod context_budget;
mod context_item;
mod context_kind;
mod context_source;
mod overflow_strategy;
mod scored_item;

pub use context_budget::ContextBudget;
pub use context_item::{ContextItem, ContextItemBuilder};
pub use context_kind::{ContextKind, ParseContextKindError};
pub use context_source::ContextSource;
pub use overflow_strategy::OverflowStrategy;
pub use scored_item::ScoredItem;
