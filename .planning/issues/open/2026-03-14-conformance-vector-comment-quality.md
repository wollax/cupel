---
created: 2026-03-14T00:00
title: Clean up conformance vector comment quality
area: docs
provenance: github:wollax/cupel#45
files:
  - spec/conformance/required/slicing/knapsack-basic.toml:6-28
  - spec/conformance/required/scoring/composite-weighted.toml:24-27
  - spec/conformance/required/pipeline/pinned-items.toml:22-27
  - spec/conformance/required/placing/u-shaped-basic.toml
  - spec/conformance/required/placing/u-shaped-equal-scores.toml
---

## Problem

PR review of Phase 15 identified comment quality issues across 5 conformance vector files. All issues also exist in the Rust crate's vendored copies (byte-exact parity constraint), so they must be fixed in both locations simultaneously.

**Critical:**
- `knapsack-basic.toml` lines 6-28: Abandoned first scenario (items `expensive/cheap-a/cheap-b`) remains in comments but those items don't exist in the test data. The redesign rationale also contains an incorrect greedy comparison — it claims greedy would only select `small-a`, but greedy actually selects both `small-a` and `small-b` (both fit within budget).

**Important:**
- `composite-weighted.toml` lines 24-27: Scratchpad text `# Wait — let me recompute priority` is conversational debris, not clean spec documentation.
- `pinned-items.toml` lines 22-24: Greedy fill trace omits explicit density-sort step (inconsistent with `composite-greedy-chronological.toml` which shows this step clearly).
- `pinned-items.toml` line 27: `new(score=1.0)` in merged list doesn't clarify score source (RecencyScorer).
- `u-shaped-basic.toml` and `u-shaped-equal-scores.toml`: `right[N]`/`left[N]` notation ambiguous with spec pseudocode pointer variable names — prefer `result[N]`.

## Solution

Fix during Phase 17 (Crate Migration) when the Rust crate starts vendoring from the spec tree. At that point the spec becomes canonical and the Rust crate copies from it, so fixing in the spec first is the right sequence. Update both spec and Rust crate copies to maintain parity until migration completes.
