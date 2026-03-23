---
id: S06
parent: M002
milestone: M002
provides:
  - spec/src/scorers/decay.md — fully-specified DecayScorer spec chapter
  - spec/src/integrations/opentelemetry.md — fully-specified OTel verbosity chapter
  - spec/src/analytics/budget-simulation.md — fully-specified budget simulation chapter
  - spec/src/SUMMARY.md — DecayScorer entry under Scorers; new # Integrations and # Analytics sections
requires:
  - slice: S01
    provides: fresh angles on DecayScorer curves, OTel verbosity tiers, and budget simulation patterns (future-features-report.md)
  - slice: S03
    provides: count-quota design record (CountQuotaSlice guard language for FindMinBudgetFor)
affects:
  - none (M002 final slice; downstream is M003 implementation work)
key_files:
  - spec/src/scorers/decay.md
  - spec/src/integrations/opentelemetry.md
  - spec/src/analytics/budget-simulation.md
  - spec/src/SUMMARY.md
key_decisions:
  - D067 — S06 verification strategy: contract-level only (grep + test suite regression)
  - D068 — OTel stage count: 5 Activities, Sort omitted (matches PipelineStage enum precedent)
  - D069 — GetMarginalItems budget parameter: explicit ContextBudget budget + int slackTokens
  - D070 — Step curve windows: ordered list, strict >, throw-at-construction for empty/zero-width
  - D071 — Window curve boundary: half-open [0, maxAge), age == maxAge returns 0.0
patterns_established:
  - none
observability_surfaces:
  - none (spec authoring only; no code changes per D039)
drill_down_paths:
  - .kata/milestones/M002/slices/S06/tasks/T01-SUMMARY.md
  - .kata/milestones/M002/slices/S06/tasks/T02-SUMMARY.md
  - .kata/milestones/M002/slices/S06/tasks/T03-SUMMARY.md
duration: short
verification_result: passed
completed_at: 2026-03-21
---

# S06: Future Features Spec Chapters

**Three new spec chapters — DecayScorer, OTel verbosity, and budget simulation — all fully specified with zero TBD fields, linked from SUMMARY.md under new Integrations and Analytics sections; both test suites pass with no regressions.**

## What Happened

S06 produced three specification chapters across three tasks, each authored against the established chapter template from `spec/src/scorers/metadata-trust.md`.

**T01 — DecayScorer spec chapter** (`spec/src/scorers/decay.md`): Written following the Overview → Fields → TimeProvider → Algorithm → Curve Factories → Configuration → Edge Cases → Conformance Vectors → Complexity → Conformance Notes structure. Key content: contrast framing against RecencyScorer (rank-based vs absolute-decay); mandatory TimeProvider injection per D042 with both .NET `System.TimeProvider` BCL reference and Rust trait declaration per D047; DECAY-SCORE pseudocode with negative-age clamping and null-timestamp short-circuit; three curve factories (Exponential with halfLife > Duration::ZERO precondition, Step with strict `>` comparison and throw-at-construction for zero-width/empty windows per D070, Window with half-open `[0, maxAge)` interval per D071); nullTimestampScore defaulting to 0.5 ("neutral" semantics); 5 conformance vector outlines with fixed referenceTime. SUMMARY.md updated with DecayScorer entry after MetadataTrustScorer.

**T02 — OTel verbosity spec chapter** (`spec/src/integrations/opentelemetry.md`): Created new `spec/src/integrations/` directory. Confirmed Sort omission from `spec/src/diagnostics/events.md` (line 63) before committing to 5-Activity hierarchy per D068. Chapter covers: zero-dependency core guarantee (D039/R032); ActivitySource name `"Wollax.Cupel"`; pre-stability disclaimer for `cupel.*` namespace per D043; root `cupel.pipeline` Activity + 5 stage child Activities (classify, score, deduplicate, slice, place); three verbosity tiers with complete per-tier attribute tables (StageOnly, StageAndExclusions, Full); cardinality table with environment-tier recommendations and Full-in-production warning; flat reference table for all 12 `cupel.*` attributes; note on open-ended ExclusionReason values. SUMMARY.md updated with new `# Integrations` section.

**T03 — Budget simulation spec chapter** (`spec/src/analytics/budget-simulation.md`): Created new `spec/src/analytics/` directory. Read `CupelPipeline.cs:86` to confirm DryRun uses pipeline-stored `_budget`, validating the need for an explicit `ContextBudget budget` parameter in extension methods (D069). Read S03 count-quota design record for KnapsackSlice guard language to model the QuotaSlice/CountQuotaSlice guard message. Chapter covers: DryRun determinism invariant as normative MUST text; GetMarginalItems with explicit budget parameter, reduced-budget formula, `primary \ margin` diff direction, QuotaSlice guard, and GET-MARGINAL-ITEMS pseudocode; FindMinBudgetFor with binary search (~10-15 DryRun calls), `int?`/`Option<i32>` return per D048, both QuotaSlice and CountQuotaSlice guards, FIND-MIN-BUDGET-FOR pseudocode; SweepBudget out-of-scope note (moved to Smelt); Rust-parity-deferred note (M003+ per D067). SUMMARY.md updated with new `# Analytics` section. Full S06 verification suite run as part of T03.

## Verification

All slice-level verification checks passed:

**File existence:**
- `spec/src/scorers/decay.md` — present
- `spec/src/integrations/opentelemetry.md` — present
- `spec/src/analytics/budget-simulation.md` — present

**TBD counts (all 0):**
- `grep -ci "\bTBD\b" spec/src/scorers/decay.md` → 0
- `grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md` → 0
- `grep -ci "\bTBD\b" spec/src/analytics/budget-simulation.md` → 0

**DecayScorer completeness:** DECAY-SCORE, Exponential, Step, Window, TimeProvider, nullTimestampScore, Conformance — all present

**OTel completeness:** StageOnly, StageAndExclusions, Full, cupel.budget.max_tokens, cupel.exclusion.reason, Wollax.Cupel, pre-stable — all present

**Budget simulation completeness:** GetMarginalItems, FindMinBudgetFor, QuotaSlice, deterministic, monoton, Rust — all present

**SUMMARY.md links:** decay, opentelemetry, budget-simulation, Integrations, Analytics — all present

**Test suites:**
- `cargo test --manifest-path crates/cupel/Cargo.toml` → 113 passed, 1 ignored, 0 failed
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → 583 passed, 0 failed

## Requirements Advanced

- R044 — Three spec chapters produced; all three features (DecayScorer, OTel verbosity, budget simulation) now have complete implementation-ready specs with zero TBD fields

## Requirements Validated

- R044 — All three spec chapters exist with required sections, zero TBD fields, reachable from SUMMARY.md, test suites pass; R044 is now validated

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

None. All three tasks followed their written plans exactly.

## Known Limitations

- Budget simulation extension methods are scoped to .NET in v1; Rust parity explicitly deferred to M003+ per D067
- OTel `cupel.*` attribute namespace is pre-stable; names may change when OTel LLM SIG semantic conventions stabilize (D043)
- DecayScorer TimestampCoverage() analytics method noted in spec as a future precondition for implementation — not a spec gap, just a tracked dependency

## Follow-ups

- M003: Implement DecayScorer (R020) against this spec chapter
- M003: Implement Cupel.Testing vocabulary (R021) against S05 chapter
- M003: Implement OTel bridge companion package (R022) against this spec chapter
- M003: Implement GetMarginalItems and FindMinBudgetFor extension methods against budget simulation chapter
- Future: TimestampCoverage() analytics method (noted in decay.md as spec precondition)
- Future: Align `cupel.*` OTel attributes with `gen_ai.*` when OTel LLM SIG stabilizes (D043 revisit trigger)

## Files Created/Modified

- `spec/src/scorers/decay.md` — new file; fully-specified DecayScorer spec chapter
- `spec/src/integrations/opentelemetry.md` — new file; fully-specified OTel verbosity chapter (new `spec/src/integrations/` directory created)
- `spec/src/analytics/budget-simulation.md` — new file; fully-specified budget simulation chapter (new `spec/src/analytics/` directory created)
- `spec/src/SUMMARY.md` — DecayScorer entry under Scorers; `# Integrations` section with OTel entry; `# Analytics` section with budget simulation entry

## Forward Intelligence

### What the next slice should know
- The three spec chapters are the authoritative contracts for M003 implementation; do not deviate from the pseudocode without a new design decision record
- `FindMinBudgetFor` stop condition is `high - low <= 1` (not `high == low`) — important for binary search termination correctness
- The DryRun determinism MUST requirement is normative; any implementation that relies on hash-map iteration order for tie-breaking is non-conformant
- GetMarginalItems diff direction is `primary \ margin` (full-budget result minus reduced-budget result), not the other way around

### What's fragile
- `cupel.*` OTel attribute names are explicitly pre-stable — any M003 OTel implementation must surface this to callers via docs/README, not just spec
- Step curve's strict `>` comparison means `age == window.maxAge` falls through to the next window; implementors who use `>=` will produce wrong scores at boundary values — conformance vectors must cover this

### Authoritative diagnostics
- `grep -ci "\bTBD\b" spec/src/scorers/decay.md` / `opentelemetry.md` / `budget-simulation.md` — primary completeness signal; any value > 0 means an unfinished section was introduced
- `grep -q "decay\|opentelemetry\|budget-simulation" spec/src/SUMMARY.md` — reachability signal; missing entry means mdBook won't serve the chapter

### What assumptions changed
- No assumption changes; all three chapters were authored against locked decisions (D042/D043/D044/D047/D048/D067-D071) with no need for new debates
