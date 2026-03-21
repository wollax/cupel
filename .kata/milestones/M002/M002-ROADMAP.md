# M002: v1.3 Design Sprint

**Vision:** Resolve every deferred design question blocking v1.3 implementation — count-based quota semantics, Cupel.Testing assertion vocabulary, DecayScorer/OTel/budget simulation spec chapters, metadata convention system, and spec editorial debt — producing a fully-specified, implementation-ready backlog for M003.

## Success Criteria

- Count-based quota design record exists with all 5 open questions answered, pseudocode written, no remaining TBD fields
- Spec editorial debt closed: all ~8-10 open spec issues from `.planning/issues/open/` with `spec` or `phase24` prefix are resolved
- `MetadataTrustScorer` spec chapter and `"cupel:<key>"` namespace reserved in spec
- Cupel.Testing vocabulary document defines ≥10 named assertion patterns with precise specs (tolerance, error message format, edge cases)
- `DecayScorer` spec chapter complete with algorithm, three curve factories, null-timestamp policy, and conformance vector outlines
- OpenTelemetry verbosity levels fully specified (exact `cupel.*` attributes per tier)
- Budget simulation API contracts written (`GetMarginalItems`, `FindMinBudgetFor`) with monotonicity precondition spec
- Fresh brainstorm summary committed to `.planning/brainstorms/`
- `cargo test` and `dotnet test` still pass (spec/doc changes only — no regressions)

## Key Risks / Unknowns

- **Count-quota algorithm + KnapsackSlice** — constrained knapsack vs preprocessing step; may require multiple debate rounds to converge. S03 retires this.
- **Cupel.Testing vocabulary precision** — edge cases around ties, tolerance, and "high-scoring" definitions require real thought. An imprecise vocabulary produces unstable API surface when implemented.
- **Brainstorm scope creep** — fresh ideas may suggest new implementation scope. S01 must stay focused on design inputs, not implementation commitments.

## Proof Strategy

- Count-quota design risk → retire in S03 by running explorer/challenger debate to convergence; escalate via `ask_user_questions` if debate deadlocks rather than writing ambiguous output
- Cupel.Testing precision risk → retire in S05 by explicitly specifying tolerance, tie-breaking, and error message format for each of the ≥10 assertion patterns

## Verification Classes

- Contract verification: spec chapter completeness (all mandatory sections present), design records have no TBD fields, `.planning/issues/open/` closed issues removed
- Integration verification: `spec/src/SUMMARY.md` updated so all new chapters are reachable; `cargo test` and `dotnet test` pass (no code changes introduced)
- Operational verification: none (no runtime components)
- UAT / human verification: human review of spec chapters for clarity and internal consistency before marking done

## Milestone Definition of Done

This milestone is complete only when all are true:

- All 6 slices are marked `[x]` with summaries
- All R041 spec issues are closed (removed from `.planning/issues/open/` or marked resolved)
- Count-quota design record has no TBD fields
- All new spec chapters are reachable via `spec/src/SUMMARY.md`
- Brainstorm output is committed to `.planning/brainstorms/` with SUMMARY.md
- `cargo test --manifest-path crates/cupel/Cargo.toml` passes
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` passes

## Requirement Coverage

- Covers: R040, R041, R042, R043, R044, R045
- Partially covers: R020 (spec chapter only; full implementation deferred to v1.3), R021 (vocabulary design only), R022 (verbosity spec only)
- Leaves for later: R020 impl, R021 impl, R022 impl (all to M003)
- Orphan risks: none

## Slices

- [x] **S01: Post-v1.2 Brainstorm Sprint** `risk:low` `depends:[]`
  > After this: a fresh `.planning/brainstorms/<date>-brainstorm/` directory with SUMMARY.md committed; new ideas catalogued; deferred items from March 15 brainstorm re-evaluated against v1.2 reality.

- [x] **S02: Spec Editorial Debt** `risk:low` `depends:[]`
  > After this: all ~8-10 spec/phase24 issues from `.planning/issues/open/` are closed; event ordering, item_count sentinel, observer callback normative status, greedy zero-token note, knapsack floor/truncation note, UShapedPlacer pinned edge, composite pseudocode, ScaledScorer nesting warning are all addressed in `spec/src/`.

- [ ] **S03: Count-Based Quota Design** `risk:high` `depends:[S01]`
  > After this: a design decision record in `.planning/design/count-quota-design.md` resolves all 5 open questions — algorithm, tag non-exclusivity, pinned interaction, conflict detection, knapsack path — with pseudocode and no remaining TBD fields.

- [ ] **S04: Metadata Convention System Spec** `risk:low` `depends:[]`
  > After this: `spec/src/scorers/metadata-trust.md` exists and is linked from `SUMMARY.md`; `"cupel:<key>"` namespace reserved; `cupel:trust` and `cupel:source-type` conventions specified; `MetadataTrustScorer` spec chapter complete.

- [ ] **S05: Cupel.Testing Vocabulary Design** `risk:medium` `depends:[S01]`
  > After this: `spec/src/testing/vocabulary.md` defines ≥10 named assertion patterns over `SelectionReport`; each pattern specifies what it asserts, tolerance/edge cases, and error message format; no ambiguous "high-scoring" or similar undefined terms remain.

- [ ] **S06: Future Features Spec Chapters** `risk:medium` `depends:[S01,S03]`
  > After this: `spec/src/scorers/decay.md`, `spec/src/integrations/opentelemetry.md`, and `spec/src/analytics/budget-simulation.md` exist and are reachable from `SUMMARY.md`; DecayScorer algorithm and curves fully specified; OTel attributes per verbosity tier defined; `GetMarginalItems`/`FindMinBudgetFor` API contracts written with monotonicity spec.

## Boundary Map

### S01 (standalone — feeds S03, S05, S06)

Produces:
- `.planning/brainstorms/<date>-brainstorm/SUMMARY.md` — fresh idea register with post-v1.2 perspective
- Refined list of design considerations for count-quota algorithm (feeds S03)
- Refined list of analytics/testing patterns (feeds S05)
- Any new angles on DecayScorer curves or OTel verbosity (feeds S06)

Consumes: nothing (fresh brainstorm from current codebase state)

### S02 (standalone)

Produces:
- Closed/removed issue files from `.planning/issues/open/` for all R041 items
- Updated spec files in `spec/src/diagnostics/` and `spec/src/slicers/` and `spec/src/scorers/`:
  - `events.md` — event ordering rule (item-level before/after stage-level); item_count sentinel note
  - `trace-collector.md` — observer callback labeled with MAY (non-normative)
  - `slicers/greedy.md` — zero-token item ordering note
  - `slicers/knapsack.md` — floor vs truncation-toward-zero equivalence note
  - `placers/u-shaped.md` — pinned edge case row corrected
  - `scorers/composite.md` — pseudocode storage assignment added
  - `scorers/scaled.md` — nesting depth warning added

Consumes: nothing (standalone editorial work)

### S03 → S06

Produces:
- `.planning/design/count-quota-design.md` — full design decision record:
  - Algorithm: `RequireCount(kind, minCount)` and `CapCount(kind, maxCount)` constraints
  - Tag non-exclusivity ruling: whether multi-tag items count toward all matching tag quotas or only the first
  - Pinned item interaction: how pinned items satisfy or reduce count minimums
  - Conflict detection: build-time (`RequireCount(2).CapCount(1)` → error) vs run-time (too few candidates → behavior spec)
  - KnapsackSlice compatibility: preprocessing path vs constrained-knapsack variant
- Pseudocode for `COUNT-DISTRIBUTE-BUDGET` subroutine

Consumes from S01:
- Any fresh angles on count-quota design surfaced in brainstorm

### S04 (standalone)

Produces:
- `spec/src/scorers/metadata-trust.md` — `MetadataTrustScorer` spec chapter:
  - `"cupel:<key>"` namespace reservation
  - `cupel:trust` convention (float64, [0.0, 1.0], caller-computed)
  - `cupel:source-type` convention (string enum: "user", "tool", "external", "system")
  - `MetadataTrustScorer` algorithm (reads `cupel:trust`, returns value directly; missing key → configurable default)
  - Conformance vector outlines (3-5 scenarios)
- `spec/src/SUMMARY.md` updated to include new chapter

Consumes: nothing (additive new chapter, no dependencies on other M002 slices)

### S05 → (M003 Cupel.Testing implementation)

Produces:
- `spec/src/testing/vocabulary.md` — Cupel.Testing assertion vocabulary:
  - ≥10 named assertion patterns (e.g. `IncludeItemWith`, `ExcludeItemWith`, `HaveTokenUtilizationAbove`, `HaveKindInIncluded`, `HaveAtLeastNExclusions`, `PlaceItemAtEdge`, `HaveKindDiversity`, `ExcludeWithReason`, `HaveBudgetUtilizationAbove`, `HaveNoExclusionsForKind`)
  - For each: what it asserts (precise), tolerance (if applicable), tie-breaking behavior, error message format on failure
  - Explicit note: no snapshot assertions until ordering stability is guaranteed
- `spec/src/SUMMARY.md` updated

Consumes from S01:
- Any fresh analytics patterns surfaced in brainstorm that should become vocabulary candidates

### S06 (depends on S01 for fresh angles, S03 for count-quota resolution)

Produces:
- `spec/src/scorers/decay.md` — DecayScorer spec chapter:
  - Algorithm (score = curve(age) where age = referenceTime − item.timestamp)
  - `TimeProvider` injection pattern (mandatory; no silent default)
  - Three curve factories: `Exponential(halfLife)`, `Step(windows)`, `Window(maxAge)`
  - Null-timestamp policy: configurable `nullTimestampScore` (default 0.5)
  - Edge cases: zero half-life, age exactly at window boundary, negative age (future-dated items)
  - Conformance vector outlines (5 scenarios with fixed `referenceTime`)
  - Rust `TimeProvider` equivalent design note
- `spec/src/integrations/opentelemetry.md` — OTel verbosity spec:
  - Three verbosity tiers: `StageOnly`, `StageAndExclusions`, `Full`
  - Exact `cupel.*` attribute names and values per tier
  - Pre-stability disclaimer (attribute names may change as OTel LLM SIG stabilizes)
  - Cardinality warning for `Full` verbosity at scale
- `spec/src/analytics/budget-simulation.md` — Budget simulation API contracts:
  - `GetMarginalItems(items, budget, slackTokens)` — single additional `DryRun` call, diff spec
  - `FindMinBudgetFor(items, targetItem, searchCeiling)` — binary search over `DryRun` calls (~10-15 invocations), monotonicity precondition
  - `QuotaSlice` incompatibility guard for `FindMinBudgetFor` (non-monotonic inclusion risk)
- `spec/src/SUMMARY.md` updated

Consumes from S01:
- Any fresh angles on DecayScorer curves or OTel verbosity surfaced in brainstorm

Consumes from S03:
- Count-quota design record (S06 may reference it when specifying `FindMinBudgetFor` + `QuotaSlice` interaction note)
