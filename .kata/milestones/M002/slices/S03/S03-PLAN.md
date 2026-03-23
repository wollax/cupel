# S03: Count-Based Quota Design

**Goal:** Produce `.planning/design/count-quota-design.md` — a design decision record that resolves all 5 open questions for count-based quotas with pseudocode for `COUNT-DISTRIBUTE-BUDGET` and zero remaining TBD fields.
**Demo:** `grep -c "TBD" .planning/design/count-quota-design.md` → 0; all five decision areas present with unambiguous rulings and worked examples; pseudocode section complete.

## Must-Haves

- Algorithm architecture decided: `CountQuotaSlice` as a standalone decorator (wrapping any inner slicer) vs. extension of `QuotaSlice`; includes two-phase algorithm (count-satisfy first, then budget-distribute on remainder) with specified ordering for committed items
- Tag non-exclusivity semantics settled: a canonical worked example (`RequireCount("critical", 2).RequireCount("urgent", 2)` + item tagged `["critical","urgent"]`) with an unambiguous ruling; no qualifiers or deferred sub-questions
- Pinned item interaction fully specified: how pinned items decrement `require_count`; overshoot edge case (pinned_count > cap_count) ruled build-time vs. run-time; scope of cap defined (total included vs. slicer-selected only)
- Conflict detection rules complete: enumeration of which configurations are build-time checks (at minimum: `require_count > cap_count` same kind, and `cap_count = 0` with `require_count > 0`); cross-kind scarcity explicitly documented as run-time (D040 confirmed)
- KnapsackSlice compatibility path decided: one of {5E: reject combination, 5A: pre-processing, 5D: separate slicer} chosen with explicit rationale; if 5E, build-time guard specified
- `COUNT-DISTRIBUTE-BUDGET` pseudocode written (adapts existing `DISTRIBUTE-BUDGET` from `spec/src/slicers/quota.md` to add count constraints)
- `ExclusionReason` and `SelectionReport` backward-compatibility audit complete: Rust `#[non_exhaustive]` status confirmed for both types; .NET `sealed record` addition confirmed safe; new variants (`CountCapExceeded`) and new fields (`quota_violations`) declared safe or unsafe with rationale
- Design record has zero TBD fields
- `cargo test` and `dotnet test` still pass (no code changes introduced)

## Proof Level

- This slice proves: contract (design record completeness and internal consistency)
- Real runtime required: no (spec/doc changes only)
- Human/UAT required: no (document completeness verifiable mechanically; tag non-exclusivity ruling is the one area where human escalation may be triggered if debate deadlocks — see Proof Strategy)

## Verification

```bash
# File exists
test -f .planning/design/count-quota-design.md && echo "PASS: file exists"

# Zero TBD fields
test $(grep -ci "\bTBD\b" .planning/design/count-quota-design.md) -eq 0 && echo "PASS: no TBD fields"

# All five decision areas present
grep -q "Algorithm Architecture" .planning/design/count-quota-design.md && echo "PASS: algorithm section"
grep -q "Tag Non-Exclusivity" .planning/design/count-quota-design.md && echo "PASS: tag section"
grep -q "Pinned Item" .planning/design/count-quota-design.md && echo "PASS: pinned section"
grep -q "Conflict Detection" .planning/design/count-quota-design.md && echo "PASS: conflict section"
grep -q "KnapsackSlice" .planning/design/count-quota-design.md && echo "PASS: knapsack section"

# Pseudocode present
grep -q "COUNT-DISTRIBUTE-BUDGET" .planning/design/count-quota-design.md && echo "PASS: pseudocode present"

# No regressions
cargo test --manifest-path crates/cupel/Cargo.toml 2>&1 | tail -3
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj 2>&1 | tail -3
```

## Observability / Diagnostics

- Runtime signals: none (pure design artifact; no runtime behavior)
- Inspection surfaces: `cat .planning/design/count-quota-design.md` — single authoritative file; `grep -n "Decision:" .planning/design/count-quota-design.md` — quick decision index
- Failure visibility: if any section contains "TBD", "open question", or "deferred" without an explicit resolution link, the design record is incomplete; `grep -in "TBD\|open question\|deferred" .planning/design/count-quota-design.md` is the diagnostic command
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed:
  - `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — DI-1 through DI-6 design framing
  - `spec/src/slicers/quota.md` — existing `DISTRIBUTE-BUDGET` pseudocode (basis for `COUNT-DISTRIBUTE-BUDGET`)
  - `crates/cupel/src/diagnostics/mod.rs` — `#[non_exhaustive]` status audit for `SelectionReport`, `ExclusionReason`
  - `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — .NET `sealed record` extensibility audit
- New wiring introduced in this slice: `.planning/design/count-quota-design.md` (new file; new directory `.planning/design/`)
- What remains before the milestone is truly usable end-to-end: S06 must reference the design record when specifying `FindMinBudgetFor + QuotaSlice` interaction note; S06 is the last consumer of this artifact

## Tasks

- [x] **T01: Debate algorithm architecture and tag non-exclusivity (DI-1, DI-2)** `est:45m`
  - Why: DI-1 (separate slicer vs. QuotaSlice extension) and DI-2 (tag non-exclusivity) are the most foundational design questions — DI-2 in particular cannot change post-ship without a breaking version bump. Both must be settled before the remaining questions can be framed correctly.
  - Files: `.planning/design/count-quota-design-notes.md` (scratch, intermediate), `spec/src/slicers/quota.md`, `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md`
  - Do: (1) Re-read `spec/src/slicers/quota.md` QUOTA-SLICE pseudocode to understand the existing decorator pattern. (2) Re-read DI-1 and DI-2 framing from `count-quota-report.md`. (3) For DI-1: run a two-position debate (separate `CountQuotaSlice` wrapping inner slicer vs. unified extension of `QuotaSlice`); the deciding question is whether callers need count + percentage on the same kind simultaneously. (4) For DI-2: construct the canonical worked example — one item tagged `["critical","urgent"]`, two `RequireCount(n:2)` constraints — and run an explicit explorer/challenger debate on non-exclusive vs. exclusive semantics; document the production-risk scenario for each choice. (5) If the DI-2 debate produces genuinely irreconcilable arguments (equally strong production cases for each semantics), escalate via `ask_user_questions` with the concrete options rather than writing an ambiguous ruling. (6) Write settled rulings to `count-quota-design-notes.md`.
  - Verify: `count-quota-design-notes.md` contains an unambiguous ruling for DI-1 and DI-2 with no open sub-questions; if `ask_user_questions` was called, the user's answer is recorded as the ruling.
  - Done when: DI-1 and DI-2 have clear, one-sentence rulings with supporting rationale; no qualifiers like "may be" or "possibly."

- [x] **T02: Settle remaining questions and audit backward compatibility (DI-3, DI-4, DI-5, DI-6)** `est:45m`
  - Why: DI-3 (scarcity behavior + SelectionReport representation), DI-4 (KnapsackSlice compatibility), DI-5 (pinned overshoot), and DI-6 (backward-compat precondition) are all prerequisite to writing the complete design record in T03. DI-6 is a precondition for DI-3: the diagnostic mechanism choice (`TraceEvent` vs. `quota_violations` field) depends on whether the types can be extended safely.
  - Files: `crates/cupel/src/diagnostics/mod.rs`, `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`, `.planning/design/count-quota-design-notes.md`
  - Do: (1) **DI-6 audit first:** grep `#[non_exhaustive]` on `SelectionReport` and `ExclusionReason` in `mod.rs`; check .NET `sealed record SelectionReport` extensibility — confirm adding a field to a `sealed record` is non-breaking for callers who use the record via properties (not positional). Record findings. (2) **DI-3:** Given DI-6 result, choose between `SelectionReport.quota_violations` field vs. `TraceEvent::CountQuotaScarcity` — default scarcity behavior (degrade vs. throw); whether `ScarcityBehavior` is per-entry or per-slicer; precise shape of `quota_violations` (list of `{ kind, required, available }` structs). (3) **DI-4:** Choose KnapsackSlice path. Apply the conservative-first principle: start with 5E (reject combination with build-time guard) unless there is a concrete use case in M002 scope that demands 5A or 5D; 5E + pre-processing upgrade (5A) is the recommended two-step path. Specify the guard message if 5E is chosen. (4) **DI-5:** Define scope of count-quota caps (total-included vs. slicer-selected only); specify the `pinned_count > cap_count` outcome — favor option 3 (no special treatment; cap applies to slicer-selected items only, pinned items are outside scope) unless there is a strong reason for run-time warning. (5) Append all rulings to `count-quota-design-notes.md`.
  - Verify: `count-quota-design-notes.md` now covers all 6 design inputs (DI-1 through DI-6) with concrete, one-sentence rulings; `grep -ci "\bTBD\b" .planning/design/count-quota-design-notes.md` → 0.
  - Done when: All six design inputs have unambiguous rulings recorded.

- [x] **T03: Write design decision record and pseudocode** `est:45m`
  - Why: Synthesize the settled rulings from T01+T02 into the authoritative design record. This is the primary deliverable of S03 and the input to S06's KnapsackSlice interaction note and M003's implementation work.
  - Files: `.planning/design/count-quota-design.md` (new), `.planning/design/count-quota-design-notes.md` (consumed), `spec/src/slicers/quota.md` (reference for pseudocode style)
  - Do: (1) Create `.planning/design/` directory (implicit, via file write). (2) Write `.planning/design/count-quota-design.md` with the following sections: **Overview** (CountQuotaSlice description and role in the pipeline), **1. Algorithm Architecture** (ruling + two-phase algorithm description), **2. Tag Non-Exclusivity Semantics** (ruling + canonical worked example, `cupel:primary_tag` as documented workaround), **3. Pinned Item Interaction** (ruling + cap scope definition), **4. Conflict Detection Rules** (build-time enumeration, run-time scarcity behavior, ScarcityBehavior enum shape, `quota_violations` field spec or TraceEvent spec), **5. KnapsackSlice Compatibility** (ruling + guard spec or algorithm spec), **6. ExclusionReason Extensions** (CountCapExceeded variant shape, CountRequireUnmet placement in SelectionReport), **Pseudocode** (COUNT-DISTRIBUTE-BUDGET subroutine adapting existing DISTRIBUTE-BUDGET to add count constraints — RequireCount pre-allocation phase before proportional distribution), **Conformance Vector Outlines** (3-5 scenario sketches covering: baseline count satisfaction, count-cap exclusion, pinned-count decrement, scarcity degrade, tag non-exclusivity). (3) Delete or archive `count-quota-design-notes.md` if it is a scratch file. (4) Run: `grep -ci "\bTBD\b" .planning/design/count-quota-design.md` → 0. (5) Run `cargo test` and `dotnet test` to confirm no regressions. (6) Commit with message `docs(S03): add count-quota design record`.
  - Verify: `test -f .planning/design/count-quota-design.md` passes; `grep -ci "\bTBD\b" .planning/design/count-quota-design.md` → 0; all 5 section headers present; pseudocode block present; both test suites pass.
  - Done when: `.planning/design/count-quota-design.md` exists with zero TBD fields, all 5 design questions answered, pseudocode present, both test suites green.

## Files Likely Touched

- `.planning/design/count-quota-design.md` — new; primary deliverable
- `.planning/design/count-quota-design-notes.md` — new scratch file (T01/T02); consumed in T03
- `spec/src/slicers/quota.md` — read-only reference (pseudocode style)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — read-only input (DI-1 through DI-6)
- `crates/cupel/src/diagnostics/mod.rs` — read-only audit (DI-6)
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — read-only audit (DI-6)
