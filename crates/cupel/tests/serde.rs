#![cfg(feature = "serde")]

use std::collections::HashMap;

use chrono::{TimeZone, Utc};
use cupel::{
    ContextBudget, ContextItem, ContextItemBuilder, ContextKind, ContextSource,
    DiagnosticTraceCollector, ExclusionReason, InclusionReason, OverflowStrategy, QuotaEntry,
    ScoredItem, SelectionReport, TraceCollector, TraceDetailLevel,
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
    let scored = ScoredItem { item, score: 0.85 };
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

// ---------------------------------------------------------------------------
// 6. Diagnostics serde tests
// ---------------------------------------------------------------------------

// --- 6a. Wire-format assertions ---

#[test]
fn exclusion_reason_budget_exceeded_wire_format() {
    let reason = ExclusionReason::BudgetExceeded {
        item_tokens: 100,
        available_tokens: 50,
    };
    let json = serde_json::to_string(&reason).unwrap();
    // Must use internally-tagged envelope: {"reason":"BudgetExceeded",...}
    // NOT the adjacently-tagged {"BudgetExceeded":{"item_tokens":...}} form.
    assert!(
        json.contains(r#""reason":"BudgetExceeded""#),
        "expected internally-tagged wire format, got: {json}"
    );
    assert!(
        json.contains("item_tokens"),
        "expected item_tokens field in JSON, got: {json}"
    );
    // Confirm the outer-key form is absent.
    assert!(
        !json.contains(r#""BudgetExceeded":"#),
        "unexpected adjacently-tagged format detected in: {json}"
    );
}

#[test]
fn inclusion_reason_scored_wire_format() {
    let reason = InclusionReason::Scored;
    let json = serde_json::to_string(&reason).unwrap();
    // Unit variant must produce {"reason":"Scored"}, NOT the bare string "Scored".
    assert_eq!(
        json, r#"{"reason":"Scored"}"#,
        "expected internally-tagged wire format"
    );
}

// --- 6b. ExclusionReason round-trips ---

#[test]
fn roundtrip_exclusion_budget_exceeded() {
    let original = ExclusionReason::BudgetExceeded {
        item_tokens: 100,
        available_tokens: 50,
    };
    let json = serde_json::to_string(&original).unwrap();
    let restored: ExclusionReason = serde_json::from_str(&json).unwrap();
    assert_eq!(restored, original);
}

#[test]
fn roundtrip_exclusion_negative_tokens() {
    let original = ExclusionReason::NegativeTokens { tokens: -5 };
    let json = serde_json::to_string(&original).unwrap();
    let restored: ExclusionReason = serde_json::from_str(&json).unwrap();
    assert_eq!(restored, original);
}

#[test]
fn roundtrip_exclusion_deduplicated() {
    let original = ExclusionReason::Deduplicated {
        deduplicated_against: "abc-123".to_string(),
    };
    let json = serde_json::to_string(&original).unwrap();
    let restored: ExclusionReason = serde_json::from_str(&json).unwrap();
    assert_eq!(restored, original);
}

#[test]
fn roundtrip_exclusion_pinned_override() {
    let original = ExclusionReason::PinnedOverride {
        displaced_by: "pinned-item-id".to_string(),
    };
    let json = serde_json::to_string(&original).unwrap();
    let restored: ExclusionReason = serde_json::from_str(&json).unwrap();
    assert_eq!(restored, original);
}

#[test]
fn roundtrip_exclusion_scored_too_low() {
    let original = ExclusionReason::ScoredTooLow {
        score: 0.12,
        threshold: 0.5,
    };
    let json = serde_json::to_string(&original).unwrap();
    let restored: ExclusionReason = serde_json::from_str(&json).unwrap();
    assert_eq!(restored, original);
}

#[test]
fn roundtrip_exclusion_quota_cap_exceeded() {
    let original = ExclusionReason::QuotaCapExceeded {
        kind: "Document".to_string(),
        cap: 10,
        actual: 15,
    };
    let json = serde_json::to_string(&original).unwrap();
    let restored: ExclusionReason = serde_json::from_str(&json).unwrap();
    assert_eq!(restored, original);
}

#[test]
fn roundtrip_exclusion_quota_require_displaced() {
    let original = ExclusionReason::QuotaRequireDisplaced {
        displaced_by_kind: "Message".to_string(),
    };
    let json = serde_json::to_string(&original).unwrap();
    let restored: ExclusionReason = serde_json::from_str(&json).unwrap();
    assert_eq!(restored, original);
}

#[test]
fn roundtrip_exclusion_filtered() {
    let original = ExclusionReason::Filtered {
        filter_name: "ProfanityFilter".to_string(),
    };
    let json = serde_json::to_string(&original).unwrap();
    let restored: ExclusionReason = serde_json::from_str(&json).unwrap();
    assert_eq!(restored, original);
}

// --- 6c. InclusionReason round-trips ---

#[test]
fn roundtrip_inclusion_scored() {
    let original = InclusionReason::Scored;
    let json = serde_json::to_string(&original).unwrap();
    let restored: InclusionReason = serde_json::from_str(&json).unwrap();
    assert_eq!(restored, original);
}

#[test]
fn roundtrip_inclusion_pinned() {
    let original = InclusionReason::Pinned;
    let json = serde_json::to_string(&original).unwrap();
    let restored: InclusionReason = serde_json::from_str(&json).unwrap();
    assert_eq!(restored, original);
}

#[test]
fn roundtrip_inclusion_zero_token() {
    let original = InclusionReason::ZeroToken;
    let json = serde_json::to_string(&original).unwrap();
    let restored: InclusionReason = serde_json::from_str(&json).unwrap();
    assert_eq!(restored, original);
}

// --- 6d. SelectionReport and DiagnosticTraceCollector ---

#[test]
fn roundtrip_selection_report_full() {
    // Build a SelectionReport via DiagnosticTraceCollector.
    let included_item = ContextItemBuilder::new("included content", 50)
        .build()
        .unwrap();
    let excluded_item = ContextItemBuilder::new("excluded content", 9999)
        .build()
        .unwrap();

    let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
    collector.record_included(included_item, 0.9, InclusionReason::Scored);
    collector.record_excluded(
        excluded_item,
        0.5,
        ExclusionReason::BudgetExceeded {
            item_tokens: 9999,
            available_tokens: 50,
        },
    );
    collector.set_candidates(2, 10049);
    let report = collector.into_report();

    let json = serde_json::to_string(&report).unwrap();
    let restored: SelectionReport = serde_json::from_str(&json).unwrap();

    assert_eq!(restored.total_candidates, 2);
    assert_eq!(restored.included.len(), 1);
    assert_eq!(restored.excluded.len(), 1);
    assert_eq!(restored.included[0].item.content(), "included content");
    assert_eq!(restored.excluded[0].item.content(), "excluded content");
    assert_eq!(
        restored.excluded[0].reason,
        ExclusionReason::BudgetExceeded {
            item_tokens: 9999,
            available_tokens: 50,
        }
    );
}

#[test]
fn reject_selection_report_total_candidates_mismatch() {
    // total_candidates claims 99 but included + excluded = 2.
    let json = r#"{
        "events": [],
        "included": [{"item":{"content":"x","tokens":1},"score":0.5,"reason":{"reason":"Scored"}}],
        "excluded": [{"item":{"content":"y","tokens":2},"score":0.1,"reason":{"reason":"BudgetExceeded","item_tokens":2,"available_tokens":0}}],
        "total_candidates": 99,
        "total_tokens_considered": 3
    }"#;
    let result = serde_json::from_str::<SelectionReport>(json);
    assert!(
        result.is_err(),
        "expected Err for total_candidates mismatch, got Ok"
    );
    let err_msg = result.unwrap_err().to_string();
    assert!(
        err_msg.contains("total_candidates"),
        "error message should mention total_candidates, got: {err_msg}"
    );
}

#[test]
fn exclusion_reason_unknown_variant_graceful() {
    // A future spec variant not known to this version must deserialize to _Unknown.
    let result = serde_json::from_str::<ExclusionReason>(r#"{"reason":"FutureVariantFromSpec3"}"#);
    let reason = result.expect("deserialization of unknown variant must not panic or error");
    assert!(
        matches!(reason, ExclusionReason::_Unknown),
        "expected ExclusionReason::_Unknown, got: {reason:?}"
    );
}
