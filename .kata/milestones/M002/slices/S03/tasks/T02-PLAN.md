---
estimated_steps: 5
estimated_files: 4
---

# T02: Settle remaining questions and audit backward compatibility (DI-3, DI-4, DI-5, DI-6)

**Slice:** S03 — Count-Based Quota Design
**Milestone:** M002

## Description

With DI-1 (algorithm architecture) and DI-2 (tag non-exclusivity) settled in T01, this task completes the design question inventory. DI-6 (backward-compatibility audit) is done first because it is a precondition for DI-3: the diagnostic mechanism for scarcity notification depends on whether `SelectionReport` and `ExclusionReason` can be extended without breaking existing callers. DI-4 (KnapsackSlice compatibility) and DI-5 (pinned overshoot) are more tractable and follow the audit.

The conservative-first principle applies throughout: when a simpler option is available with known limitations vs. a complex option with better properties, prefer the simpler option for v1 and document the upgrade path explicitly.

## Steps

1. **DI-6 audit (backward-compatibility precondition):**
   - In `crates/cupel/src/diagnostics/mod.rs`: confirm `SelectionReport` struct is `#[non_exhaustive]` (search for `#[non_exhaustive]` immediately before `pub struct SelectionReport`). Confirm `ExclusionReason` enum is `#[non_exhaustive]`.
   - In `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`: confirm `sealed record SelectionReport` — verify that adding a new property to a C# `sealed record` is non-breaking for callers who access the record via property access (not positional/deconstruct syntax). Note: callers using positional deconstruction (`var (included, excluded, ...) = report`) would break; callers using `report.Included` would not.
   - Record verdict in `count-quota-design-notes.md`: "Rust extension safe (non_exhaustive on struct and enum). .NET extension safe for property-access callers; positional deconstruction callers would break. Recommend documenting: do not use positional deconstruction on SelectionReport."

2. **DI-3 (scarcity behavior + SelectionReport representation):**
   - Given DI-6 verdict (both languages support extension), choose `SelectionReport.quota_violations` field as the diagnostic mechanism for scarcity (over TraceEvent-only approach), because it provides a prominent, structured, easily-accessed signal for callers who want to know if their count requirements were fully satisfied.
   - Specify the field shape: `quota_violations: list of CountQuotaViolation` where `CountQuotaViolation = { kind: string, required_count: int, satisfied_count: int }`. Empty list (not null) when no violations occurred.
   - Specify default `ScarcityBehavior`: **Degrade** (include all available candidates, populate `quota_violations`, continue execution). This matches D040: run-time scarcity is behavioral, not an exception by default.
   - Specify configurability: `ScarcityBehavior` is **per-slicer** (not per-entry) for v1 — callers who want throw-on-violation configure the slicer. Per-entry override is deferred to a later version if demand is established.
   - Record ruling in `count-quota-design-notes.md`.

3. **DI-4 (KnapsackSlice compatibility path):**
   - Apply conservative-first: choose **5E (reject the combination with a build-time guard)** for v1. Rationale: `CountQuotaSlice` wrapping `GreedySlice` covers the overwhelming majority of use cases; count-constrained knapsack is a significant implementation investment with no confirmed demand in M002 scope; the existing `CupelError::TableTooLarge` pattern provides the template for this guard.
   - Specify the guard: at `CountQuotaSlice` construction time, if the inner slicer is `KnapsackSlice`, throw/return an error. Guard message: `"CountQuotaSlice does not support KnapsackSlice as the inner slicer in this version. Use GreedySlice as the inner slicer. A CountConstrainedKnapsackSlice will be provided in a future release."` (public API names only, per D032).
   - Document the upgrade path: `CountConstrainedKnapsackSlice` as a separate slicer in M003+ if demand is established.
   - Record ruling in `count-quota-design-notes.md`.

4. **DI-5 (pinned item + overshoot edge case):**
   - Settle the cap scope: count-quota caps apply to **slicer-selected items only**. Pinned items are committed before the slicer runs and are outside the slicer's scope. The cap is applied only to the items `CountQuotaSlice` selects via the inner slicer, not to the combined total of pinned + selected.
   - Specify the `pinned_count > cap_count` outcome: **no special treatment** (option 3 from DI-5 framing). Pinned items of kind K always win. If the caller configures `CapCount("system", 1)` but pins 3 system-prompt items, the slicer selects 0 additional system items — the cap is satisfied by the pinned items consuming the slicer's allocation for that kind (i.e., the slicer's budget for kind K after pinned items are accounted for is 0 selectable items).
   - Specify how `require_count` decrements: pinned items of kind K decrement `require_count(K)` before the count-satisfy phase. If `pinned_count(K) >= require_count(K)`, the slicer selects 0 additional items for kind K in the count-satisfy phase; the requirement is already met.
   - Record ruling in `count-quota-design-notes.md`.

5. Append all four rulings (DI-3, DI-4, DI-5, DI-6) to `count-quota-design-notes.md`. Run `grep -ci "\bTBD\b" .planning/design/count-quota-design-notes.md` — must be 0.

## Must-Haves

- [ ] DI-6 audit complete: both Rust and .NET extension safety explicitly recorded
- [ ] DI-3 ruling: default ScarcityBehavior, `quota_violations` field shape, per-slicer vs. per-entry configurability — all specified
- [ ] DI-4 ruling: 5E chosen; guard message written; upgrade path documented
- [ ] DI-5 ruling: cap scope defined (slicer-selected only); `require_count` decrement rule specified; overshoot handled without special treatment
- [ ] `grep -ci "\bTBD\b" .planning/design/count-quota-design-notes.md` → 0 after this task

## Verification

```bash
# All DI sections present
for di in DI-3 DI-4 DI-5 DI-6; do
  grep -q "$di" .planning/design/count-quota-design-notes.md && echo "PASS: $di present" || echo "FAIL: $di missing"
done

# No TBD fields
test $(grep -ci "\bTBD\b" .planning/design/count-quota-design-notes.md) -eq 0 && echo "PASS: no TBD" || echo "FAIL: TBD found"
```

## Observability Impact

- Signals added/changed: None (pure design artifact)
- How a future agent inspects this: `cat .planning/design/count-quota-design-notes.md` — complete intermediate notes; `grep "^## DI-" .planning/design/count-quota-design-notes.md` — section index
- Failure state exposed: any remaining TBD or "to be determined" in the notes file indicates incomplete settling — `grep -in "TBD\|to be determined\|deferred" .planning/design/count-quota-design-notes.md` is the diagnostic

## Inputs

- `.planning/design/count-quota-design-notes.md` — DI-1 and DI-2 rulings from T01 (this task appends DI-3 through DI-6)
- `crates/cupel/src/diagnostics/mod.rs` — Rust `#[non_exhaustive]` audit
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — .NET sealed record extensibility audit
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — DI-3, DI-4, DI-5, DI-6 framing
- D040 (locked: run-time scarcity is behavioral, not an exception)
- D046 (locked: CountRequireUnmet is SelectionReport-level field, not per-item ExclusionReason)
- D032 (locked: error messages must not name internal types)

## Expected Output

- `.planning/design/count-quota-design-notes.md` — updated with DI-3, DI-4, DI-5, DI-6 rulings; all six design inputs (DI-1 through DI-6) now have concrete, unambiguous rulings; zero TBD fields
