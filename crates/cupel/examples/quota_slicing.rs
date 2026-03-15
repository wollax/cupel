//! Quota-based slicing walkthrough — per-kind budget allocation.
//!
//! Run with: `cargo run --example quota_slicing`
//!
//! This example demonstrates:
//! - Creating items across multiple kinds (Message, ToolOutput, Document, Memory)
//! - Configuring QuotaEntry instances with require/cap percentages
//! - Building a pipeline with QuotaSlice and UShapedPlacer
//! - Inspecting how the quota affects per-kind selection

use std::collections::HashMap;

use chrono::Utc;
use cupel::{
    ContextBudget, ContextItemBuilder, ContextKind, GreedySlice, KindScorer, Pipeline, QuotaEntry,
    QuotaSlice, UShapedPlacer,
};

fn main() -> Result<(), cupel::CupelError> {
    let now = Utc::now();

    // -- Step 1: Create a diverse set of context items ---------------------------------
    //
    // We create 11 items across 4 kinds with varying token counts. The token counts are
    // intentionally varied so the quota effects are visible in the output.

    let items = vec![
        // Messages (4 items, 340 tokens total)
        ContextItemBuilder::new("User: What's the deployment status?", 12)
            .kind(ContextKind::new("Message")?)
            .timestamp(now - chrono::Duration::minutes(10))
            .build()?,
        ContextItemBuilder::new("Assistant: Checking the CI pipeline now...", 15)
            .kind(ContextKind::new("Message")?)
            .timestamp(now - chrono::Duration::minutes(9))
            .build()?,
        ContextItemBuilder::new("User: Also check the staging environment logs", 13)
            .kind(ContextKind::new("Message")?)
            .timestamp(now - chrono::Duration::minutes(5))
            .build()?,
        ContextItemBuilder::new(
            "Assistant: Here's what I found across both environments.",
            300,
        )
        .kind(ContextKind::new("Message")?)
        .timestamp(now - chrono::Duration::minutes(4))
        .build()?,
        // Tool outputs (3 items, 680 tokens total)
        ContextItemBuilder::new("CI pipeline: 3/5 stages passed, deploy stage pending", 80)
            .kind(ContextKind::new("ToolOutput")?)
            .timestamp(now - chrono::Duration::minutes(8))
            .build()?,
        ContextItemBuilder::new("Staging logs: 12 warnings, 0 errors in last 24h", 200)
            .kind(ContextKind::new("ToolOutput")?)
            .timestamp(now - chrono::Duration::minutes(7))
            .build()?,
        ContextItemBuilder::new(
            "Production health: all endpoints 200 OK, p99 latency 45ms",
            400,
        )
        .kind(ContextKind::new("ToolOutput")?)
        .timestamp(now - chrono::Duration::minutes(3))
        .build()?,
        // Documents (2 items, 550 tokens total)
        ContextItemBuilder::new(
            "Runbook: deployment rollback procedure requires 3 approvals",
            250,
        )
        .kind(ContextKind::new("Document")?)
        .timestamp(now - chrono::Duration::minutes(6))
        .build()?,
        ContextItemBuilder::new(
            "Architecture doc: staging mirrors prod with 1h sync delay",
            300,
        )
        .kind(ContextKind::new("Document")?)
        .timestamp(now - chrono::Duration::minutes(2))
        .build()?,
        // Memories (2 items, 30 tokens total)
        ContextItemBuilder::new("User is on-call this week for the payments service", 15)
            .kind(ContextKind::new("Memory")?)
            .timestamp(now - chrono::Duration::hours(2))
            .build()?,
        ContextItemBuilder::new("Team uses blue-green deployment strategy", 15)
            .kind(ContextKind::new("Memory")?)
            .timestamp(now - chrono::Duration::hours(1))
            .build()?,
    ];

    println!("Created {} candidate items across 4 kinds\n", items.len());

    // Print candidate token mass per kind
    let mut kind_totals: HashMap<String, (usize, i64)> = HashMap::new();
    for item in &items {
        let entry = kind_totals
            .entry(item.kind().as_str().to_string())
            .or_insert((0, 0));
        entry.0 += 1;
        entry.1 += item.tokens();
    }
    println!("Candidate token mass per kind:");
    let mut kinds_sorted: Vec<_> = kind_totals.iter().collect();
    kinds_sorted.sort_by_key(|(k, _)| (*k).clone());
    for (kind, (count, tokens)) in &kinds_sorted {
        println!("  {kind:<12} {count} items, {tokens} tokens");
    }
    println!();

    // -- Step 2: Configure quota entries -----------------------------------------------
    //
    // QuotaEntry defines two percentages of the target budget for each kind:
    //   require — minimum guaranteed allocation (the slicer MUST try to fill this)
    //   cap     — maximum allowed allocation (the slicer MUST NOT exceed this)
    //
    // Kinds without a quota entry get whatever budget remains, capped at 100%.

    let quotas = vec![
        // ToolOutput: guarantee at least 30%, allow up to 50%
        QuotaEntry::new(ContextKind::new("ToolOutput")?, 30.0, 50.0)?,
        // Document: guarantee at least 20%, allow up to 40%
        QuotaEntry::new(ContextKind::new("Document")?, 20.0, 40.0)?,
        // Message: guarantee at least 10%, allow up to 60%
        QuotaEntry::new(ContextKind::new("Message")?, 10.0, 60.0)?,
        // Memory has no quota — it gets whatever remains (uncapped)
    ];

    println!("Quota configuration:");
    for q in &quotas {
        println!(
            "  {:<12} require={:>4.1}%  cap={:>4.1}%",
            q.kind(),
            q.require(),
            q.cap(),
        );
    }
    println!("  {:<12} (no quota — uncapped)", "Memory");
    println!();

    // -- Step 3: Build the pipeline ----------------------------------------------------
    //
    // Scorer: KindScorer with default weights gives systematic preference by kind.
    // Slicer: QuotaSlice distributes the budget per-kind, then delegates to GreedySlice.
    // Placer: UShapedPlacer puts highest-scored items at the edges of the context window
    //         where LLM attention is strongest (primacy and recency bias).

    let slicer = QuotaSlice::new(quotas, Box::new(GreedySlice))?;

    let pipeline = Pipeline::builder()
        .scorer(Box::new(KindScorer::with_default_weights()))
        .slicer(Box::new(slicer))
        .placer(Box::new(UShapedPlacer))
        .build()?;

    // -- Step 4: Define the budget and run ---------------------------------------------

    let budget = ContextBudget::new(
        2048, // max_tokens
        1000, // target_tokens — this is what the quotas are percentages of
        512,  // output_reserve
        HashMap::new(),
        0.0,
    )?;

    println!(
        "Budget: target_tokens={} (quotas are percentages of this)\n",
        budget.target_tokens()
    );

    let result = pipeline.run(&items, &budget)?;

    // -- Step 5: Analyze results -------------------------------------------------------

    println!("Pipeline selected {} items:\n", result.len());

    // Group selected items by kind for the summary
    let mut selected_by_kind: HashMap<String, (usize, i64)> = HashMap::new();
    for item in &result {
        let entry = selected_by_kind
            .entry(item.kind().as_str().to_string())
            .or_insert((0, 0));
        entry.0 += 1;
        entry.1 += item.tokens();
    }

    // Print each selected item in placement order
    for (i, item) in result.iter().enumerate() {
        println!(
            "  [{}] kind={:<12} tokens={:>4}  {}",
            i + 1,
            item.kind(),
            item.tokens(),
            item.content().chars().take(65).collect::<String>(),
        );
    }

    // Print per-kind breakdown
    let total_tokens: i64 = result.iter().map(|item| item.tokens()).sum();
    println!("\nPer-kind breakdown:");

    let mut selected_sorted: Vec<_> = selected_by_kind.iter().collect();
    selected_sorted.sort_by_key(|(k, _)| (*k).clone());

    for (kind, (count, tokens)) in &selected_sorted {
        let pct = *tokens as f64 / budget.target_tokens() as f64 * 100.0;
        println!("  {kind:<12} {count} items, {tokens:>4} tokens ({pct:>5.1}% of target)");
    }

    println!(
        "\nTotal: {} items, {} tokens ({:.1}% of {} target)",
        result.len(),
        total_tokens,
        total_tokens as f64 / budget.target_tokens() as f64 * 100.0,
        budget.target_tokens(),
    );

    Ok(())
}
