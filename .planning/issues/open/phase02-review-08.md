---
area: testing
source: pr-review-phase-02
priority: low
---

# NullTraceCollector "doesn't throw" tests are vacuous

The tests for `NullTraceCollector` only assert that calling its methods does not throw. Because the no-op implementation intentionally does nothing, these tests pass trivially and provide no meaningful coverage. They should be replaced or supplemented with assertions that verify observable side-effects are absent (e.g., that no events are stored, that returned values have expected defaults).

**File(s):** `tests/Wollax.Cupel.Tests/Diagnostics/NullTraceCollectorTests.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure
