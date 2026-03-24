use std::collections::HashMap;
use std::sync::Arc;

use cupel::{
    ChronologicalPlacer, ContextBudget, ContextItemBuilder, ExclusionReason, GreedySlice,
    KnapsackSlice, OverflowStrategy, Pipeline, PolicyBuilder, PriorityScorer, ReflexiveScorer,
};

fn budget(target: i64, max: i64) -> ContextBudget {
    ContextBudget::new(max, target, 0, HashMap::new(), 0.0).unwrap()
}

/// A policy with `PriorityScorer` selects different items than a `ReflexiveScorer` pipeline
/// when priority and future_relevance_hint disagree under a tight budget.
#[test]
fn scorer_is_respected() {
    // 3 items × 40 tokens each. Budget target = 80 → fits exactly 2.
    // Item A: high future_relevance_hint (0.9), low priority (1)
    // Item B: medium future_relevance_hint (0.5), medium priority (5)
    // Item C: low future_relevance_hint (0.1), high priority (10)
    //
    // ReflexiveScorer (pipeline): ranks A > B > C by future_relevance_hint → includes A, B
    // PriorityScorer (policy): ranks C > B > A by priority → includes C, B
    let items = vec![
        ContextItemBuilder::new("item-a", 40)
            .future_relevance_hint(0.9)
            .priority(1)
            .build()
            .unwrap(),
        ContextItemBuilder::new("item-b", 40)
            .future_relevance_hint(0.5)
            .priority(5)
            .build()
            .unwrap(),
        ContextItemBuilder::new("item-c", 40)
            .future_relevance_hint(0.1)
            .priority(10)
            .build()
            .unwrap(),
    ];

    let tight_budget = budget(80, 120);

    // Host pipeline uses ReflexiveScorer.
    let pipeline = Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .build()
        .unwrap();

    // Policy overrides with PriorityScorer.
    let policy = PolicyBuilder::new()
        .scorer(Arc::new(PriorityScorer))
        .slicer(Arc::new(GreedySlice))
        .placer(Arc::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .build()
        .unwrap();

    let report = pipeline
        .dry_run_with_policy(&items, &tight_budget, &policy)
        .unwrap();

    // PriorityScorer picks C (priority 10) and B (priority 5) → item-a is excluded.
    assert_eq!(
        report.included.len(),
        2,
        "expected 2 included items, got {:?}",
        report
            .included
            .iter()
            .map(|i| i.item.content())
            .collect::<Vec<_>>()
    );

    let included_contents: Vec<&str> = report.included.iter().map(|i| i.item.content()).collect();
    assert!(
        included_contents.contains(&"item-c"),
        "PriorityScorer should include item-c (highest priority), got: {included_contents:?}"
    );
    assert!(
        included_contents.contains(&"item-b"),
        "PriorityScorer should include item-b (medium priority), got: {included_contents:?}"
    );

    let excluded_contents: Vec<&str> = report.excluded.iter().map(|i| i.item.content()).collect();
    assert!(
        excluded_contents.contains(&"item-a"),
        "PriorityScorer should exclude item-a (lowest priority), got: {excluded_contents:?}"
    );
}

/// A policy with `KnapsackSlice` selects a different pair than a `GreedySlice` pipeline
/// when item token weights make optimal packing differ from greedy first-fit.
#[test]
fn slicer_is_respected() {
    // Items (sorted by score descending — ReflexiveScorer uses future_relevance_hint):
    //   item-a: 50 tokens, relevance 0.9  ← greedy picks this first
    //   item-b: 30 tokens, relevance 0.8
    //   item-c: 50 tokens, relevance 0.7
    //
    // Budget target = 80 tokens.
    // GreedySlice: picks item-a (50) → remaining 30 → picks item-b (30) → total 80. ✓
    //   Result: [item-a, item-b], excludes item-c
    // KnapsackSlice: optimal is [item-a, item-b] = 80 OR [item-b, item-c] = 80.
    //   KnapsackSlice maximises total token utilisation; both pairs are equal at 80.
    //   But because KnapsackSlice maximises total weight (tokens), and both combos
    //   weigh 80, it could pick either. To force a difference we adjust:
    //
    // Adjusted:
    //   item-a: 60 tokens, relevance 0.9
    //   item-b: 30 tokens, relevance 0.8
    //   item-c: 50 tokens, relevance 0.7
    //   Budget target = 80.
    //   GreedySlice (sorted by score): item-a(60) fits → remaining 20 → item-b(30) doesn't fit
    //     → item-c(50) doesn't fit → Result: [item-a], excludes item-b, item-c
    //   KnapsackSlice: [item-b(30) + item-c(50)] = 80 tokens > [item-a alone = 60]
    //     → KnapsackSlice picks [item-b, item-c]  ✓ Different selection!
    let items = vec![
        ContextItemBuilder::new("item-a", 60)
            .future_relevance_hint(0.9)
            .build()
            .unwrap(),
        ContextItemBuilder::new("item-b", 30)
            .future_relevance_hint(0.8)
            .build()
            .unwrap(),
        ContextItemBuilder::new("item-c", 50)
            .future_relevance_hint(0.7)
            .build()
            .unwrap(),
    ];

    let tight_budget = budget(80, 120);

    // Host pipeline uses GreedySlice → selects only item-a (60 tokens).
    let pipeline = Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .build()
        .unwrap();

    // Policy overrides with KnapsackSlice (bucket_size=1 for exact token capacity) →
    // selects item-b + item-c (80 tokens total = better utilisation than item-a alone at 60).
    let policy = PolicyBuilder::new()
        .scorer(Arc::new(ReflexiveScorer))
        .slicer(Arc::new(KnapsackSlice::new(1).unwrap()))
        .placer(Arc::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .build()
        .unwrap();

    let report = pipeline
        .dry_run_with_policy(&items, &tight_budget, &policy)
        .unwrap();

    // KnapsackSlice should pick item-b + item-c (better total utilisation).
    let included_contents: Vec<&str> = report.included.iter().map(|i| i.item.content()).collect();
    assert!(
        included_contents.contains(&"item-b"),
        "KnapsackSlice should include item-b, got: {included_contents:?}"
    );
    assert!(
        included_contents.contains(&"item-c"),
        "KnapsackSlice should include item-c, got: {included_contents:?}"
    );
    assert!(
        !included_contents.contains(&"item-a"),
        "KnapsackSlice should exclude item-a (suboptimal alone), got: {included_contents:?}"
    );
}

/// With `deduplication: false`, two items with identical content are both included.
#[test]
fn deduplication_false_allows_duplicates() {
    let items = vec![
        ContextItemBuilder::new("duplicate-content", 30)
            .future_relevance_hint(0.8)
            .build()
            .unwrap(),
        ContextItemBuilder::new("duplicate-content", 30)
            .future_relevance_hint(0.7)
            .build()
            .unwrap(),
    ];

    let generous_budget = budget(100, 150);

    let pipeline = Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .build()
        .unwrap();

    // Policy explicitly disables deduplication.
    let policy = PolicyBuilder::new()
        .scorer(Arc::new(ReflexiveScorer))
        .slicer(Arc::new(GreedySlice))
        .placer(Arc::new(ChronologicalPlacer))
        .deduplication(false)
        .overflow_strategy(OverflowStrategy::Throw)
        .build()
        .unwrap();

    let report = pipeline
        .dry_run_with_policy(&items, &generous_budget, &policy)
        .unwrap();

    // Both identical-content items should appear in included.
    assert_eq!(
        report.included.len(),
        2,
        "deduplication=false should include both duplicate items, got included: {:?}, excluded: {:?}",
        report
            .included
            .iter()
            .map(|i| i.item.content())
            .collect::<Vec<_>>(),
        report
            .excluded
            .iter()
            .map(|i| i.item.content())
            .collect::<Vec<_>>(),
    );

    // No item should be excluded as Deduplicated.
    let has_dedup_exclusion = report
        .excluded
        .iter()
        .any(|e| matches!(e.reason, ExclusionReason::Deduplicated { .. }));
    assert!(
        !has_dedup_exclusion,
        "no item should be excluded as Deduplicated when deduplication=false"
    );
}

/// With `deduplication: true`, one of two identical-content items appears in
/// `report.excluded` with `ExclusionReason::Deduplicated`.
#[test]
fn deduplication_true_excludes_duplicates() {
    let items = vec![
        ContextItemBuilder::new("duplicate-content", 30)
            .future_relevance_hint(0.8)
            .build()
            .unwrap(),
        ContextItemBuilder::new("duplicate-content", 30)
            .future_relevance_hint(0.7)
            .build()
            .unwrap(),
    ];

    let generous_budget = budget(100, 150);

    let pipeline = Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .build()
        .unwrap();

    // Policy enables deduplication (this is the default, but explicit here).
    let policy = PolicyBuilder::new()
        .scorer(Arc::new(ReflexiveScorer))
        .slicer(Arc::new(GreedySlice))
        .placer(Arc::new(ChronologicalPlacer))
        .deduplication(true)
        .overflow_strategy(OverflowStrategy::Throw)
        .build()
        .unwrap();

    let report = pipeline
        .dry_run_with_policy(&items, &generous_budget, &policy)
        .unwrap();

    // Exactly one item is included (the higher-scored one).
    assert_eq!(
        report.included.len(),
        1,
        "deduplication=true should include only one of the two duplicate items, got: {:?}",
        report
            .included
            .iter()
            .map(|i| i.item.content())
            .collect::<Vec<_>>()
    );

    // Exactly one item is excluded as Deduplicated.
    let dedup_excluded: Vec<_> = report
        .excluded
        .iter()
        .filter(|e| matches!(e.reason, ExclusionReason::Deduplicated { .. }))
        .collect();

    assert_eq!(
        dedup_excluded.len(),
        1,
        "expected 1 item excluded as Deduplicated, got: {:?}",
        dedup_excluded
    );

    assert_eq!(
        dedup_excluded[0].item.content(),
        "duplicate-content",
        "the deduplicated item should have the duplicate content"
    );
}

/// A policy with `OverflowStrategy::Throw` returns an error when merged tokens
/// exceed the target; a policy with `OverflowStrategy::Truncate` succeeds.
#[test]
fn overflow_strategy_is_respected() {
    // Setup: pinned item (90 tokens) + one scoreable item (20 tokens).
    // Budget: target = 100, max = 200.
    // After classify: pinned_tokens = 90, effective budget for slicing = 100 - 90 = 10.
    // GreedySlice: scoreable item (20 tokens) does NOT fit in effective budget of 10 → sliced = [].
    // Merged = pinned(90) + sliced([]) = 90 tokens ≤ target(100). No overflow!
    //
    // That won't trigger overflow. Instead, we need pinned + sliced > target.
    // Use pinned(90) + scoreable(30). Effective budget for slicing = 100 - 90 = 10.
    // scoreable(30) doesn't fit in 10 → sliced = [].
    //
    // Actually, the Overflow error fires after merge when total > target. If GreedySlice
    // already excludes items that don't fit the effective budget, total will never
    // exceed target via slicing alone.
    //
    // The only path to CupelError::Overflow is: total merged tokens > target.
    // With GreedySlice this requires pinned overflow the target after sliced items added.
    // But classify already validates pinned ≤ max_tokens - output_reserve, not ≤ target.
    //
    // Solution: pinned = 90 tokens, two scoreable items each fitting individually in
    // effective budget (100 - 90 = 10 is too tight for any positive-token item ≥ 10+1).
    // Use target=80 and 2 scoreable items each 40 tokens, budget max=200, pinned=50.
    // Effective budget for slicing = 80 - 50 = 30. GreedySlice picks 1 × 40... nope, 40 > 30.
    //
    // Better: Use OverflowStrategy::Proceed as the "no error" baseline.
    // The Overflow error only fires in Throw mode when place detects total > target.
    // We need: after slicing, pinned + sliced > target.
    // pinned=0, scoreable=[item-a(60), item-b(60)], target=80, max=200.
    // KnapsackSlice might pick both if... no, target=80 and each is 60, KnapsackSlice
    // can't fit 2×60=120 > 80.
    //
    // The Overflow error is distinct from PinnedExceedsBudget. It fires in place.rs when
    // the MERGED total (pinned + sliced) exceeds target. With GreedySlice, this can't
    // happen unless there's a bug. But it CAN happen if we use OverflowStrategy::Proceed
    // and the slicer allows items exceeding the remaining capacity.
    //
    // Simpler: create a pipeline whose overflow_strategy is Truncate, and a policy
    // whose overflow_strategy is Throw — then find conditions that trigger the error.
    //
    // Actually the cleanest demonstration: 1 non-pinned item of 200 tokens, target=100.
    // GreedySlice: 200 > 100, item doesn't fit, sliced=[]. Merged=0. No overflow error.
    // The Overflow fires only when merged > target, and slicer prevents that.
    //
    // The task plan says "oversized pinned item". Let's use:
    // pinned item = 110 tokens, budget max=200, target=100, output_reserve=0.
    // classify: pinned_tokens(110) ≤ max(200) - reserve(0) = 200. OK.
    // place: merged = 110 > target 100 → Overflow with Throw; Truncate removes non-pinned
    //   (none to remove since only pinned), Proceed accepts it.
    // Wait, Truncate removes non-pinned items to bring merged ≤ target, but if pinned alone > target...
    // Let's check the Truncate code in place.rs.

    // Use: pinned=110, target=100, max=200. Throw → Overflow error. Truncate → succeeds (accepts pinned only).
    let pinned_item = ContextItemBuilder::new("big-pinned-item", 110)
        .pinned(true)
        .build()
        .unwrap();

    let items = vec![pinned_item];
    let tight_budget = budget(100, 200);

    // Host pipeline with Truncate (lenient) — used as the host for both policy calls.
    let pipeline = Pipeline::builder()
        .scorer(Box::new(ReflexiveScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Truncate)
        .build()
        .unwrap();

    // Policy A: Throw → should return Err when pinned(110) > target(100).
    let policy_throw = PolicyBuilder::new()
        .scorer(Arc::new(ReflexiveScorer))
        .slicer(Arc::new(GreedySlice))
        .placer(Arc::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Throw)
        .build()
        .unwrap();

    let result_throw = pipeline.dry_run_with_policy(&items, &tight_budget, &policy_throw);
    assert!(
        result_throw.is_err(),
        "OverflowStrategy::Throw should return Err when pinned tokens exceed target"
    );

    // Policy B: Truncate → should succeed even though pinned item exceeds target.
    let policy_truncate = PolicyBuilder::new()
        .scorer(Arc::new(ReflexiveScorer))
        .slicer(Arc::new(GreedySlice))
        .placer(Arc::new(ChronologicalPlacer))
        .overflow_strategy(OverflowStrategy::Truncate)
        .build()
        .unwrap();

    let result_truncate = pipeline.dry_run_with_policy(&items, &tight_budget, &policy_truncate);
    assert!(
        result_truncate.is_ok(),
        "OverflowStrategy::Truncate should succeed even when pinned item exceeds target, got: {:?}",
        result_truncate.err()
    );
}
