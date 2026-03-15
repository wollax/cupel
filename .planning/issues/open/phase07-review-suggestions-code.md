---
title: "Phase 7 review suggestions — code"
source: PR review (Phase 7)
priority: low
area: code-quality
---

# Phase 7 Code Review Suggestions

Suggestions from the Phase 7 PR review that were not fixed immediately.

## Code

1. **`Enum.IsDefined` reflection in `WithOverflowStrategy`** — Replace with manual range check or switch for zero-allocation consistency (`PipelineBuilder.cs:175`)
2. **`Proceed` allocates `List<ContextItem>` to wrap `merged`** — Use array (already fixed) or read-only projection adapter (`CupelPipeline.cs`)
3. **`ReportBuilder.Build` not guarded against double-call** — Add comment/assertion that builder is single-use (`ReportBuilder.cs`)
4. **Missing `TotalTokensIncluded` property on `SelectionReport`** — Consumers must iterate `Included` to get selected token count
5. **`ScoredTooLow` and `Filtered` declared but never emitted** — Already marked as reserved with `<remarks>`, but consider removing entirely until feature exists
6. **Slicer re-association relies on undocumented reference-identity contract on `ISlicer`** — Document that slicers must return the exact same `ContextItem` references (`CupelPipeline.cs:262`)
7. **No `OverflowEvent?` property on `SelectionReport`** — Overflow event only accessible via callback/exception, not in the report itself
8. **Streaming path (`ExecuteStreamAsync`) ignores `_overflowStrategy`** — Either document limitation or guard at entry
