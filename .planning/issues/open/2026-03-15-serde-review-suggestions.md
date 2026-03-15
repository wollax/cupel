---
created: 2026-03-15T12:00
title: Serde implementation polish from Phase 20 PR review
area: testing
provenance: github:wollax/cupel#59
files:
  - crates/cupel/tests/serde.rs
  - crates/cupel/src/model/context_item.rs
  - crates/cupel/src/model/overflow_strategy.rs
  - crates/cupel/src/model/scored_item.rs
  - crates/cupel/src/slicer/quota.rs
---

## Problem

Phase 20 PR review surfaced 8 suggestions that are nice-to-have improvements but not blocking merge. These are polish items for serde implementation quality.

## Suggestions

### Tests
1. **Wire format snapshot test for ContextItem** — pin the exact JSON field names to catch silent renames
2. **OverflowStrategy unknown variant rejection** — assert `"Unknown"` variant fails deserialization
3. **Case-insensitive roundtrip test** — verify `"document"` deserializes equal to `ContextKind::new("Document")`
4. **QuotaEntry boundary value tests** — test `require == cap`, `0.0`, `100.0` exact boundaries

### Code simplification
5. **ContextItem Raw struct** — use direct `#[serde(default)]` on `Vec`/`HashMap` fields instead of `Option<T>` wrapping (eliminates `if let Some` branches)
6. **Combine cfg_attr lines** — merge two `#[cfg_attr]` into one on OverflowStrategy and ScoredItem

### Validation gaps
7. **ContextItemBuilder: validate tokens >= 0** — inconsistent with ContextBudget's strict non-negative enforcement
8. **ScoredItem score: validate range** — accepts NaN/infinity/negative despite convention of [0.0, 1.0]

## Solution

Items 1-4: Add tests to `crates/cupel/tests/serde.rs`
Items 5-6: Minor refactors, no behavior change
Items 7-8: Design decision needed — are these invariants or conventions?
