//! Integration tests for `Pipeline::get_marginal_items` and `Pipeline::find_min_budget_for`.

use std::collections::HashMap;

use cupel::{
    ChronologicalPlacer, ContextBudget, ContextItemBuilder, ContextKind, CountQuotaEntry,
    CountQuotaSlice, CupelError, GreedySlice, Pipeline, QuotaEntry, QuotaSlice, RecencyScorer,
};

fn greedy_pipeline() -> Pipeline {
    Pipeline::builder()
        .scorer(Box::new(RecencyScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .build()
        .unwrap()
}

fn quota_pipeline() -> Pipeline {
    let quotas = vec![QuotaEntry::new(ContextKind::new("msg").unwrap(), 10.0, 90.0).unwrap()];
    Pipeline::builder()
        .scorer(Box::new(RecencyScorer))
        .slicer(Box::new(
            QuotaSlice::new(quotas, Box::new(GreedySlice)).unwrap(),
        ))
        .placer(Box::new(ChronologicalPlacer))
        .build()
        .unwrap()
}

fn count_quota_pipeline() -> Pipeline {
    let entries = vec![CountQuotaEntry::new(ContextKind::new("msg").unwrap(), 1, 5).unwrap()];
    Pipeline::builder()
        .scorer(Box::new(RecencyScorer))
        .slicer(Box::new(
            CountQuotaSlice::new(entries, Box::new(GreedySlice), Default::default()).unwrap(),
        ))
        .placer(Box::new(ChronologicalPlacer))
        .build()
        .unwrap()
}

// ── get_marginal_items ───────────────────────────────────────────────────────

#[test]
fn get_marginal_items_basic() {
    let pipeline = greedy_pipeline();

    let now = chrono::Utc::now();
    let items = vec![
        ContextItemBuilder::new("small", 50)
            .timestamp(now)
            .build()
            .unwrap(),
        ContextItemBuilder::new("medium", 150)
            .timestamp(now - chrono::Duration::seconds(1))
            .build()
            .unwrap(),
        ContextItemBuilder::new("large", 300)
            .timestamp(now - chrono::Duration::seconds(2))
            .build()
            .unwrap(),
    ];

    // Full budget fits all 3 items (50 + 150 + 300 = 500)
    let budget = ContextBudget::new(500, 500, 0, HashMap::new(), 0.0).unwrap();

    // Reduce by 200 tokens — reduced budget = 300, can't fit all 3 items (500 total)
    let marginal = pipeline.get_marginal_items(&items, &budget, 200).unwrap();

    // With 500 target, all fit. With 300 target, only some items fit.
    // The pipeline scores by recency (most recent = highest score), then greedy by density.
    // At least one item should be marginal.
    assert!(
        !marginal.is_empty(),
        "expected at least one marginal item when budget is reduced by 200"
    );

    // Verify marginal items are items that were included at full budget
    let full_report = pipeline.dry_run(&items, &budget).unwrap();
    let full_contents: Vec<&str> = full_report
        .included
        .iter()
        .map(|i| i.item.content())
        .collect();
    for m in &marginal {
        assert!(
            full_contents.contains(&m.content()),
            "marginal item '{}' should have been in full-budget result",
            m.content()
        );
    }
}

#[test]
fn get_marginal_items_slack_zero() {
    let pipeline = greedy_pipeline();

    let items = vec![
        ContextItemBuilder::new("item", 100)
            .timestamp(chrono::Utc::now())
            .build()
            .unwrap(),
    ];
    let budget = ContextBudget::new(500, 500, 0, HashMap::new(), 0.0).unwrap();

    let marginal = pipeline.get_marginal_items(&items, &budget, 0).unwrap();
    assert!(
        marginal.is_empty(),
        "slack_tokens == 0 should return empty vec"
    );
}

#[test]
fn get_marginal_items_rejects_quota_slice() {
    let pipeline = quota_pipeline();

    let items = vec![
        ContextItemBuilder::new("item", 100)
            .kind(ContextKind::new("msg").unwrap())
            .timestamp(chrono::Utc::now())
            .build()
            .unwrap(),
    ];
    let budget = ContextBudget::new(500, 500, 0, HashMap::new(), 0.0).unwrap();

    let result = pipeline.get_marginal_items(&items, &budget, 50);
    match result {
        Err(CupelError::PipelineConfig(msg)) => {
            assert!(
                msg.contains("QuotaSlice"),
                "error message should mention QuotaSlice: {msg}"
            );
        }
        other => panic!("expected Err(PipelineConfig), got {other:?}"),
    }
}

// ── find_min_budget_for ──────────────────────────────────────────────────────

#[test]
fn find_min_budget_basic() {
    let pipeline = greedy_pipeline();

    let now = chrono::Utc::now();
    // Target item with 100 tokens. Create a competitor with higher score (more recent).
    let target = ContextItemBuilder::new("target-item", 100)
        .timestamp(now - chrono::Duration::seconds(10))
        .build()
        .unwrap();
    let high_scorer = ContextItemBuilder::new("high-scorer", 80)
        .timestamp(now)
        .build()
        .unwrap();

    let items = vec![high_scorer, target.clone()];

    // Budget covers all; find min budget for target
    let budget = ContextBudget::new(500, 500, 0, HashMap::new(), 0.0).unwrap();
    let result = pipeline
        .find_min_budget_for(&items, &budget, &target, 500)
        .unwrap();

    assert!(
        result.is_some(),
        "target should be findable within search ceiling"
    );
    let min_budget = result.unwrap();
    assert!(
        min_budget >= target.tokens() as i32,
        "min budget ({min_budget}) must be >= target tokens ({})",
        target.tokens()
    );

    // Verify: at the found budget, target is actually included
    let verify_budget =
        ContextBudget::new(min_budget as i64, min_budget as i64, 0, HashMap::new(), 0.0).unwrap();
    let report = pipeline.dry_run(&items, &verify_budget).unwrap();
    let found = report
        .included
        .iter()
        .any(|i| i.item.content() == "target-item");
    assert!(
        found,
        "target should be included at min budget {min_budget}"
    );
}

#[test]
fn find_min_budget_not_found() {
    let pipeline = greedy_pipeline();

    let now = chrono::Utc::now();
    // Target is 200 tokens but search ceiling is too low to include it alongside
    // a higher-scored item that also needs space.
    let target = ContextItemBuilder::new("target", 200)
        .timestamp(now - chrono::Duration::seconds(10))
        .build()
        .unwrap();
    let blocker = ContextItemBuilder::new("blocker", 150)
        .timestamp(now) // more recent = higher recency score
        .build()
        .unwrap();

    let items = vec![blocker, target.clone()];

    let budget = ContextBudget::new(1000, 1000, 0, HashMap::new(), 0.0).unwrap();
    // Search ceiling barely above target tokens — not enough room for both items
    // and target has lower score. The greedy slicer will pick blocker first.
    let result = pipeline
        .find_min_budget_for(&items, &budget, &target, 200)
        .unwrap();

    // At budget=200, only room for one item. Blocker (150 tokens, higher score) fits.
    // Target (200 tokens, lower score) won't be selected because blocker takes priority.
    // Greedy slicer selects by value density (score/tokens): blocker has higher score so
    // it gets picked first, consuming most of the budget. At budget=200, after picking
    // blocker (150), only 50 tokens remain — not enough for target (200).
    // So result should be None since blocker always takes priority up to ceiling=200.
    assert!(
        result.is_none(),
        "target should not be findable at ceiling=200 because blocker takes priority"
    );
}

#[test]
fn find_min_budget_rejects_quota_slice() {
    let pipeline = quota_pipeline();

    let target = ContextItemBuilder::new("target", 100)
        .kind(ContextKind::new("msg").unwrap())
        .timestamp(chrono::Utc::now())
        .build()
        .unwrap();
    let items = vec![target.clone()];
    let budget = ContextBudget::new(500, 500, 0, HashMap::new(), 0.0).unwrap();

    let result = pipeline.find_min_budget_for(&items, &budget, &target, 500);
    match result {
        Err(CupelError::PipelineConfig(msg)) => {
            assert!(
                msg.contains("QuotaSlice"),
                "error message should mention QuotaSlice: {msg}"
            );
        }
        other => panic!("expected Err(PipelineConfig), got {other:?}"),
    }
}

#[test]
fn find_min_budget_rejects_count_quota_slice() {
    let pipeline = count_quota_pipeline();

    let target = ContextItemBuilder::new("target", 100)
        .kind(ContextKind::new("msg").unwrap())
        .timestamp(chrono::Utc::now())
        .build()
        .unwrap();
    let items = vec![target.clone()];
    let budget = ContextBudget::new(500, 500, 0, HashMap::new(), 0.0).unwrap();

    let result = pipeline.find_min_budget_for(&items, &budget, &target, 500);
    match result {
        Err(CupelError::PipelineConfig(msg)) => {
            assert!(
                msg.contains("CountQuotaSlice"),
                "error message should mention CountQuotaSlice: {msg}"
            );
        }
        other => panic!("expected Err(PipelineConfig), got {other:?}"),
    }
}

#[test]
fn find_min_budget_target_not_in_items() {
    let pipeline = greedy_pipeline();

    let target = ContextItemBuilder::new("not-in-list", 100)
        .timestamp(chrono::Utc::now())
        .build()
        .unwrap();
    let items = vec![
        ContextItemBuilder::new("other", 100)
            .timestamp(chrono::Utc::now())
            .build()
            .unwrap(),
    ];
    let budget = ContextBudget::new(500, 500, 0, HashMap::new(), 0.0).unwrap();

    let result = pipeline.find_min_budget_for(&items, &budget, &target, 500);
    assert!(
        matches!(result, Err(CupelError::InvalidBudget(_))),
        "should return InvalidBudget when target not in items: {result:?}"
    );
}

#[test]
fn find_min_budget_ceiling_below_tokens() {
    let pipeline = greedy_pipeline();

    let target = ContextItemBuilder::new("big-item", 500)
        .timestamp(chrono::Utc::now())
        .build()
        .unwrap();
    let items = vec![target.clone()];
    let budget = ContextBudget::new(1000, 1000, 0, HashMap::new(), 0.0).unwrap();

    // search_ceiling (200) < target.tokens() (500)
    let result = pipeline.find_min_budget_for(&items, &budget, &target, 200);
    assert!(
        matches!(result, Err(CupelError::InvalidBudget(_))),
        "should return InvalidBudget when ceiling < target tokens: {result:?}"
    );
}
