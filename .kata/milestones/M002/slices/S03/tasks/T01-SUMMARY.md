---
id: T01
parent: S03
milestone: M002
provides:
  - DI-1 ruling: separate CountQuotaSlice decorator (not a QuotaSlice extension)
  - DI-2 ruling: non-exclusive tag semantics (item counts toward all matching RequireCount constraints)
key_files:
  - .planning/design/count-quota-design-notes.md
key_decisions:
  - DI-1: CountQuotaSlice is a separate decorator; callers compose CountQuotaSlice(inner: QuotaSlice(...)) for combined constraints
  - DI-2: Non-exclusive tag semantics — an item tagged ["critical","urgent"] counts +1 toward both RequireCount("critical") and RequireCount("urgent")
patterns_established:
  - Composition over unification: separate slicer types for semantically distinct constraint systems
  - Non-exclusive multi-tag counting as the mandatory (not configurable) semantics for count-quota
observability_surfaces:
  - none (pure design artifact)
duration: 1 session
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T01: Debate algorithm architecture and tag non-exclusivity (DI-1, DI-2)

**Settled DI-1 (separate decorator) and DI-2 (non-exclusive tag semantics) — the two most foundational and irreversible design decisions for count-quota.**

## What Happened

Re-read `spec/src/slicers/quota.md` to understand the existing `QuotaSlice` decorator pattern (partitions by kind, runs `DISTRIBUTE-BUDGET`, delegates per-kind to inner slicer), then re-read Sections 1 and 2 plus the Downstream Inputs from the challenger brainstorm report. Conducted formal two-position debates for both DI-1 and DI-2.

**DI-1 (algorithm architecture):**

The deciding question is whether callers commonly need both count AND percentage constraints on the same kind simultaneously. They can need both (e.g., "≥2 tool calls AND tool calls ≤30% of budget"), but this is served naturally by decorator composition: `CountQuotaSlice(inner: QuotaSlice([Cap("tool", 30%)], inner: GreedySlice()))`. Count requirements are satisfied in phase 1, percentage caps in the inner QuotaSlice. No unified type is needed.

The "unified QuotaSlice extension" position (1B from brainstorm) was already rejected by the challenger as architecturally unsound — token-cost estimation at policy construction time is non-deterministic because token counts per item are not known until run time. The two-phase algorithm (count-satisfy → budget-distribute) maps cleanly to a separate decorator.

**Ruling:** Separate `CountQuotaSlice` decorator.

**DI-2 (tag non-exclusivity):**

The exclusive alternative requires either formal tag-ordering in the Cupel spec (not currently defined — 2B rejected) or a priority-ordering mechanism (2C — premature without a confirmed use case). Non-deterministic exclusive semantics would be a spec defect.

The saturation concern (single item with 10 tags satisfying 10 × RequireCount(1)) is acceptable behavior when documented: if an item genuinely has all those properties, satisfying all n=1 requirements is correct. For n>1 requirements a single item can only contribute 1 to each count, so total saturation only occurs at n=1.

No escalation via `ask_user_questions` was needed — the exclusive positions were definitively ruled out by the challenger report (tag-order non-determinism is a spec defect), making the non-exclusive ruling unambiguous.

**Ruling:** Non-exclusive — item counts toward all matching RequireCount constraints simultaneously.

Both rulings, their rationale, caller guidance, and downstream implications were recorded in `.planning/design/count-quota-design-notes.md` with a canonical worked example for DI-2.

## Verification

```
PASS: DI-1 present
PASS: DI-2 present
TBD count: 0
Ambiguity check: (no output — no "or possibly", "alternatively", "unclear" in notes)
```

All three verification checks passed. No TBD fields, no ambiguous language in either ruling.

Slice-level verification (partial — T01 is the first task):
- `.planning/design/count-quota-design.md` does not exist yet (created by a later task); no slice verification checks apply to T01's output.

## Diagnostics

- `cat .planning/design/count-quota-design-notes.md` — full DI-1 and DI-2 rulings with rationale
- `grep "^## DI-" .planning/design/count-quota-design-notes.md` — quick section index
- `grep -n "or possibly\|alternatively\|unclear" .planning/design/count-quota-design-notes.md` — ambiguity diagnostic (should return nothing)

## Deviations

None. No escalation via `ask_user_questions` was required — the exclusive tag semantics positions were definitively eliminated by the challenger report's own analysis (2B rejected: non-deterministic without tag-order spec; 2C rejected: premature generalization), leaving non-exclusive as the unambiguous ruling.

## Known Issues

None.

## Files Created/Modified

- `.planning/design/count-quota-design-notes.md` — new file; DI-1 ruling (separate CountQuotaSlice), DI-2 ruling (non-exclusive), rationale, canonical worked example, caller guidance, downstream implications
