---
id: S03
parent: M002
milestone: M002
provides:
  - ".planning/design/count-quota-design.md — authoritative design decision record for CountQuotaSlice with all six DI rulings, COUNT-DISTRIBUTE-BUDGET pseudocode, and five conformance vector outlines; zero TBD fields"
requires:
  - slice: S01
    provides: "DI-1 through DI-6 design inputs from count-quota-report.md"
affects:
  - S06
key_files:
  - .planning/design/count-quota-design.md
  - .planning/design/count-quota-design-notes.md
key_decisions:
  - "DI-1: CountQuotaSlice is a separate decorator, not a QuotaSlice extension; composition handles combined count+percentage constraints"
  - "DI-2: Non-exclusive tag semantics — an item tagged [A, B] counts +1 toward both RequireCount(A) and RequireCount(B); not configurable"
  - "DI-3: ScarcityBehavior::Degrade default; quota_violations: Vec<CountQuotaViolation> on SelectionReport; CountQuotaViolation shape {kind, required_count, satisfied_count}"
  - "DI-4: 5E chosen — CountQuotaSlice + KnapsackSlice rejected at construction time; CountConstrainedKnapsackSlice deferred to M003+"
  - "DI-5: caps apply to slicer-selected items only; pinned items decrement require_count before phase 1; pinned_count > cap_count silently floors slicer budget to 0"
  - "DI-6: Rust #[non_exhaustive] on SelectionReport and ExclusionReason makes extension safe; .NET non-required property addition safe; positional deconstruction explicitly unsupported"
patterns_established:
  - "Composition over unification: separate slicer types for semantically distinct constraint systems"
  - "Non-exclusive multi-tag counting as mandatory (not configurable) semantics for count-quota"
  - "Degrade-by-default for run-time scarcity (consistent with D040)"
  - "Conservative-first approach for unsupported slicer combinations: build-time guard before algorithm investment (D052)"
  - "Pinned items as pre-committed context outside slicer authority; slicer operates on residual budget and count slots"
observability_surfaces:
  - "cat .planning/design/count-quota-design.md — single authoritative file"
  - "grep -n 'Decision:' .planning/design/count-quota-design.md — quick ruling index"
  - "grep -ci '\\bTBD\\b' .planning/design/count-quota-design.md — completeness check (must be 0)"
  - "grep -in 'TBD|open question|deferred' .planning/design/count-quota-design.md — failure diagnostic (must return nothing)"
drill_down_paths:
  - .kata/milestones/M002/slices/S03/tasks/T01-SUMMARY.md
  - .kata/milestones/M002/slices/S03/tasks/T02-SUMMARY.md
  - .kata/milestones/M002/slices/S03/tasks/T03-SUMMARY.md
duration: ~3 tasks, 1 session
verification_result: passed
completed_at: 2026-03-21
---

# S03: Count-Based Quota Design

**Design decision record `.planning/design/count-quota-design.md` produced — all six design inputs settled, `COUNT-DISTRIBUTE-BUDGET` pseudocode written, zero TBD fields, both test suites green.**

## What Happened

Three sequential tasks resolved all open design questions and synthesized them into the authoritative design record.

**T01 — DI-1 and DI-2 (algorithm architecture + tag non-exclusivity):**

Re-read `spec/src/slicers/quota.md` to understand the existing `QuotaSlice` decorator pattern and the challenger brainstorm's framing of each design input. Ran two-position debates for both decisions.

For DI-1, the deciding question was whether callers need count + percentage constraints on the same kind simultaneously. They can — and decorator composition handles this naturally: `CountQuotaSlice(inner: QuotaSlice([Cap(...)], inner: GreedySlice()))`. No unified extension type is needed. The "unified extension" position was already eliminated by the challenger (token-cost estimation at policy construction time is non-deterministic). **Ruling: separate CountQuotaSlice decorator.**

For DI-2, the exclusive alternatives (2B: tag-order-based, 2C: priority mechanism) were both definitively ruled out by the challenger — 2B introduces non-determinism, 2C is premature generalization. Non-exclusive semantics handle the saturation concern correctly when documented. No escalation via `ask_user_questions` was needed. **Ruling: non-exclusive — item counts toward all matching RequireCount constraints.**

Both rulings, rationale, and a canonical worked example were recorded in `.planning/design/count-quota-design-notes.md`.

**T02 — DI-3, DI-4, DI-5, DI-6 (scarcity + compat + pinned + backward-compat):**

DI-6 audit ran first (it's a precondition for DI-3's diagnostic mechanism choice): confirmed `#[non_exhaustive]` on both `SelectionReport` and `ExclusionReason` in `crates/cupel/src/diagnostics/mod.rs`; confirmed `sealed record SelectionReport` in .NET permits non-required property addition safely; documented positional deconstruction as unsupported.

DI-3: Given safe extension, chose `SelectionReport.quota_violations` field (preferred over TraceEvent-only). Default `ScarcityBehavior::Degrade` consistent with D040. `CountQuotaViolation` shape `{kind, required_count, satisfied_count}` per D046.

DI-4: Applied conservative-first principle — 5E (build-time rejection) chosen. Guard message specified using only public API names (D032). `CountConstrainedKnapsackSlice` documented as the M003+ upgrade path.

DI-5: Caps are slicer-scoped (not total-included). Pinned items reduce residual `require_count`. `pinned_count > cap_count` silently floors slicer budget to 0 — no special treatment.

All four rulings appended to `count-quota-design-notes.md`; TBD count: 0.

**T03 — Design record synthesis:**

Re-read `spec/src/slicers/quota.md` DISTRIBUTE-BUDGET pseudocode to internalize notation conventions. Wrote `.planning/design/count-quota-design.md` with all required sections: Overview, 6 numbered decision sections, COUNT-DISTRIBUTE-BUDGET pseudocode (three phases: count pre-allocation by score → residual proportional distribution → cap enforcement), and 5 conformance vector outlines. All section content checks passed; `grep -ci "\bTBD\b"` → 0. Committed as `docs(S03): add count-quota design record` (cb3fec6).

## Verification

```
PASS: file exists (.planning/design/count-quota-design.md)
PASS: no TBD fields (grep count = 0)
PASS: algorithm section (Algorithm Architecture present)
PASS: tag section (Tag Non-Exclusivity present)
PASS: pinned section (Pinned Item present)
PASS: conflict section (Conflict Detection present)
PASS: knapsack section (KnapsackSlice present)
PASS: pseudocode present (COUNT-DISTRIBUTE-BUDGET present)

cargo test: 113 passed, 0 failed (Rust suite clean)
dotnet test: 583 passed, 0 failed (no regressions)
```

## Requirements Advanced

- R040 — All five open count-quota design questions answered; design record produced with zero TBD fields; pseudocode written; implementation-ready for M003.

## Requirements Validated

- R040 — Design record exists at `.planning/design/count-quota-design.md`; `grep -ci "\bTBD\b"` → 0; all 5 decision areas present; pseudocode section complete; both test suites pass.

## New Requirements Surfaced

- None (all design inputs were pre-catalogued in DI-1 through DI-6).

## Requirements Invalidated or Re-scoped

- None.

## Deviations

None across all three tasks. The COUNT-DISTRIBUTE-BUDGET pseudocode returns a tuple `(kindBudgets, preAllocated, selectedCount)` rather than only `kindBudgets` — a minor expansion to make the three outputs explicit for the outer slice loop, consistent with the task plan's description.

## Known Limitations

- `CountConstrainedKnapsackSlice` (5A/5D path) is explicitly deferred to M003+; the build-time guard (5E) blocks the combination until demand is confirmed and the constrained-knapsack algorithm is worth implementing.
- Per-entry `ScarcityBehavior` override (as opposed to per-slicer) is deferred to a future release; per-slicer configuration is sufficient for v1 use cases.
- The `cupel:primary_tag` workaround for exclusive tag semantics is documented in the design record but not specified as a formal spec convention; that formalization is deferred to S04 or S05 if needed.

## Follow-ups

- S06 must reference `.planning/design/count-quota-design.md` when specifying the `FindMinBudgetFor + QuotaSlice` incompatibility guard note.
- M003 implementation work should start from the COUNT-DISTRIBUTE-BUDGET pseudocode in the design record.
- Positional deconstruction of `SelectionReport` in .NET must be documented as explicitly unsupported in the public API contract — this should be captured in the spec chapter when M003 ships.

## Files Created/Modified

- `.planning/design/count-quota-design.md` — new; authoritative design decision record (343 lines, all six DI rulings, pseudocode, conformance vectors, zero TBD fields)
- `.planning/design/count-quota-design-notes.md` — new scratch file (T01/T02); consumed by T03; retained as working notes

## Forward Intelligence

### What the next slice should know
- S06 needs only one paragraph from S03: Section 5 (KnapsackSlice) and the note that `FindMinBudgetFor` + `CountQuotaSlice` has non-monotonic inclusion risk — read `count-quota-design.md` Section 5 for the guard language.
- The COUNT-DISTRIBUTE-BUDGET pseudocode uses the same variable naming and step-comment style as DISTRIBUTE-BUDGET in `spec/src/slicers/quota.md` — maintain that consistency in S06 if you extend the pseudocode.

### What's fragile
- `cupel:primary_tag` workaround — mentioned in the design record as a caller-side workaround for exclusive semantics, but not yet a formal `cupel:` namespace convention. If S04 reserves the `cupel:` namespace, it should either formalize `cupel:primary_tag` or explicitly exclude it.
- DI-3 ScarcityBehavior is per-slicer for v1 — if per-entry override is needed in M003, the `CountQuotaSet` API will need a breaking change.

### Authoritative diagnostics
- `grep -n 'Decision:' .planning/design/count-quota-design.md` — returns exactly six decision one-liners; if any section is missing this line the record is incomplete
- `grep -ci "\bTBD\b" .planning/design/count-quota-design.md` — must always return 0; non-zero means the record is incomplete

### What assumptions changed
- Initial assumption (from brainstorm): DI-2 might require escalation via `ask_user_questions`. Actual: the exclusive alternatives were both definitively eliminated by the challenger's own analysis, making non-exclusive the unambiguous ruling without user input.
