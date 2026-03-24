use std::collections::HashMap;

use cupel::{
    ChronologicalPlacer, ContextBudget, ContextItemBuilder, ContextKind, CountQuotaEntry,
    CountQuotaSlice, GreedySlice, OverflowStrategy, Pipeline, QuotaConstraintMode, QuotaEntry,
    QuotaSlice, ReflexiveScorer, ScarcityBehavior, quota_utilization,
};

fn kind(name: &str) -> ContextKind {
    ContextKind::new(name).unwrap()
}

fn budget(max: i64, target: i64) -> ContextBudget {
    ContextBudget::new(max, target, 0, HashMap::new(), 0.0).unwrap()
}

/// A simple greedy pipeline — items all fit, so the report includes everything.
fn greedy_pipeline() -> Pipeline {
    Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .build()
        .unwrap()
}

// ── QuotaSlice (percentage mode) ──────────────────────────────────────────────

#[test]
fn quota_utilization_percentage_mode_two_kinds() {
    // Policy: Message require 20% cap 60%, SystemPrompt require 10% cap 40%.
    let policy = QuotaSlice::new(
        vec![
            QuotaEntry::new(kind("Message"), 20.0, 60.0).unwrap(),
            QuotaEntry::new(kind("SystemPrompt"), 10.0, 40.0).unwrap(),
        ],
        Box::new(GreedySlice),
    )
    .unwrap();

    let items = vec![
        ContextItemBuilder::new("msg-1", 300)
            .kind(kind("Message"))
            .future_relevance_hint(0.9)
            .build()
            .unwrap(),
        ContextItemBuilder::new("msg-2", 200)
            .kind(kind("Message"))
            .future_relevance_hint(0.8)
            .build()
            .unwrap(),
        ContextItemBuilder::new("sys-1", 100)
            .kind(kind("SystemPrompt"))
            .future_relevance_hint(0.7)
            .build()
            .unwrap(),
    ];

    // target_tokens = 1000, all items fit (600 total tokens).
    let b = budget(1000, 1000);
    let report = greedy_pipeline().dry_run(&items, &b).unwrap();

    let utils = quota_utilization(&report, &policy, &b);

    // One entry per constraint, sorted by kind name.
    assert_eq!(utils.len(), 2);

    // "Message" < "SystemPrompt" lexicographically.
    let msg = &utils[0];
    assert_eq!(msg.kind, kind("Message"));
    assert_eq!(msg.mode, QuotaConstraintMode::Percentage);
    assert_eq!(msg.require, 20.0);
    assert_eq!(msg.cap, 60.0);
    // actual = (300 + 200) / 1000 * 100 = 50.0%
    assert!(
        (msg.actual - 50.0).abs() < 0.01,
        "expected actual ~50.0, got {}",
        msg.actual
    );
    // utilization = 50.0 / 60.0 ≈ 0.833
    assert!(
        (msg.utilization - 50.0 / 60.0).abs() < 0.01,
        "expected utilization ~0.833, got {}",
        msg.utilization
    );

    let sys = &utils[1];
    assert_eq!(sys.kind, kind("SystemPrompt"));
    assert_eq!(sys.mode, QuotaConstraintMode::Percentage);
    assert_eq!(sys.require, 10.0);
    assert_eq!(sys.cap, 40.0);
    // actual = 100 / 1000 * 100 = 10.0%
    assert!(
        (sys.actual - 10.0).abs() < 0.01,
        "expected actual ~10.0, got {}",
        sys.actual
    );
    // utilization = 10.0 / 40.0 = 0.25
    assert!(
        (sys.utilization - 0.25).abs() < 0.01,
        "expected utilization ~0.25, got {}",
        sys.utilization
    );
}

// ── CountQuotaSlice (count mode) ──────────────────────────────────────────────

#[test]
fn quota_utilization_count_mode() {
    // Policy: Message require 1 cap 3, Tool require 0 cap 2.
    let policy = CountQuotaSlice::new(
        vec![
            CountQuotaEntry::new(kind("Message"), 1, 3).unwrap(),
            CountQuotaEntry::new(kind("Tool"), 0, 2).unwrap(),
        ],
        Box::new(GreedySlice),
        ScarcityBehavior::Degrade,
    )
    .unwrap();

    let items = vec![
        ContextItemBuilder::new("msg-1", 50)
            .kind(kind("Message"))
            .future_relevance_hint(0.9)
            .build()
            .unwrap(),
        ContextItemBuilder::new("msg-2", 50)
            .kind(kind("Message"))
            .future_relevance_hint(0.8)
            .build()
            .unwrap(),
        ContextItemBuilder::new("tool-1", 50)
            .kind(kind("Tool"))
            .future_relevance_hint(0.7)
            .build()
            .unwrap(),
    ];

    let b = budget(1000, 1000);
    let report = greedy_pipeline().dry_run(&items, &b).unwrap();

    let utils = quota_utilization(&report, &policy, &b);

    assert_eq!(utils.len(), 2);

    // "Message" < "Tool" lexicographically.
    let msg = &utils[0];
    assert_eq!(msg.kind, kind("Message"));
    assert_eq!(msg.mode, QuotaConstraintMode::Count);
    assert_eq!(msg.require, 1.0);
    assert_eq!(msg.cap, 3.0);
    // 2 message items included.
    assert!(
        (msg.actual - 2.0).abs() < f64::EPSILON,
        "expected actual 2.0, got {}",
        msg.actual
    );
    // utilization = 2 / 3 ≈ 0.667
    assert!(
        (msg.utilization - 2.0 / 3.0).abs() < 0.01,
        "expected utilization ~0.667, got {}",
        msg.utilization
    );

    let tool = &utils[1];
    assert_eq!(tool.kind, kind("Tool"));
    assert_eq!(tool.mode, QuotaConstraintMode::Count);
    assert_eq!(tool.actual, 1.0);
    // utilization = 1 / 2 = 0.5
    assert!(
        (tool.utilization - 0.5).abs() < f64::EPSILON,
        "expected utilization 0.5, got {}",
        tool.utilization
    );
}

// ── Edge cases ────────────────────────────────────────────────────────────────

#[test]
fn quota_utilization_empty_report_returns_zero() {
    let policy = QuotaSlice::new(
        vec![
            QuotaEntry::new(kind("Message"), 10.0, 50.0).unwrap(),
            QuotaEntry::new(kind("Tool"), 5.0, 30.0).unwrap(),
        ],
        Box::new(GreedySlice),
    )
    .unwrap();

    let b = budget(1000, 1000);
    let report = greedy_pipeline().dry_run(&[], &b).unwrap();

    let utils = quota_utilization(&report, &policy, &b);

    assert_eq!(utils.len(), 2);
    for u in &utils {
        assert_eq!(u.actual, 0.0, "expected actual 0.0 for kind {:?}", u.kind);
        assert_eq!(
            u.utilization, 0.0,
            "expected utilization 0.0 for kind {:?}",
            u.kind
        );
    }
}

#[test]
fn quota_utilization_kind_in_policy_absent_from_report() {
    // Policy has "Message" and "Tool" kinds, but only "Message" items exist.
    let policy = CountQuotaSlice::new(
        vec![
            CountQuotaEntry::new(kind("Message"), 1, 5).unwrap(),
            CountQuotaEntry::new(kind("Tool"), 0, 3).unwrap(),
        ],
        Box::new(GreedySlice),
        ScarcityBehavior::Degrade,
    )
    .unwrap();

    let items = vec![
        ContextItemBuilder::new("msg-1", 50)
            .kind(kind("Message"))
            .future_relevance_hint(0.9)
            .build()
            .unwrap(),
    ];

    let b = budget(1000, 1000);
    let report = greedy_pipeline().dry_run(&items, &b).unwrap();

    let utils = quota_utilization(&report, &policy, &b);

    assert_eq!(utils.len(), 2);

    // Tool kind present in policy but absent from report → actual = 0.0.
    let tool = utils.iter().find(|u| u.kind == kind("Tool")).unwrap();
    assert_eq!(tool.actual, 0.0);
    assert_eq!(tool.utilization, 0.0);

    // Message kind should have actual = 1.0 (one item).
    let msg = utils.iter().find(|u| u.kind == kind("Message")).unwrap();
    assert_eq!(msg.actual, 1.0);
    assert!(msg.utilization > 0.0);
}
