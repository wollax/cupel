//! Serde roundtrip walkthrough — serialize and deserialize Cupel types.
//!
//! Run with: `cargo run --example serde_roundtrip --features serde`
//!
//! This example demonstrates:
//! - Serializing a ContextItem to JSON and deserializing it back
//! - Serializing a ContextBudget to JSON and deserializing it back
//! - Validation-on-deserialize: invalid JSON is rejected at the boundary

use std::collections::HashMap;

use cupel::{ContextBudget, ContextItemBuilder, ContextKind, ContextSource};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // -- Part 1: ContextItem roundtrip -------------------------------------------------
    //
    // Build a realistic context item, serialize it to JSON, then deserialize it back.
    // All fields survive the roundtrip because Cupel's serde implementation preserves
    // every field including optional ones.

    println!("=== ContextItem roundtrip ===\n");

    let item = ContextItemBuilder::new("User: explain ownership in Rust", 12)
        .kind(ContextKind::new("Message")?)
        .source(ContextSource::new("Chat")?)
        .priority(5)
        .tags(vec!["rust".to_string(), "beginner".to_string()])
        .timestamp(chrono::Utc::now())
        .future_relevance_hint(0.75)
        .build()?;

    // Serialize to pretty JSON
    let json = serde_json::to_string_pretty(&item)?;
    println!("Serialized ContextItem:\n{json}\n");

    // Deserialize back into a ContextItem
    let restored: cupel::ContextItem = serde_json::from_str(&json)?;

    println!("Deserialized fields:");
    println!("  content  = {}", restored.content());
    println!("  tokens   = {}", restored.tokens());
    println!("  kind     = {}", restored.kind());
    println!("  source   = {}", restored.source());
    println!("  priority = {:?}", restored.priority());
    println!("  tags     = {:?}", restored.tags());
    println!("  pinned   = {}", restored.pinned());
    println!();

    // -- Part 2: ContextBudget roundtrip -----------------------------------------------
    //
    // ContextBudget validates constraints on both construction and deserialization.
    // A valid budget survives the roundtrip; an invalid one is rejected.

    println!("=== ContextBudget roundtrip ===\n");

    let budget = ContextBudget::new(
        8192, // max_tokens
        6000, // target_tokens
        2048, // output_reserve
        HashMap::new(),
        5.0, // estimation_safety_margin_percent
    )?;

    let budget_json = serde_json::to_string_pretty(&budget)?;
    println!("Serialized ContextBudget:\n{budget_json}\n");

    let restored_budget: ContextBudget = serde_json::from_str(&budget_json)?;
    println!("Deserialized budget:");
    println!("  max_tokens    = {}", restored_budget.max_tokens());
    println!("  target_tokens = {}", restored_budget.target_tokens());
    println!("  output_reserve = {}", restored_budget.output_reserve());
    println!(
        "  safety_margin  = {}%",
        restored_budget.estimation_safety_margin_percent()
    );
    println!();

    // -- Part 3: Validation on deserialize ---------------------------------------------
    //
    // When JSON contains invalid values (e.g., target > max), deserialization fails with
    // a descriptive error. This means invalid data is caught at the boundary — before
    // it ever reaches your pipeline logic.

    println!("=== Validation-on-deserialize ===\n");

    let invalid_json = r#"{
        "max_tokens": 100,
        "target_tokens": 200,
        "output_reserve": 0,
        "reserved_slots": {},
        "estimation_safety_margin_percent": 0.0
    }"#;

    println!("Attempting to deserialize invalid budget (target > max)...");

    let result: Result<ContextBudget, _> = serde_json::from_str(invalid_json);

    match result {
        Ok(_) => println!("  Unexpected success!"),
        Err(e) => println!("  Rejected: {e}"),
    }

    println!("\nValidation-on-deserialize ensures invalid data never enters the pipeline.");

    Ok(())
}
