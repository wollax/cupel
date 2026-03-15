---
title: "Validate total ReservedSlots don't exceed MaxTokens"
area: core-models
priority: low
source: pr-review-phase-1
---

# Cross-field validation: ReservedSlots total vs MaxTokens

Currently individual ReservedSlots values are validated non-negative, but the sum could exceed MaxTokens. Consider adding a cross-field check.

**Consideration:** This interacts with OutputReserve and EstimationSafetyMarginPercent — the full budget arithmetic should be designed holistically in Phase 5 when the pipeline consumes budgets.
