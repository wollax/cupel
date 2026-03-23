#![doc = include_str!("../README.md")]
#![cfg_attr(docsrs, feature(doc_auto_cfg))]

pub mod diagnostics;
pub mod error;
pub mod model;
pub mod pipeline;
pub mod placer;
pub mod scorer;
pub mod slicer;

pub use diagnostics::{
    DiagnosticTraceCollector, ExcludedItem, ExclusionReason, IncludedItem, InclusionReason,
    NullTraceCollector, OverflowEvent, PipelineStage, SelectionReport, TraceCollector,
    TraceDetailLevel, TraceEvent,
};
pub use error::CupelError;
pub use model::{
    ContextBudget, ContextItem, ContextItemBuilder, ContextKind, ContextSource, OverflowStrategy,
    ParseContextKindError, ScoredItem,
};
pub use pipeline::{Pipeline, PipelineBuilder};
pub use placer::{ChronologicalPlacer, Placer, UShapedPlacer};
pub use scorer::{
    CompositeScorer, DecayCurve, DecayScorer, FrequencyScorer, KindScorer, PriorityScorer,
    RecencyScorer, ReflexiveScorer, ScaledScorer, Scorer, SystemTimeProvider, TagScorer,
    TimeProvider,
};
pub use slicer::{GreedySlice, KnapsackSlice, QuotaEntry, QuotaSlice, Slicer};
