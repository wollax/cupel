---
area: code-quality
source: pr-review-phase-02
priority: low
---

# PipelineStage enum should have explicit integer assignments

`PipelineStage.cs` does not assign explicit integer values to its members, unlike `TraceDetailLevel` which does. Explicit assignments make serialization round-trips, database storage, and future insertions safer by preventing accidental renumbering when members are reordered or added.

**File(s):** `src/Wollax.Cupel/Enums/PipelineStage.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure
