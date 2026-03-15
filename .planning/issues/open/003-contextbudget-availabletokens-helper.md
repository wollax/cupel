---
title: "Add AvailableTokens computed property to ContextBudget"
area: core-models
priority: low
source: pr-review-phase-1
---

# AvailableTokens helper property

Consider adding `int AvailableTokens => MaxTokens - OutputReserve - ReservedSlots.Values.Sum()` as a convenience for callers who need to know the effective budget for unreserved context.

**Consideration:** Wait until pipeline implementation (Phase 5) to see if this calculation is needed in multiple places.
