use std::collections::HashMap;

use cupel::{
    ContextBudget, DiagnosticTraceCollector, ExclusionReason, OverflowStrategy, Pipeline,
    TraceDetailLevel,
};

use super::{
    assert_ordered_eq, build_items, build_placer_by_type, build_scorer_by_type,
    build_slicer_by_type, load_vector,
};

fn build_pipeline_from_config(vector: &toml::Value) -> Pipeline {
    let config = vector.get("config").expect("missing [config]");

    // Build scorer(s)
    let scorers_arr = config
        .get("scorers")
        .and_then(|v| v.as_array())
        .expect("missing config.scorers");

    let scorer: Box<dyn cupel::Scorer> = if scorers_arr.len() == 1 {
        let entry = &scorers_arr[0];
        let scorer_type = entry["type"].as_str().expect("scorer missing type");
        build_scorer_by_type(scorer_type, None)
    } else {
        let entries: Vec<(Box<dyn cupel::Scorer>, f64)> = scorers_arr
            .iter()
            .map(|entry| {
                let scorer_type = entry["type"].as_str().expect("scorer missing type");
                let weight = entry["weight"]
                    .as_float()
                    .or_else(|| entry["weight"].as_integer().map(|i| i as f64))
                    .expect("scorer missing weight");
                (build_scorer_by_type(scorer_type, None), weight)
            })
            .collect();
        Box::new(cupel::CompositeScorer::new(entries).unwrap())
    };

    // Build slicer
    let slicer_type = config["slicer"].as_str().expect("missing config.slicer");
    let slicer = build_slicer_by_type(slicer_type, Some(config));

    // Build placer
    let placer_type = config["placer"].as_str().expect("missing config.placer");
    let placer = build_placer_by_type(placer_type);

    // Deduplication
    let deduplication = config
        .get("deduplication")
        .and_then(|v| v.as_bool())
        .unwrap_or(true);

    // Overflow strategy
    let overflow_strategy = config
        .get("overflow_strategy")
        .and_then(|v| v.as_str())
        .map(|s| match s {
            "throw" => OverflowStrategy::Throw,
            "truncate" => OverflowStrategy::Truncate,
            "proceed" => OverflowStrategy::Proceed,
            other => panic!("unknown overflow strategy: {other}"),
        })
        .unwrap_or_default();

    Pipeline::builder()
        .scorer(scorer)
        .slicer(slicer)
        .placer(placer)
        .deduplication(deduplication)
        .overflow_strategy(overflow_strategy)
        .build()
        .expect("failed to build pipeline")
}

fn run_pipeline_test(vector_path: &str) {
    let vector = load_vector(vector_path);
    let items = build_items(&vector);
    let pipeline = build_pipeline_from_config(&vector);

    let budget_table = vector.get("budget").expect("missing [budget]");
    let max_tokens = budget_table["max_tokens"]
        .as_integer()
        .expect("missing budget.max_tokens");
    let target_tokens = budget_table["target_tokens"]
        .as_integer()
        .expect("missing budget.target_tokens");
    let output_reserve = budget_table
        .get("output_reserve")
        .and_then(|v| v.as_integer())
        .unwrap_or(0);

    let budget = ContextBudget::new(
        max_tokens,
        target_tokens,
        output_reserve,
        HashMap::new(),
        0.0,
    )
    .expect("budget should be valid");

    let result = pipeline
        .run(&items, &budget)
        .expect("pipeline run should succeed");

    let actual_contents: Vec<String> = result.iter().map(|i| i.content().to_owned()).collect();

    let expected_output = vector
        .get("expected_output")
        .and_then(|v| v.as_array())
        .expect("missing [[expected_output]]");

    let expected_contents: Vec<String> = expected_output
        .iter()
        .map(|v| {
            v.get("content")
                .and_then(|c| c.as_str())
                .expect("expected_output missing content")
                .to_owned()
        })
        .collect();

    assert_ordered_eq(&expected_contents, &actual_contents);
}

fn exclusion_reason_tag(reason: &cupel::ExclusionReason) -> &'static str {
    match reason {
        cupel::ExclusionReason::BudgetExceeded { .. } => "BudgetExceeded",
        cupel::ExclusionReason::NegativeTokens { .. } => "NegativeTokens",
        cupel::ExclusionReason::Deduplicated { .. } => "Deduplicated",
        cupel::ExclusionReason::PinnedOverride { .. } => "PinnedOverride",
        cupel::ExclusionReason::ScoredTooLow { .. } => "ScoredTooLow",
        cupel::ExclusionReason::QuotaCapExceeded { .. } => "QuotaCapExceeded",
        cupel::ExclusionReason::QuotaRequireDisplaced { .. } => "QuotaRequireDisplaced",
        cupel::ExclusionReason::Filtered { .. } => "Filtered",
        _ => "Unknown",
    }
}

fn inclusion_reason_tag(reason: &cupel::InclusionReason) -> &'static str {
    match reason {
        cupel::InclusionReason::Scored => "Scored",
        cupel::InclusionReason::Pinned => "Pinned",
        cupel::InclusionReason::ZeroToken => "ZeroToken",
        _ => "Unknown",
    }
}

fn run_pipeline_diagnostics_test(vector_path: &str) {
    let vector = load_vector(vector_path);
    let items = build_items(&vector);
    let pipeline = build_pipeline_from_config(&vector);

    let budget_table = vector.get("budget").expect("missing [budget]");
    let max_tokens = budget_table["max_tokens"]
        .as_integer()
        .expect("missing budget.max_tokens");
    let target_tokens = budget_table["target_tokens"]
        .as_integer()
        .expect("missing budget.target_tokens");
    let output_reserve = budget_table
        .get("output_reserve")
        .and_then(|v| v.as_integer())
        .unwrap_or(0);

    let budget = ContextBudget::new(
        max_tokens,
        target_tokens,
        output_reserve,
        HashMap::new(),
        0.0,
    )
    .expect("budget should be valid");

    let epsilon = vector
        .get("tolerance")
        .and_then(|t| t.get("score_epsilon"))
        .and_then(|v| v.as_float())
        .unwrap_or(1e-9);

    let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
    pipeline
        .run_traced(&items, &budget, &mut collector)
        .expect("run_traced should succeed");
    let report = collector.into_report();

    // --- summary ---
    let diag = vector
        .get("expected")
        .and_then(|e| e.get("diagnostics"))
        .expect("missing [expected.diagnostics]");

    let summary = diag
        .get("summary")
        .expect("missing [expected.diagnostics.summary]");

    let expected_tc = summary["total_candidates"]
        .as_integer()
        .expect("missing total_candidates") as usize;
    assert_eq!(
        report.total_candidates, expected_tc,
        "total_candidates mismatch: expected {expected_tc}, got {}",
        report.total_candidates
    );

    let expected_ttc = summary["total_tokens_considered"]
        .as_integer()
        .expect("missing total_tokens_considered");
    assert_eq!(
        report.total_tokens_considered, expected_ttc,
        "total_tokens_considered mismatch: expected {expected_ttc}, got {}",
        report.total_tokens_considered
    );

    // --- included ---
    if let Some(expected_included) = diag.get("included").and_then(|v| v.as_array()) {
        assert_eq!(
            report.included.len(),
            expected_included.len(),
            "included count mismatch: expected {}, got {}",
            expected_included.len(),
            report.included.len()
        );
        for (i, exp) in expected_included.iter().enumerate() {
            let exp_content = exp["content"].as_str().expect("included missing content");
            let exp_score = exp["score_approx"]
                .as_float()
                .or_else(|| exp["score_approx"].as_integer().map(|v| v as f64))
                .expect("included missing score_approx");
            let exp_reason = exp["inclusion_reason"]
                .as_str()
                .expect("included missing inclusion_reason");

            let actual = &report.included[i];
            assert_eq!(
                actual.item.content(),
                exp_content,
                "included[{i}] content mismatch: expected '{exp_content}', got '{}'",
                actual.item.content()
            );
            let score_diff = (actual.score - exp_score).abs();
            assert!(
                score_diff < epsilon,
                "included[{i}] score mismatch for '{}': expected {exp_score}, got {}, diff {score_diff} >= epsilon {epsilon}",
                actual.item.content(),
                actual.score
            );
            let actual_reason_tag = inclusion_reason_tag(&actual.reason);
            assert_eq!(
                actual_reason_tag,
                exp_reason,
                "included[{i}] inclusion_reason mismatch for '{}': expected '{exp_reason}', got '{actual_reason_tag}'",
                actual.item.content()
            );
        }
    }

    // --- excluded ---
    if let Some(expected_excluded) = diag.get("excluded").and_then(|v| v.as_array()) {
        assert_eq!(
            report.excluded.len(),
            expected_excluded.len(),
            "excluded count mismatch: expected {}, got {}",
            expected_excluded.len(),
            report.excluded.len()
        );
        for (i, exp) in expected_excluded.iter().enumerate() {
            let exp_content = exp["content"].as_str().expect("excluded missing content");
            let exp_score = exp["score_approx"]
                .as_float()
                .or_else(|| exp["score_approx"].as_integer().map(|v| v as f64))
                .expect("excluded missing score_approx");
            let exp_reason_str = exp["exclusion_reason"]
                .as_str()
                .expect("excluded missing exclusion_reason");

            let actual = &report.excluded[i];
            assert_eq!(
                actual.item.content(),
                exp_content,
                "excluded[{i}] content mismatch: expected '{exp_content}', got '{}'",
                actual.item.content()
            );
            let score_diff = (actual.score - exp_score).abs();
            assert!(
                score_diff < epsilon,
                "excluded[{i}] score mismatch for '{}': expected {exp_score}, got {}, diff {score_diff} >= epsilon {epsilon}",
                actual.item.content(),
                actual.score
            );
            let actual_reason_tag = exclusion_reason_tag(&actual.reason);
            assert_eq!(
                actual_reason_tag,
                exp_reason_str,
                "excluded[{i}] exclusion_reason mismatch for '{}': expected '{exp_reason_str}', got '{actual_reason_tag}'",
                actual.item.content()
            );

            // variant-specific field checks
            match exp_reason_str {
                "NegativeTokens" => {
                    if let Some(exp_tokens) = exp.get("tokens").and_then(|v| v.as_integer()) {
                        if let ExclusionReason::NegativeTokens { tokens } = &actual.reason {
                            assert_eq!(
                                *tokens, exp_tokens,
                                "excluded[{i}] NegativeTokens.tokens mismatch: expected {exp_tokens}, got {tokens}"
                            );
                        }
                    }
                }
                "Deduplicated" => {
                    if let Some(exp_against) =
                        exp.get("deduplicated_against").and_then(|v| v.as_str())
                    {
                        if let ExclusionReason::Deduplicated {
                            deduplicated_against,
                        } = &actual.reason
                        {
                            assert_eq!(
                                deduplicated_against.as_str(),
                                exp_against,
                                "excluded[{i}] Deduplicated.deduplicated_against mismatch: expected '{exp_against}', got '{deduplicated_against}'"
                            );
                        }
                    }
                }
                "BudgetExceeded" => {
                    if let ExclusionReason::BudgetExceeded {
                        item_tokens,
                        available_tokens,
                    } = &actual.reason
                    {
                        if let Some(exp_item_tokens) =
                            exp.get("item_tokens").and_then(|v| v.as_integer())
                        {
                            assert_eq!(
                                *item_tokens, exp_item_tokens,
                                "excluded[{i}] BudgetExceeded.item_tokens mismatch: expected {exp_item_tokens}, got {item_tokens}"
                            );
                        }
                        if let Some(exp_avail) =
                            exp.get("available_tokens").and_then(|v| v.as_integer())
                        {
                            assert_eq!(
                                *available_tokens, exp_avail,
                                "excluded[{i}] BudgetExceeded.available_tokens mismatch: expected {exp_avail}, got {available_tokens}"
                            );
                        }
                    }
                }
                "PinnedOverride" => {
                    if let Some(exp_displaced_by) = exp.get("displaced_by").and_then(|v| v.as_str())
                    {
                        if let ExclusionReason::PinnedOverride { displaced_by } = &actual.reason {
                            assert_eq!(
                                displaced_by.as_str(),
                                exp_displaced_by,
                                "excluded[{i}] PinnedOverride.displaced_by mismatch: expected '{exp_displaced_by}', got '{displaced_by}'"
                            );
                        }
                    }
                }
                _ => {}
            }
        }
    }
}

#[test]
fn greedy_chronological() {
    run_pipeline_test("pipeline/greedy-chronological.toml");
}

#[test]
fn greedy_ushaped() {
    run_pipeline_test("pipeline/greedy-ushaped.toml");
}

#[test]
fn knapsack_chronological() {
    run_pipeline_test("pipeline/knapsack-chronological.toml");
}

#[test]
fn composite_greedy_chronological() {
    run_pipeline_test("pipeline/composite-greedy-chronological.toml");
}

#[test]
fn pinned_items() {
    run_pipeline_test("pipeline/pinned-items.toml");
}

#[test]
fn diag_negative_tokens() {
    run_pipeline_diagnostics_test("pipeline/diag-negative-tokens.toml");
}

#[test]
fn diag_deduplicated() {
    run_pipeline_diagnostics_test("pipeline/diag-deduplicated.toml");
}

#[test]
fn diag_pinned_override() {
    run_pipeline_diagnostics_test("pipeline/diag-pinned-override.toml");
}

#[test]
fn diag_scored_inclusion() {
    run_pipeline_diagnostics_test("pipeline/diag-scored-inclusion.toml");
}

#[test]
fn diagnostics_budget_exceeded() {
    run_pipeline_diagnostics_test("pipeline/diagnostics-budget-exceeded.toml");
}
