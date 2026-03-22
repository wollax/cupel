---
id: M002
provides:
  - ".planning/brainstorms/2026-03-21T09-00-brainstorm/ — 9-file post-v1.2 brainstorm (S01)"
  - "All 20 spec/phase24 editorial issues closed; 13 spec files updated (S02)"
  - ".planning/design/count-quota-design.md — count-quota design record with 6 DI rulings + pseudocode (S03)"
  - "spec/src/scorers/metadata-trust.md — MetadataTrustScorer spec + cupel: namespace reservation (S04)"
  - "spec/src/testing/vocabulary.md — 13 named Cupel.Testing assertion patterns (S05)"
  - "spec/src/scorers/decay.md — DecayScorer spec chapter (S06)"
  - "spec/src/integrations/opentelemetry.md — OTel verbosity spec chapter (S06)"
  - "spec/src/analytics/budget-simulation.md — budget simulation API contracts (S06)"
  - "spec/src/SUMMARY.md — Scorers, Testing, Integrations, Analytics sections all updated"
key_decisions:
  - "D039 — M002 is design-only; no implementation code"
  - "D040 — Count-quota conflict detection: build-time vs run-time distinction is a hard requirement"
  - "D041 — Cupel.Testing: no FluentAssertions dependency; no snapshot assertions until ordering stability guaranteed"
  - "D042 — DecayScorer TimeProvider is mandatory (not optional)"
  - "D043 — OTel attribute namespace is cupel.* (pre-stable); do not chase gen_ai.*"
  - "D044 — Multi-budget DryRun API variant rejected"
  - "D045 — BudgetUtilization and KindDiversity belong in Wollax.Cupel core, not a separate analytics package"
  - "D046 — CountRequireUnmet is a SelectionReport-level field, not a per-item ExclusionReason"
  - "D047 — Rust TimeProvider trait shape: minimal trait with Box<dyn> at construction"
  - "D048 — FindMinBudgetFor return type is int? / Option<i32>"
  - "D054 — CountQuotaSlice is a separate decorator, not a QuotaSlice extension"
  - "D055 — Count-quota tag semantics: non-exclusive and not configurable"
  - "D056 — ScarcityBehavior::Degrade is the default; Throw is opt-in per slicer"
  - "D057 — SelectionReport positional deconstruction explicitly unsupported"
  - "D065 — Cupel.Testing assertion predicate type: IncludedItem/ExcludedItem, not raw ContextItem"
  - "D066 — Cupel.Testing chain entry point: SelectionReport.Should() → SelectionReportAssertionChain"
  - "D068 — OTel stage count: 5 Activities (Sort omitted)"
  - "D069 — GetMarginalItems budget parameter: explicit ContextBudget + int slackTokens"
  - "D070 — Step curve windows: ordered list, strict >, throw-at-construction for empty/zero-width"
  - "D071 — Window curve boundary: half-open [0, maxAge), age == maxAge returns 0.0"
patterns_established:
  - "Explorer/challenger brainstorm format: ideas.md (uncensored ≥15-25 proposals) → report.md (challenged, scoped, verdict table + downstream inputs)"
  - "Spec chapter structure: Overview → Fields/Config → Algorithm → Edge Cases → Conformance Vectors → Conformance Notes (established by metadata-trust.md)"
  - "Design record structure: per-decision sections with Decision/Rationale/Alternatives headings; each section ends with a Decision: one-liner"
  - "COUNT-DISTRIBUTE-BUDGET pseudocode style consistent with DISTRIBUTE-BUDGET in quota.md (named variables, three-phase structure)"
  - "Vocabulary spec pattern: pre-decisions table + chain plumbing section + per-pattern headers with Assertion Semantics / Predicate Type / Edge Cases / Error Message Format"
  - "Conformance vector outlines in narrative form (not TOML) when schema lacks feature support"
observability_surfaces:
  - "grep -ci '\\bTBD\\b' <chapter> — TBD count must be 0 for any completed spec chapter or design record"
  - "grep -q '<chapter-key>' spec/src/SUMMARY.md — reachability signal; missing entry means mdBook won't serve the chapter"
  - "grep -n 'Decision:' .planning/design/count-quota-design.md — returns exactly 6 lines; fewer means a section is missing"
  - "ls .planning/issues/open/ | grep -E 'spec-|phase24-' — returns only spec-workflow-checksum-verification.md (1 intentionally deferred)"
requirement_outcomes:
  - id: R040
    from_status: active
    to_status: validated
    proof: ".planning/design/count-quota-design.md exists with 6 DI rulings settled (separate decorator, non-exclusive tags, ScarcityBehavior::Degrade + quota_violations, 5E KnapsackSlice guard, slicer-scoped caps + pinned decrement, backward-compat audit); COUNT-DISTRIBUTE-BUDGET pseudocode written; grep -ci '\\bTBD\\b' → 0; cargo test (113 passed) and dotnet test (583 passed) both green"
  - id: R041
    from_status: active
    to_status: validated
    proof: "All 20 spec/phase24 issue files closed (deleted from .planning/issues/open/); 13 spec files updated with precise ordering rules, normative alignment, algorithm clarifications, and reserved variant JSON examples; TOML drift guard satisfied; cargo test and dotnet test both green"
  - id: R042
    from_status: active
    to_status: validated
    proof: "spec/src/scorers/metadata-trust.md exists with 'cupel:' namespace reserved normatively (MUST NOT); cupel:trust (float64 [0,1], string storage, configurable defaultScore, explicit parse-failure/non-finite handling) and cupel:source-type (open string, 4 RECOMMENDED values) conventions defined; 5 conformance vector outlines; grep -ci '\\bTBD\\b' → 0; SUMMARY.md and scorers.md both updated; cargo test and dotnet test both green"
  - id: R043
    from_status: active
    to_status: validated
    proof: "spec/src/testing/vocabulary.md exists with 13 named assertion patterns (≥10 required); each pattern has assertion semantics, predicate type, edge cases, tie-breaking behavior, and error message format; grep -ci '\\bTBD\\b' → 0; no 'high-scoring' undefined terms; PlaceItemAtEdge defines edge as position 0 or count−1 exactly; HaveBudgetUtilizationAbove denominator locked to MaxTokens; SUMMARY.md updated; cargo test and dotnet test both green"
  - id: R044
    from_status: active
    to_status: validated
    proof: "spec/src/scorers/decay.md (DECAY-SCORE pseudocode, 3 curve factories, mandatory TimeProvider per D042/D047, nullTimestampScore default 0.5, 5 conformance vector outlines, 0 TBD fields); spec/src/integrations/opentelemetry.md (5-Activity hierarchy per D068, 3 CupelVerbosity tiers with exact cupel.* attribute tables, pre-stability disclaimer per D043, cardinality table, 0 TBD fields); spec/src/analytics/budget-simulation.md (DryRun determinism MUST, GetMarginalItems with explicit ContextBudget param per D069, FindMinBudgetFor with binary search + int?/Option<i32> return per D048, QuotaSlice + CountQuotaSlice guards, 0 TBD fields); all three reachable via SUMMARY.md; cargo test and dotnet test both green"
  - id: R045
    from_status: active
    to_status: validated
    proof: ".planning/brainstorms/2026-03-21T09-00-brainstorm/ committed with 9 files (SUMMARY.md, 4 explorer/challenger pairs); 5 deferred items re-evaluated; 13 M003+ backlog candidates catalogued; downstream inputs written for S03/S05/S06; cargo test and dotnet test both green"
duration: 1 day (6 slices, 2026-03-21)
verification_result: passed
completed_at: 2026-03-21
---

# M002: v1.3 Design Sprint

**Every deferred design question blocking v1.3 implementation resolved in a single day: 6 spec chapters written (0 TBD fields each), count-quota design record complete, 20 spec editorial issues closed, fresh brainstorm committed — M003 implementation backlog is fully specified.**

## What Happened

M002 executed as 6 sequential slices across a single day, producing an entirely design/spec output set with no implementation code (D039).

**S01 — Post-v1.2 Brainstorm Sprint** opened the milestone by running four explorer/challenger debate pairs covering count-quota algorithm, Cupel.Testing vocabulary, future features (DecayScorer/OTel/budget simulation), and deferred-item re-evaluation against the v1.2 reality. The 9-file output catalogued 13 M003+ backlog candidates, settled several design pre-decisions (multi-budget DryRun rejected, FindMinBudgetFor return type as `int?`/`Option<i32>`, BudgetUtilization and KindDiversity in Wollax.Cupel core), and produced structured downstream inputs for S03/S05/S06. The brainstorm identified DryRun determinism invariant as a spec gap that S06 must close.

**S02 — Spec Editorial Debt** processed all 20 open spec/phase24 issues, grouping edits into three task waves: diagnostics spec files (events ordering MUST rule, item_count sentinel, Sort omission confirmation, section reordering), algorithm spec files (zero-token tiebreak, floor/truncation equivalence, UShapedPlacer pinned row correction, composite pseudocode assignment lines, ScaledScorer nesting warning), and data-model/TOML/issue deletion. The TOML drift guard was satisfied; one issue (`spec-workflow-checksum-verification.md`) was intentionally deferred as a CI security concern outside D039 scope.

**S03 — Count-Based Quota Design** resolved the hardest M002 design problem through three sequential tasks: DI-1 and DI-2 debates (separate decorator architecture and non-exclusive tag semantics — the only two positions that survived both challenger scrutiny and the constraint that tag semantics are a public contract that cannot change post-ship); DI-3 through DI-6 resolution (ScarcityBehavior::Degrade default, 5E KnapsackSlice guard, slicer-scoped caps with pinned decrement, backward-compat audit via #[non_exhaustive]); and synthesis into the authoritative design record with COUNT-DISTRIBUTE-BUDGET pseudocode. Tag non-exclusivity (DI-2) — flagged as the highest-risk question — was resolved without `ask_user_questions` escalation because the exclusive alternatives were both definitively eliminated by the challenger's own analysis.

**S04 — Metadata Convention System Spec** produced the `spec/src/scorers/metadata-trust.md` chapter using the reflexive.md structural template plus two added sections (Metadata Namespace Reservation with normative MUST NOT, Conventions with both cupel:trust and cupel:source-type). The chapter encodes `"cupel:"` namespace reservation, configurable defaultScore with explicit parse-failure/non-finite handling, 5 conformance vector outlines, and an explicit anti-gate statement (trust is a scoring input only; items are never excluded based on trust score). SUMMARY.md and scorers.md both updated.

**S05 — Cupel.Testing Vocabulary Design** produced `spec/src/testing/vocabulary.md` across three tasks: foundational skeleton with 4 locked pre-decisions (PD-1 through PD-4) and chain plumbing type shape; inclusion and exclusion group patterns (7 patterns including the .NET language-asymmetry note on ExcludeItemWithBudgetDetails); and placement/ordering/budget group patterns (6 patterns) plus a precision audit. The vocabulary locked all 13 patterns with no undefined terms — "high-scoring" language was replaced throughout with explicit parameterized forms. The D019 insertion-order tiebreak limitation was explicitly documented as a known conformance gap in ExcludedItemsAreSortedByScoreDescending.

**S06 — Future Features Spec Chapters** produced three spec chapters: DecayScorer (contrast framing against RecencyScorer, mandatory TimeProvider per D042/D047, DECAY-SCORE pseudocode with negative-age clamping, three curve factories with precise boundary semantics per D070/D071, 5 conformance vector outlines); OTel verbosity (5-Activity hierarchy per D068, three CupelVerbosity tiers with complete attribute tables, cardinality recommendations, pre-stability disclaimer per D043, open-ended ExclusionReason values note); and budget simulation (DryRun determinism MUST statement, GetMarginalItems with explicit budget param and QuotaSlice guard per D069, FindMinBudgetFor with binary search and both QuotaSlice + CountQuotaSlice guards per D052, SweepBudget out-of-scope note, Rust-parity-deferred note). All three chapters referenced locked decisions directly without reopening debates.

## Cross-Slice Verification

**Success Criterion 1 — Count-quota design record with all 5 questions answered, pseudocode, no TBD:**
- `.planning/design/count-quota-design.md` exists ✓
- `grep -ci "\bTBD\b" .planning/design/count-quota-design.md` → 0 ✓
- `grep -c "Decision:" .planning/design/count-quota-design.md` → 6 (one per section) ✓
- COUNT-DISTRIBUTE-BUDGET pseudocode present ✓
- All 5 design questions covered (+ DI-6 backward-compat audit as bonus) ✓

**Success Criterion 2 — Spec editorial debt closed (~8-10 open spec issues):**
- Actual issue count was 20 (not ~8-10); all 20 closed ✓
- `ls .planning/issues/open/ | grep -E 'spec-|phase24-'` → 1 (only the intentionally deferred `spec-workflow-checksum-verification.md`) ✓
- 13 spec files updated with ordering rules, normative alignment, algorithm clarifications ✓
- TOML drift guard: `diff spec/conformance/.../greedy-chronological.toml crates/cupel/conformance/.../greedy-chronological.toml` → no output ✓

**Success Criterion 3 — MetadataTrustScorer spec chapter and `"cupel:<key>"` namespace reserved:**
- `spec/src/scorers/metadata-trust.md` exists with MUST NOT language for `cupel:` prefix ✓
- `cupel:trust` and `cupel:source-type` conventions defined ✓
- `grep -q "metadata-trust" spec/src/SUMMARY.md` → PASS ✓
- `grep -ci "\bTBD\b" spec/src/scorers/metadata-trust.md` → 0 ✓

**Success Criterion 4 — Cupel.Testing vocabulary ≥10 named patterns:**
- `spec/src/testing/vocabulary.md` exists with 13 patterns (exceeds ≥10 requirement) ✓
- Each pattern: assertion semantics, predicate type, edge cases, error message format ✓
- `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md` → 0 ✓
- `grep -c "high-scoring\|high scoring" spec/src/testing/vocabulary.md` → 0 ✓

**Success Criterion 5 — DecayScorer spec chapter complete:**
- `spec/src/scorers/decay.md` exists ✓
- Algorithm (DECAY-SCORE), three curve factories (Exponential, Step, Window), null-timestamp policy, conformance vector outlines, Rust TimeProvider trait — all present ✓
- `grep -ci "\bTBD\b" spec/src/scorers/decay.md` → 0 ✓

**Success Criterion 6 — OpenTelemetry verbosity levels fully specified:**
- `spec/src/integrations/opentelemetry.md` exists ✓
- StageOnly / StageAndExclusions / Full tiers with exact `cupel.*` attribute names per tier ✓
- Pre-stability disclaimer present ✓
- `grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md` → 0 ✓

**Success Criterion 7 — Budget simulation API contracts written:**
- `spec/src/analytics/budget-simulation.md` exists ✓
- GetMarginalItems and FindMinBudgetFor with monotonicity precondition spec ✓
- QuotaSlice incompatibility guard present ✓
- `grep -ci "\bTBD\b" spec/src/analytics/budget-simulation.md` → 0 ✓

**Success Criterion 8 — Fresh brainstorm summary committed:**
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` present ✓
- 9 files total in directory ✓

**Success Criterion 9 — `cargo test` and `dotnet test` pass:**
- `cargo test --manifest-path crates/cupel/Cargo.toml` → 35 passed (+ 78 doctests), 0 failed ✓
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → 583 passed, 0 failed ✓

**Definition of Done:**
- All 6 slices `[x]` with summaries ✓
- All R041 spec issues closed (20/20; 1 intentionally deferred per D050) ✓
- Count-quota design record no TBD fields ✓
- All new spec chapters reachable via `spec/src/SUMMARY.md` ✓
- Brainstorm output committed to `.planning/brainstorms/` with SUMMARY.md ✓
- `cargo test` passes ✓
- `dotnet test` passes ✓
- **Human review of S06 spec chapters** — outstanding per S06-UAT.md; this is the final gate before M002 is declared fully complete. All automated checks pass.

**Note on spec-workflow-checksum-verification.md:** One spec issue file remains open by design (D050 — CI security concern, out of M002 spec-editorial scope). This is not a DoD failure; the roadmap criterion is "all R041 spec issues closed" and the actual issue was classified as deferred, not an R041 editorial item.

## Requirement Changes

- R040: active → validated — `.planning/design/count-quota-design.md` exists with all 5 decision areas answered, 6 DI rulings settled, COUNT-DISTRIBUTE-BUDGET pseudocode written, 0 TBD fields; both test suites green
- R041: active → validated — all 20 spec/phase24 issue files closed; 13 spec files updated; TOML drift guard satisfied; both test suites green
- R042: active → validated — `spec/src/scorers/metadata-trust.md` exists with `"cupel:"` namespace reserved normatively, both conventions defined, 5 conformance vector outlines, 0 TBD fields; both navigation files updated; both test suites green
- R043: active → validated — `spec/src/testing/vocabulary.md` exists with 13 patterns (≥10 required), each with error message format, 0 TBD fields, no undefined qualifiers; SUMMARY.md updated; both test suites green
- R044: active → validated — all three spec chapters (decay.md, opentelemetry.md, budget-simulation.md) exist with required sections, 0 TBD fields each, reachable from SUMMARY.md; both test suites green
- R045: active → validated — `.planning/brainstorms/2026-03-21T09-00-brainstorm/` committed with 9 files and SUMMARY.md; downstream inputs for S03/S05/S06 produced; both test suites green

## Forward Intelligence

### What the next milestone should know
- **M003 implementation contracts are locked.** All six new spec chapters and the count-quota design record are the authoritative implementation contracts. Do not deviate from the pseudocode without a new design decision record (append to DECISIONS.md).
- **Conformance vectors for new chapters are narrative-only.** metadata-trust.md, vocabulary.md, decay.md, opentelemetry.md, and budget-simulation.md all have narrative conformance vector outlines, not TOML-backed vectors. M003 should produce TOML conformance vectors as part of each implementation slice.
- **`rtk dotnet test` is incompatible with TUnit.** The `rtk` wrapper injects `--nologo` and `--logger` arguments that confuse TUnit's CLI runner. Always use bare `dotnet test --project` for the .NET test suite.
- **BudgetUtilization, KindDiversity, and TimestampCoverage belong in Wollax.Cupel core (not a separate analytics package).** Decision D045; this was revised from an earlier brainstorm recommendation. Do not create a Wollax.Cupel.Analytics package without revisiting D045.
- **Fork diagnostic (PolicySensitivityReport) is M003-ready.** dry_run is now live in both languages; this is the sole prerequisite. S01 identified it as a ~80-line orchestration over dry_run — a small M003 task.
- **CountConstrainedKnapsackSlice is explicitly deferred (D052).** v1.3 ships with the 5E build-time rejection; the constrained-knapsack algorithm is a future investment once demand is confirmed.

### What's fragile
- **`cupel.*` OTel attribute names are pre-stable (D043).** Any M003 OTel implementation must surface this to callers via package README, not just spec. Attributes may change when OTel LLM SIG semantic conventions stabilize.
- **Step curve `>` comparison at boundary values.** D070 specifies strict `>`, meaning `age == window.maxAge` falls through to the next window. Conformance vectors must cover this boundary case explicitly — implementors who use `>=` will be silently wrong.
- **`ExcludeItemWithBudgetDetails` is Rust-only in practice.** .NET `ExclusionReason` is a flat enum with no associated data. M003 .NET implementation must decide whether to omit this assertion or provide an alternative form.
- **SelectionReport positional deconstruction is explicitly unsupported (D057).** Adding `quota_violations` field to SelectionReport in M003 will break any .NET callers using positional deconstruction. This must be called out prominently in the M003 release notes.
- **TOML drift guard is CI-fidelity-limited.** `spec-workflow-checksum-verification.md` remains open (D050). Any manual TOML edits to conformance vectors must be applied to both `spec/conformance/` and `crates/cupel/conformance/` — the drift check catches this, but the checksum verification issue remains unresolved.

### Authoritative diagnostics
- `grep -ci "\bTBD\b" <chapter>` → 0 — primary completeness signal for any spec chapter or design record; non-zero means incomplete
- `grep -q "<chapter-path>" spec/src/SUMMARY.md` — reachability signal; missing entry means mdBook won't serve the chapter
- `grep -n "Decision:" .planning/design/count-quota-design.md` → exactly 6 lines — completeness signal for the count-quota design record
- `ls .planning/issues/open/ | grep -E 'spec-|phase24-'` → should return only `spec-workflow-checksum-verification.md`

### What assumptions changed
- **Spec issue count was 20, not ~8-10.** The milestone estimate was significantly low; the actual issue count was 2× higher. Future "batch editorial" estimates should assume more issues than reported.
- **DI-2 (tag non-exclusivity) did not require escalation.** The original risk assumption was that this might need `ask_user_questions` if the challenger debate deadlocked. The exclusive alternatives were both eliminated by the challenger's own analysis, making the non-exclusive ruling unambiguous.
- **T04 BudgetUtilization/KindDiversity placement overrides T02.** The T02 explorer recommended placing these in a separate analytics package; T04 challenger revised this based on the OTel dependency chain argument. The SUMMARY.md notes this override explicitly.

## Files Created/Modified

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/` — 9 new files (count-quota-ideas.md, count-quota-report.md, testing-vocabulary-ideas.md, testing-vocabulary-report.md, future-features-ideas.md, future-features-report.md, post-v1-2-ideas.md, post-v1-2-report.md, SUMMARY.md)
- `.planning/design/count-quota-design.md` — new; 343-line authoritative design record for CountQuotaSlice
- `.planning/design/count-quota-design-notes.md` — new; working notes scratch file from S03 tasks
- `spec/src/diagnostics/events.md` — item ordering MUST rule; item_count sentinel note; Sort rejected-alternative block removed
- `spec/src/diagnostics/trace-collector.md` — section reorder; Observer Callback labeled (Optional — MAY); null-path cross-reference
- `spec/src/diagnostics/selection-report.md` — section reorder; ExcludedItem cross-reference; bold Rejected alternative
- `spec/src/diagnostics/exclusion-reasons.md` — JSON examples for 4 reserved variants
- `spec/src/diagnostics.md` — Summary table "Spec page" column header
- `spec/src/slicers/greedy.md` — zero-token tiebreak note; Conformance Notes MUST NOT bullet
- `spec/src/slicers/knapsack.md` — floor/truncation equivalence note
- `spec/src/placers/u-shaped.md` — corrected Pinned items Edge Cases table row
- `spec/src/scorers/composite.md` — two explicit assignment lines replacing comment
- `spec/src/scorers/scaled.md` — nesting depth performance warning
- `spec/src/scorers/kind.md` — case-insensitivity source clarification
- `spec/src/data-model/context-item.md` — content field table cell demoted to plain description
- `spec/src/conformance/format.md` — QuotaSlice clarifying sentence
- `spec/conformance/required/pipeline/greedy-chronological.toml` — jan density comment updated
- `crates/cupel/conformance/required/pipeline/greedy-chronological.toml` — identical update (drift guard)
- `spec/src/scorers/metadata-trust.md` — new; MetadataTrustScorer spec chapter
- `spec/src/testing/vocabulary.md` — new; 13 Cupel.Testing assertion patterns
- `spec/src/scorers/decay.md` — new; DecayScorer spec chapter
- `spec/src/integrations/opentelemetry.md` — new (in new `spec/src/integrations/` directory); OTel verbosity spec chapter
- `spec/src/analytics/budget-simulation.md` — new (in new `spec/src/analytics/` directory); budget simulation API contracts
- `spec/src/scorers.md` — MetadataTrustScorer row + Absolute Scorers bullet
- `spec/src/SUMMARY.md` — MetadataTrustScorer, DecayScorer, Testing section, Integrations section, Analytics section
- `.planning/issues/open/` — 20 resolved issue files deleted
- `.kata/REQUIREMENTS.md` — R040–R045 all moved to validated
- `.kata/milestones/M002/M002-ROADMAP.md` — S06 marked [x]
- `.kata/milestones/M002/slices/S01/S01-SUMMARY.md` — new
- `.kata/milestones/M002/slices/S02/S02-SUMMARY.md` — new
- `.kata/milestones/M002/slices/S03/S03-SUMMARY.md` — new
- `.kata/milestones/M002/slices/S04/S04-SUMMARY.md` — new
- `.kata/milestones/M002/slices/S05/S05-SUMMARY.md` — new
- `.kata/milestones/M002/slices/S06/S06-SUMMARY.md` — new
- `.kata/milestones/M002/slices/S06/S06-UAT.md` — new
- `.kata/PROJECT.md` — M002 completion section added
- `.kata/STATE.md` — updated throughout milestone
- `.kata/DECISIONS.md` — D039–D071 appended
