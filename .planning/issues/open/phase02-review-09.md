---
area: testing
source: pr-review-phase-02
priority: low
---

# Add test for callback exception propagation

There is no test documenting what happens when a callback registered with `DiagnosticTraceCollector` throws an exception. If the collector silently swallows exceptions, callers cannot detect failures in their callbacks. The intended behavior (swallow, propagate, or wrap) should be explicitly tested to prevent accidental behavior changes.

**File(s):** `tests/Wollax.Cupel.Tests/Diagnostics/DiagnosticTraceCollectorTests.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure
