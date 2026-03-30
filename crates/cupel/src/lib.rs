#![doc = include_str!("../README.md")]
#![cfg_attr(docsrs, feature(doc_cfg))]

pub mod analytics;
pub mod diagnostics;
pub mod error;
pub mod model;
pub mod pipeline;
pub mod placer;
pub mod scorer;
pub mod slicer;

pub use analytics::{
    ItemStatus, KindQuotaUtilization, PolicySensitivityDiffEntry, PolicySensitivityReport,
    budget_utilization, kind_diversity, policy_sensitivity, policy_sensitivity_from_pipelines,
    quota_utilization, timestamp_coverage,
};
pub use diagnostics::{
    CountRequirementShortfall, DiagnosticTraceCollector, ExcludedItem, ExclusionReason,
    IncludedItem, InclusionReason, NullTraceCollector, OverflowEvent, PipelineStage,
    SelectionReport, StageTraceSnapshot, TraceCollector, TraceDetailLevel, TraceEvent,
};
pub use error::CupelError;
pub use model::{
    ContextBudget, ContextItem, ContextItemBuilder, ContextKind, ContextSource, OverflowStrategy,
    ParseContextKindError, ScoredItem,
};
pub use pipeline::{Pipeline, PipelineBuilder, Policy, PolicyBuilder};
pub use placer::{ChronologicalPlacer, Placer, UShapedPlacer};
pub use scorer::{
    CompositeScorer, DecayCurve, DecayScorer, FrequencyScorer, KindScorer, MetadataKeyScorer,
    MetadataTrustScorer, PriorityScorer, RecencyScorer, ReflexiveScorer, ScaledScorer, Scorer,
    SystemTimeProvider, TagScorer, TimeProvider,
};
pub use slicer::{
    CountConstrainedKnapsackSlice, CountQuotaEntry, CountQuotaSlice, GreedySlice, KnapsackSlice,
    QuotaConstraint, QuotaConstraintMode, QuotaEntry, QuotaPolicy, QuotaSlice, ScarcityBehavior,
    Slicer,
};
