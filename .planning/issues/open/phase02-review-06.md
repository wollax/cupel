---
area: code-quality
source: pr-review-phase-02
priority: low
---

# DiagnosticTraceCollector detail level comparison is fragile

`DiagnosticTraceCollector` uses a `<` numeric comparison on the `TraceDetailLevel` enum to decide whether to record an event. This is brittle: if a new level is inserted between existing values with a non-sequential integer, or members are reordered, the comparison silently breaks. Prefer explicit membership checks or a dedicated helper that maps levels to included levels.

**File(s):** `src/Wollax.Cupel/Diagnostics/DiagnosticTraceCollector.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure
