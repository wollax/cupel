---
area: testing
source: pr-review-phase-02
priority: low
---

# TraceEvent inequality tests only vary Stage

The `TraceEvent` inequality tests currently only verify that two events with different `Stage` values are not equal. They do not test inequality when `Duration` or `ItemCount` differ while other fields are equal. This leaves equality/hash-code correctness for those properties unverified.

**File(s):** `tests/Wollax.Cupel.Tests/Models/TraceEventTests.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure
