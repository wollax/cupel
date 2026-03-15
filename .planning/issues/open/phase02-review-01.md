---
area: docs
source: pr-review-phase-02
priority: low
---

# ISlicer sort precondition should be on interface summary

The `ISlicer.cs` parameter documentation notes that candidates must be "sorted by score descending", but this is a precondition that callers of the interface must satisfy. It belongs prominently in the interface-level summary, not buried in a param doc, so that implementors and callers encounter the constraint immediately.

**File(s):** `src/Wollax.Cupel/Interfaces/ISlicer.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure
