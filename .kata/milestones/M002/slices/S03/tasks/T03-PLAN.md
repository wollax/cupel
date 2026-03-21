---
estimated_steps: 6
estimated_files: 3
---

# T03: Write design decision record with pseudocode

**Slice:** S03 — Count-Based Quota Design
**Milestone:** M002

## Description

Synthesize the settled rulings from T01 and T02 into the authoritative design decision record. This is the primary deliverable of S03 and the direct input to S06's `FindMinBudgetFor + CountQuotaSlice` interaction note and to M003's implementation work. The document must be implementation-ready: a developer starting M003 should be able to implement `CountQuotaSlice` directly from this record without needing to re-open any design question.

The pseudocode for `COUNT-DISTRIBUTE-BUDGET` adapts the existing `DISTRIBUTE-BUDGET` subroutine from `spec/src/slicers/quota.md`: it adds a count-constraint pre-allocation phase before the proportional distribution phase, preserving the structure and notation conventions established in the spec.

## Steps

1. Re-read `spec/src/slicers/quota.md` QUOTA-SLICE + DISTRIBUTE-BUDGET pseudocode to internalize the notation conventions (variable naming, loop structure, step comments) before writing the new pseudocode.

2. Write `.planning/design/count-quota-design.md` with the following sections in order:
   - **Overview** — what `CountQuotaSlice` is (a decorator slicer that enforces absolute item-count requirements and caps per `ContextKind` before delegating to an inner slicer), its role in the fixed pipeline, and its relationship to `QuotaSlice` (separate type, complementary use case).
   - **1. Algorithm Architecture** — ruling from T01 DI-1; two-phase algorithm description: Phase 1 (count-satisfy: select required items from each kind, score-first ordering within kind); Phase 2 (budget-distribute: run `COUNT-DISTRIBUTE-BUDGET` on the remaining items and budget); how `CountQuotaSlice` wraps any inner slicer except `KnapsackSlice` (see Section 5).
   - **2. Tag Non-Exclusivity Semantics** — ruling from T01 DI-2; canonical worked example with explicit item list, policy, and step-by-step satisfaction trace; caller guidance note; `cupel:primary_tag` metadata workaround documented for callers who need exclusive semantics.
   - **3. Pinned Item Interaction** — ruling from T02 DI-5; cap scope definition (slicer-selected items only); `require_count` decrement rule; example: `RequireCount("system", 2)` + 1 pinned system item → slicer must select 1 additional system item in count-satisfy phase.
   - **4. Conflict Detection Rules** — build-time checks enumeration (at minimum: `require_count > cap_count` for same kind; `cap_count == 0` with `require_count > 0` is also a build-time error); explicit statement that cross-kind conflicts are run-time (D040); default `ScarcityBehavior::Degrade`; `ScarcityBehavior::Throw` as the per-slicer override; `quota_violations` field shape on `SelectionReport`; `CountCapExceeded { kind, cap, count }` as the `ExclusionReason` variant for items excluded by count cap.
   - **5. KnapsackSlice Compatibility** — ruling from T02 DI-4 (5E: rejected for v1); exact build-time guard message; upgrade path note (`CountConstrainedKnapsackSlice` in M003+).
   - **6. ExclusionReason + SelectionReport Extensions** — backward-compatibility audit result from T02 DI-6; `CountCapExceeded` variant shape; `quota_violations` field shape; `.NET` positional-deconstruction caveat.
   - **Pseudocode: COUNT-DISTRIBUTE-BUDGET** — write the subroutine. It takes `(partitions, candidateTokenMass, targetTokens, quotas)` where `quotas` now includes both `requireTokens`, `capTokens`, `requireCount`, and `capCount` per kind. Structure: (a) **Phase 1 — Count pre-allocation**: for each kind with `requireCount > 0`, select top-`requireCount` items by score, remove them from the candidate pool, compute their token cost; (b) **Phase 2 — Budget adjustment**: subtract total pre-allocated token cost from `targetTokens` to get `remainingBudget`; apply same proportional DISTRIBUTE-BUDGET logic from `quota.md` on the remaining candidates with `remainingBudget`; (c) **Phase 3 — Count cap enforcement**: track how many items of each kind have been selected; once `capCount(kind)` is reached, exclude additional candidates of that kind with `CountCapExceeded` reason. Follow the pseudocode style from `spec/src/slicers/quota.md` (text pseudocode, not code blocks with language syntax).
   - **Conformance Vector Outlines** — 3-5 scenario sketches (not full TOML vectors — just prose descriptions): (1) baseline count satisfaction (greedy inner, require 2 of kind X, 3 candidates available), (2) count-cap exclusion (cap 1 of kind X, 3 candidates, first wins, 2 get CountCapExceeded), (3) pinned-count decrement (1 pinned of kind X, require 2, slicer selects 1 more), (4) scarcity degrade (require 3 of kind X, only 1 candidate, quota_violations populated), (5) tag non-exclusivity (multi-tag item satisfies 2 require constraints simultaneously).

3. Verify zero TBD fields: `grep -ci "\bTBD\b" .planning/design/count-quota-design.md` → 0.

4. Verify all five required section headers present: `grep -E "^## [0-9]\." .planning/design/count-quota-design.md | wc -l` → 5.

5. Run `cargo test --manifest-path crates/cupel/Cargo.toml` and `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` (raw, not via rtk wrapper — see S01 forward intelligence). Confirm both pass — this slice introduces no code changes, so these should be green.

6. Commit: `docs(S03): add count-quota design record` — commit `.planning/design/count-quota-design.md` (and clean up or commit the notes scratch file if it was created).

## Must-Haves

- [ ] `.planning/design/count-quota-design.md` exists
- [ ] `grep -ci "\bTBD\b" .planning/design/count-quota-design.md` → 0
- [ ] Sections 1–5 (and Overview) all present
- [ ] `COUNT-DISTRIBUTE-BUDGET` pseudocode section present and complete
- [ ] Conformance vector outlines present (≥3 scenarios)
- [ ] `CountCapExceeded` variant shape specified
- [ ] `quota_violations` field shape specified (or TraceEvent if DI-6 changed the choice)
- [ ] KnapsackSlice compatibility ruling + guard message present
- [ ] `cargo test` passes (35 Rust tests minimum)
- [ ] `dotnet test` passes (583 .NET tests minimum)

## Verification

```bash
# File exists
test -f .planning/design/count-quota-design.md && echo "PASS: file exists" || echo "FAIL: file missing"

# Zero TBD
test $(grep -ci "\bTBD\b" .planning/design/count-quota-design.md) -eq 0 && echo "PASS: no TBD" || echo "FAIL: TBD found"

# All 5 numbered sections
test $(grep -cE "^## [0-9]\." .planning/design/count-quota-design.md) -ge 5 && echo "PASS: sections present" || echo "FAIL: sections missing"

# Key content present
grep -q "COUNT-DISTRIBUTE-BUDGET" .planning/design/count-quota-design.md && echo "PASS: pseudocode" || echo "FAIL: no pseudocode"
grep -q "CountCapExceeded" .planning/design/count-quota-design.md && echo "PASS: CountCapExceeded" || echo "FAIL"
grep -q "quota_violations" .planning/design/count-quota-design.md && echo "PASS: quota_violations" || echo "FAIL"
grep -q "KnapsackSlice" .planning/design/count-quota-design.md && echo "PASS: knapsack" || echo "FAIL"

# No regressions
cargo test --manifest-path crates/cupel/Cargo.toml 2>&1 | tail -3
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj 2>&1 | tail -3
```

## Observability Impact

- Signals added/changed: None (pure design artifact; no code changes)
- How a future agent inspects this: `cat .planning/design/count-quota-design.md` — single authoritative file; `grep "^## " .planning/design/count-quota-design.md` — section index; `grep "Decision:" .planning/design/count-quota-design.md` — quick ruling scan
- Failure state exposed: `grep -in "TBD\|open question\|deferred" .planning/design/count-quota-design.md` — detects incomplete sections; if pseudocode section is absent, `grep "COUNT-DISTRIBUTE-BUDGET" .planning/design/count-quota-design.md` returns empty

## Inputs

- `.planning/design/count-quota-design-notes.md` — all six DI rulings from T01 and T02 (this task consumes and synthesizes them)
- `spec/src/slicers/quota.md` — pseudocode notation conventions (DISTRIBUTE-BUDGET as the base to adapt)
- D039 (locked: design-only, no implementation code)
- D040 (locked: build-time vs. run-time distinction)
- D046 (locked: CountRequireUnmet is SelectionReport-level field)

## Expected Output

- `.planning/design/count-quota-design.md` — authoritative design decision record with all 5 questions answered, pseudocode for COUNT-DISTRIBUTE-BUDGET, conformance vector outlines, zero TBD fields; committed to git
