---
area: docs
source: pr-review-phase-02
priority: low
---

# IScorer should document it doesn't receive ITraceCollector

`IScorer` currently has no documentation clarifying that per-item trace events are the orchestrator's responsibility, not the scorer's. Without this note, implementors may attempt to accept or use an `ITraceCollector` directly, creating confusion about where item-level instrumentation belongs in the pipeline architecture.

**File(s):** `src/Wollax.Cupel/Interfaces/IScorer.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure
