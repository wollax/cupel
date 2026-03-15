---
created: 2026-03-14T00:00
title: UShapedPlacer expect on Vec<Option> is fragile invariant
area: scorer
provenance: github:wollax/cupel#50
files:
  - crates/cupel/src/placer/u_shaped.rs:52
---

## Problem

`UShapedPlacer` uses `Vec<Option<ContextItem>>` and calls `.expect()` to unwrap all slots, relying on a behavioral invariant (all slots filled by left/right pointer arithmetic) enforced by prose, not the type system. If the placement logic ever changes or introduces an off-by-one, this becomes a production panic with no error recovery path.

## Solution

Options:
1. Replace `Vec<Option<ContextItem>>` with direct index writes into a pre-sized `Vec<ContextItem>` (collect left/right halves separately and concatenate)
2. Replace `expect` with `ok_or(CupelError::PipelineConfig(...))` if `Placer::place` is ever allowed to return `Result`
3. At minimum, add a unit test that exercises edge cases (0 items, 1 item, 2 items) to verify the invariant
