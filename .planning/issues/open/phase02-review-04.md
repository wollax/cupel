---
area: api-design
source: pr-review-phase-02
priority: low
---

# ExclusionReason needs wiring to TraceEvent or ItemTraceEvent

`ExclusionReason.cs` is defined but there is currently no field on `TraceEvent` or `ItemTraceEvent` capable of carrying it. Without a transport mechanism, the enum value cannot be surfaced to consumers during pipeline execution. This is likely Phase 7 work when exclusion reporting is fully implemented.

**File(s):** `src/Wollax.Cupel/Enums/ExclusionReason.cs`, `src/Wollax.Cupel/Models/TraceEvent.cs`, `src/Wollax.Cupel/Models/ItemTraceEvent.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure (deferred to Phase 7)
