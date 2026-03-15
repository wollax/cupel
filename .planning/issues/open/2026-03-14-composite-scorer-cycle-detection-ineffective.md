---
created: 2026-03-14T00:00
title: CompositeScorer cycle detection is ineffective with owned Box types
area: scorer
provenance: github:wollax/cupel#49
files:
  - crates/cupel/src/scorer/composite.rs:88-90
---

## Problem

The `scorer_identity` function in `CompositeScorer` uses data pointer comparison (`*const dyn Scorer as *const () as usize`) for cycle detection via DFS. However, since all scorers in a `CompositeScorer` are owned `Box`es on the heap, they always have distinct addresses — a true structural cycle (A contains B contains A) cannot exist with owned children. The DFS consequently can never detect a cycle that actually exists at runtime, providing false assurance.

## Solution

Options:
1. Remove cycle detection entirely — owned `Box` types make cycles structurally impossible. Document this invariant instead.
2. If keeping validation for defense-in-depth, use a sealed internal trait to avoid leaking `as_any` on the public `Scorer` trait surface (which currently forces boilerplate on downstream implementors).
3. Evaluate whether `Scorer::as_any` (`#[doc(hidden)]` but still `pub`) can be removed from the public trait entirely.
