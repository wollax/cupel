/// Integration tests for SelectionReportAssertionChain patterns 1–7.
///
/// Each pattern has two tests:
///   - `*_passes`: a real pipeline report that satisfies the condition → no panic
///   - `*_panics`: a real pipeline report that does NOT satisfy → expected panic
///
/// Mini-pipeline helpers produce `SelectionReport` via `DiagnosticTraceCollector`.
use std::collections::HashMap;

use cupel::{
    ContextBudget, ContextItemBuilder, ContextKind, DiagnosticTraceCollector, ExclusionReason,
    GreedySlice, Pipeline, PriorityScorer, RecencyScorer, TraceDetailLevel, UShapedPlacer,
};
use cupel_testing::SelectionReportAssertions;

// ── helpers ────────────────────────────────────────────────────────────────

fn make_pipeline() -> Pipeline {
    Pipeline::builder()
        .scorer(Box::new(RecencyScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(UShapedPlacer))
        .build()
        .expect("pipeline build failed")
}

/// Pipeline using PriorityScorer — items need `.priority(n)` to get distinct scores.
fn make_priority_pipeline() -> Pipeline {
    Pipeline::builder()
        .scorer(Box::new(PriorityScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(UShapedPlacer))
        .build()
        .expect("priority pipeline build failed")
}

/// Budget that fits `budget_tokens` worth of items.
fn budget(max_tokens: i64) -> ContextBudget {
    ContextBudget::new(max_tokens, max_tokens, 0, HashMap::new(), 0.0)
        .expect("budget construction failed")
}

/// Run the pipeline and return a `SelectionReport`.
fn run(
    pipeline: &Pipeline,
    items: &[cupel::ContextItem],
    budget: &ContextBudget,
) -> cupel::SelectionReport {
    let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
    pipeline
        .run_traced(items, budget, &mut collector)
        .expect("pipeline run failed");
    collector.into_report()
}

// ── Pattern 1: include_item_with_kind ──────────────────────────────────────

#[test]
fn include_item_with_kind_passes() {
    let pipeline = make_pipeline();
    let kind = ContextKind::new("Message").unwrap();
    let items = vec![ContextItemBuilder::new("hello world", 5)
        .kind(kind.clone())
        .build()
        .unwrap()];
    let report = run(&pipeline, &items, &budget(100));
    // Should not panic: there is one included item with kind=Message
    report.should().include_item_with_kind(kind);
}

#[test]
#[should_panic(expected = "include_item_with_kind(Document) failed")]
fn include_item_with_kind_panics() {
    let pipeline = make_pipeline();
    let msg_kind = ContextKind::new("Message").unwrap();
    let items = vec![ContextItemBuilder::new("hello world", 5)
        .kind(msg_kind)
        .build()
        .unwrap()];
    let report = run(&pipeline, &items, &budget(100));
    // Panics: no included item has kind=Document
    report
        .should()
        .include_item_with_kind(ContextKind::new("Document").unwrap());
}

// ── Pattern 2: include_item_matching ───────────────────────────────────────

#[test]
fn include_item_matching_passes() {
    let pipeline = make_pipeline();
    let items = vec![ContextItemBuilder::new("special-content", 5)
        .build()
        .unwrap()];
    let report = run(&pipeline, &items, &budget(100));
    report
        .should()
        .include_item_matching(|i| i.item.content() == "special-content");
}

#[test]
#[should_panic(expected = "include_item_matching failed")]
fn include_item_matching_panics() {
    let pipeline = make_pipeline();
    let items = vec![ContextItemBuilder::new("hello", 5).build().unwrap()];
    let report = run(&pipeline, &items, &budget(100));
    // Panics: no item has content "not-here"
    report
        .should()
        .include_item_matching(|i| i.item.content() == "not-here");
}

// ── Pattern 3: include_exact_n_items_with_kind ─────────────────────────────

#[test]
fn include_exact_n_items_with_kind_passes() {
    let pipeline = make_pipeline();
    let kind = ContextKind::new("Message").unwrap();
    let items = vec![
        ContextItemBuilder::new("msg1", 5)
            .kind(kind.clone())
            .build()
            .unwrap(),
        ContextItemBuilder::new("msg2", 5)
            .kind(kind.clone())
            .build()
            .unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(100));
    // Exactly 2 included items with kind=Message
    report.should().include_exact_n_items_with_kind(kind, 2);
}

#[test]
#[should_panic(expected = "include_exact_n_items_with_kind(Message, 5) failed")]
fn include_exact_n_items_with_kind_panics() {
    let pipeline = make_pipeline();
    let kind = ContextKind::new("Message").unwrap();
    let items = vec![ContextItemBuilder::new("msg1", 5)
        .kind(kind.clone())
        .build()
        .unwrap()];
    let report = run(&pipeline, &items, &budget(100));
    // Panics: only 1 item with kind=Message, not 5
    report.should().include_exact_n_items_with_kind(kind, 5);
}

// ── Pattern 4: exclude_item_with_reason ────────────────────────────────────

#[test]
fn exclude_item_with_reason_passes() {
    let pipeline = make_pipeline();
    // Small budget: the large item will be excluded with BudgetExceeded
    let items = vec![
        ContextItemBuilder::new("fits", 10).build().unwrap(),
        ContextItemBuilder::new("too-big", 1000).build().unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(20));
    // At least one excluded item has reason BudgetExceeded
    report
        .should()
        .exclude_item_with_reason(ExclusionReason::BudgetExceeded {
            item_tokens: 0,
            available_tokens: 0,
        });
}

#[test]
#[should_panic(expected = "exclude_item_with_reason(Deduplicated")]
fn exclude_item_with_reason_panics() {
    let pipeline = make_pipeline();
    let items = vec![
        ContextItemBuilder::new("fits", 5).build().unwrap(),
        ContextItemBuilder::new("too-big", 1000).build().unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(20));
    // Panics: no Deduplicated exclusion in this run
    report
        .should()
        .exclude_item_with_reason(ExclusionReason::Deduplicated {
            deduplicated_against: String::new(),
        });
}

// ── Pattern 5: exclude_item_matching_with_reason ───────────────────────────

#[test]
fn exclude_item_matching_with_reason_passes() {
    let pipeline = make_pipeline();
    let items = vec![
        ContextItemBuilder::new("small", 5).build().unwrap(),
        ContextItemBuilder::new("giant", 9999).build().unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(20));
    // The "giant" item is excluded with BudgetExceeded
    report
        .should()
        .exclude_item_matching_with_reason(
            |e| e.item.content() == "giant",
            ExclusionReason::BudgetExceeded {
                item_tokens: 0,
                available_tokens: 0,
            },
        );
}

#[test]
#[should_panic(expected = "exclude_item_matching_with_reason(reason=Deduplicated")]
fn exclude_item_matching_with_reason_panics() {
    let pipeline = make_pipeline();
    let items = vec![
        ContextItemBuilder::new("small", 5).build().unwrap(),
        ContextItemBuilder::new("giant", 9999).build().unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(20));
    // "giant" is excluded, but with BudgetExceeded, not Deduplicated
    report
        .should()
        .exclude_item_matching_with_reason(
            |e| e.item.content() == "giant",
            ExclusionReason::Deduplicated {
                deduplicated_against: String::new(),
            },
        );
}

// ── Pattern 6: have_excluded_item_with_budget_details ──────────────────────

#[test]
fn have_excluded_item_with_budget_details_passes() {
    let pipeline = make_pipeline();
    // Budget of 10 tokens; "giant" costs 500 tokens; after "small" (5 tokens) is included,
    // 5 tokens remain available → BudgetExceeded { item_tokens=500, available_tokens=5 }
    let items = vec![
        ContextItemBuilder::new("small", 5).build().unwrap(),
        ContextItemBuilder::new("giant", 500).build().unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(10));
    // Find the actual values from the report so the test is robust
    let excluded = &report.excluded;
    let budget_item = excluded
        .iter()
        .find(|e| matches!(e.reason, ExclusionReason::BudgetExceeded { .. }))
        .expect("expected a BudgetExceeded exclusion");
    let (actual_it, actual_at) =
        if let ExclusionReason::BudgetExceeded { item_tokens, available_tokens } = budget_item.reason {
            (item_tokens, available_tokens)
        } else {
            panic!("unexpected reason");
        };
    report
        .should()
        .have_excluded_item_with_budget_details(
            |e| e.item.content() == "giant",
            actual_it,
            actual_at,
        );
}

#[test]
#[should_panic(expected = "have_excluded_item_with_budget_details failed")]
fn have_excluded_item_with_budget_details_panics() {
    let pipeline = make_pipeline();
    let items = vec![
        ContextItemBuilder::new("small", 5).build().unwrap(),
        ContextItemBuilder::new("giant", 500).build().unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(10));
    // Wrong token values → should panic
    report
        .should()
        .have_excluded_item_with_budget_details(
            |e| e.item.content() == "giant",
            999_999, // wrong expected_item_tokens
            999_999, // wrong expected_available_tokens
        );
}

// ── Pattern 7: have_no_exclusions_for_kind ─────────────────────────────────

#[test]
fn have_no_exclusions_for_kind_passes() {
    let pipeline = make_pipeline();
    let msg_kind = ContextKind::new("Message").unwrap();
    let doc_kind = ContextKind::new("Document").unwrap();
    // Only a Document item is excluded; no Message items are excluded
    let items = vec![
        ContextItemBuilder::new("msg", 5)
            .kind(msg_kind.clone())
            .build()
            .unwrap(),
        ContextItemBuilder::new("big-doc", 9999)
            .kind(doc_kind)
            .build()
            .unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(20));
    // No Message items are excluded
    report.should().have_no_exclusions_for_kind(msg_kind);
}

#[test]
#[should_panic(expected = "have_no_exclusions_for_kind(Document) failed")]
fn have_no_exclusions_for_kind_panics() {
    let pipeline = make_pipeline();
    let doc_kind = ContextKind::new("Document").unwrap();
    let items = vec![
        ContextItemBuilder::new("msg", 5).build().unwrap(),
        ContextItemBuilder::new("big-doc", 9999)
            .kind(doc_kind.clone())
            .build()
            .unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(20));
    // Panics: there IS an excluded Document item
    report.should().have_no_exclusions_for_kind(doc_kind);
}

// ── Pattern 8: have_at_least_n_exclusions ──────────────────────────────────

#[test]
fn have_at_least_n_exclusions_passes() {
    let pipeline = make_pipeline();
    // Small budget: at least one item will be excluded
    let items = vec![
        ContextItemBuilder::new("small", 5).build().unwrap(),
        ContextItemBuilder::new("too-big", 9999).build().unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(20));
    // At least 1 excluded item
    report.should().have_at_least_n_exclusions(1);
}

#[test]
#[should_panic(expected = "have_at_least_n_exclusions(999) failed")]
fn have_at_least_n_exclusions_panics() {
    let pipeline = make_pipeline();
    let items = vec![
        ContextItemBuilder::new("small", 5).build().unwrap(),
        ContextItemBuilder::new("too-big", 9999).build().unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(20));
    // Panics: fewer than 999 excluded items
    report.should().have_at_least_n_exclusions(999);
}

// ── Pattern 9: excluded_items_are_sorted_by_score_descending ───────────────

#[test]
fn excluded_items_are_sorted_by_score_descending_passes() {
    let pipeline = make_pipeline();
    // Two items excluded (budget too small for both large ones),
    // ensuring we have ≥2 excluded items to check ordering.
    let items = vec![
        ContextItemBuilder::new("small", 5).build().unwrap(),
        ContextItemBuilder::new("big1", 9000).build().unwrap(),
        ContextItemBuilder::new("big2", 8000).build().unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(20));
    // DiagnosticTraceCollector always produces a sorted excluded list.
    // This is a conformance check: if sorting is broken, this panics.
    report
        .should()
        .have_at_least_n_exclusions(1)
        .excluded_items_are_sorted_by_score_descending();
}

#[test]
fn excluded_items_are_sorted_by_score_descending_vacuous_pass_on_zero_or_one() {
    let pipeline = make_pipeline();
    // Empty excluded list: vacuous truth, always passes.
    let items = vec![ContextItemBuilder::new("fits", 5).build().unwrap()];
    let report = run(&pipeline, &items, &budget(100));
    report.should().excluded_items_are_sorted_by_score_descending();
    // NOTE: A truly failing case (out-of-order excluded list) cannot be
    // constructed without direct SelectionReport construction (non_exhaustive).
    // The assertion logic checks adjacent pairs and panics on any score inversion;
    // correctness is validated here by confirming that a valid (always-sorted)
    // pipeline report passes and that the loop does not trip on 0 or 1 items.
}

// ── Pattern 10: have_budget_utilization_above ──────────────────────────────

#[test]
fn have_budget_utilization_above_passes() {
    let pipeline = make_pipeline();
    // Items fill 100 tokens; budget is 110 → utilization ≈ 0.909 > 0.5
    let items = vec![
        ContextItemBuilder::new("a", 50).build().unwrap(),
        ContextItemBuilder::new("b", 50).build().unwrap(),
    ];
    let b = budget(110);
    let report = run(&pipeline, &items, &b);
    report.should().have_budget_utilization_above(0.5, &b);
}

#[test]
#[should_panic(expected = "have_budget_utilization_above(0.9999) failed")]
fn have_budget_utilization_above_panics() {
    let pipeline = make_pipeline();
    // Only 5 tokens included in a 10000-token budget → utilization = 0.0005, well below 0.9999
    let items = vec![ContextItemBuilder::new("tiny", 5).build().unwrap()];
    let b = budget(10000);
    let report = run(&pipeline, &items, &b);
    report.should().have_budget_utilization_above(0.9999, &b);
}

// ── Pattern 11: have_kind_coverage_count ───────────────────────────────────

#[test]
fn have_kind_coverage_count_passes() {
    let pipeline = make_pipeline();
    let msg_kind = ContextKind::new("Message").unwrap();
    let doc_kind = ContextKind::new("Document").unwrap();
    let items = vec![
        ContextItemBuilder::new("msg", 5)
            .kind(msg_kind)
            .build()
            .unwrap(),
        ContextItemBuilder::new("doc", 5)
            .kind(doc_kind)
            .build()
            .unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(100));
    // 2 distinct kinds in included
    report.should().have_kind_coverage_count(2);
}

#[test]
#[should_panic(expected = "have_kind_coverage_count(99) failed")]
fn have_kind_coverage_count_panics() {
    let pipeline = make_pipeline();
    let items = vec![ContextItemBuilder::new("a", 5).build().unwrap()];
    let report = run(&pipeline, &items, &budget(100));
    // Panics: only 1 distinct kind, not 99
    report.should().have_kind_coverage_count(99);
}

// ── Pattern 12: place_item_at_edge ─────────────────────────────────────────

#[test]
fn place_item_at_edge_passes() {
    let pipeline = make_priority_pipeline();
    // UShapedPlacer puts the highest-scored item at position 0.
    // PriorityScorer assigns scores by priority value.
    // "first" has the highest priority → highest score → lands at position 0 (edge).
    let items = vec![
        ContextItemBuilder::new("first", 10).priority(30).build().unwrap(),
        ContextItemBuilder::new("second", 10).priority(20).build().unwrap(),
        ContextItemBuilder::new("third", 10).priority(10).build().unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(100));
    // The highest-scored item is at position 0 (edge)
    report
        .should()
        .place_item_at_edge(|i| i.item.content() == "first");
}

#[test]
#[should_panic(expected = "place_item_at_edge failed")]
fn place_item_at_edge_panics() {
    let pipeline = make_priority_pipeline();
    // 4 items: UShapedPlacer places top-scored at 0, 2nd at 3, 3rd at 1, 4th at 2.
    // "third" (priority 20) is the 3rd-highest → lands at position 1 (not an edge).
    let items = vec![
        ContextItemBuilder::new("first", 10).priority(40).build().unwrap(),  // score=1.0 → pos 0
        ContextItemBuilder::new("second", 10).priority(30).build().unwrap(), // score≈0.67 → pos 3
        ContextItemBuilder::new("third", 10).priority(20).build().unwrap(),  // score≈0.33 → pos 1
        ContextItemBuilder::new("fourth", 10).priority(10).build().unwrap(), // score=0.0 → pos 2
    ];
    let report = run(&pipeline, &items, &budget(100));
    // "third" lands at position 1 — not an edge position → panics
    report
        .should()
        .place_item_at_edge(|i| i.item.content() == "third");
}

// ── Pattern 13: place_top_n_scored_at_edges ────────────────────────────────

#[test]
fn place_top_n_scored_at_edges_n_zero_passes() {
    let pipeline = make_pipeline();
    let items = vec![ContextItemBuilder::new("a", 5).build().unwrap()];
    let report = run(&pipeline, &items, &budget(100));
    // n=0 always passes
    report.should().place_top_n_scored_at_edges(0);
}

#[test]
#[should_panic(expected = "place_top_n_scored_at_edges(99) failed: n=99 exceeds Included count=")]
fn place_top_n_scored_at_edges_n_exceeds_count_panics() {
    let pipeline = make_pipeline();
    let items = vec![ContextItemBuilder::new("a", 5).build().unwrap()];
    let report = run(&pipeline, &items, &budget(100));
    // Panics: n=99 > 1 included item
    report.should().place_top_n_scored_at_edges(99);
}

#[test]
fn place_top_n_scored_at_edges_n2_passes() {
    let pipeline = make_priority_pipeline();
    // 4 items with distinct priorities; UShapedPlacer places:
    //   top-scored  (priority=40, score=1.0)  → position 0
    //   2nd-scored  (priority=30, score≈0.67) → position 3
    //   3rd-scored  (priority=20, score≈0.33) → position 1
    //   4th-scored  (priority=10, score=0.0)  → position 2
    // So top-2 items should occupy positions 0 and 3 (the two edge positions).
    let items = vec![
        ContextItemBuilder::new("item0", 10).priority(40).build().unwrap(),
        ContextItemBuilder::new("item1", 10).priority(30).build().unwrap(),
        ContextItemBuilder::new("item2", 10).priority(20).build().unwrap(),
        ContextItemBuilder::new("item3", 10).priority(10).build().unwrap(),
    ];
    let report = run(&pipeline, &items, &budget(100));
    // Top-2 (item0 and item1) should occupy positions 0 and 3 (edges)
    report.should().place_top_n_scored_at_edges(2);
}
