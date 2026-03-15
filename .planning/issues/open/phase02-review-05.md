---
area: docs
source: pr-review-phase-02
priority: low
---

# ITraceCollector.IsEnabled should document constancy requirement

The `ITraceCollector` interface does not document whether `IsEnabled` is expected to remain constant for the lifetime of a pipeline run. Nothing in the contract prevents a mutable implementation from toggling `IsEnabled` mid-run, which could leave the pipeline in an inconsistent tracing state. The summary should explicitly state whether callers may cache the value or must re-check it on every call.

**File(s):** `src/Wollax.Cupel/Interfaces/ITraceCollector.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure
