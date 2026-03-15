---
area: docs
source: pr-review-phase-02
priority: low
---

# ContextResult.Report doc ties nullability to NullTraceCollector

The XML documentation on `ContextResult.Report` explains the property's nullability in terms of `NullTraceCollector` specifically. This couples the public API contract to a concrete implementation. The doc should instead describe the condition in terms of pipeline behavior (e.g., "null when tracing is disabled or no collector is provided") so it remains accurate regardless of which collector is used.

**File(s):** `src/Wollax.Cupel/Models/ContextResult.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure
