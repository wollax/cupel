---
id: T02
parent: S03
milestone: M002
provides:
  - DI-6 ruling: Rust SelectionReport and ExclusionReason both #[non_exhaustive] — field/variant addition is safe; .NET sealed record addition safe for property-access callers; positional deconstruction is explicitly unsupported
  - DI-3 ruling: default ScarcityBehavior=Degrade; quota_violations field (Vec<CountQuotaViolation>) on SelectionReport; per-slicer configurability for v1; CountQuotaViolation shape {kind, required_count, satisfied_count}
  - DI-4 ruling: 5E chosen (reject CountQuotaSlice+KnapsackSlice at construction time); guard message specified; CountConstrainedKnapsackSlice deferred to M003+
  - DI-5 ruling: caps apply to slicer-selected items only; pinned items decrement require_count before phase 1; pinned_count > cap_count has no special treatment (slicer budget floors at 0)
key_files:
  - .planning/design/count-quota-design-notes.md
key_decisions:
  - DI-6: Rust #[non_exhaustive] on both SelectionReport struct and ExclusionReason enum makes field/variant extension backward-compatible; .NET sealed record is safe for property-access callers; positional deconstruction must be documented as unsupported
  - DI-3: quota_violations field on SelectionReport preferred over TraceEvent-only approach; default behavior is Degrade (not Throw); per-slicer ScarcityBehavior for v1
  - DI-4: 5E (build-time guard rejecting KnapsackSlice inner) for v1; guard message uses only public API names per D032
  - DI-5: caps are slicer-scoped (not total-included); pinned items reduce residual require_count; pinned overshoot silently floors slicer budget to 0
patterns_established:
  - Degrade-by-default for run-time scarcity conditions (consistent with D040)
  - Early-construction guard for unsupported slicer combinations (consistent with CupelError::TableTooLarge pattern)
  - Pinned items as pre-committed context outside slicer authority — slicer operates on residual budget and count slots
observability_surfaces:
  - SelectionReport.quota_violations — empty list = no violations; non-empty list = count requirements partially unsatisfied
  - ExclusionReason::CountCapExceeded — per-item reason for items excluded by CapCount limit
  - Diagnostic command: grep "^## DI-" .planning/design/count-quota-design-notes.md
duration: ~30m
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T02: Settle remaining questions and audit backward compatibility (DI-3, DI-4, DI-5, DI-6)

**Appended DI-3, DI-4, DI-5, and DI-6 rulings to `count-quota-design-notes.md`; all six design inputs now have unambiguous, zero-TBD rulings.**

## What Happened

Executed all five plan steps:

**Step 1 — DI-6 audit (backward-compatibility precondition):**
- `crates/cupel/src/diagnostics/mod.rs`: confirmed `SelectionReport` struct is `#[non_exhaustive]` (line 263); confirmed `ExclusionReason` enum is `#[non_exhaustive]` (line 96). Adding new fields or variants is non-breaking for all external Rust callers (compiler enforces wildcard arms on non-exhaustive enums; non-exhaustive structs cannot be constructed via struct literal outside the crate).
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`: confirmed `public sealed record SelectionReport` with `required` init-only properties. Adding a new optional (non-required) property is safe for property-access callers. Positional deconstruction callers would break — this must be documented as unsupported.
- Verdict recorded: both languages support the `quota_violations` field approach (6D).

**Step 2 — DI-3 (scarcity behavior + SelectionReport representation):**
- Chose `SelectionReport.quota_violations` field (over TraceEvent-only), enabled by the DI-6 audit confirming extension safety.
- Default `ScarcityBehavior`: Degrade (consistent with D040).
- `CountQuotaViolation` shape: `{ kind: string, required_count: int, satisfied_count: int }`. Empty list on success.
- Configurability: per-slicer for v1. Per-entry override deferred.
- D046 honored: `CountRequireUnmet` does NOT appear as `ExclusionReason` — scarcity is `SelectionReport`-level, not per-item.

**Step 3 — DI-4 (KnapsackSlice compatibility):**
- Applied conservative-first principle: 5E chosen.
- Guard message specified: `"CountQuotaSlice does not support KnapsackSlice as the inner slicer in this version. Use GreedySlice as the inner slicer. A CountConstrainedKnapsackSlice will be provided in a future release."` — uses only public API names per D032.
- Upgrade path: `CountConstrainedKnapsackSlice` as separate slicer in M003+ if demand established.

**Step 4 — DI-5 (pinned item + overshoot):**
- Cap scope: slicer-selected items only. Pinned items are committed before slicer runs.
- `require_count` decrement: pinned items of kind K reduce residual require_count entering phase 1 to `max(0, require_count(K) - pinned_count(K))`.
- `pinned_count > cap_count` outcome: no special treatment. Slicer's selectable budget floors at `max(0, cap_count - pinned_count) = 0`.
- Worked example included in notes.

**Step 5 — Append and verify:**
- All four rulings appended to `.planning/design/count-quota-design-notes.md`.
- `grep -ci "\bTBD\b"` → 0.

## Verification

```
# DI sections present
PASS: DI-3 present
PASS: DI-4 present
PASS: DI-5 present
PASS: DI-6 present

# No TBD fields
PASS: no TBD
```

All five must-haves satisfied:
- [x] DI-6 audit complete: Rust #[non_exhaustive] confirmed for SelectionReport and ExclusionReason; .NET sealed record extension safety confirmed with positional-deconstruction caveat documented
- [x] DI-3 ruling: ScarcityBehavior=Degrade default; quota_violations field with CountQuotaViolation shape {kind, required_count, satisfied_count}; per-slicer configurability for v1
- [x] DI-4 ruling: 5E chosen; guard message written using only public API names (D032 compliant); CountConstrainedKnapsackSlice upgrade path documented
- [x] DI-5 ruling: cap scope = slicer-selected only; require_count decrements by pinned_count before phase 1; pinned overshoot handled silently (slicer budget floors at 0)
- [x] `grep -ci "\bTBD\b" .planning/design/count-quota-design-notes.md` → 0

## Diagnostics

- `cat .planning/design/count-quota-design-notes.md` — full DI-1 through DI-6 rulings
- `grep "^## DI-" .planning/design/count-quota-design-notes.md` — section index
- `grep -ci "\bTBD\b" .planning/design/count-quota-design-notes.md` — completeness check (must be 0)
- `grep -in "TBD\|to be determined\|deferred" .planning/design/count-quota-design-notes.md` — failure mode diagnostic

## Deviations

None. All steps executed as written in T02-PLAN.md.

## Known Issues

None.

## Files Created/Modified

- `.planning/design/count-quota-design-notes.md` — appended DI-6, DI-3, DI-4, DI-5 rulings (four new sections)
