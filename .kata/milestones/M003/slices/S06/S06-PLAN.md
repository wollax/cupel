# S06: Budget simulation + tiebreaker + spec alignment

**Goal:** `GetMarginalItems` and `FindMinBudgetFor` are callable extension methods on `CupelPipeline` in .NET; the tiebreaker rule is clarified in spec and verified in both languages; spec navigation and changelog are updated for all M003 features; Rust budget simulation parity deferral is documented.
**Demo:** `dotnet test` passes with budget simulation tests (marginal items, find-min-budget, guards); `cargo test` passes with tiebreaker assertion; `mdbook build spec` succeeds with CountQuotaSlice in navigation; changelog has v1.3.0 section.

## Must-Haves

- `GetMarginalItems(items, budget, slackTokens)` extension method on `CupelPipeline` with QuotaSlice guard
- `FindMinBudgetFor(items, targetItem, searchCeiling)` extension method on `CupelPipeline` with QuotaSlice + CountQuotaSlice guard
- Internal `DryRunWithBudget(items, budget)` seam on `CupelPipeline` (internal, not public) to support alternate-budget DryRun calls
- Tiebreaker rule clarified in spec as "index ascending" (not "id ascending") matching existing implementations
- `spec/src/slicers/count-quota.md` page exists and is linked from `SUMMARY.md` and `slicers.md`
- `spec/src/changelog.md` has a v1.3.0 section covering all M003 features
- `PublicAPI.Unshipped.txt` updated for all new public members
- Rust budget simulation parity deferral documented in spec

## Proof Level

- This slice proves: contract + integration (budget simulation tested via DryRun with real pipeline; spec alignment verified via mdbook build + grep)
- Real runtime required: no (unit tests exercise real pipeline internals)
- Human/UAT required: no

## Verification

- `rtk dotnet test` → all tests pass including new `BudgetSimulationTests.cs`
- `rtk cargo test --all-targets` → all tests pass (tiebreaker assertion in existing greedy tests)
- `grep -q "count-quota" spec/src/SUMMARY.md` → match
- `grep -q "CountQuotaSlice" spec/src/slicers.md` → match
- `grep -q "1.3.0" spec/src/changelog.md` → match
- `grep -q "GetMarginalItems\|FindMinBudgetFor" src/Wollax.Cupel/PublicAPI.Unshipped.txt` → match
- `dotnet build src/Wollax.Cupel/ 2>&1 | grep RS0016 | wc -l` → 0

## Observability / Diagnostics

- Runtime signals: `InvalidOperationException` with structured messages for QuotaSlice/CountQuotaSlice guard violations; `ArgumentException` for FindMinBudgetFor precondition failures
- Inspection surfaces: `SelectionReport.Included` reference equality for marginal diff; DryRun determinism as the foundation
- Failure visibility: exception messages name the slicer type and the reason for rejection; FindMinBudgetFor returns null (not exception) when target is not selectable
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `CupelPipeline.ExecuteCore` (internal, budget-override seam added); `ContextBudget` constructor; `ISlicer` interface; `QuotaSlice`/`CountQuotaSlice` type identity; `SelectionReport.Included` for reference equality diff; analytics extension methods from S04
- New wiring introduced in this slice: `DryRunWithBudget` internal method on `CupelPipeline`; `BudgetSimulationExtensions` static class with `GetMarginalItems` and `FindMinBudgetFor`
- What remains before the milestone is truly usable end-to-end: nothing — S06 is the final slice; all 6 slices complete M003

## Tasks

- [ ] **T01: Add budget-override DryRun seam + GetMarginalItems + FindMinBudgetFor** `est:45m`
  - Why: Core implementation — adds the internal budget-override seam to CupelPipeline and both budget simulation extension methods with all guards per spec
  - Files: `src/Wollax.Cupel/CupelPipeline.cs`, `src/Wollax.Cupel/BudgetSimulationExtensions.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`, `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs`
  - Do: Add `internal ContextResult DryRunWithBudget(IReadOnlyList<ContextItem> items, ContextBudget budget)` to CupelPipeline that creates a DiagnosticTraceCollector and runs ExecuteCore with the provided budget (override `_budget` for the call). Create `BudgetSimulationExtensions` static class with `GetMarginalItems` and `FindMinBudgetFor` following spec pseudocode exactly. Implement QuotaSlice guard on GetMarginalItems; QuotaSlice + CountQuotaSlice guard on FindMinBudgetFor. Use `ReferenceEquals` for item identity in marginal diff. Write BudgetSimulationTests with: marginal items happy path, marginal items empty when slackTokens=0, QuotaSlice guard throws, FindMinBudgetFor binary search finds correct budget, FindMinBudgetFor returns null when unreachable, FindMinBudgetFor precondition guards, CountQuotaSlice guard throws.
  - Verify: `rtk dotnet test --filter BudgetSimulation` → all pass; `dotnet build src/Wollax.Cupel/ 2>&1 | grep RS0016 | wc -l` → 0
  - Done when: Both extension methods callable, all guards throw correct messages, ≥6 tests pass

- [ ] **T02: Tiebreaker spec clarification + Rust assertion + spec alignment (SUMMARY, slicers, count-quota page, changelog)** `est:40m`
  - Why: Closes the spec gaps — tiebreaker rule formalized as stable-index (not id); CountQuotaSlice added to spec navigation; changelog updated for all M003 features; Rust budget simulation deferral documented
  - Files: `spec/src/slicers/greedy.md`, `spec/src/slicers/count-quota.md`, `spec/src/SUMMARY.md`, `spec/src/slicers.md`, `spec/src/changelog.md`, `spec/src/analytics/budget-simulation.md`, `crates/cupel/tests/greedy_tiebreaker.rs`
  - Do: (1) In `spec/src/slicers/greedy.md`, add a "Tiebreaker Rule" section clarifying that ties use original index ascending (not item id). (2) Create `spec/src/slicers/count-quota.md` with overview, algorithm summary, ScarcityBehavior, KnapsackSlice guard, and cross-references to existing count_quota implementation. (3) Add CountQuotaSlice to `spec/src/SUMMARY.md` under Slicers. (4) Add CountQuotaSlice row to `spec/src/slicers.md` summary table. (5) Add v1.3.0 section to `spec/src/changelog.md` covering DecayScorer, MetadataTrustScorer, CountQuotaSlice, analytics extensions, Cupel.Testing, OTel bridge, budget simulation, tiebreaker clarification. (6) Add Rust parity deferral note to `spec/src/analytics/budget-simulation.md` if not already present. (7) Add a Rust integration test `greedy_tiebreaker.rs` that constructs two items with identical density and verifies the lower-index item is selected first.
  - Verify: `grep -q "count-quota" spec/src/SUMMARY.md`; `grep -q "CountQuotaSlice" spec/src/slicers.md`; `grep -q "1.3.0" spec/src/changelog.md`; `rtk cargo test --all-targets` passes including tiebreaker test
  - Done when: All spec files updated, CountQuotaSlice page exists, changelog has v1.3.0, Rust tiebreaker test passes

- [ ] **T03: Full verification + decision register + summary** `est:20m`
  - Why: Final integration gate — runs full test suites in both languages, appends planning decisions to DECISIONS.md, writes S06-SUMMARY.md
  - Files: `.kata/DECISIONS.md`, `.kata/milestones/M003/slices/S06/S06-SUMMARY.md`
  - Do: Run `rtk dotnet test` (full solution) and `rtk cargo test --all-targets` to confirm no regressions. Verify all spec files are correct via grep checks. Write S06-SUMMARY.md following the established summary template. Append S06 decisions to DECISIONS.md.
  - Verify: `rtk dotnet test` → all pass; `rtk cargo test --all-targets` → all pass; all grep checks from slice verification pass
  - Done when: Both test suites green, S06-SUMMARY.md written, DECISIONS.md updated, all verification checks pass

## Files Likely Touched

- `src/Wollax.Cupel/CupelPipeline.cs`
- `src/Wollax.Cupel/BudgetSimulationExtensions.cs`
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
- `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs`
- `spec/src/slicers/greedy.md`
- `spec/src/slicers/count-quota.md`
- `spec/src/SUMMARY.md`
- `spec/src/slicers.md`
- `spec/src/changelog.md`
- `spec/src/analytics/budget-simulation.md`
- `crates/cupel/tests/greedy_tiebreaker.rs`
- `.kata/DECISIONS.md`
- `.kata/milestones/M003/slices/S06/S06-SUMMARY.md`
