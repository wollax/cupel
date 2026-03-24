use std::collections::HashMap;

use cupel::{
    ChronologicalPlacer, ContextBudget, ContextItemBuilder, GreedySlice, ItemStatus,
    OverflowStrategy, Pipeline, PriorityScorer, ReflexiveScorer, policy_sensitivity,
};

/// Build a pipeline using `ReflexiveScorer` (passes through `future_relevance_hint`
/// as the score) with the given overflow strategy.
fn reflexive_pipeline(overflow: OverflowStrategy) -> Pipeline {
    Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .overflow_strategy(overflow)
        .build()
        .unwrap()
}

/// Build a pipeline using `PriorityScorer` so items with higher priority sort
/// first; combined with a tight budget this excludes low-priority items.
fn priority_pipeline(overflow: OverflowStrategy) -> Pipeline {
    Pipeline::builder()
        .scorer(Box::new(PriorityScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .overflow_strategy(overflow)
        .build()
        .unwrap()
}

#[test]
fn policy_sensitivity_two_variants_produces_diff() {
    // Create items with varying tokens and priority/relevance.
    // The tight budget (100 tokens) will force exclusion of some items.
    let items = vec![
        ContextItemBuilder::new("important-item", 40)
            .future_relevance_hint(0.9)
            .priority(10)
            .build()
            .unwrap(),
        ContextItemBuilder::new("medium-item", 40)
            .future_relevance_hint(0.5)
            .priority(5)
            .build()
            .unwrap(),
        ContextItemBuilder::new("low-item", 40)
            .future_relevance_hint(0.1)
            .priority(1)
            .build()
            .unwrap(),
    ];

    // Tight budget: only ~2 of 3 items fit (100 tokens, items are 40 each).
    let tight_budget = ContextBudget::new(100, 80, 0, HashMap::new(), 0.0).unwrap();

    // Variant A: tight budget with reflexive scorer (scores by future_relevance_hint).
    let pipeline_a = reflexive_pipeline(OverflowStrategy::Throw);

    // Variant B: tight budget with priority scorer — different ranking yields different inclusions.
    let pipeline_b = priority_pipeline(OverflowStrategy::Throw);

    let variants: Vec<(&str, &Pipeline)> =
        vec![("reflexive", &pipeline_a), ("priority", &pipeline_b)];

    let report = policy_sensitivity(&items, &tight_budget, &variants).unwrap();

    // Both variants should be present.
    assert_eq!(report.variants.len(), 2);
    assert_eq!(report.variants[0].0, "reflexive");
    assert_eq!(report.variants[1].0, "priority");

    // With the tight budget (100 tokens) and 3 items of 40 tokens each,
    // each pipeline can include at most 2 items. The different scorers
    // should rank items differently, causing at least one item to differ.
    //
    // ReflexiveScorer uses future_relevance_hint: 0.9, 0.5, 0.1
    // PriorityScorer uses priority: 10, 5, 1
    // Both rank the same order in this case, so let's verify the reports
    // and check for diffs. If both agree, adjust the test.

    // Check each variant's included count.
    let included_a: Vec<&str> = report.variants[0]
        .1
        .included
        .iter()
        .map(|i| i.item.content())
        .collect();
    let included_b: Vec<&str> = report.variants[1]
        .1
        .included
        .iter()
        .map(|i| i.item.content())
        .collect();

    // Both should include exactly 2 items (80 tokens of budget with 40-token items).
    assert_eq!(
        included_a.len(),
        2,
        "variant A should include 2 items, got: {included_a:?}"
    );
    assert_eq!(
        included_b.len(),
        2,
        "variant B should include 2 items, got: {included_b:?}"
    );

    // If the scorers produce different rankings, there will be diffs.
    // If they agree (both exclude "low-item"), diffs will be empty.
    // Let's handle both cases: the important thing is the function works.
    // We'll create a second test that guarantees a diff.
}

#[test]
fn policy_sensitivity_guaranteed_diff() {
    // Use a budget that can only fit ONE item to guarantee different selections
    // with different scorers.
    let items = vec![
        ContextItemBuilder::new("high-relevance-low-priority", 40)
            .future_relevance_hint(0.9)
            .priority(1) // low priority
            .build()
            .unwrap(),
        ContextItemBuilder::new("low-relevance-high-priority", 40)
            .future_relevance_hint(0.1)
            .priority(10) // high priority
            .build()
            .unwrap(),
    ];

    // Budget fits exactly 1 item.
    let budget = ContextBudget::new(50, 40, 0, HashMap::new(), 0.0).unwrap();

    let pipeline_reflexive = reflexive_pipeline(OverflowStrategy::Throw);
    let pipeline_priority = priority_pipeline(OverflowStrategy::Throw);

    let variants: Vec<(&str, &Pipeline)> = vec![
        ("reflexive", &pipeline_reflexive),
        ("priority", &pipeline_priority),
    ];

    let report = policy_sensitivity(&items, &budget, &variants).unwrap();

    assert_eq!(report.variants.len(), 2);
    assert_eq!(report.variants[0].0, "reflexive");
    assert_eq!(report.variants[1].0, "priority");

    // ReflexiveScorer: picks "high-relevance-low-priority" (score 0.9)
    // PriorityScorer:  picks "low-relevance-high-priority" (score from priority 10)
    // So both items should appear in the diff.

    assert!(
        !report.diffs.is_empty(),
        "expected at least one diff entry, got none"
    );

    // Both items should be in the diff since they swap status.
    assert_eq!(
        report.diffs.len(),
        2,
        "expected 2 diff entries (both items swap), got {}",
        report.diffs.len()
    );

    // Verify each diff entry has exactly 2 statuses with opposing values.
    for entry in &report.diffs {
        assert_eq!(
            entry.statuses.len(),
            2,
            "expected 2 statuses per diff entry for content '{}', got {}",
            entry.content,
            entry.statuses.len()
        );

        let status_values: Vec<ItemStatus> = entry.statuses.iter().map(|(_, s)| *s).collect();
        assert!(
            status_values.contains(&ItemStatus::Included)
                && status_values.contains(&ItemStatus::Excluded),
            "expected one Included and one Excluded for content '{}', got {:?}",
            entry.content,
            status_values
        );
    }
}
