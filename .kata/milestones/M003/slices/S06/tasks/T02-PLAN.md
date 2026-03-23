---
estimated_steps: 7
estimated_files: 7
---

# T02: Tiebreaker spec clarification + Rust assertion + spec alignment (SUMMARY, slicers, count-quota page, changelog)

**Slice:** S06 — Budget simulation + tiebreaker + spec alignment
**Milestone:** M003

## Description

Close all spec navigation gaps and editorial debt for M003 features. The tiebreaker rule in the roadmap says "score ties → id ascending" but `ContextItem` has no `Id` field — both Rust and .NET implementations already use stable index-based tiebreaking. This task formalizes the existing behavior in the spec and adds a dedicated Rust test. It also adds the missing `count-quota.md` slicer page, updates `SUMMARY.md` and `slicers.md`, writes the v1.3.0 changelog section, and documents the Rust budget simulation parity deferral.

## Steps

1. **Update `spec/src/slicers/greedy.md`**: Add a "Tiebreaker Clarification" subsection under "Sort Stability" that states: the roadmap's "score ties → id ascending" language refers to the existing stable-index tiebreaker (`(density desc, index asc)`), not a separate item identifier. `ContextItem` has no `Id` field; the original input index is the canonical tiebreaker in both Sort and GreedySlice stages.

2. **Create `spec/src/slicers/count-quota.md`**: New slicer page covering: overview (decorator wrapping an inner slicer), algorithm summary (two-phase: enforce caps then fill minimums), `CountQuotaEntry` (kind, requireCount, capCount), `ScarcityBehavior` (Degrade default, Throw opt-in), KnapsackSlice guard (D052/D085), pinned item scope (D053), tag semantics (D055 — non-exclusive), `SelectionReport.CountRequirementShortfalls`, cross-references to pipeline/slice.md and quota.md. Keep it concise — link to existing decisions rather than re-deriving.

3. **Update `spec/src/SUMMARY.md`**: Add `- [CountQuotaSlice](slicers/count-quota.md)` under the Slicers section, after QuotaSlice.

4. **Update `spec/src/slicers.md`**: Add CountQuotaSlice row to the summary table: `| [CountQuotaSlice](slicers/count-quota.md) | Count-constrained decorator | O(*N* + inner cost) | Absolute minimum/maximum counts per kind |`

5. **Update `spec/src/changelog.md`**: Add `## [1.3.0]` section with all M003 features: DecayScorer (3 curve types, TimeProvider), MetadataTrustScorer (cupel:trust/source-type), CountQuotaSlice (count constraints, ScarcityBehavior), analytics extension methods (BudgetUtilization, KindDiversity, TimestampCoverage), Cupel.Testing vocabulary (13 assertion patterns), OTel bridge companion package (3 verbosity tiers), budget simulation (GetMarginalItems, FindMinBudgetFor), tiebreaker clarification, spec alignment updates.

6. **Document Rust budget simulation deferral**: In `spec/src/analytics/budget-simulation.md`, ensure the existing "Language Parity Note" clearly states Rust parity is deferred to a future release (post-v1.3). If the note is already sufficient, no changes needed.

7. **Add Rust integration test `greedy_tiebreaker.rs`**: In `crates/cupel/tests/`. Create two `ContextItem`s with identical token counts, give them identical scores (same density), and verify via `GreedySlice::slice` that the item at index 0 in the input appears before the item at index 1 in the output.

## Must-Haves

- [ ] `spec/src/slicers/greedy.md` clarifies tiebreaker as stable-index, not id-based
- [ ] `spec/src/slicers/count-quota.md` exists with overview, algorithm, ScarcityBehavior, guards, tag semantics
- [ ] `spec/src/SUMMARY.md` links to count-quota.md under Slicers
- [ ] `spec/src/slicers.md` summary table includes CountQuotaSlice row
- [ ] `spec/src/changelog.md` has v1.3.0 section covering all M003 features
- [ ] Rust budget simulation parity deferral is documented
- [ ] Rust tiebreaker integration test passes

## Verification

- `grep -q "count-quota" spec/src/SUMMARY.md` → match
- `grep -q "CountQuotaSlice" spec/src/slicers.md` → match
- `grep -q "1.3.0" spec/src/changelog.md` → match
- `grep -q "index ascending\|index asc\|original index" spec/src/slicers/greedy.md` → match
- `test -f spec/src/slicers/count-quota.md` → exists
- `rtk cargo test --all-targets` → passes including greedy_tiebreaker

## Observability Impact

- Signals added/changed: None (spec-only changes + one test file)
- How a future agent inspects this: `mdbook build spec` for navigation correctness; `cargo test greedy_tiebreaker` for tiebreaker assertion
- Failure state exposed: None

## Inputs

- `spec/src/slicers/greedy.md` — existing tiebreaker text to clarify
- `spec/src/SUMMARY.md` — existing navigation to extend
- `spec/src/slicers.md` — existing table to extend
- `spec/src/changelog.md` — existing changelog to extend
- `spec/src/analytics/budget-simulation.md` — existing parity note to verify
- S03 decisions: D052 (KnapsackSlice guard), D053 (cap scope), D054 (separate decorator), D055 (tag semantics), D056 (ScarcityBehavior), D085 (is_knapsack), D087 (LastShortfalls)
- Existing Rust GreedySlice: `crates/cupel/src/slicer/greedy.rs` — confirms `(density desc, index asc)` ordering

## Expected Output

- `spec/src/slicers/greedy.md` — modified with tiebreaker clarification
- `spec/src/slicers/count-quota.md` — new file (~80-120 lines)
- `spec/src/SUMMARY.md` — modified (+1 line)
- `spec/src/slicers.md` — modified (+1 table row)
- `spec/src/changelog.md` — modified (+v1.3.0 section, ~30-40 lines)
- `spec/src/analytics/budget-simulation.md` — verified or minor edit
- `crates/cupel/tests/greedy_tiebreaker.rs` — new file (~30-40 lines)
