# S06: Future Features Spec Chapters

**Goal:** Produce three spec chapters — `spec/src/scorers/decay.md` (DecayScorer), `spec/src/integrations/opentelemetry.md` (OTel verbosity), and `spec/src/analytics/budget-simulation.md` (budget simulation API contracts) — all reachable from `spec/src/SUMMARY.md`, with zero TBD fields and no open design gaps.
**Demo:** After T03, `grep -ci "\bTBD\b"` returns 0 across all three new files; `grep -q "decay" spec/src/SUMMARY.md` and `grep -q "opentelemetry" spec/src/SUMMARY.md` and `grep -q "budget-simulation" spec/src/SUMMARY.md` all pass; `cargo test` and `dotnet test` pass with no regressions.

## Must-Haves

- `spec/src/scorers/decay.md` exists with algorithm (DECAY-SCORE pseudocode), `TimeProvider` mandatory injection note, three curve factories (`Exponential(halfLife)`, `Step(windows)`, `Window(maxAge)`), `nullTimestampScore` default, negative-age clamping, `Window` boundary semantics, `Step` zero-width window precondition, 5 conformance vector outlines, and Rust `TimeProvider` trait declaration
- `spec/src/integrations/opentelemetry.md` exists with root + 5 stage Activities (Sort omitted per events.md precedent), exact `cupel.*` attribute names and types per tier (`StageOnly`, `StageAndExclusions`, `Full`), cardinality table, pre-stability disclaimer, `ActivitySource` name, and companion-package zero-dep note
- `spec/src/analytics/budget-simulation.md` exists with `GetMarginalItems` (explicit `budget` + `slackTokens` params, diff direction, `QuotaSlice` guard), `FindMinBudgetFor` (binary search, monotonicity precondition, `QuotaSlice` + `CountQuotaSlice` guard, `int?`/`Option<i32>` return), `DryRun` determinism invariant as normative text, Rust parity deferred note, and `SweepBudget` out-of-scope note
- `spec/src/SUMMARY.md` updated with: `DecayScorer` entry under `# Scorers`, new `# Integrations` section, new `# Analytics` section — all file paths correct so `mdBook build` succeeds
- `grep -ci "\bTBD\b"` returns 0 across all three new chapters
- `cargo test --manifest-path crates/cupel/Cargo.toml` passes
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` passes

## Proof Level

- This slice proves: contract
- Real runtime required: no (spec authoring only, no code changes per D039)
- Human/UAT required: yes — human review of spec chapters for clarity and internal consistency is the final gate before milestone done

## Verification

```bash
# All three chapters exist
test -f spec/src/scorers/decay.md
test -f spec/src/integrations/opentelemetry.md
test -f spec/src/analytics/budget-simulation.md

# No TBD fields in any new chapter
grep -ci "\bTBD\b" spec/src/scorers/decay.md        # → 0
grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md  # → 0
grep -ci "\bTBD\b" spec/src/analytics/budget-simulation.md # → 0

# DecayScorer completeness
grep -q "DECAY-SCORE" spec/src/scorers/decay.md
grep -q "Exponential" spec/src/scorers/decay.md
grep -q "Step" spec/src/scorers/decay.md
grep -q "Window" spec/src/scorers/decay.md
grep -q "TimeProvider" spec/src/scorers/decay.md
grep -q "nullTimestampScore" spec/src/scorers/decay.md
grep -q "Conformance" spec/src/scorers/decay.md

# OTel completeness
grep -q "StageOnly" spec/src/integrations/opentelemetry.md
grep -q "StageAndExclusions" spec/src/integrations/opentelemetry.md
grep -q "Full" spec/src/integrations/opentelemetry.md
grep -q "cupel.budget.max_tokens" spec/src/integrations/opentelemetry.md
grep -q "cupel.exclusion.reason" spec/src/integrations/opentelemetry.md
grep -q "Wollax.Cupel" spec/src/integrations/opentelemetry.md
grep -q "pre-stable\|pre.stable" spec/src/integrations/opentelemetry.md

# Budget simulation completeness
grep -q "GetMarginalItems" spec/src/analytics/budget-simulation.md
grep -q "FindMinBudgetFor" spec/src/analytics/budget-simulation.md
grep -q "QuotaSlice" spec/src/analytics/budget-simulation.md
grep -q "deterministic" spec/src/analytics/budget-simulation.md
grep -q "monoton" spec/src/analytics/budget-simulation.md
grep -q "Rust" spec/src/analytics/budget-simulation.md

# SUMMARY.md links
grep -q "decay" spec/src/SUMMARY.md
grep -q "opentelemetry" spec/src/SUMMARY.md
grep -q "budget-simulation" spec/src/SUMMARY.md
grep -q "Integrations" spec/src/SUMMARY.md
grep -q "Analytics" spec/src/SUMMARY.md

# Test suite regression check
cargo test --manifest-path crates/cupel/Cargo.toml
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj
```

## Observability / Diagnostics

- Runtime signals: none (spec authoring — no code changes)
- Inspection surfaces: `grep -ci "\bTBD\b" spec/src/scorers/decay.md` / `opentelemetry.md` / `budget-simulation.md` — primary completeness check; `grep -q "<chapter>" spec/src/SUMMARY.md` — reachability check; `mdBook build` (optional) — link validation
- Failure visibility: TBD-count > 0 means incomplete section; missing SUMMARY.md entry means chapter unreachable from index; test failures mean an incidental code regression was introduced
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `spec/src/scorers/metadata-trust.md` (chapter style template), `spec/src/slicers/quota.md` (DISTRIBUTE-BUDGET pseudocode naming style), `.planning/design/count-quota-design.md` Section 5 (KnapsackSlice guard language), `spec/src/diagnostics/events.md` (PipelineStage enum — Sort omission), `spec/src/diagnostics/selection-report.md` (SelectionReport fields), `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` ("S06 must specify" mandate lists)
- New wiring introduced in this slice: two new top-level SUMMARY.md sections (`# Integrations` and `# Analytics`); new `spec/src/integrations/` and `spec/src/analytics/` directories; `DecayScorer` entry under `# Scorers` in SUMMARY.md
- What remains before the milestone is truly usable end-to-end: human review; then M002 DoD checklist passes

## Tasks

- [x] **T01: Write DecayScorer Spec Chapter** `est:45m`
  - Why: DecayScorer is the simplest and most self-contained of the three chapters; establishes the scorer chapter pattern for the other two; D042/D047 locked decisions need to be expressed as normative spec text
  - Files: `spec/src/scorers/decay.md` (new), `spec/src/SUMMARY.md` (add DecayScorer entry under Scorers)
  - Do: Read `spec/src/scorers/metadata-trust.md` for the established chapter template (Overview → Fields table → Algorithm → Configuration → Edge Cases → Conformance Notes). Read `spec/src/scorers/recency.md` for the contrast with RecencyScorer (rank-based vs absolute decay). Write decay.md with: Overview section contrasting with RecencyScorer; Fields Used table; TimeProvider section (D042: mandatory injection; D047: Rust trait declaration `pub trait TimeProvider: Send + Sync { fn now(&self) -> DateTime<Utc>; }` with `SystemTimeProvider` ZST; .NET `System.TimeProvider` direct BCL reference since `net10.0`); DECAY-SCORE pseudocode (age = max(zero, referenceTime - item.timestamp) — clamping for future-dated items; null-timestamp returns `nullTimestampScore`); three curve factory sections (Exponential: halfLife precondition `halfLife > 0`, throw at construction; Step: windows as ordered list of `(maxAge: Duration, score: double)` pairs youngest-to-oldest, scorer walks list returning score for first window with `window.maxAge > age`, final catch-all for age > all boundaries; Window: binary 1.0/0.0 with half-open `[0, maxAge)` interval — age == maxAge returns 0.0); nullTimestampScore section (default 0.5, constructor parameter, defined as "neutral: neither rewards nor penalizes missing timestamps"); Edge Cases table; 5 conformance vector outlines with fixed referenceTime; TimestampCoverage() analytics method note (spec precondition for implementation — not blocking this chapter). Update SUMMARY.md to add `- [DecayScorer](scorers/decay.md)` after MetadataTrustScorer.
  - Verify: `test -f spec/src/scorers/decay.md && grep -ci "\bTBD\b" spec/src/scorers/decay.md` returns 0; `grep -q "DECAY-SCORE" spec/src/scorers/decay.md`; `grep -q "Exponential\|Step\|Window" spec/src/scorers/decay.md`; `grep -q "TimeProvider" spec/src/scorers/decay.md`; `grep -q "decay" spec/src/SUMMARY.md`
  - Done when: `spec/src/scorers/decay.md` exists with all required sections and zero TBD fields; SUMMARY.md links to it; both test suites pass

- [x] **T02: Write OTel Verbosity Spec Chapter** `est:45m`
  - Why: OTel chapter requires the most careful attribute-name specification work; creating the `spec/src/integrations/` directory establishes the Integrations section that SUMMARY.md needs
  - Files: `spec/src/integrations/opentelemetry.md` (new, in new directory), `spec/src/SUMMARY.md` (add `# Integrations` section)
  - Do: Create `spec/src/integrations/` directory. Confirm Sort omission: read `spec/src/diagnostics/events.md` — Sort is already omitted from PipelineStage per that file; OTel spec will cover 5 stage Activities (Classify, Score, Deduplicate, Slice, Place), matching the diagnostic stage set. Write `opentelemetry.md` with: Overview section (companion package `Wollax.Cupel.Diagnostics.OpenTelemetry`; zero-dep guarantee for core — D039/R032); ActivitySource name `"Wollax.Cupel"`; pre-stability disclaimer for `cupel.*` namespace (D043); Activity hierarchy section (root `cupel.pipeline` + 5 child `cupel.stage.{name}` Activities, stage names lowercase: classify/score/deduplicate/slice/place); three verbosity tiers with exact attribute table per tier: `StageOnly` (root: `cupel.budget.max_tokens` int + `cupel.verbosity` string; each stage Activity: `cupel.stage.name` string + `cupel.stage.item_count_in` int + `cupel.stage.item_count_out` int; no `duration_ms`); `StageAndExclusions` adds `cupel.exclusion` Event per excluded item with `cupel.exclusion.reason` string + `cupel.exclusion.item_kind` string + `cupel.exclusion.item_tokens` int; `cupel.exclusion.count` int summary on stage Activity; `Full` adds `cupel.item.included` Event per included item with `cupel.item.kind` string + `cupel.item.tokens` int + `cupel.item.score` float64 (no placement attribute); cardinality table (StageOnly ~10 events/call — Production; StageAndExclusions ~10+0-300 — Staging; Full ~10+0-1000 — Development only); note that `cupel.exclusion.reason` values are open-ended (new ExclusionReason variants arrive without schema change). Update SUMMARY.md to add `# Integrations` section with `- [OpenTelemetry](integrations/opentelemetry.md)`.
  - Verify: `test -f spec/src/integrations/opentelemetry.md && grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md` returns 0; `grep -q "StageOnly\|StageAndExclusions\|Full" spec/src/integrations/opentelemetry.md`; `grep -q "cupel.budget.max_tokens" spec/src/integrations/opentelemetry.md`; `grep -q "Integrations" spec/src/SUMMARY.md`; both test suites pass
  - Done when: `spec/src/integrations/opentelemetry.md` exists with all tier attribute tables and zero TBD fields; `# Integrations` section in SUMMARY.md links to it; both test suites pass

- [x] **T03: Write Budget Simulation Spec Chapter + Full Verification** `est:60m`
  - Why: Budget simulation is the most complex chapter (two API methods, binary search, monotonicity preconditions, budget-override mechanism); it references S03 output (CountQuotaSlice guard language); this task also closes out S06 with the complete verification pass
  - Files: `spec/src/analytics/budget-simulation.md` (new, in new directory), `spec/src/SUMMARY.md` (add `# Analytics` section)
  - Do: Create `spec/src/analytics/` directory. Read `.planning/design/count-quota-design.md` Section 5 for the KnapsackSlice guard language to mirror for CountQuotaSlice in the budget simulation guard. Read `src/Wollax.Cupel/CupelPipeline.cs:86` to confirm current `DryRun` signature (pipeline-fixed budget). Write `budget-simulation.md` with: Overview section (extension methods on `CupelPipeline`; scoped to .NET in v1; Rust parity deferred to M003+ per D067); DryRun determinism invariant as a normative MUST statement ("DryRun MUST produce identical output for identical inputs; tie-breaking order MUST be stable across calls"); `GetMarginalItems` section: signature `GetMarginalItems(IReadOnlyList<ContextItem> items, ContextBudget budget, int slackTokens)` — explicit `budget` param overrides pipeline budget for internal calls; reduced-budget run uses `budget.MaxTokens - slackTokens`; diff direction is `primary \ margin` (items in full-budget result not in reduced-budget result); assumes monotonic inclusion; QuotaSlice guard: `InvalidOperationException` with message "GetMarginalItems requires monotonic item inclusion. QuotaSlice produces non-monotonic inclusion as budget changes shift percentage allocations."; return type `IReadOnlyList<ContextItem>`; GET-MARGINAL-ITEMS pseudocode (two DryRun calls, set-diff); `FindMinBudgetFor` section: signature `FindMinBudgetFor(IReadOnlyList<ContextItem> items, ContextItem targetItem, int searchCeiling)` — no budget param (binary search over token counts); preconditions: `targetItem in items` (ArgumentException), `searchCeiling >= targetItem.Tokens` (ArgumentException); lower bound `targetItem.Tokens`; binary search (~10-15 DryRun invocations), stop when `high - low <= 1`; return `int?` — null means not found within `[targetItem.Tokens, searchCeiling]`; QuotaSlice + CountQuotaSlice guard (InvalidOperationException, same pattern as above but name both); FIND-MIN-BUDGET-FOR pseudocode; `SweepBudget` out-of-scope note (moved to Smelt); language parity note. Update SUMMARY.md to add `# Analytics` section with `- [Budget Simulation](analytics/budget-simulation.md)`. Run full verification suite (all grep checks + cargo test + dotnet test).
  - Verify: `test -f spec/src/analytics/budget-simulation.md && grep -ci "\bTBD\b" spec/src/analytics/budget-simulation.md` returns 0; `grep -q "GetMarginalItems\|FindMinBudgetFor" spec/src/analytics/budget-simulation.md`; `grep -q "QuotaSlice" spec/src/analytics/budget-simulation.md`; `grep -q "deterministic" spec/src/analytics/budget-simulation.md`; `grep -q "Analytics" spec/src/SUMMARY.md`; `cargo test --manifest-path crates/cupel/Cargo.toml` exits 0; `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` exits 0
  - Done when: all three chapters exist with zero TBD fields; SUMMARY.md has all three new entries in correct sections; both test suites pass; all completeness grep checks pass

## Files Likely Touched

- `spec/src/scorers/decay.md` (new)
- `spec/src/integrations/opentelemetry.md` (new, in new directory)
- `spec/src/analytics/budget-simulation.md` (new, in new directory)
- `spec/src/SUMMARY.md` (add DecayScorer entry + `# Integrations` + `# Analytics` sections)
