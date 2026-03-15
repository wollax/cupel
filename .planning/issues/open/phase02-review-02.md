---
area: docs
source: pr-review-phase-02
priority: low
---

# SelectionReport doc should reference ITraceCollector not DiagnosticTraceCollector

The XML documentation in `SelectionReport.cs` references `DiagnosticTraceCollector` (the concrete implementation) rather than `ITraceCollector` (the abstraction). This ties public API documentation to an implementation detail and will become misleading if additional collectors are introduced.

**File(s):** `src/Wollax.Cupel/Models/SelectionReport.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure
