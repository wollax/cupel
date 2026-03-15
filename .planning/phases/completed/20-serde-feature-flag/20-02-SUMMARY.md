---
phase: "20"
plan: "02"
subsystem: "model, slicer"
tags: ["serde", "serialization", "validation-routing"]
requires: ["20-01"]
provides: ["complex-type-serde"]
affects: ["crates/cupel/src/model/", "crates/cupel/src/slicer/"]
tech-stack:
  added: []
  patterns: ["raw-intermediary-struct deserialization", "builder-routed deserialization", "constructor-routed deserialization", "manual Serialize", "cfg_attr derive"]
key-files:
  created: []
  modified:
    - "crates/cupel/src/model/context_item.rs"
    - "crates/cupel/src/model/scored_item.rs"
    - "crates/cupel/src/model/context_budget.rs"
    - "crates/cupel/src/slicer/quota.rs"
decisions: []
metrics:
  duration: "~2 min"
  completed: "2026-03-15"
---

# Phase 20 Plan 02: Complex Type Serde Summary

## Outcome

All tasks completed successfully. Four complex types now have serde support gated behind `cfg(feature = "serde")`. All validated types route deserialization through their constructors or builders, making it impossible to bypass validation via blind deserialization.

## Tasks Completed

### Task 1: Implement serde for ContextItem and ScoredItem
- **ContextItem**: Manual Serialize (11 fields, snake_case) + Deserialize via raw intermediary struct that populates `ContextItemBuilder` and calls `.build()`. Empty content is rejected. Unknown fields denied.
- **ScoredItem**: `cfg_attr` derive Serialize/Deserialize with `deny_unknown_fields` (public fields, no validation needed).

### Task 2: Implement serde for ContextBudget and QuotaEntry
- **ContextBudget**: Manual Serialize (5 fields) + Deserialize via raw intermediary struct routing through `ContextBudget::new()`. All validation rules enforced (max >= target, non-negative values, margin range). `reserved_slots` defaults to empty HashMap. Unknown fields denied.
- **QuotaEntry**: Manual Serialize (3 fields) + Deserialize via raw intermediary struct routing through `QuotaEntry::new()`. Require/cap validation enforced. Unknown fields denied.

## Verification

- `cargo check` without features: pass
- `cargo check --features serde`: pass
- `cargo clippy --features serde`: no warnings
- All 28 existing tests pass (with and without feature)

## Deviations

None.

## Commits

| Hash | Message |
|------|---------|
| db2a537 | feat(20-02): implement serde for ContextItem and ScoredItem |
| 6f0d390 | feat(20-02): implement serde for ContextBudget and QuotaEntry |
