---
estimated_steps: 6
estimated_files: 3
---

# T01: Write count-constrained-knapsack spec chapter

**Slice:** S03 — Spec chapters — count-constrained-knapsack + metadata-key
**Milestone:** M009

## Description

Author `spec/src/slicers/count-constrained-knapsack.md` using `count-quota.md` as the structural template. The chapter documents the 3-phase COUNT-KNAPSACK-CAP algorithm for `CountConstrainedKnapsackSlice`, with special emphasis on the Phase 2 re-sort requirement (D180), Phase 3 count seeding from Phase 1 (D181), the pre-processing sub-optimality trade-off (D174), and the `is_count_quota() → true` monotonicity interaction. Add the chapter link to `SUMMARY.md` and add a table row to `slicers.md`.

All content is derived from the working implementations (Rust and .NET) and the 5 TOML conformance vectors — no invention required.

## Steps

1. Read `spec/src/slicers/count-quota.md` to internalize the section structure and pseudocode style.
2. Read all 5 TOML files in `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml` for the ground-truth algorithm behavior (especially cap-exclusion for D180, and require-and-cap for D181).
3. Create `spec/src/slicers/count-constrained-knapsack.md` with these sections in order:
   - **Overview** — decorator wrapping KnapsackSlice; 3-phase summary; contrast with CountQuotaSlice (CCKS hardwires KnapsackSlice, no inner slicer guard).
   - **Configuration** — CountQuotaEntry (same as count-quota.md); ScarcityBehavior (same); construction parameters (Rust: `Vec<CountQuotaEntry>`, `KnapsackSlice`, `ScarcityBehavior`; .NET: `IReadOnlyList<CountQuotaEntry>`, `KnapsackSlice`, `ScarcityBehavior = ScarcityBehavior.Degrade`); Validation Rules (same as CountQuotaSlice).
   - **Algorithm** — COUNT-KNAPSACK-CAP pseudocode block. Must include: Phase 1 identical to CountQuotaSlice; Phase 2 using `innerSelected <- KnapsackSlice(residual, residualBudget)` then `innerSelected <- SORT(innerSelected, by score descending)` (D180 — critical re-sort before Phase 3); Phase 3 cap loop with `selectedCount` seeded from Phase 1 committed counts (D181 — not from zero).
   - **Scarcity Reporting** — same CountRequirementShortfall table; exact ScarcityBehavior.Throw error message: `"CountConstrainedKnapsackSlice: candidate pool for kind '<kind>' has <satisfied> items but RequireCount is <requireCount>."`. Note that in Rust, shortfalls flow to `SelectionReport.count_requirement_shortfalls` (pipeline-level); in .NET, `CountConstrainedKnapsackSlice.LastShortfalls` (inspection surface post-Slice).
   - **Monotonicity** — `is_count_quota() → true`; MUST NOT be used with budget simulation methods that require monotonic inclusion; `FindMinBudgetFor` MUST guard against it (cross-reference to `../analytics/budget-simulation.md#quotaslice--countquotaslice-guard`). Note: no KnapsackSlice guard at construction time (unlike CountQuotaSlice — CCKS is the knapsack wrapper itself).
   - **Trade-offs** — Two sub-sections: (a) Pre-processing sub-optimality: Phase 1 commits required items before knapsack runs, consuming budget; if required items are token-heavy the residual budget may be insufficient for globally optimal Phase 2 selection (D174); (b) Cap waste: knapsack may select items in Phase 2 that Phase 3 then drops due to cap enforcement; budget was "spent" on items that do not appear in the result.
   - **Edge Cases** — table adapting count-quota.md edge cases; add: "No KnapsackSlice guard" row (N/A — CCKS hardwires it); "Phase 1 exhausts budget" row (same as count-quota); "KnapsackSlice OOM guard" row (CupelError::TableTooLarge propagates from KnapsackSlice when DP table exceeds 50M cells).
   - **Complexity** — same big-O structure as CountQuotaSlice but Phase 2 is O(N×C) for KnapsackSlice DP (where C = residual capacity / bucket_size).
   - **Conformance Notes** — same ContextKind case-insensitivity note; Phase 2 re-sort requirement is normative (MUST sort by score descending before Phase 3); Phase 3 MUST initialize `selectedCount` from Phase 1 committed counts, not from zero.
   - **Conformance Vector Outlines** — 5 outlines derived from the TOML files: (1) baseline — require 2 of kind, knapsack selects residual; (2) cap exclusion — cap=2, 4 candidates, 2 excluded by cap after Phase 2 re-sort; (3) scarcity degrade — require_count=3, only 1 candidate, shortfall recorded; (4) tag non-exclusivity — two kinds with independent require constraints; (5) require-and-cap — require=2, cap=2, knapsack picks best msg items from residual.
4. Add `  - [CountConstrainedKnapsackSlice](slicers/count-constrained-knapsack.md)` to `spec/src/SUMMARY.md` under the Slicers section, after the `CountQuotaSlice` entry.
5. Add a row for CountConstrainedKnapsackSlice to the Slicer Summary table in `spec/src/slicers.md`: `| [CountConstrainedKnapsackSlice](slicers/count-constrained-knapsack.md) | Count-require/cap + knapsack-optimal selection | O(*N* log *N* + *N* × *C*) | Count guarantees with globally optimal token packing |`
6. Run grep TBD check to confirm zero TBD fields.

## Must-Haves

- [ ] `spec/src/slicers/count-constrained-knapsack.md` exists with all required sections (Overview, Configuration, Algorithm, Scarcity Reporting, Monotonicity, Trade-offs, Edge Cases, Complexity, Conformance Notes, Conformance Vector Outlines)
- [ ] Algorithm pseudocode includes Phase 2 re-sort line: `innerSelected <- SORT(innerSelected, by score descending)` between the KnapsackSlice call and the Phase 3 cap loop (D180)
- [ ] Algorithm pseudocode: Phase 3 `selectedCount` is seeded from Phase 1 committed counts, not initialized to zero (D181)
- [ ] Monotonicity section cross-references `../analytics/budget-simulation.md` and states `is_count_quota() → true`
- [ ] Trade-offs section documents both pre-processing sub-optimality (D174) and cap waste
- [ ] Exact ScarcityBehavior.Throw message string documented
- [ ] 5 conformance vector outlines present
- [ ] `grep -ci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md` → 0
- [ ] `grep -q "count-constrained-knapsack" spec/src/SUMMARY.md` exits 0
- [ ] `grep -q "CountConstrainedKnapsackSlice" spec/src/slicers.md` exits 0

## Verification

- `grep -ci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md` → 0
- `grep -q "count-constrained-knapsack" spec/src/SUMMARY.md` → exits 0
- `grep -q "CountConstrainedKnapsackSlice" spec/src/slicers.md` → exits 0
- `grep -q "SORT.*innerSelected\|innerSelected.*SORT\|by score descending" spec/src/slicers/count-constrained-knapsack.md` → exits 0 (Phase 2 re-sort present)
- `grep -q "is_count_quota\|isCountQuota" spec/src/slicers/count-constrained-knapsack.md` → exits 0 (monotonicity reference)

## Observability Impact

- Signals added/changed: None — spec-only file
- How a future agent inspects this: `grep -ci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md` as the primary completeness signal
- Failure state exposed: Zero TBD count is the binary pass/fail signal

## Inputs

- `spec/src/slicers/count-quota.md` — structural template; copy section organization verbatim and adapt CCKS differences
- `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml` — ground truth for 5 conformance vector outlines and algorithm behavior
- `crates/cupel/src/slicer/count_constrained_knapsack.rs` — working Rust implementation; module docs describe 3-phase algorithm; exact error messages
- `src/Wollax.Cupel/Slicing/CountConstrainedKnapsackSlice.cs` — working .NET implementation; exact ScarcityBehavior.Throw message format
- S01-SUMMARY.md and S02-SUMMARY.md — confirm D180, D181, algorithm phases are correctly documented
- `spec/src/analytics/budget-simulation.md` — section anchor for is_count_quota() monotonicity guard cross-reference

## Expected Output

- `spec/src/slicers/count-constrained-knapsack.md` — complete spec chapter, zero TBD fields
- `spec/src/SUMMARY.md` — updated with CountConstrainedKnapsackSlice link under Slicers
- `spec/src/slicers.md` — updated summary table with CountConstrainedKnapsackSlice row
