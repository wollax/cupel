# Phase 20 Verification — Serde Feature Flag

**Status:** passed
**Score:** 22/22 must-haves verified
**Date:** 2026-03-15

---

## Plan 01 Must-Haves

### `cargo check` passes without `--features serde`
**Result:** PASS
`rtk cargo check --manifest-path crates/cupel/Cargo.toml` → ✓ cargo build (1 crates compiled)

### `cargo check --features serde` passes
**Result:** PASS
`rtk cargo check --features serde --manifest-path crates/cupel/Cargo.toml` → ✓ cargo build (1 crates compiled)

### ContextKind serializes as a bare string, deserializes through `ContextKind::new()`
**Result:** PASS
`context_kind.rs` has a manual `Serialize` impl that calls `serializer.serialize_str(&self.0)` and a manual `Deserialize` impl that calls `ContextKind::new(s).map_err(serde::de::Error::custom)`. Wire format test `context_kind_serializes_as_bare_string` confirms `"Document"` (not a JSON object).

### ContextSource serializes as a bare string, deserializes through `ContextSource::new()`
**Result:** PASS
`context_source.rs` has identical pattern: `serializer.serialize_str(&self.0)` for serialize, `ContextSource::new(s).map_err(...)` for deserialize.

### OverflowStrategy serializes/deserializes as PascalCase enum variant name
**Result:** PASS
`overflow_strategy.rs` uses `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]`. Serde derive uses the variant name as-is (`Throw`, `Truncate`, `Proceed`), which is PascalCase. Wire format test `overflow_strategy_serializes_as_pascal_case` confirms the exact strings `"Throw"`, `"Truncate"`, `"Proceed"`.

---

## Plan 02 Must-Haves

### ContextItem deserializes through ContextItemBuilder, enforcing non-empty content validation
**Result:** PASS
`context_item.rs` `Deserialize` impl deserializes into an internal `Raw` struct then calls `ContextItemBuilder::new(raw.content, raw.tokens)` and routes all optional fields through builder methods, terminating with `.build().map_err(serde::de::Error::custom)`. `ContextItemBuilder::build()` rejects empty content with `CupelError::EmptyContent`. Test `reject_empty_content_context_item` confirms this.

### ContextItem serializes all fields using snake_case keys
**Result:** PASS
`context_item.rs` manual `Serialize` impl explicitly uses snake_case field names: `content`, `tokens`, `kind`, `source`, `priority`, `tags`, `metadata`, `timestamp`, `future_relevance_hint`, `pinned`, `original_tokens`.

### ContextBudget deserializes through `ContextBudget::new()`, enforcing all validation rules
**Result:** PASS
`context_budget.rs` `Deserialize` impl deserializes into internal `Raw` struct then calls `ContextBudget::new(...)` directly, routing the result through `.map_err(serde::de::Error::custom)`. All 7 validation rules (non-negative max/target/output_reserve, target <= max, output_reserve <= max, margin in [0,100], non-negative slot counts) are enforced. Tests `reject_invalid_budget_target_exceeds_max` and `reject_invalid_budget_negative_max` confirm bypass is impossible.

### QuotaEntry deserializes through `QuotaEntry::new()`, enforcing require/cap validation
**Result:** PASS
`slicer/quota.rs` `Deserialize` impl uses an internal `Raw` struct then calls `QuotaEntry::new(raw.kind, raw.require, raw.cap).map_err(...)`. Validation: require and cap in [0,100], require <= cap. Tests `reject_invalid_quota_require_exceeds_cap` and `reject_invalid_quota_negative_require` confirm enforcement.

### ScoredItem derives serde with cfg_attr
**Result:** PASS
`scored_item.rs` uses:
```rust
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
#[cfg_attr(feature = "serde", serde(deny_unknown_fields))]
```

### All types deny unknown fields
**Result:** PASS
- `OverflowStrategy`: `#[cfg_attr(feature = "serde", serde(deny_unknown_fields))]`
- `ScoredItem`: `#[cfg_attr(feature = "serde", serde(deny_unknown_fields))]`
- `ContextItem`: internal `Raw` struct has `#[serde(deny_unknown_fields)]`
- `ContextBudget`: internal `Raw` struct has `#[serde(deny_unknown_fields)]`
- `QuotaEntry`: internal `Raw` struct has `#[serde(deny_unknown_fields)]`
- Tests `reject_unknown_field_context_item`, `reject_unknown_field_context_budget`, `reject_unknown_field_quota_entry`, `reject_unknown_field_scored_item` all confirm enforcement.

---

## Plan 03 Must-Haves

### Roundtrip serde tests pass for all 7 types
**Result:** PASS
`tests/serde.rs` contains 8 roundtrip tests covering all 7 types (ContextItem has minimal + full variants):
- `roundtrip_context_kind`
- `roundtrip_context_source`
- `roundtrip_overflow_strategy`
- `roundtrip_context_item_minimal`
- `roundtrip_context_item_full`
- `roundtrip_context_budget`
- `roundtrip_quota_entry`
- `roundtrip_scored_item`

### Validation-on-deserialize rejection tests prove constructors cannot be bypassed
**Result:** PASS
Rejection tests present: empty/whitespace ContextKind, empty ContextSource, empty ContextItem content, invalid budget (target > max, negative max), invalid quota (require > cap, negative require). All tests run and pass.

### Unknown field rejection tests pass for all struct types
**Result:** PASS
Tests `reject_unknown_field_context_item`, `reject_unknown_field_context_budget`, `reject_unknown_field_quota_entry`, `reject_unknown_field_scored_item` all present and passing.

### Default handling tests pass for ContextItem optional fields
**Result:** PASS
Test `context_item_defaults` deserializes `{"content":"hello","tokens":10}` and asserts all optional fields resolve to their defaults (kind=Message, source=Chat, priority/tags/metadata/timestamp/future_relevance_hint/original_tokens all None/empty/false).

### `cargo test` passes without `--features serde` (feature is additive)
**Result:** PASS
`rtk cargo test --manifest-path crates/cupel/Cargo.toml` → ✓ 28 passed (4 suites). Serde tests are gated with `#![cfg(feature = "serde")]` at the top of `tests/serde.rs` so they are excluded from non-feature runs.

### `cargo test --features serde` passes including all new serde tests
**Result:** PASS
`rtk cargo test --features serde --manifest-path crates/cupel/Cargo.toml` → ✓ 52 passed (4 suites, 0.02s). The 24 additional tests (52 - 28) are the serde tests.

### Crate version is 1.1.0
**Result:** PASS
`crates/cupel/Cargo.toml` line 3: `version = "1.1.0"`

---

## Summary

All 22 must-haves from Plans 01, 02, and 03 are verified against the actual codebase and confirmed by passing test runs. The serde feature flag implementation is complete and correct.
