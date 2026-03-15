//! Basic pipeline walkthrough — the core Cupel flow in one file.
//!
//! Run with: `cargo run --example basic_pipeline`
//!
//! This example demonstrates:
//! - Creating context items with different kinds and timestamps
//! - Configuring a token budget
//! - Building a pipeline with composite scoring, greedy slicing, and chronological placement
//! - Running the pipeline and inspecting the selected items

use std::collections::HashMap;

use chrono::Utc;
use cupel::{
    ChronologicalPlacer, CompositeScorer, ContextBudget, ContextItemBuilder, ContextKind,
    ContextSource, GreedySlice, KindScorer, Pipeline, RecencyScorer,
};

fn main() -> Result<(), cupel::CupelError> {
    let now = Utc::now();

    // -- Step 1: Create context items --------------------------------------------------
    //
    // Each item has content, a token count (pre-counted by your tokenizer), an optional
    // kind, source, timestamp, and other metadata. Pinned items bypass scoring and slicing.

    let items = vec![
        // System prompt — pinned so it always appears in the output
        ContextItemBuilder::new(
            "You are a senior Rust engineer. Answer concisely with code examples.",
            18,
        )
        .kind(ContextKind::new("SystemPrompt")?)
        .source(ContextSource::new("Chat")?)
        .timestamp(now - chrono::Duration::minutes(5))
        .pinned(true)
        .build()?,
        // User message: initial question
        ContextItemBuilder::new("How do I implement the Iterator trait for a custom type?", 14)
            .kind(ContextKind::new("Message")?)
            .source(ContextSource::new("Chat")?)
            .timestamp(now - chrono::Duration::minutes(4))
            .build()?,
        // Assistant reply (a previous turn)
        ContextItemBuilder::new(
            "Implement `fn next(&mut self) -> Option<Self::Item>` on your type...",
            120,
        )
        .kind(ContextKind::new("Message")?)
        .source(ContextSource::new("Chat")?)
        .timestamp(now - chrono::Duration::minutes(3))
        .build()?,
        // Tool output: documentation lookup
        ContextItemBuilder::new(
            "std::iter::Iterator trait — 76 provided methods, 1 required method: next()",
            200,
        )
        .kind(ContextKind::new("ToolOutput")?)
        .source(ContextSource::new("Tool")?)
        .timestamp(now - chrono::Duration::minutes(2))
        .tags(vec!["docs".to_string(), "stdlib".to_string()])
        .build()?,
        // Document: a relevant code file the user is working on
        ContextItemBuilder::new(
            "pub struct Fibonacci { curr: u64, next: u64 }",
            350,
        )
        .kind(ContextKind::new("Document")?)
        .source(ContextSource::new("Rag")?)
        .timestamp(now - chrono::Duration::minutes(1))
        .build()?,
        // User follow-up: the most recent message
        ContextItemBuilder::new(
            "Can you show how to make Fibonacci implement Iterator with a stop condition?",
            16,
        )
        .kind(ContextKind::new("Message")?)
        .source(ContextSource::new("Chat")?)
        .timestamp(now)
        .build()?,
        // Memory: a stored user preference
        ContextItemBuilder::new("User prefers examples using iterators over manual loops", 10)
            .kind(ContextKind::new("Memory")?)
            .timestamp(now - chrono::Duration::hours(1))
            .build()?,
    ];

    println!("Created {} candidate context items\n", items.len());

    // -- Step 2: Define the token budget -----------------------------------------------
    //
    // max_tokens: hard ceiling (model context window)
    // target_tokens: soft goal the slicer aims for
    // output_reserve: tokens reserved for model output generation
    // reserved_slots: per-kind minimum item guarantees (empty here)
    // estimation_safety_margin_percent: buffer for token estimation error

    let budget = ContextBudget::new(
        4096, // max_tokens
        3000, // target_tokens
        1024, // output_reserve
        HashMap::new(),
        0.0,
    )?;

    println!(
        "Budget: max={}, target={}, output_reserve={}\n",
        budget.max_tokens(),
        budget.target_tokens(),
        budget.output_reserve(),
    );

    // -- Step 3: Build the pipeline ----------------------------------------------------
    //
    // Scorer: CompositeScorer blending RecencyScorer (weight 2.0) with KindScorer (weight 1.0).
    //   Recent items and high-value kinds (SystemPrompt, Memory) get boosted.
    // Slicer: GreedySlice fills the budget by value density (score / tokens).
    // Placer: ChronologicalPlacer orders results by timestamp for natural conversation flow.

    let scorer = CompositeScorer::new(vec![
        (Box::new(RecencyScorer), 2.0),
        (Box::new(KindScorer::with_default_weights()), 1.0),
    ])?;

    let pipeline = Pipeline::builder()
        .scorer(Box::new(scorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .build()?;

    // -- Step 4: Run the pipeline and inspect results ----------------------------------

    let result = pipeline.run(&items, &budget)?;

    println!("Pipeline selected {} items:\n", result.len());

    let total_tokens: i64 = result.iter().map(|item| item.tokens()).sum();

    for (i, item) in result.iter().enumerate() {
        println!(
            "  [{}] kind={:<13} tokens={:>4}  pinned={}  {}",
            i + 1,
            item.kind(),
            item.tokens(),
            if item.pinned() { "yes" } else { "no " },
            // Show first 60 chars of content
            &item.content()[..item.content().len().min(60)],
        );
    }

    println!("\nTotal tokens used: {} / {} target", total_tokens, budget.target_tokens());

    Ok(())
}
