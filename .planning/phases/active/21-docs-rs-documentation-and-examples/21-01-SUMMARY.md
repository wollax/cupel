---
phase: "21"
plan: "01"
subsystem: "documentation"
tags: ["readme", "docs-rs", "cargo-toml", "lib-rs", "crate-docs"]
requires: ["20"]
provides: ["crate-level-docs", "docs-rs-config", "readme"]
affects: ["21-02", "21-03"]
tech-stack:
  added: []
  patterns: ["include-str-readme", "doc-auto-cfg", "docs-rs-metadata"]
key-files:
  created: []
  modified:
    - "crates/cupel/README.md"
    - "crates/cupel/src/lib.rs"
    - "crates/cupel/Cargo.toml"
decisions: []
metrics:
  duration: "<1 min"
  completed: "2026-03-15"
---

# Phase 21 Plan 01: Crate README & docs.rs Configuration Summary

## Outcome

All tasks completed successfully. The crate README serves as the single source of truth for both crates.io and docs.rs, wired into lib.rs via `include_str!`. docs.rs metadata is configured with `all-features = true` and `doc_auto_cfg` for automatic feature-gate badges.

## Tasks Completed

### Task 1: Write crate README with quickstart narrative
- Title and tagline from Cargo.toml description
- Conceptual intro explaining the score-slice-place pipeline model
- Glossary table defining 8 key terms (ContextItem, ContextBudget, Scorer, Slicer, Placer, Pipeline, ContextKind, ContextSource)
- Minimal quickstart example: RecencyScorer + GreedySlice + ChronologicalPlacer (compiles as doctest)
- Realistic multi-scorer example: CompositeScorer with KindScorer + RecencyScorer wrapped in ScaledScorer, QuotaSlice, UShapedPlacer (compiles as doctest)
- Scorer comparison table mapping 8 scorers to use cases and mechanisms
- Serde feature section with Cargo.toml snippet and validation-on-deserialize note
- MIT license footer

### Task 2: Wire lib.rs crate docs and configure Cargo.toml for docs.rs
- Added `#![doc = include_str!("../README.md")]` at top of lib.rs
- Added `#![cfg_attr(docsrs, feature(doc_auto_cfg))]` for automatic feature badges
- `examples/**/*.rs` already in Cargo.toml include array
- `[package.metadata.docs.rs]` section with `all-features = true` and `rustdoc-args = ["--cfg", "docsrs"]`

## Verification

- `cargo doc --no-deps --all-features`: zero warnings
- `cargo test --doc` (no features): 33 doctests pass
- `cargo test --doc --all-features` (with serde): 33 doctests pass
- `cargo package --list`: includes README.md, src/**, conformance/**, tests/**

## Deviations

None.

## Commits

| Hash | Message |
|------|---------|
| 3be0382 | docs(21-01): write crate README with quickstart narrative |
| 1e0a178 | docs(21-01): wire lib.rs crate docs and configure Cargo.toml for docs.rs |
