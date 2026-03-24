//! Structural equality tests for diagnostic types.
//!
//! The diagnostic structs are `#[non_exhaustive]`, so they cannot be constructed
//! via struct literals from outside the crate. These tests exercise equality by
//! running minimal pipelines through `DiagnosticTraceCollector` to produce real
//! instances, then comparing cloned values and structural differences.

use cupel::{
    ChronologicalPlacer, ContextBudget, ContextItemBuilder, DiagnosticTraceCollector,
    ExclusionReason, GreedySlice, InclusionReason, OverflowStrategy, Pipeline, ReflexiveScorer,
    SelectionReport, TraceDetailLevel,
};
use std::collections::HashMap;

fn build_pipeline(overflow: OverflowStrategy) -> Pipeline {
    Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .overflow_strategy(overflow)
        .build()
        .unwrap()
}

/// Run a minimal pipeline that includes one item, producing a SelectionReport.
fn make_report(content: &str, tokens: i64) -> SelectionReport {
    let item = ContextItemBuilder::new(content, tokens)
        .future_relevance_hint(0.5)
        .build()
        .unwrap();
    let budget = ContextBudget::new(1000, 800, 0, HashMap::new(), 0.0).unwrap();
    let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
    let pipeline = build_pipeline(OverflowStrategy::Throw);
    let _result = pipeline.run_traced(&[item], &budget, &mut collector);
    collector.into_report()
}

/// Run a pipeline with two items where one exceeds the budget.
fn make_report_with_exclusion(
    included_content: &str,
    included_tokens: i64,
    excluded_content: &str,
    excluded_tokens: i64,
) -> SelectionReport {
    let item1 = ContextItemBuilder::new(included_content, included_tokens)
        .future_relevance_hint(0.8)
        .build()
        .unwrap();
    let item2 = ContextItemBuilder::new(excluded_content, excluded_tokens)
        .future_relevance_hint(0.3)
        .build()
        .unwrap();
    // Budget fits only item1
    let budget =
        ContextBudget::new(included_tokens + 1, included_tokens + 1, 0, HashMap::new(), 0.0)
            .unwrap();
    let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
    let pipeline = build_pipeline(OverflowStrategy::Throw);
    let _result = pipeline.run_traced(&[item1, item2], &budget, &mut collector);
    collector.into_report()
}

/// Run an empty pipeline to produce a report with no items.
fn make_empty_report() -> SelectionReport {
    let budget = ContextBudget::new(1000, 800, 0, HashMap::new(), 0.0).unwrap();
    let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
    let pipeline = build_pipeline(OverflowStrategy::Throw);
    let _result = pipeline.run_traced(&[], &budget, &mut collector);
    collector.into_report()
}

// ── SelectionReport ───────────────────────────────────────────────────────────

#[test]
fn cloned_report_equals_original() {
    let r = make_report("hello", 10);
    let r2 = r.clone();
    assert_eq!(r, r2);
}

#[test]
fn cloned_empty_report_equals_original() {
    let r = make_empty_report();
    let r2 = r.clone();
    assert_eq!(r, r2);
}

#[test]
fn reports_differing_in_included_item_content_are_unequal() {
    let r1 = make_report("hello", 10);
    let r2 = make_report("world", 10);
    assert_ne!(r1.included, r2.included);
}

#[test]
fn reports_differing_in_excluded_item_are_unequal() {
    let r1 = make_report_with_exclusion("keep", 5, "drop-a", 9999);
    let r2 = make_report_with_exclusion("keep", 5, "drop-b", 9999);
    assert_ne!(r1.excluded, r2.excluded);
}

// ── IncludedItem (via clone equality) ─────────────────────────────────────────

#[test]
fn included_item_clone_equals_original() {
    let r = make_report("same content", 20);
    assert!(!r.included.is_empty());
    let item = &r.included[0];
    let item2 = item.clone();
    assert_eq!(*item, item2);
}

#[test]
fn included_items_different_context_item_are_unequal() {
    let r1 = make_report("content A", 20);
    let r2 = make_report("content B", 20);
    assert_ne!(r1.included[0].item, r2.included[0].item);
}

// ── ExcludedItem (via clone equality) ──────────────────────────────────────────

#[test]
fn excluded_item_clone_equals_original() {
    let r = make_report_with_exclusion("keep", 5, "drop", 9999);
    assert!(!r.excluded.is_empty());
    let item = &r.excluded[0];
    let item2 = item.clone();
    assert_eq!(*item, item2);
}

#[test]
fn excluded_items_different_content_are_unequal() {
    let r1 = make_report_with_exclusion("keep", 5, "drop-x", 9999);
    let r2 = make_report_with_exclusion("keep", 5, "drop-y", 9999);
    assert_ne!(r1.excluded[0].item, r2.excluded[0].item);
}

// ── TraceEvent (via clone equality) ───────────────────────────────────────────

#[test]
fn trace_event_clone_equals_original() {
    let r = make_report("hello", 10);
    assert!(!r.events.is_empty());
    let event = &r.events[0];
    let event2 = event.clone();
    assert_eq!(*event, event2);
}

#[test]
fn trace_events_from_different_pipelines_differ_in_item_count() {
    let r_empty = make_empty_report();
    let r_one = make_report("hello", 10);
    // Classify stage processes 0 items for empty vs 1 item for one-item pipeline
    let empty_counts: Vec<usize> = r_empty.events.iter().map(|e| e.item_count).collect();
    let one_counts: Vec<usize> = r_one.events.iter().map(|e| e.item_count).collect();
    assert_ne!(empty_counts, one_counts);
}

// ── CountRequirementShortfall (via clone equality) ────────────────────────────

#[test]
fn count_requirement_shortfalls_clone_equals_original() {
    let r = make_report("hello", 10);
    let shortfalls = r.count_requirement_shortfalls.clone();
    assert_eq!(r.count_requirement_shortfalls, shortfalls);
}

// ── OverflowEvent (via report clone equality with Proceed strategy) ───────────

#[test]
fn overflow_report_clone_equals_original() {
    let item = ContextItemBuilder::new("big content", 500)
        .future_relevance_hint(0.5)
        .build()
        .unwrap();
    let budget = ContextBudget::new(10, 10, 0, HashMap::new(), 0.0).unwrap();
    let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
    let pipeline = build_pipeline(OverflowStrategy::Proceed);
    let _result = pipeline.run_traced(&[item], &budget, &mut collector);
    let report = collector.into_report();
    let report2 = report.clone();
    assert_eq!(report, report2);
}

// ── ExclusionReason direct equality ───────────────────────────────────────────

#[test]
fn exclusion_reason_budget_exceeded_equality() {
    let a = ExclusionReason::BudgetExceeded {
        item_tokens: 100,
        available_tokens: 50,
    };
    let b = ExclusionReason::BudgetExceeded {
        item_tokens: 100,
        available_tokens: 50,
    };
    assert_eq!(a, b);

    let c = ExclusionReason::BudgetExceeded {
        item_tokens: 200,
        available_tokens: 50,
    };
    assert_ne!(a, c);
}

#[test]
fn exclusion_reason_different_variants_are_unequal() {
    let a = ExclusionReason::BudgetExceeded {
        item_tokens: 100,
        available_tokens: 50,
    };
    let b = ExclusionReason::NegativeTokens { tokens: -1 };
    assert_ne!(a, b);
}

// ── InclusionReason direct equality ───────────────────────────────────────────

#[test]
fn inclusion_reason_equality() {
    assert_eq!(InclusionReason::Scored, InclusionReason::Scored);
    assert_eq!(InclusionReason::Pinned, InclusionReason::Pinned);
    assert_ne!(InclusionReason::Scored, InclusionReason::Pinned);
}
