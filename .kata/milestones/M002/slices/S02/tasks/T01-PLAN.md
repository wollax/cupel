---
estimated_steps: 5
estimated_files: 5
---

# T01: Fix diagnostics spec files

**Slice:** S02 — Spec Editorial Debt
**Milestone:** M002

## Description

Apply all 11 editorial changes across the five diagnostics chapter files. Issues addressed: event ordering rule, item_count sentinel note, duplicate Sort rejected-alternative block, observer callback MAY labeling, Conformance Notes section reorder, null-path prose cross-reference, selection-report section reorder, Deduplicated rationale cross-reference, rejected-alternative formatting, reserved variant JSON examples, and summary table column header rename.

## Steps

1. **`spec/src/diagnostics/events.md`** (3 changes):
   - In the `item_count` field table row, add a note to the Description cell: append "(For item-level events, the value is `1`, which is a sentinel indicating a single item was processed — it carries no aggregate meaning.)"
   - In the Conformance Notes section, add after the existing "emitted after the stage completes" sentence: "Item-level events for a stage MUST precede the corresponding stage-level event. Item-level events are recorded as each item is processed; the stage-level event is emitted only after all items in the stage have been processed. Conformance vectors that test the `events` list must be authored to reflect this ordering."
   - Remove the "Rejected alternative:" block under the "Sort is omitted." paragraph (the block that begins "**Rejected alternative:** Including `Sort` for completeness..."). Keep the primary "Sort is omitted." paragraph intact.

2. **`spec/src/diagnostics/trace-collector.md`** (3 changes):
   - Move the entire Conformance Notes section (heading + all bullets) to appear before the Observer Callback section. Resulting section order: Contract → NullTraceCollector → DiagnosticTraceCollector → TraceDetailLevel → Conformance Notes → Observer Callback.
   - Change the Observer Callback section heading from `## Observer Callback` to `## Observer Callback (Optional — MAY)`.
   - Replace the `NullTraceCollector` rationale paragraph body ("The null collector ensures zero overhead…") with a cross-reference: "See [Null-Path Guarantee](../diagnostics.md#null-path-guarantee) for the rationale and implementation contract."

3. **`spec/src/diagnostics/selection-report.md`** (3 changes):
   - Move the `## SelectionReport Fields` section (table + everything after Overview up to `## IncludedItem`) to appear before `## How to Obtain`. New order: Overview → SelectionReport Fields → How to Obtain → IncludedItem → ExcludedItem → Conformance Notes.
   - In the ExcludedItem section, find the long rationale paragraph starting "*Rationale: `deduplicated_against` is modelled as a field...*" through "*Rejected alternative: nullable `deduplicated_against` on `ExcludedItem`...*". Replace the "Rejected alternative" sentence inside that block with a cross-reference: "See [Exclusion Reasons](exclusion-reasons.md) for the full rationale on data-carrying variants."
   - In the Conformance Notes section, find the final note that uses inline italics for a "Rejected alternative" (e.g., "*Rejected alternative: insertion order...*"). Convert it to a bold standalone paragraph: "**Rejected alternative:** insertion order — less useful for diagnosis; the highest-scored excluded item is rarely the first item processed."

4. **`spec/src/diagnostics/exclusion-reasons.md`** (1 change):
   - After the existing `BudgetExceeded`, `Deduplicated`, and `NegativeTokens` JSON examples, add JSON examples for the four reserved variants. Place them in the same section, under a subheading or separated by brief prose "Reserved variant examples:":
     - `ScoredTooLow`: `{ "reason": "ScoredTooLow", "score": 0.12, "threshold": 0.25 }`
     - `QuotaCapExceeded`: `{ "reason": "QuotaCapExceeded", "kind": "ToolOutput", "cap": 3, "actual": 4 }`
     - `QuotaRequireDisplaced`: `{ "reason": "QuotaRequireDisplaced", "displaced_by_kind": "SystemPrompt" }`
     - `Filtered`: `{ "reason": "Filtered", "filter_name": "max_age_filter" }`

5. **`spec/src/diagnostics.md`** (1 change):
   - In the Summary table, rename the column header `Defined in` to `Spec page`. The table header row currently reads `| Type | Role | Defined in |` — change to `| Type | Role | Spec page |`.

## Must-Haves

- [ ] `events.md` item_count field description includes sentinel note
- [ ] `events.md` Conformance Notes includes item-level events MUST precede stage-level event rule
- [ ] `events.md` "Rejected alternative" Sort block (the one starting "**Rejected alternative:** Including `Sort` for completeness...") is removed; "Sort is omitted." paragraph remains
- [ ] `trace-collector.md` section order is Contract → NullTraceCollector → DiagnosticTraceCollector → TraceDetailLevel → Conformance Notes → Observer Callback
- [ ] `trace-collector.md` Observer Callback heading is labeled "(Optional — MAY)"
- [ ] `trace-collector.md` null-path paragraph replaced with cross-reference to `diagnostics.md`
- [ ] `selection-report.md` SelectionReport Fields section appears before How to Obtain
- [ ] `selection-report.md` ExcludedItem rationale block contains cross-reference to exclusion-reasons.md instead of inline repeat rationale
- [ ] `selection-report.md` Conformance Notes rejected-alternative uses bold standalone paragraph
- [ ] `exclusion-reasons.md` contains JSON examples for all 4 reserved variants
- [ ] `diagnostics.md` Summary table header reads "Spec page" not "Defined in"

## Verification

- `grep -c "Defined in" spec/src/diagnostics.md` → 0
- `grep -n "Rejected alternative.*Sort\|Including.*Sort.*completeness" spec/src/diagnostics/events.md` → 0
- `grep -n "item-level events.*MUST precede" spec/src/diagnostics/events.md` → at least 1 match
- `grep -n "Optional.*MAY\|MAY.*Optional" spec/src/diagnostics/trace-collector.md` → at least 1 match
- `grep -n "ScoredTooLow" spec/src/diagnostics/exclusion-reasons.md` → match (JSON example added)
- `grep -n "QuotaCapExceeded" spec/src/diagnostics/exclusion-reasons.md` → match (JSON example added)

## Observability Impact

- Signals added/changed: None (spec-only changes)
- How a future agent inspects this: Read the spec files and compare section order / content against the target described in the research
- Failure state exposed: None

## Inputs

- `spec/src/diagnostics/events.md` — current content (read before editing to locate exact target text)
- `spec/src/diagnostics/trace-collector.md` — current content
- `spec/src/diagnostics/selection-report.md` — current content
- `spec/src/diagnostics/exclusion-reasons.md` — current content (existing JSON examples are the anchors for inserting reserved variant examples)
- `spec/src/diagnostics.md` — current content (Summary table is the target)
- S02-RESEARCH.md pitfall note: when moving Conformance Notes in trace-collector.md, verify the null-path prose lives in the NullTraceCollector section (not the Overview); replace only that paragraph, not the entire section
- S02-RESEARCH.md pitfall note: remove only the "Rejected alternative:" block in events.md — the "Sort is omitted." primary paragraph immediately before it must be kept

## Expected Output

- `spec/src/diagnostics/events.md` — item_count sentinel note added; event ordering conformance rule added; duplicate Sort rejected-alternative block removed
- `spec/src/diagnostics/trace-collector.md` — section order corrected; Observer Callback labeled MAY; null-path prose replaced with cross-reference
- `spec/src/diagnostics/selection-report.md` — SelectionReport Fields before How to Obtain; cross-reference in ExcludedItem; bold rejected-alternative paragraph in Conformance Notes
- `spec/src/diagnostics/exclusion-reasons.md` — JSON examples for all 4 reserved variants
- `spec/src/diagnostics.md` — "Spec page" column header
