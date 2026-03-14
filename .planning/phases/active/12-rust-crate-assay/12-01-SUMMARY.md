---
phase: 12-rust-crate-assay
plan: 01
subsystem: assay-cupel-core
tags: [rust, data-model, scorers, trait-design]
dependency-graph:
  requires: [11-language-agnostic-specification]
  provides: [assay-cupel-crate, context-item, context-budget, scorer-trait, 8-scorer-implementations]
  affects: [12-02, 12-03]
tech-stack:
  added: [chrono, thiserror]
  patterns: [builder-pattern, newtype-with-custom-hash-eq, trait-object-downcasting-via-any, reference-identity-via-ptr-eq]
key-files:
  created:
    - crates/assay-cupel/Cargo.toml
    - crates/assay-cupel/src/lib.rs
    - crates/assay-cupel/src/error.rs
    - crates/assay-cupel/src/model/mod.rs
    - crates/assay-cupel/src/model/context_kind.rs
    - crates/assay-cupel/src/model/context_source.rs
    - crates/assay-cupel/src/model/context_item.rs
    - crates/assay-cupel/src/model/context_budget.rs
    - crates/assay-cupel/src/model/scored_item.rs
    - crates/assay-cupel/src/model/overflow_strategy.rs
    - crates/assay-cupel/src/scorer/mod.rs
    - crates/assay-cupel/src/scorer/recency.rs
    - crates/assay-cupel/src/scorer/priority.rs
    - crates/assay-cupel/src/scorer/kind.rs
    - crates/assay-cupel/src/scorer/tag.rs
    - crates/assay-cupel/src/scorer/frequency.rs
    - crates/assay-cupel/src/scorer/reflexive.rs
    - crates/assay-cupel/src/scorer/composite.rs
    - crates/assay-cupel/src/scorer/scaled.rs
  modified:
    - Cargo.toml (workspace dependencies)
    - Cargo.lock
decisions:
  - id: D-12-01-01
    decision: "Scorer trait requires Any supertrait + as_any() method for CompositeScorer cycle detection downcasting"
    rationale: "Rust cannot cast &dyn Scorer to &dyn Any without the trait bound; as_any() enables downcast_ref for cycle DFS traversal"
  - id: D-12-01-02
    decision: "Use HashMap<String, String> for ContextItem metadata instead of serde_json::Value"
    rationale: "Avoids public dependency on serde_json; metadata is opaque to pipeline per spec"
  - id: D-12-01-03
    decision: "Cycle detection uses usize (data pointer cast) in HashSet instead of *const dyn Scorer"
    rationale: "Avoids lifetime issues with raw fat pointers in HashSet; data pointer identity is sufficient for reference identity"
metrics:
  duration: "7m30s"
  completed: "2026-03-14"
---

# Phase 12 Plan 01: Crate Scaffold, Data Model & Scorers Summary

**assay-cupel crate with complete data model (6 types), error enum, Scorer trait, and all 8 scorer implementations matching the Cupel specification**

## What Was Done

### Task 1: Crate scaffold, error types, and data model
Created the `assay-cupel` crate within the `wollax/assay` workspace with:
- **CupelError** enum (8 variants) using thiserror for ergonomic error messages
- **ContextKind** and **ContextSource** newtypes with custom `PartialEq`/`Eq`/`Hash` using ASCII case-insensitive folding (byte-by-byte `to_ascii_lowercase`)
- **ContextItem** with private fields, public accessors, and `ContextItemBuilder` (content+tokens required, all optional fields defaulted per spec)
- **ContextBudget** with 7-rule validation at construction returning `Result<ContextBudget, CupelError>`
- **ScoredItem** value type and **OverflowStrategy** closed enum (Throw/Truncate/Proceed, default Throw)

### Task 2: Scorer trait and 8 implementations
- **Scorer trait**: `fn score(&self, item: &ContextItem, all_items: &[ContextItem]) -> f64` with `as_any()` for downcasting
- **RecencyScorer**: rank-based on timestamp, 0.0 for no timestamp, 1.0 for single timestamped item
- **PriorityScorer**: rank-based on priority, same pattern as Recency
- **KindScorer**: weight map lookup with default weights (SystemPrompt=1.0 down to Message=0.2), validates non-negative/finite
- **TagScorer**: case-sensitive tag matching, normalized by total weight, clamped to 1.0
- **FrequencyScorer**: peer tag overlap proportion with `std::ptr::eq` self-exclusion, `eq_ignore_ascii_case` tag comparison
- **ReflexiveScorer**: futureRelevanceHint passthrough, finiteness check before clamp
- **CompositeScorer**: weighted average with weight normalization at construction, DFS cycle detection via data-pointer identity
- **ScaledScorer**: min-max normalization with `std::ptr::eq` self-identification, 0.5 for degenerate cases

## Decisions Made

| ID | Decision | Rationale |
|---|---|---|
| D-12-01-01 | Scorer trait requires `Any` supertrait + `as_any()` | Enables `downcast_ref` for CompositeScorer DFS cycle detection |
| D-12-01-02 | `HashMap<String, String>` for metadata | Avoids public serde_json dependency; metadata is opaque to pipeline |
| D-12-01-03 | Cycle detection uses `usize` (data pointer) in HashSet | Avoids lifetime issues with raw fat pointers |

## Deviations from Plan

None — plan executed exactly as written.

## Verification Results

- `cargo check -p assay-cupel`: zero errors, zero warnings
- `cargo clippy -p assay-cupel -- -D warnings`: passes clean

## Next Phase Readiness

All data model types and scorers are in place. Plan 12-02 (slicers, placers, pipeline) can proceed immediately — it depends only on the types and Scorer trait delivered here.
