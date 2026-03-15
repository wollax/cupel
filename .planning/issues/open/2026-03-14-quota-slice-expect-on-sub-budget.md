---
created: 2026-03-14T00:00
title: QuotaSlice expect on sub-budget hides potential float edge cases
area: scorer
provenance: github:wollax/cupel#51
files:
  - crates/cupel/src/slicer/quota.rs:194-195
---

## Problem

`QuotaSlice::slice` calls `.expect()` on `ContextBudget::new()` for sub-budget construction. While the assertion is largely sound in practice (defensive guard ensures `cap >= kind_budget >= 0`), the `expect` hides the failure from the caller. `ContextBudget::new` already returns `Result` — propagating that error would require `Slicer::slice` to return `Result`, which is a trait signature change.

## Solution

Options:
1. Change `Slicer::slice` signature to return `Result<Vec<ContextItem>, CupelError>` (breaking change, affects all slicer implementations)
2. Add additional defensive checks before the `expect` to make the invariant airtight (e.g., clamp `cap` and `kind_budget` to non-negative)
3. Keep as-is but add a comment explaining the full invariant chain
