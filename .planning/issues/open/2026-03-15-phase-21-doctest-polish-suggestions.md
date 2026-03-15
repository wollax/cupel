---
created: 2026-03-15T15:30
title: Phase 21 doctest and example polish suggestions
area: docs
provenance: github:wollax/cupel#61
files:
  - crates/cupel/src/scorer/tag.rs:28
  - crates/cupel/examples/serde_roundtrip.rs:99
  - crates/cupel/src/placer/u_shaped.rs:22-25
  - crates/cupel/src/model/mod.rs
  - crates/cupel/src/slicer/greedy.rs
  - crates/cupel/examples/quota_slicing.rs:93-107
  - crates/cupel/src/model/context_item.rs
  - crates/cupel/src/scorer/composite.rs
  - crates/cupel/src/scorer/mod.rs
---

## Problem

PR review of Phase 21 surfaced 7 low-severity suggestions that were deferred in favor of fixing the 9 important issues. These are all documentation quality improvements, not correctness bugs.

## Solution

1. **scorer/tag.rs:28** — Use exact `2.0/3.0` instead of approximate `0.6666` in assertion, tighten tolerance to `1e-10`
2. **serde_roundtrip.rs:99** — Change `Ok(_) => println!("Unexpected success!")` to `panic!()` so validation regression causes non-zero exit
3. **u_shaped.rs doctest** — Add `assert_eq!(placed[1].content(), "B")` to demonstrate middle position in U-shape
4. **HashMap hidden-line convention** — Standardize: hide `# use std::collections::HashMap;` in doctests where it's boilerplate (currently mixed across model/mod.rs vs slicer/*.rs)
5. **quota_slicing.rs:93-107** — Extract per-kind analytics grouping into a local helper to reduce noise around the core QuotaSlice demonstration
6. **ContextItemBuilder doctest** — Assert fields that were actually set (kind, source, tags, future_relevance_hint) instead of only tokens/pinned
7. **CompositeScorer vs scorer module doctests** — Further differentiate (both now use multi-item assertions but still follow similar pattern)
