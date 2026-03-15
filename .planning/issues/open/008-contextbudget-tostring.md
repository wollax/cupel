---
title: "Add ToString override to ContextBudget"
area: core-models
priority: low
source: pr-review-phase-1
---

# Readable ToString for ContextBudget

ContextBudget.ToString() currently returns the default type name. A descriptive override (e.g., `ContextBudget { MaxTokens=128000, TargetTokens=100000 }`) would improve debuggability.

**Consideration:** Low priority — add when trace/diagnostics infrastructure is built in Phase 2.
