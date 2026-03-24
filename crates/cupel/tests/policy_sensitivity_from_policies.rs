use cupel::{
    ChronologicalPlacer, ContextBudget, ContextItemBuilder, GreedySlice, KnapsackSlice,
    OverflowStrategy, Policy, PolicyBuilder, PriorityScorer, ReflexiveScorer, policy_sensitivity,
};
use std::collections::HashMap;
use std::sync::Arc;

// ── Helpers ───────────────────────────────────────────────────────────────────

fn budget(max_tokens: i64, target: i64) -> ContextBudget {
    ContextBudget::new(max_tokens, target, 0, HashMap::new(), 0.0).unwrap()
}

fn reflexive_policy() -> Policy {
    PolicyBuilder::new()
        .scorer(Arc::new(ReflexiveScorer))
        .slicer(Arc::new(GreedySlice))
        .placer(Arc::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .deduplication(true)
        .build()
        .unwrap()
}

fn priority_policy() -> Policy {
    PolicyBuilder::new()
        .scorer(Arc::new(PriorityScorer))
        .slicer(Arc::new(GreedySlice))
        .placer(Arc::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .deduplication(true)
        .build()
        .unwrap()
}

// ── Tests ─────────────────────────────────────────────────────────────────────

/// Both items swing: with a tight budget (fits 1), PolicyA (PriorityScorer) picks
/// item-a (high priority) and PolicyB (ReflexiveScorer) picks item-b (high relevance hint).
/// Both items must appear in diffs with opposing statuses.
#[test]
fn all_items_swing() {
    // item-a: high priority → PriorityScorer picks this one
    // item-b: high future_relevance_hint → ReflexiveScorer picks this one
    let items = vec![
        ContextItemBuilder::new("item-a", 40)
            .priority(10)
            .future_relevance_hint(0.1)
            .build()
            .unwrap(),
        ContextItemBuilder::new("item-b", 40)
            .priority(1)
            .future_relevance_hint(0.9)
            .build()
            .unwrap(),
    ];
    // Budget fits exactly 1 item (max_tokens=50, target=40)
    let budget = budget(50, 40);

    let policy_a = priority_policy(); // picks item-a (priority=10)
    let policy_b = reflexive_policy(); // picks item-b (relevance=0.9)

    let variants: Vec<(&str, &Policy)> = vec![("priority", &policy_a), ("reflexive", &policy_b)];
    let report = policy_sensitivity(&items, &budget, &variants).unwrap();

    // Both variants must have exactly 1 included item
    assert_eq!(
        report.variants[0].1.included.len(),
        1,
        "priority variant should include 1 item"
    );
    assert_eq!(
        report.variants[1].1.included.len(),
        1,
        "reflexive variant should include 1 item"
    );

    let priority_included = report.variants[0].1.included[0].item.content();
    let reflexive_included = report.variants[1].1.included[0].item.content();
    assert_eq!(
        priority_included, "item-a",
        "PriorityScorer should pick item-a"
    );
    assert_eq!(
        reflexive_included, "item-b",
        "ReflexiveScorer should pick item-b"
    );

    // Both items should be in diffs (each swings between included/excluded)
    assert_eq!(report.diffs.len(), 2, "both items should swing");
    let diff_contents: std::collections::HashSet<&str> =
        report.diffs.iter().map(|d| d.content.as_str()).collect();
    assert!(
        diff_contents.contains("item-a"),
        "item-a should be in diffs"
    );
    assert!(
        diff_contents.contains("item-b"),
        "item-b should be in diffs"
    );
}

/// No items swing: both variants use identical policy configs and ample budget —
/// every item is included by both. Diffs must be empty.
#[test]
fn no_items_swing() {
    let items = vec![
        ContextItemBuilder::new("item-x", 30)
            .future_relevance_hint(0.5)
            .build()
            .unwrap(),
        ContextItemBuilder::new("item-y", 30)
            .future_relevance_hint(0.7)
            .build()
            .unwrap(),
        ContextItemBuilder::new("item-z", 30)
            .future_relevance_hint(0.3)
            .build()
            .unwrap(),
    ];
    // Ample budget: fits all 3 items
    let budget = budget(200, 150);

    // Two identical policies
    let policy_1 = PolicyBuilder::new()
        .scorer(Arc::new(PriorityScorer))
        .slicer(Arc::new(GreedySlice))
        .placer(Arc::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .deduplication(true)
        .build()
        .unwrap();
    let policy_2 = PolicyBuilder::new()
        .scorer(Arc::new(PriorityScorer))
        .slicer(Arc::new(GreedySlice))
        .placer(Arc::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .deduplication(true)
        .build()
        .unwrap();

    let variants: Vec<(&str, &Policy)> = vec![("v1", &policy_1), ("v2", &policy_2)];
    let report = policy_sensitivity(&items, &budget, &variants).unwrap();

    // Both variants include all 3 items
    assert_eq!(
        report.variants[0].1.included.len(),
        3,
        "v1 should include all 3 items"
    );
    assert_eq!(
        report.variants[1].1.included.len(),
        3,
        "v2 should include all 3 items"
    );

    // No diffs — all items agree
    assert!(
        report.diffs.is_empty(),
        "diffs should be empty when policies are identical"
    );
}

/// Partial swing: 3 items (30 tokens each), budget fits 2.
/// PolicyA (ReflexiveScorer) picks the 2 with highest relevance hint.
/// PolicyB (PriorityScorer) picks the 2 with highest priority.
/// Items A and B have both high relevance and high priority → both policies pick them.
/// Item C has high relevance but low priority → only PolicyA picks it.
/// Item D has low relevance but high priority → only PolicyB picks it.
/// Wait, that's 4 items. Use 3 items where 1 is stable and 2 swing:
///   - "stable" (relevance=0.8, priority=10): included by both
///   - "swing-relevance" (relevance=0.7, priority=1): included by ReflexiveScorer only
///   - "swing-priority" (relevance=0.1, priority=9): included by PriorityScorer only
///     Budget = 70 tokens (fits 2 of 3 items at 30 tokens each)
#[test]
fn partial_swing() {
    let items = vec![
        ContextItemBuilder::new("stable", 30)
            .priority(10)
            .future_relevance_hint(0.8)
            .build()
            .unwrap(),
        ContextItemBuilder::new("swing-relevance", 30)
            .priority(1)
            .future_relevance_hint(0.7)
            .build()
            .unwrap(),
        ContextItemBuilder::new("swing-priority", 30)
            .priority(9)
            .future_relevance_hint(0.1)
            .build()
            .unwrap(),
    ];
    // Budget fits 2 items at 30 tokens each (max=70, target=60)
    // Use KnapsackSlice::new(1) for tight deterministic selection
    let budget = budget(70, 60);

    let policy_relevance = PolicyBuilder::new()
        .scorer(Arc::new(ReflexiveScorer))
        .slicer(Arc::new(KnapsackSlice::new(1).unwrap()))
        .placer(Arc::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .deduplication(true)
        .build()
        .unwrap();
    let policy_priority = PolicyBuilder::new()
        .scorer(Arc::new(PriorityScorer))
        .slicer(Arc::new(KnapsackSlice::new(1).unwrap()))
        .placer(Arc::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .deduplication(true)
        .build()
        .unwrap();

    let variants: Vec<(&str, &Policy)> = vec![
        ("relevance", &policy_relevance),
        ("priority", &policy_priority),
    ];
    let report = policy_sensitivity(&items, &budget, &variants).unwrap();

    // Each variant picks exactly 2 items
    assert_eq!(
        report.variants[0].1.included.len(),
        2,
        "relevance policy should include 2 items"
    );
    assert_eq!(
        report.variants[1].1.included.len(),
        2,
        "priority policy should include 2 items"
    );

    // "stable" should be included by both (high on both scorers)
    let relevance_included: std::collections::HashSet<&str> = report.variants[0]
        .1
        .included
        .iter()
        .map(|i| i.item.content())
        .collect();
    let priority_included: std::collections::HashSet<&str> = report.variants[1]
        .1
        .included
        .iter()
        .map(|i| i.item.content())
        .collect();

    assert!(
        relevance_included.contains("stable"),
        "stable should be in relevance policy"
    );
    assert!(
        priority_included.contains("stable"),
        "stable should be in priority policy"
    );
    assert!(
        relevance_included.contains("swing-relevance"),
        "swing-relevance picked by relevance"
    );
    assert!(
        priority_included.contains("swing-priority"),
        "swing-priority picked by priority"
    );
    assert!(
        !relevance_included.contains("swing-priority"),
        "swing-priority excluded by relevance"
    );
    assert!(
        !priority_included.contains("swing-relevance"),
        "swing-relevance excluded by priority"
    );

    // Diffs should contain exactly 2 items (swing-relevance and swing-priority)
    assert_eq!(report.diffs.len(), 2, "exactly 2 items should swing");
    let diff_contents: std::collections::HashSet<&str> =
        report.diffs.iter().map(|d| d.content.as_str()).collect();
    assert!(
        diff_contents.contains("swing-relevance"),
        "swing-relevance should be in diffs"
    );
    assert!(
        diff_contents.contains("swing-priority"),
        "swing-priority should be in diffs"
    );
    assert!(
        !diff_contents.contains("stable"),
        "stable should NOT be in diffs"
    );
}
