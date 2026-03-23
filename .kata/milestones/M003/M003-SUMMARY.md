---
id: M003
provides:
  - DecayScorer (Exponential, Window, Step curves) in Rust + .NET with TimeProvider injection and 5 conformance vectors
  - MetadataTrustScorer using cupel:trust convention in Rust + .NET with 5 conformance vectors
  - CountQuotaSlice decorator slicer in Rust + .NET with ScarcityBehavior, ExclusionReason variants, and shortfall reporting
  - BudgetUtilization, KindDiversity, TimestampCoverage analytics extensions in both languages
  - Wollax.Cupel.Testing NuGet package with 13 fluent assertion patterns via SelectionReport.Should()
  - GetMarginalItems and FindMinBudgetFor budget-simulation APIs on CupelPipeline (.NET)
  - Deterministic tie-break contract (original-index ascending) locked in GreedySlice across .NET, Rust, and spec
  - CountQuotaSlice spec page, updated SUMMARY.md/slicers.md/scorers.md nav, v1.3.0 changelog
  - ITraceCollector OnPipelineCompleted hook + StageTraceSnapshot model (S05 partial — on branch only)
key_decisions:
  - "D072-D077: DecayScorer — millisecond precision, age clamping, abstract base protected ctor pattern"
  - "D078-D083: MetadataTrustScorer — NaN-safe scoring, dual-type dispatch, three-location vector sync"
  - "D084-D087: CountQuotaSlice — is_knapsack() trait method, LastShortfalls sidecar, cap-reason deferral"
  - "D088-D096: S04 analytics + Cupel.Testing — free functions in analytics.rs, internal chain ctor, local feed workflow"
  - "D097-D099: Budget simulation — explicit budget param, DryRunWithBudget seam, original-index tiebreak"
  - "D100-D102: OTel bridge — structured completion handoff, StageTraceSnapshot, package-specific verbosity enum"
patterns_established:
  - "Injected TimeProvider trait for deterministic time-dependent scorers (Rust trait, BCL System.TimeProvider in .NET)"
  - "Three-location conformance vector sync: spec/ → conformance/ (root) → crates/cupel/conformance/"
  - "Budget-override seam: DryRunWithBudget internal method reuses ExecuteCore with temporary budget"
  - "Fluent assertion chain: internal ctor + public methods returning this; Should() entry point; dedicated exception"
  - "New NuGet package template: csproj with IsPackable=true, PublicApiAnalyzers, local feed workflow"
  - "Two-phase slicer decorator: Phase 1 commits required items, Phase 2 delegates residual, Phase 3 cap-filters"
observability_surfaces:
  - "Conformance drift guard: diff -r spec/conformance/ crates/cupel/conformance/ must exit 0"
  - "Stable exception messages for DecayScorer/MetadataTrustScorer construction, QuotaSlice/CountQuotaSlice guards"
  - "SelectionReportAssertionException.Message with structured '{AssertionName}({params}) failed: {expected}. {actual}' format"
  - "BudgetSimulationTests.cs / GreedySliceTests.cs as authoritative contract surfaces"
  - "cargo test -- decay_|metadata_trust|count_quota|greedy|analytics for targeted Rust test runs"
requirement_outcomes:
  - id: R020
    from_status: deferred
    to_status: validated
    proof: "DecayScorer in both languages; 5 conformance vectors pass; drift guard clean; cargo test 128 passed; dotnet test 723 passed"
  - id: R021
    from_status: deferred
    to_status: validated
    proof: "Wollax.Cupel.Testing package with 13 patterns; 26 TUnit tests pass; consumption test via local feed; dotnet pack produces nupkg"
  - id: R022
    from_status: active
    to_status: active
    proof: "S05 INCOMPLETE: only T01-T02 merged (core seam); T03-T04 (actual OTel package) never implemented on main; branch kata/M003/S05 has partial work but was not squash-merged"
  - id: R040
    from_status: validated
    to_status: validated
    proof: "CountQuotaSlice implemented in both languages; 35+ tests; 5 conformance vectors; R040 was already validated by M002 design — M003 added implementation proof"
  - id: R042
    from_status: validated
    to_status: validated
    proof: "MetadataTrustScorer implemented in both languages; 5 conformance vectors with passing implementations; R042 was already validated by M002 spec — M003 added implementation proof"
duration: ~6h across 6 slices (S01-S04 + S06 complete; S05 incomplete)
verification_result: partial — S05 (OTel bridge) not complete
completed_at: 2026-03-23
---

# M003: v1.3 Implementation Sprint

**Shipped DecayScorer, MetadataTrustScorer, CountQuotaSlice, core analytics, Cupel.Testing package, budget simulation API, and deterministic tie-break contract across .NET and Rust — but the OTel bridge companion package (S05/R022) was not completed**

## What Happened

M003 implemented the features designed in M002 across six slices. Five of six completed successfully; one (S05) is incomplete.

**S01 (DecayScorer)** retired the highest-risk item first: `chrono` dependency confirmed, `TimeProvider` trait established in Rust with `SystemTimeProvider` ZST, and `DecayCurve` enum with three validated variants (Exponential, Window, Step). .NET used `System.TimeProvider` (BCL, no NuGet). Five conformance vectors pass in both languages. Key discovery: `.Duration()` in .NET returns absolute value — explicit zero-clamp required for future-dated items.

**S02 (MetadataTrustScorer)** built on S01's scorer pattern. Critical insight: `"NaN".parse::<f64>()` returns `Ok(NaN)`, so `is_finite()` must follow `parse()` (D081). .NET implements D059 dual-type dispatch (double before string). Discovered the three-location conformance vector convention (D082).

**S03 (CountQuotaSlice)** was the largest slice (~115min). Introduced `is_knapsack()` default method on the `Slicer` trait for the KnapsackSlice guard, avoiding `Any`/downcast. Extended `ExclusionReason` with two new variants and `SelectionReport` with `CountRequirementShortfalls`. The `deny_unknown_fields` removal from `RawSelectionReport` was a deliberate backward-compat change. Known limitation: shortfalls don't propagate through the standard pipeline path yet.

**S04 (Core analytics + Cupel.Testing)** delivered analytics extension methods in both languages and the `Wollax.Cupel.Testing` NuGet package with all 13 assertion patterns. The local NuGet feed workflow (`./nupkg` → `./packages`) was established as a reusable pattern. Pattern 6 uses a degenerate .NET form due to flat `ExclusionReason` enum.

**S05 (OTel bridge) — INCOMPLETE.** Only T01-T02 were executed on the `kata/M003/S05` branch, adding the structured `ITraceCollector.OnPipelineCompleted` hook and `StageTraceSnapshot` model. T03-T04 (the actual `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package, SDK-backed tests, and CI/release wiring) were never implemented. The branch was never squash-merged to main. S06 was built and merged on top of main without S05's core seam changes.

**S06 (Budget simulation + tiebreaker + spec alignment)** shipped `GetMarginalItems` and `FindMinBudgetFor` as extension methods on `CupelPipeline` with an internal `DryRunWithBudget` seam. Locked the GreedySlice tie-break contract to "original-index ascending" across .NET, Rust, and spec. Completed all M003 spec navigation/changelog alignment including the CountQuotaSlice spec page.

## Cross-Slice Verification

### Success Criteria Status

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | `cargo test` passes with new scorer/slicer/analytics tests | ✅ PASS | 128/128 passed (crates/cupel) |
| 2 | `dotnet test` passes with Cupel.Testing, budget simulation tests | ✅ PASS | 723/723 passed |
| 3 | `Wollax.Cupel.Testing` installs independently | ✅ PASS | Consumption test via local feed; 26 TUnit tests green |
| 4 | `Wollax.Cupel.Diagnostics.OpenTelemetry` produces real OTel output | ❌ FAIL | Package has zero source files on main; S05 branch incomplete (T01-T02 of 4) |
| 5 | DecayScorer + MetadataTrustScorer conformance vectors pass | ✅ PASS | diff -r exits 0; 10 vectors across both scorers |
| 6 | Tiebreaker rule spec-committed and implemented | ✅ PASS | greedy.md "Deterministic Tie-Break Contract"; .NET + Rust regression tests |
| 7 | BudgetUtilization, KindDiversity, TimestampCoverage callable | ✅ PASS | Rust: pub use in lib.rs; .NET: extension methods in SelectionReportExtensions.cs |
| 8 | GetMarginalItems + FindMinBudgetFor ship in .NET | ✅ PASS | BudgetSimulationExtensions.cs; 11 tests green; PublicAPI.Unshipped.txt entries |

### Definition of Done

- 5 of 6 slices complete with summaries: S01 ✅, S02 ✅, S03 ✅, S04 ✅, S05 ❌ (no summary, branch not merged), S06 ✅
- `cargo test` and `dotnet test` both pass: ✅
- DecayScorer + MetadataTrustScorer conformance vectors: ✅
- Cupel.Testing builds and installs: ✅
- **OTel companion package: ❌ NOT MET — S05 incomplete**
- Tiebreaker rule in spec and GreedySlice: ✅
- Analytics extension methods in both languages: ✅
- Budget simulation in .NET; Rust parity documented: ✅

**Milestone verification: PARTIAL — 7 of 8 success criteria met. S05/R022 (OTel bridge) is the outstanding gap.**

## Requirement Changes

- R020 (DecayScorer): deferred → **validated** — implemented in both languages with conformance vectors passing; mandatory TimeProvider injection; drift guard clean
- R021 (Cupel.Testing): deferred → **validated** — 13 assertion patterns, Should() entry point, dedicated exception, NuGet package builds and installs, 26 tests pass
- R022 (OTel bridge): active → **active (still incomplete)** — S05 branch has core seam (T01-T02) but actual companion package never built; the branch was not merged to main; R022 remains active and unvalidated
- R040 (Count-based quota): validated → validated (implementation added; status unchanged — M002 validated the design)
- R042 (Metadata convention): validated → validated (implementation added; status unchanged — M002 validated the spec)

## Forward Intelligence

### What the next milestone should know
- S05 must be completed before v1.3 can ship. The `kata/M003/S05` branch has diverged from main (S06 was squash-merged without S05's changes). The branch contains T01-T02 work (core seam: `OnPipelineCompleted` hook on `ITraceCollector`, `StageTraceSnapshot` model, `CupelPipeline.ExecuteCore` stage timing). T03-T04 (companion package implementation, SDK tests, CI/release wiring) remain.
- The branch conflict is the primary risk: S05 modifies `CupelPipeline.cs` and `ITraceCollector.cs` which also changed in S06. A rebase or fresh branch from main will be needed.
- All non-OTel features are complete and ready for v1.3. The decision to ship v1.3 without OTel (and add OTel in v1.3.1) vs waiting for OTel is a user decision.

### What's fragile
- S05 branch divergence from main — `CupelPipeline.cs` was modified by both S05 (adding `OnPipelineCompleted` and stage timing) and S06 (adding `DryRunWithBudget` override seam). Merge will require careful conflict resolution.
- `PublicAPI.Unshipped.txt` will need reconciliation across branches — S05 added new API entries that aren't on main.
- `FindMinBudgetFor` binary search has a non-obvious low-bound verification step — if the spec pseudocode is ever implemented literally without it, single-item edge cases will be off-by-one.

### Authoritative diagnostics
- `cargo test --all-targets` (in crates/cupel) — canonical Rust signal; 128 tests
- `dotnet test` — canonical .NET signal; 723 tests
- `diff -r spec/conformance/required crates/cupel/conformance/required` — conformance drift guard
- `BudgetSimulationTests.cs` — budget simulation contract authority
- `GreedySliceTests.cs` — tie-break regression authority

### What assumptions changed
- S05 was marked `[x]` in the roadmap but was never completed or merged. The auto-mode appears to have proceeded to S06 after only partial S05 work (T01-T02 on branch), then squash-merged S06 independently. This created a state where the roadmap claimed S05 was done but the actual code was not on main.
- The "id ascending" tiebreak in the roadmap resolved to "original-index ascending" — no ContextItem.Id field needed.
- `.Duration()` in .NET returns absolute value, not clamped-to-zero — plan text for multiple slices was incorrect on this point.

## Files Created/Modified

Key files across the milestone (see individual slice summaries for complete lists):

### Rust
- `crates/cupel/src/scorer/decay.rs` — DecayScorer with TimeProvider trait
- `crates/cupel/src/scorer/metadata_trust.rs` — MetadataTrustScorer with NaN-safe scoring
- `crates/cupel/src/slicer/count_quota.rs` — CountQuotaSlice decorator (~680 lines)
- `crates/cupel/src/analytics.rs` — budget_utilization, kind_diversity, timestamp_coverage
- `crates/cupel/src/slicer/greedy.rs` — Tie-break regression tests + doc comments

### .NET
- `src/Wollax.Cupel/Scoring/DecayScorer.cs` + `DecayCurve.cs` — DecayScorer with System.TimeProvider
- `src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs` — D059 dual-type dispatch
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` + `CountQuotaEntry.cs` + `ScarcityBehavior.cs`
- `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` — Analytics extension methods
- `src/Wollax.Cupel/BudgetSimulationExtensions.cs` — GetMarginalItems + FindMinBudgetFor
- `src/Wollax.Cupel/CupelPipeline.cs` — DryRunWithBudget internal seam
- `src/Wollax.Cupel.Testing/` — Complete NuGet package with 13 assertion patterns

### Spec
- `spec/conformance/required/scoring/decay-*.toml` — 5 DecayScorer vectors
- `spec/conformance/required/scoring/metadata-trust-*.toml` — 5 MetadataTrustScorer vectors
- `spec/conformance/required/slicing/count-quota-*.toml` — 5 CountQuotaSlice vectors
- `spec/src/slicers/count-quota.md` — New CountQuotaSlice spec page
- `spec/src/slicers/greedy.md` — Deterministic Tie-Break Contract section
- `spec/src/changelog.md` — v1.3.0 entry

### Incomplete (on kata/M003/S05 branch only)
- `src/Wollax.Cupel/Diagnostics/StageTraceSnapshot.cs` — Stage snapshot model (not on main)
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — OnPipelineCompleted hook (not on main)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/` — Empty on main; package never built
