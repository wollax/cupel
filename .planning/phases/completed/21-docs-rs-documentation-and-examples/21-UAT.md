---
status: complete
phase: 21-docs-rs-documentation-and-examples
source: 21-01-SUMMARY.md, 21-02-SUMMARY.md, 21-03-SUMMARY.md
started: 2026-03-15T15:35:00Z
updated: 2026-03-15T15:50:00Z
---

## Current Test

[testing complete]

## Tests

### 1. README renders as crate-level docs
expected: `cargo doc --open --no-deps --all-features` shows README as crate root — intro, glossary, quickstarts, scorer table, serde section
result: pass

### 2. Serde feature badge appears on gated items
expected: In the generated docs, types behind `#[cfg(feature = "serde")]` show an "Available on crate feature serde only" badge
result: pass

### 3. Module docs visible in sidebar navigation
expected: Clicking into model, scorer, slicer, placer, pipeline, error modules shows //! doc comments with purpose, type lists, and examples
result: pass

### 4. Scorer comparison table in scorer module
expected: scorer module page shows an 8-row table mapping scorers to strategy, input, and use case
result: pass

### 5. Pipeline stage flow in pipeline module
expected: pipeline module page describes the 6 stages (Classify, Score, Deduplicate, Sort, Slice, Place) with explanations
result: pass

### 6. basic_pipeline example output
expected: `cargo run --example basic_pipeline` prints 7 selected items with kind, tokens, pinned status, and content preview
result: pass

### 7. serde_roundtrip example output
expected: `cargo run --example serde_roundtrip --features serde` prints JSON roundtrip of ContextItem and ContextBudget, plus a validation rejection
result: pass

### 8. quota_slicing example output
expected: `cargo run --example quota_slicing` prints selected items and per-kind breakdown showing quota effects
result: pass

## Summary

total: 8
passed: 8
issues: 0
pending: 0
skipped: 0

## Gaps
