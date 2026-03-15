#![doc = include_str!("../README.md")]
#![cfg_attr(docsrs, feature(doc_auto_cfg))]

pub mod error;
pub mod model;
pub mod pipeline;
pub mod placer;
pub mod scorer;
pub mod slicer;

pub use error::CupelError;
pub use model::{
    ContextBudget, ContextItem, ContextItemBuilder, ContextKind, ContextSource, OverflowStrategy,
    ScoredItem,
};
pub use pipeline::{Pipeline, PipelineBuilder};
pub use placer::{ChronologicalPlacer, Placer, UShapedPlacer};
pub use scorer::{
    CompositeScorer, FrequencyScorer, KindScorer, PriorityScorer, RecencyScorer, ReflexiveScorer,
    ScaledScorer, Scorer, TagScorer,
};
pub use slicer::{GreedySlice, KnapsackSlice, QuotaEntry, QuotaSlice, Slicer};
