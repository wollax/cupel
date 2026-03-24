use std::collections::HashMap;

use cupel::{
    ChronologicalPlacer, ContextBudget, ContextItemBuilder, ContextKind, CountQuotaEntry,
    CountQuotaSlice, ExclusionReason, GreedySlice, OverflowStrategy, Pipeline, QuotaEntry,
    QuotaSlice, ReflexiveScorer, ScarcityBehavior,
};

fn kind(name: &str) -> ContextKind {
    ContextKind::new(name).unwrap()
}

fn budget(max: i64, target: i64) -> ContextBudget {
    ContextBudget::new(max, target, 0, HashMap::new(), 0.0).unwrap()
}

/// Proves the `CountQuotaSlice(inner: QuotaSlice(inner: GreedySlice))` composition.
///
/// Policy:
/// - Inner `QuotaSlice`: ToolOutput require 10%, cap 60% of target_tokens.
/// - Outer `CountQuotaSlice`: ToolOutput require 1, cap 2.
///
/// With 3 ToolOutput items (100 tokens each) and a 400-token budget, the percentage
/// quota allows up to 240 tokens (60%) of ToolOutput, which would admit all three.
/// But the count quota caps at 2 items, so at least one ToolOutput must be excluded
/// with `ExclusionReason::CountCapExceeded`.
#[test]
fn count_quota_composition_quota_slice_inner() {
    // Inner slicer: percentage-based quota.
    let quota_entry = QuotaEntry::new(kind("ToolOutput"), 10.0, 60.0).unwrap();
    let quota_slicer = QuotaSlice::new(vec![quota_entry], Box::new(GreedySlice)).unwrap();

    // Outer slicer: count-based quota wrapping the percentage-based quota.
    let count_entry = CountQuotaEntry::new(kind("ToolOutput"), 1, 2).unwrap();
    let slicer = CountQuotaSlice::new(
        vec![count_entry],
        Box::new(quota_slicer),
        ScarcityBehavior::Degrade,
    )
    .unwrap();

    let pipeline = Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(slicer))
        .placer(Box::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .build()
        .unwrap();

    // 3 ToolOutput items: 100 tokens each, descending relevance.
    // 2 Message items: 100 tokens each, descending relevance.
    let items = vec![
        ContextItemBuilder::new("tool-1", 100)
            .kind(kind("ToolOutput"))
            .future_relevance_hint(0.9)
            .build()
            .unwrap(),
        ContextItemBuilder::new("tool-2", 100)
            .kind(kind("ToolOutput"))
            .future_relevance_hint(0.7)
            .build()
            .unwrap(),
        ContextItemBuilder::new("tool-3", 100)
            .kind(kind("ToolOutput"))
            .future_relevance_hint(0.5)
            .build()
            .unwrap(),
        ContextItemBuilder::new("msg-1", 100)
            .kind(kind("Message"))
            .future_relevance_hint(0.8)
            .build()
            .unwrap(),
        ContextItemBuilder::new("msg-2", 100)
            .kind(kind("Message"))
            .future_relevance_hint(0.6)
            .build()
            .unwrap(),
    ];

    // 600-token budget fits all 5 items (500 tokens total); the count cap of 2 for
    // ToolOutput is the binding constraint, not budget exhaustion.
    let b = budget(600, 600);
    let report = pipeline.dry_run(&items, &b).unwrap();

    // Count cap = 2: at most 2 ToolOutput items may appear in `included`.
    let included_tool_count = report
        .included
        .iter()
        .filter(|i| i.item.kind() == &kind("ToolOutput"))
        .count();
    assert!(
        included_tool_count <= 2,
        "count cap=2 must hold; got {included_tool_count} ToolOutput items included"
    );

    // At least one item must be excluded due to the count cap.
    assert!(
        report
            .excluded
            .iter()
            .any(|e| matches!(e.reason, ExclusionReason::CountCapExceeded { .. })),
        "at least one item must be cap-excluded with CountCapExceeded; excluded reasons: {:?}",
        report
            .excluded
            .iter()
            .map(|e| &e.reason)
            .collect::<Vec<_>>()
    );
}
