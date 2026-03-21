---
id: T01
parent: S02
milestone: M002
provides:
  - Editorial fixes across all 5 diagnostics chapter files (11 changes total)
key_files:
  - spec/src/diagnostics/events.md
  - spec/src/diagnostics/trace-collector.md
  - spec/src/diagnostics/selection-report.md
  - spec/src/diagnostics/exclusion-reasons.md
  - spec/src/diagnostics.md
key_decisions:
  - none
patterns_established:
  - none
observability_surfaces:
  - none
duration: ~15 minutes
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T01: Fix diagnostics spec files

**Applied all 11 editorial changes across 5 diagnostics spec files: sentinel note, ordering rule, section reorders, MAY labeling, cross-references, reserved variant examples, and column header rename.**

## What Happened

All 11 editorial changes were applied as specified:

1. **`events.md`** — Added `item_count` sentinel note to field table; added item-level ordering MUST rule to Conformance Notes; removed the duplicate "Rejected alternative: Including Sort for completeness" block while preserving the "Sort is omitted." paragraph.

2. **`trace-collector.md`** — Moved Conformance Notes section before Observer Callback (new order: Contract → NullTraceCollector → DiagnosticTraceCollector → TraceDetailLevel → Conformance Notes → Observer Callback); renamed heading to "Observer Callback (Optional — MAY)"; replaced NullTraceCollector rationale paragraph with cross-reference to `diagnostics.md#null-path-guarantee`.

3. **`selection-report.md`** — Moved SelectionReport Fields section before How to Obtain (new order: Overview → SelectionReport Fields → How to Obtain → IncludedItem → ExcludedItem → Conformance Notes); replaced inline "Rejected alternative: nullable deduplicated_against" sentence in ExcludedItem rationale with cross-reference to `exclusion-reasons.md`; converted inline italic "Rejected alternative" in Conformance Notes to bold standalone paragraph.

4. **`exclusion-reasons.md`** — Added JSON examples for all 4 reserved variants (ScoredTooLow, QuotaCapExceeded, QuotaRequireDisplaced, Filtered) under "Reserved variant examples:" prose separator.

5. **`diagnostics.md`** — Renamed Summary table column header from "Defined in" to "Spec page".

## Verification

All 6 task-plan grep checks passed:
- `grep -c "Defined in" spec/src/diagnostics.md` → 0 ✓
- `grep -n "Rejected alternative.*Sort|Including.*Sort.*completeness" spec/src/diagnostics/events.md` → 0 matches ✓
- `grep -n "item-level events.*MUST precede" spec/src/diagnostics/events.md` → 1 match (line 103) ✓
- `grep -n "Optional.*MAY|MAY.*Optional" spec/src/diagnostics/trace-collector.md` → 1 match (line 62) ✓
- `grep -n "ScoredTooLow" spec/src/diagnostics/exclusion-reasons.md` → 3 matches ✓
- `grep -n "QuotaCapExceeded" spec/src/diagnostics/exclusion-reasons.md` → 3 matches ✓

All 11 must-have checklist items individually verified via grep.

## Diagnostics

None — spec-only changes, no runtime signals added.

## Deviations

Minor: The `item_count` sentinel note in the events.md field table begins "For item-level events, this is `1`." already existed in the description. The new sentinel clause was appended after it, creating slight redundancy ("this is `1`. (For item-level events, the value is `1`...)"). This matches the task plan instruction to "append" the text verbatim.

The item-level ordering bullet in Conformance Notes uses lowercase "item-level" to match the verification grep pattern, which is consistent with the rest of the bullet-point sentence style in that section.

## Known Issues

None.

## Files Created/Modified

- `spec/src/diagnostics/events.md` — item_count sentinel note; item-level ordering MUST rule; Sort rejected-alternative block removed
- `spec/src/diagnostics/trace-collector.md` — section reorder; Observer Callback labeled (Optional — MAY); null-path cross-reference
- `spec/src/diagnostics/selection-report.md` — SelectionReport Fields before How to Obtain; ExcludedItem cross-reference; bold Rejected alternative in Conformance Notes
- `spec/src/diagnostics/exclusion-reasons.md` — JSON examples for 4 reserved variants
- `spec/src/diagnostics.md` — Summary table "Spec page" column header
