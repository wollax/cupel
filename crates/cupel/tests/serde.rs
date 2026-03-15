#![cfg(feature = "serde")]

use std::collections::HashMap;

use chrono::{TimeZone, Utc};
use cupel::{
    ContextBudget, ContextItem, ContextItemBuilder, ContextKind, ContextSource, OverflowStrategy,
    QuotaEntry, ScoredItem,
};

// ---------------------------------------------------------------------------
// 1. Roundtrip tests
// ---------------------------------------------------------------------------

#[test]
fn roundtrip_context_kind() {
    let kind = ContextKind::new("Document").unwrap();
    let json = serde_json::to_string(&kind).unwrap();
    let restored: ContextKind = serde_json::from_str(&json).unwrap();
    assert_eq!(restored.as_str(), "Document");
}

#[test]
fn roundtrip_context_source() {
    let source = ContextSource::new("Tool").unwrap();
    let json = serde_json::to_string(&source).unwrap();
    let restored: ContextSource = serde_json::from_str(&json).unwrap();
    assert_eq!(restored.as_str(), "Tool");
}

#[test]
fn roundtrip_overflow_strategy() {
    for variant in [
        OverflowStrategy::Throw,
        OverflowStrategy::Truncate,
        OverflowStrategy::Proceed,
    ] {
        let json = serde_json::to_string(&variant).unwrap();
        let restored: OverflowStrategy = serde_json::from_str(&json).unwrap();
        assert_eq!(restored, variant);
    }
}

#[test]
fn roundtrip_context_item_minimal() {
    let item = ContextItemBuilder::new("hello", 10).build().unwrap();
    let json = serde_json::to_string(&item).unwrap();
    let restored: ContextItem = serde_json::from_str(&json).unwrap();
    assert_eq!(restored.content(), "hello");
    assert_eq!(restored.tokens(), 10);
    assert_eq!(restored.kind().as_str(), "Message");
    assert_eq!(restored.source().as_str(), "Chat");
}

#[test]
fn roundtrip_context_item_full() {
    let ts = Utc.with_ymd_and_hms(2025, 1, 15, 12, 0, 0).unwrap();
    let mut meta = HashMap::new();
    meta.insert("key".to_owned(), "value".to_owned());

    let item = ContextItemBuilder::new("full item", 42)
        .kind(ContextKind::new("Document").unwrap())
        .source(ContextSource::new("Tool").unwrap())
        .priority(5)
        .tags(vec!["alpha".to_owned(), "beta".to_owned()])
        .metadata(meta.clone())
        .timestamp(ts)
        .future_relevance_hint(0.75)
        .pinned(true)
        .original_tokens(100)
        .build()
        .unwrap();

    let json = serde_json::to_string(&item).unwrap();
    let restored: ContextItem = serde_json::from_str(&json).unwrap();

    assert_eq!(restored.content(), "full item");
    assert_eq!(restored.tokens(), 42);
    assert_eq!(restored.kind().as_str(), "Document");
    assert_eq!(restored.source().as_str(), "Tool");
    assert_eq!(restored.priority(), Some(5));
    assert_eq!(restored.tags(), &["alpha", "beta"]);
    assert_eq!(restored.metadata(), &meta);
    assert_eq!(restored.timestamp(), Some(ts));
    assert_eq!(restored.future_relevance_hint(), Some(0.75));
    assert!(restored.pinned());
    assert_eq!(restored.original_tokens(), Some(100));
}

#[test]
fn roundtrip_context_budget() {
    let mut reserved = HashMap::new();
    reserved.insert(ContextKind::new("Document").unwrap(), 5_i64);
    reserved.insert(ContextKind::new("Memory").unwrap(), 3_i64);

    let budget = ContextBudget::new(8000, 6000, 1000, reserved, 5.0).unwrap();
    let json = serde_json::to_string(&budget).unwrap();
    let restored: ContextBudget = serde_json::from_str(&json).unwrap();

    assert_eq!(restored.max_tokens(), 8000);
    assert_eq!(restored.target_tokens(), 6000);
    assert_eq!(restored.output_reserve(), 1000);
    assert_eq!(restored.estimation_safety_margin_percent(), 5.0);
    assert_eq!(
        restored
            .reserved_slots()
            .get(&ContextKind::new("Document").unwrap()),
        Some(&5)
    );
    assert_eq!(
        restored
            .reserved_slots()
            .get(&ContextKind::new("Memory").unwrap()),
        Some(&3)
    );
}

#[test]
fn roundtrip_quota_entry() {
    let entry = QuotaEntry::new(ContextKind::new("Document").unwrap(), 10.0, 50.0).unwrap();
    let json = serde_json::to_string(&entry).unwrap();
    let restored: QuotaEntry = serde_json::from_str(&json).unwrap();

    assert_eq!(restored.kind().as_str(), "Document");
    assert_eq!(restored.require(), 10.0);
    assert_eq!(restored.cap(), 50.0);
}

#[test]
fn roundtrip_scored_item() {
    let item = ContextItemBuilder::new("scored", 20).build().unwrap();
    let scored = ScoredItem {
        item,
        score: 0.85,
    };
    let json = serde_json::to_string(&scored).unwrap();
    let restored: ScoredItem = serde_json::from_str(&json).unwrap();

    assert_eq!(restored.item.content(), "scored");
    assert_eq!(restored.item.tokens(), 20);
    assert_eq!(restored.score, 0.85);
}

// ---------------------------------------------------------------------------
// 2. Validation rejection tests
// ---------------------------------------------------------------------------

#[test]
fn reject_empty_context_kind() {
    let result = serde_json::from_str::<ContextKind>(r#""""#);
    assert!(result.is_err());
}

#[test]
fn reject_whitespace_context_kind() {
    let result = serde_json::from_str::<ContextKind>(r#""   ""#);
    assert!(result.is_err());
}

#[test]
fn reject_empty_context_source() {
    let result = serde_json::from_str::<ContextSource>(r#""""#);
    assert!(result.is_err());
}

#[test]
fn reject_whitespace_context_source() {
    let result = serde_json::from_str::<ContextSource>(r#""   ""#);
    assert!(result.is_err());
}

#[test]
fn reject_empty_content_context_item() {
    let json = r#"{"content":"","tokens":10}"#;
    let result = serde_json::from_str::<ContextItem>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_budget_target_exceeds_max() {
    let json = r#"{
        "max_tokens": 100,
        "target_tokens": 200,
        "output_reserve": 0,
        "reserved_slots": {},
        "estimation_safety_margin_percent": 0.0
    }"#;
    let result = serde_json::from_str::<ContextBudget>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_budget_negative_max() {
    let json = r#"{
        "max_tokens": -1,
        "target_tokens": 0,
        "output_reserve": 0,
        "reserved_slots": {},
        "estimation_safety_margin_percent": 0.0
    }"#;
    let result = serde_json::from_str::<ContextBudget>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_budget_negative_target() {
    let json = r#"{
        "max_tokens": 1000,
        "target_tokens": -1,
        "output_reserve": 0,
        "reserved_slots": {},
        "estimation_safety_margin_percent": 0.0
    }"#;
    let result = serde_json::from_str::<ContextBudget>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_budget_negative_output_reserve() {
    let json = r#"{
        "max_tokens": 1000,
        "target_tokens": 800,
        "output_reserve": -1,
        "reserved_slots": {},
        "estimation_safety_margin_percent": 5.0
    }"#;
    let result = serde_json::from_str::<ContextBudget>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_budget_output_reserve_exceeds_max() {
    let json = r#"{
        "max_tokens": 100,
        "target_tokens": 80,
        "output_reserve": 200,
        "reserved_slots": {},
        "estimation_safety_margin_percent": 5.0
    }"#;
    let result = serde_json::from_str::<ContextBudget>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_budget_safety_margin_too_high() {
    let json = r#"{
        "max_tokens": 1000,
        "target_tokens": 800,
        "output_reserve": 100,
        "reserved_slots": {},
        "estimation_safety_margin_percent": 101.0
    }"#;
    let result = serde_json::from_str::<ContextBudget>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_budget_safety_margin_negative() {
    let json = r#"{
        "max_tokens": 1000,
        "target_tokens": 800,
        "output_reserve": 100,
        "reserved_slots": {},
        "estimation_safety_margin_percent": -1.0
    }"#;
    let result = serde_json::from_str::<ContextBudget>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_budget_negative_reserved_slot() {
    let json = r#"{
        "max_tokens": 1000,
        "target_tokens": 800,
        "output_reserve": 100,
        "reserved_slots": {"Document": -1},
        "estimation_safety_margin_percent": 5.0
    }"#;
    let result = serde_json::from_str::<ContextBudget>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_quota_require_exceeds_cap() {
    let json = r#"{"kind":"Document","require":80.0,"cap":50.0}"#;
    let result = serde_json::from_str::<QuotaEntry>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_quota_negative_require() {
    let json = r#"{"kind":"Document","require":-1.0,"cap":50.0}"#;
    let result = serde_json::from_str::<QuotaEntry>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_quota_cap_exceeds_100() {
    let json = r#"{"kind":"Document","require":10.0,"cap":150.0}"#;
    let result = serde_json::from_str::<QuotaEntry>(json);
    assert!(result.is_err());
}

#[test]
fn reject_invalid_quota_negative_cap() {
    let json = r#"{"kind":"Document","require":0.0,"cap":-1.0}"#;
    let result = serde_json::from_str::<QuotaEntry>(json);
    assert!(result.is_err());
}

// ---------------------------------------------------------------------------
// 3. Unknown field rejection tests
// ---------------------------------------------------------------------------

#[test]
fn reject_unknown_field_context_item() {
    let json = r#"{"content":"hello","tokens":10,"extra_field":true}"#;
    let result = serde_json::from_str::<ContextItem>(json);
    assert!(result.is_err());
}

#[test]
fn reject_unknown_field_context_budget() {
    let json = r#"{
        "max_tokens": 1000,
        "target_tokens": 800,
        "output_reserve": 100,
        "reserved_slots": {},
        "estimation_safety_margin_percent": 5.0,
        "surprise": 42
    }"#;
    let result = serde_json::from_str::<ContextBudget>(json);
    assert!(result.is_err());
}

#[test]
fn reject_unknown_field_quota_entry() {
    let json = r#"{"kind":"Document","require":10.0,"cap":50.0,"bonus":true}"#;
    let result = serde_json::from_str::<QuotaEntry>(json);
    assert!(result.is_err());
}

#[test]
fn reject_unknown_field_scored_item() {
    let json = r#"{"item":{"content":"x","tokens":1},"score":0.5,"extra":1}"#;
    let result = serde_json::from_str::<ScoredItem>(json);
    assert!(result.is_err());
}

// ---------------------------------------------------------------------------
// 4. Default handling tests for ContextItem
// ---------------------------------------------------------------------------

#[test]
fn context_item_defaults() {
    let json = r#"{"content":"hello","tokens":10}"#;
    let item: ContextItem = serde_json::from_str(json).unwrap();

    assert_eq!(item.content(), "hello");
    assert_eq!(item.tokens(), 10);
    assert_eq!(item.kind().as_str(), "Message");
    assert_eq!(item.source().as_str(), "Chat");
    assert_eq!(item.priority(), None);
    assert!(item.tags().is_empty());
    assert!(item.metadata().is_empty());
    assert_eq!(item.timestamp(), None);
    assert_eq!(item.future_relevance_hint(), None);
    assert!(!item.pinned());
    assert_eq!(item.original_tokens(), None);
}

// ---------------------------------------------------------------------------
// 5. Wire format tests
// ---------------------------------------------------------------------------

#[test]
fn context_kind_serializes_as_bare_string() {
    let kind = ContextKind::new("Document").unwrap();
    let json = serde_json::to_string(&kind).unwrap();
    assert_eq!(json, r#""Document""#);
}

#[test]
fn overflow_strategy_serializes_as_pascal_case() {
    assert_eq!(
        serde_json::to_string(&OverflowStrategy::Throw).unwrap(),
        r#""Throw""#
    );
    assert_eq!(
        serde_json::to_string(&OverflowStrategy::Truncate).unwrap(),
        r#""Truncate""#
    );
    assert_eq!(
        serde_json::to_string(&OverflowStrategy::Proceed).unwrap(),
        r#""Proceed""#
    );
}

#[test]
fn context_budget_reserved_slots_string_keys() {
    let mut reserved = HashMap::new();
    reserved.insert(ContextKind::new("Document").unwrap(), 3_i64);

    let budget = ContextBudget::new(1000, 800, 100, reserved, 5.0).unwrap();
    let json = serde_json::to_string(&budget).unwrap();
    let parsed: serde_json::Value = serde_json::from_str(&json).unwrap();

    let slots = parsed
        .get("reserved_slots")
        .and_then(|v| v.as_object())
        .expect("reserved_slots should be an object");

    assert!(
        slots.contains_key("Document"),
        "reserved_slots keys should be strings, got: {slots:?}"
    );
}
