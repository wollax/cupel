---
estimated_steps: 6
estimated_files: 3
---

# T01: Debate algorithm architecture and tag non-exclusivity (DI-1, DI-2)

**Slice:** S03 — Count-Based Quota Design
**Milestone:** M002

## Description

DI-1 (separate `CountQuotaSlice` vs. extension of `QuotaSlice`) and DI-2 (tag non-exclusivity semantics) are the most foundational design questions in S03. DI-2 in particular is irreversible post-ship — it defines the public semantic contract for count-quota and cannot be changed without a breaking version bump. Both must be settled before the remaining questions (scarcity behavior, KnapsackSlice path, pinned interaction) can be framed correctly, because those downstream questions all assume a particular algorithm architecture and multi-tag item semantics.

DI-1 ruling determines whether callers can compose count and percentage constraints on the same kind (unified type) or must choose between them (separate types). DI-2 ruling determines whether an item tagged `["critical","urgent"]` satisfies one or both RequireCount constraints — directly affecting how callers reason about policy correctness.

The proof strategy for S03 explicitly calls for escalation via `ask_user_questions` if the debate deadlocks rather than writing an ambiguous ruling. This task is where the deadlock risk is highest (DI-2 in particular).

## Steps

1. Re-read `spec/src/slicers/quota.md` QUOTA-SLICE pseudocode to understand the existing decorator pattern: how `QuotaSlice` wraps an inner slicer, how `QuotaSet` is structured, and where count constraints would need to attach.

2. Re-read DI-1 and DI-2 framing from `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` (Sections 1 and 2 plus Downstream Inputs DI-1 and DI-2).

3. **Debate DI-1 (algorithm architecture):** Construct a two-position argument:
   - Position A: Separate `CountQuotaSlice` decorator (wraps any inner slicer, own entry in `QuotaSet`-equivalent, clean separation of count and percentage semantics)
   - Position B: Unified `QuotaSlice` extension (single type, unified `QuotaEntry` with optional count fields)
   - The deciding question: *Do callers need both count AND percentage constraints on the same kind simultaneously?* (e.g., `RequireCount("tool", 2).Require("tool", 20%)`) If yes, a unified type or a two-layer composition (`CountQuotaSlice(QuotaSlice(...))`) is needed. If no, separate types are cleaner. Work through the composition case.
   - Apply D039 (design-only, no implementation code) — the ruling is about the public API contract, not the implementation.

4. **Debate DI-2 (tag non-exclusivity):** Construct the canonical worked example and debate it:
   - Setup: items = `[Item(kind:"critical,urgent", score:0.9, tokens:100), Item(kind:"critical", score:0.8, tokens:100), Item(kind:"urgent", score:0.7, tokens:100)]`, budget = 500 tokens, policy = `RequireCount("critical", 2).RequireCount("urgent", 2)`
   - Question: does including the first item satisfy one count-toward-"critical" AND one count-toward-"urgent" (non-exclusive), or only one?
   - Non-exclusive case: 1 critical+urgent item + 1 critical item satisfies `RequireCount("critical", 2)`; 1 critical+urgent item + 1 urgent item satisfies `RequireCount("urgent", 2)`. Total items needed: 3.
   - Exclusive case: would require 2 distinct critical-tagged items (not counting the multi-tag item toward both), or a priority ordering to determine which constraint it satisfies.
   - Risk for non-exclusive: a caller with 10 requirements can satisfy all of them with 1 item tagged with all 10 tags — is this intentional or a footgun?
   - Risk for exclusive: non-deterministic (depends on tag order) unless tag ordering is formally specified.
   - If genuinely irreconcilable after explicit debate, call `ask_user_questions` with the two positions rather than leaving an ambiguous ruling.

5. Record settled rulings for DI-1 and DI-2 in `.planning/design/count-quota-design-notes.md` with: the ruling (one sentence), the rationale (2-3 sentences), and any caller guidance note (what callers should know about the semantics).

6. Note any implications of DI-1/DI-2 rulings for downstream questions (e.g., if separate decorator: how is composition with QuotaSlice allowed? if non-exclusive: should the spec include a "satisfy-by-minimum" note warning about saturation?).

## Must-Haves

- [ ] DI-1 ruling: one sentence, no qualifiers ("may be" / "possibly"), with rationale
- [ ] DI-2 ruling: one sentence covering the canonical worked example, with rationale and caller guidance
- [ ] If ask_user_questions escalated for DI-2: user's answer recorded as the ruling
- [ ] DI-1 and DI-2 notes exist in `.planning/design/count-quota-design-notes.md`
- [ ] No open sub-questions left in the notes for DI-1 or DI-2

## Verification

```bash
# Notes file exists with DI-1 and DI-2 rulings
grep -q "DI-1" .planning/design/count-quota-design-notes.md && echo "PASS: DI-1 present"
grep -q "DI-2" .planning/design/count-quota-design-notes.md && echo "PASS: DI-2 present"

# No TBD fields in notes
grep -ci "\bTBD\b" .planning/design/count-quota-design-notes.md
# Expected: 0
```

## Observability Impact

- Signals added/changed: None (pure design artifact)
- How a future agent inspects this: `cat .planning/design/count-quota-design-notes.md` — intermediate design notes; `grep "^## DI-" .planning/design/count-quota-design-notes.md` — quick section index
- Failure state exposed: If DI-2 notes contain "or possibly" or "alternatively", the ruling is ambiguous — `grep -n "or possibly\|alternatively\|unclear" .planning/design/count-quota-design-notes.md` is the diagnostic

## Inputs

- `spec/src/slicers/quota.md` — existing QUOTA-SLICE pseudocode and QuotaSet structure
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — DI-1 and DI-2 framing (Sections 1 and 2 plus Downstream Inputs)
- D039 (locked: design-only, no implementation code)
- D040 (locked: build-time vs. run-time distinction is a hard requirement)

## Expected Output

- `.planning/design/count-quota-design-notes.md` — new scratch file with unambiguous rulings for DI-1 and DI-2, supporting rationale, and caller guidance notes; zero TBD fields for these two sections
