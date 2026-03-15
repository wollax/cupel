---
phase: 24-diagnostics-spec-chapter
plan: 02
status: complete
started: 2026-03-15T19:40:09Z
completed: 2026-03-15T19:41:47Z
duration: 1m 38s
---

# Plan 24-02 Summary: Diagnostics Spec Consumer Side

Created the remaining two diagnostics sub-pages and registered the complete chapter in SUMMARY.md. The diagnostics spec chapter is now complete and navigable.

## Tasks Completed

### Task 1: exclusion-reasons.md and selection-report.md
- **Commit:** `645e01e`
- Created `spec/src/diagnostics/exclusion-reasons.md` with:
  - Overview linking to SelectionReport
  - `ExclusionReason` — 8 data-carrying variants in a 5-column table (Variant, Description, Fields, Emitted by, Status); 4 Active, 4 Reserved; rationale + rejected alternative for data-carrying design; JSON examples for BudgetExceeded, Deduplicated, NegativeTokens; wire format prose
  - `InclusionReason` — 3 variants (Scored, Pinned, ZeroToken) with rationale for non-data-carrying design; JSON example
  - Conformance Notes (5 MUST statements)
- Created `spec/src/diagnostics/selection-report.md` with:
  - Overview positioning SelectionReport as primary diagnostic artifact
  - How to Obtain — pseudocode (`OBTAIN-REPORT`) with rationale + rejected alternative
  - SelectionReport fields table (5 fields) with complete JSON example
  - IncludedItem field table (3 fields) with JSON example
  - ExcludedItem field table (3 fields) with two JSON examples (BudgetExceeded and Deduplicated); inline rationale for data-carrying variant design + rejected alternative
  - Conformance Notes (5 MUST statements including sort invariant)

### Task 2: SUMMARY.md + book build
- **Commit:** `b52eb9d`
- Added Diagnostics chapter entry with 4 sub-pages to `spec/src/SUMMARY.md`, positioned after Placers and before `# Conformance`
- Verified `mdbook build` succeeds without errors

## Deviations

None. All tasks executed as specified.

## Key Decisions Resolved

| Decision | Resolution |
|----------|------------|
| D2 — Data-carrying ExclusionReason | Implemented as specified; each variant carries its own fields; fieldless rejected alternative documented |
| D3 — Post-call extraction | SelectionReport obtained via `collector.BUILD-REPORT()` after pipeline run; rationale and rejected alternative documented |
| D4 — Reserved variants | All 4 reserved variants defined with fields; reserved status documented; forward-compatibility rationale documented |
| D6 — Absent fields, no nulls | Applied throughout all JSON examples in both files |

## Commits

| SHA | Message |
|-----|---------|
| `645e01e` | docs(24-02): add exclusion-reasons and selection-report spec pages |
| `b52eb9d` | docs(24-02): register Diagnostics chapter in SUMMARY.md |

## Verification Status

All must_haves satisfied:
- `spec/src/diagnostics/exclusion-reasons.md` — 8 data-carrying ExclusionReason variants with Fields column, 4 reserved variants documented, 3 InclusionReason variants, BudgetExceeded JSON example with `item_tokens` + `available_tokens`, Conformance Notes with MUST
- `spec/src/diagnostics/selection-report.md` — pseudocode for obtaining report, SelectionReport fields table with `total_candidates`, complete JSON example, IncludedItem and ExcludedItem field tables + JSON examples, `deduplicated_against` present/absent per reason documented, sort invariant in Conformance Notes
- `spec/src/SUMMARY.md` — `[Diagnostics](diagnostics.md)` entry with 4 sub-pages, after Placers, before `# Conformance`
- All 5 spec files exist and are referenced from SUMMARY.md
- No C#, Rust, or language-specific syntax in any file
- snake_case throughout all JSON examples; absent fields omitted (no nulls)
- MUST keyword appears only in Conformance Notes sections
- `mdbook build` succeeded without errors
