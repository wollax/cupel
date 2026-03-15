---
phase: 24-diagnostics-spec-chapter
plan: 01
status: complete
started: 2026-03-15T19:32:05Z
completed: 2026-03-15T19:36:24Z
duration: 4m 19s
---

# Plan 24-01 Summary: Diagnostics Spec Scaffold (Producer Side)

Created three new spec files establishing the diagnostics chapter structure and producer-side types.

## Tasks Completed

### Task 1: Top-level diagnostics.md
- **Commit:** `90faca5`
- Created `spec/src/diagnostics.md` with: overview, ownership model (with rationale + rejected alternative), null-path guarantee (with rationale), Mermaid data-flow diagram (5 nodes), and 7-row summary table linking all diagnostic types.

### Task 2: trace-collector.md and events.md
- **Commit:** `e271648`
- Created `spec/src/diagnostics/trace-collector.md` with: contract table (3 members), NullTraceCollector, DiagnosticTraceCollector (construction parameters + behavior), TraceDetailLevel enum (2 values, ordered), observer callback capability, and Conformance Notes.
- Created `spec/src/diagnostics/events.md` with: TraceEvent fields table + 2 JSON examples (stage-level, item-level), PipelineStage enum (5 values, Sort omitted with rationale), OverflowEvent fields table + JSON example, and Conformance Notes.

## Deviations

**One minor deviation:** The PipelineStage section contained a `Callers MUST handle unknown values...` sentence outside a Conformance Notes section. Fixed immediately by moving the normative requirement to prose form ("Callers should handle...") and pointing to the Conformance Notes below, where the formal MUST requirement lives.

## Key Decisions Resolved

| Decision | Resolution |
|----------|------------|
| D1 — No Sort in PipelineStage | Omitted; rationale and rejected alternative documented in events.md |
| D5 — OverflowEvent data-only | Delivery mechanism left to implementations; rationale documented |
| D7 — duration_ms float64 | float64 milliseconds; rationale and rejected alternatives documented |
| D8 — Observer callback capability | Defined as capability without prescribing mechanism; rationale documented |

## Verification Status

All must_haves satisfied:
- `spec/src/diagnostics.md` — exists, 5 sections, 7-type summary table, Mermaid diagram, no MUST keywords
- `spec/src/diagnostics/trace-collector.md` — contract table, NullTraceCollector, DiagnosticTraceCollector, TraceDetailLevel, observer callback, Conformance Notes
- `spec/src/diagnostics/events.md` — TraceEvent (2 JSON examples), PipelineStage (5 values, no Sort), OverflowEvent (JSON example), Conformance Notes
- Language-agnostic throughout (pseudocode + JSON only)
- snake_case in all JSON examples; absent fields omitted
- MUST only in Conformance Notes sections
