---
id: T03
parent: S03
milestone: M002
provides:
  - ".planning/design/count-quota-design.md — authoritative design decision record for CountQuotaSlice with all six DI rulings, COUNT-DISTRIBUTE-BUDGET pseudocode, and five conformance vector outlines; zero TBD fields"
key_files:
  - .planning/design/count-quota-design.md
key_decisions:
  - "All DI-1 through DI-6 rulings from T01/T02 are fully captured in count-quota-design.md; no open questions remain"
  - "COUNT-DISTRIBUTE-BUDGET pseudocode adapts DISTRIBUTE-BUDGET from spec/src/slicers/quota.md: Phase 1 (count pre-allocation by score), Phase 2 (residual proportional budget distribution), Phase 3 (cap enforcement via CountCapExceeded)"
  - "CountCapExceeded variant shape: { kind: string, cap: int, count: int } — per-item ExclusionReason for cap-excluded items"
  - "quota_violations field shape: Vec<CountQuotaViolation> { kind, required_count, satisfied_count } — SelectionReport-level scarcity surface"
  - "KnapsackSlice guard message verbatim: 'CountQuotaSlice does not support KnapsackSlice as the inner slicer in this version. Use GreedySlice as the inner slicer. A CountConstrainedKnapsackSlice will be provided in a future release.'"
patterns_established:
  - "Design record first, then implementation: all six design decisions settled across T01–T03 before any M003 code is written"
  - "Pseudocode adapts existing spec notation (variable naming, loop structure, step comments) rather than inventing new conventions"
observability_surfaces:
  - "cat .planning/design/count-quota-design.md — single authoritative file"
  - "grep '^## ' .planning/design/count-quota-design.md — section index"
  - "grep 'Decision:' .planning/design/count-quota-design.md — quick ruling scan"
  - "grep -ci '\\bTBD\\b' .planning/design/count-quota-design.md — completeness check (must be 0)"
duration: ~20 minutes
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T03: Write design decision record with pseudocode

**Synthesized all DI-1–DI-6 rulings from T01/T02 into `.planning/design/count-quota-design.md` — the authoritative, implementation-ready design record for `CountQuotaSlice` with `COUNT-DISTRIBUTE-BUDGET` pseudocode and zero TBD fields.**

## What Happened

Re-read `spec/src/slicers/quota.md` to internalize the `DISTRIBUTE-BUDGET` pseudocode notation (variable naming, loop structure, floor-truncation conventions, step commentary style). Then wrote `.planning/design/count-quota-design.md` with all required sections in order:

- **Overview** — `CountQuotaSlice` as a decorator, its relationship to `QuotaSlice` (separate type, complementary), and `CountQuotaSet` as the configuration type.
- **Section 1 (Algorithm Architecture)** — DI-1 ruling, two-phase algorithm description (count-satisfy → budget-distribute), wrapping rules.
- **Section 2 (Tag Non-Exclusivity Semantics)** — DI-2 ruling, canonical worked example with 3 items and dual-constraint policy, caller guidance including the `cupel:primary_tag` workaround.
- **Section 3 (Pinned Item Interaction)** — DI-5 ruling, cap scope definition, require-count decrement formula, worked example.
- **Section 4 (Conflict Detection Rules)** — DI-3 ruling, build-time checks (require > cap; cap==0 with require>0), `ScarcityBehavior::Degrade` default, `Throw` override, `quota_violations` field shape, `CountCapExceeded` exclusion reason shape.
- **Section 5 (KnapsackSlice Compatibility)** — DI-4 ruling (5E rejected for v1), verbatim guard message, upgrade path note.
- **Section 6 (ExclusionReason + SelectionReport Extensions)** — DI-6 backward-compat audit result; Rust `#[non_exhaustive]` safety; .NET non-required property approach; positional-deconstruction caveat.
- **Pseudocode: COUNT-DISTRIBUTE-BUDGET** — three-phase subroutine adapting `DISTRIBUTE-BUDGET` from quota.md: Phase 1 commits required items by score, Phase 2 applies residual proportional distribution, Phase 3 describes cap enforcement by the wrapping slice loop.
- **Conformance Vector Outlines** — five scenario sketches covering baseline satisfaction, cap exclusion, pinned decrement, scarcity degrade, and tag non-exclusivity.

## Verification

All content checks passed in one pass:

    PASS: file exists
    PASS: no TBD
    Numbered sections found: 6          (≥5 required)
    PASS: pseudocode (COUNT-DISTRIBUTE-BUDGET present)
    PASS: CountCapExceeded
    PASS: quota_violations
    PASS: knapsack
    PASS: algorithm section
    PASS: tag section
    PASS: pinned section
    PASS: conflict section

Test suites — no regressions (pure design artifact, no code changes):

    cargo test: 35 passed, 0 failed
    dotnet test: 583 passed, 0 failed

Committed as `docs(S03): add count-quota design record` (cb3fec6).

## Diagnostics

- `cat .planning/design/count-quota-design.md` — full design record
- `grep -n "^## " .planning/design/count-quota-design.md` — section index
- `grep -n "Decision:" .planning/design/count-quota-design.md` — ruling scan
- `grep -ci "\bTBD\b" .planning/design/count-quota-design.md` — completeness check (returns 0)
- `grep -in "TBD\|open question\|deferred" .planning/design/count-quota-design.md` — failure diagnostic (returns nothing)

## Deviations

None. The document was written exactly as specified in the task plan. The pseudocode subroutine returns `(kindBudgets, preAllocated, selectedCount)` as a tuple rather than only `kindBudgets` — this was a minor expansion to make the three outputs of the subroutine explicit, consistent with the plan's description of what the outer slice loop needs.

## Known Issues

None.

## Files Created/Modified

- `.planning/design/count-quota-design.md` — authoritative design decision record for `CountQuotaSlice`; 343 lines; all six DI rulings, `COUNT-DISTRIBUTE-BUDGET` pseudocode, five conformance vector outlines, zero TBD fields
