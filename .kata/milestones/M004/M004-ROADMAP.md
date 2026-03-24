# M004: v1.4 Diagnostics & Simulation Parity

**Vision:** Complete the brainstormed feature cluster — structural equality on SelectionReport, a fork diagnostic for comparing pipeline configurations, IQuotaPolicy abstraction with QuotaUtilization analytics, snapshot testing in Cupel.Testing, and Rust budget simulation parity — shipping a cohesive set of developer-productivity features in both languages.

## Success Criteria

- `SelectionReport`, `IncludedItem`, and `ExcludedItem` support `==` comparison in both languages with exact f64 equality
- `PolicySensitivity(items, [(label, pipeline)])` returns labeled `SelectionReport`s plus a structured diff in both languages; a test harness proves at least two pipeline configurations produce a meaningful diff
- `IQuotaPolicy` is implemented by both `QuotaSlice` and `CountQuotaSlice` without breaking changes; `QuotaUtilization(report, policy)` returns per-kind utilization in both languages
- `report.Should().MatchSnapshot("name")` creates/reads/updates JSON snapshot files in .NET; `CUPEL_UPDATE_SNAPSHOTS=1` rewrites snapshots; a test proves the create→match→fail→update cycle
- `get_marginal_items` and `find_min_budget_for` in Rust pass unit tests matching .NET behavior; monotonicity guard rejects QuotaSlice/CountQuotaSlice inner slicers
- `cargo test --all-targets` and `dotnet test --configuration Release` both pass with all new tests included

## Key Risks / Unknowns

- `IQuotaPolicy` extraction may require careful API design to avoid breaking the sealed surface of existing slicers
- Snapshot file I/O adds filesystem dependency to `Wollax.Cupel.Testing` — acceptable for a testing package but needs clean caller-attribute-based path resolution
- Rust budget simulation requires a temporary-budget execution seam in Pipeline — .NET uses an internal method; Rust may need a different approach due to ownership constraints

## Proof Strategy

- `IQuotaPolicy` breaking-change risk → retire in S03 by implementing the interface on both slicers and passing PublicAPI analyzers + existing tests
- Snapshot file I/O complexity → retire in S04 by proving the full create→match→fail→update cycle in a test
- Rust budget simulation ownership constraints → retire in S05 by implementing and testing both methods with `cargo test`

## Verification Classes

- Contract verification: `cargo test --all-targets`, `dotnet test --configuration Release`, PublicAPI analyzer, `grep`-based artifact checks
- Integration verification: fork diagnostic exercised with real multi-pipeline comparison; snapshot test exercises real file I/O cycle; QuotaUtilization exercised against real QuotaSlice + CountQuotaSlice configurations
- Operational verification: none (library — no service lifecycle)
- UAT / human verification: none required; all must-haves are mechanically checkable

## Milestone Definition of Done

This milestone is complete only when all are true:

- All 5 slices are complete with summaries written and verified
- `cargo test --all-targets` and `dotnet test --configuration Release` both pass with all new feature tests included
- `SelectionReport` equality works in both languages (exact f64)
- Fork diagnostic returns labeled reports + structured diff for ≥2 pipeline configurations
- `IQuotaPolicy` implemented by `QuotaSlice` and `CountQuotaSlice` without breaking changes
- `QuotaUtilization` returns correct per-kind data in both languages
- Snapshot testing proves the full create→match→fail→update JSON cycle
- Rust `get_marginal_items` and `find_min_budget_for` match .NET behavior

## Requirement Coverage

- Covers: R050, R051, R052, R053, R054
- Partially covers: none
- Leaves for later: R055 (ProfiledPlacer), R056 (DryRunWithPolicy), R057 (TimestampCoverageReport split)
- Orphan risks: none

## Slices

- [x] **S01: SelectionReport structural equality** `risk:medium` `depends:[]`
  > After this: `SelectionReport`, `IncludedItem`, and `ExcludedItem` support `==` in both languages with exact f64 equality; all existing tests pass; downstream slices can compare reports programmatically.

- [x] **S02: PolicySensitivityReport — fork diagnostic** `risk:high` `depends:[S01]`
  > After this: `RunPolicySensitivity(items, [(label, pipeline)])` returns labeled `SelectionReport`s plus a structured diff showing items that changed status; proved by a test exercising ≥2 pipeline configurations in both languages.

- [x] **S03: IQuotaPolicy abstraction + QuotaUtilization** `risk:high` `depends:[S01]`
  > After this: `QuotaSlice` and `CountQuotaSlice` both implement `IQuotaPolicy`; `QuotaUtilization(report, policy)` returns per-kind utilization data in both languages; PublicAPI analyzers clean; no breaking changes.

- [ ] **S04: Snapshot testing in Cupel.Testing** `risk:medium` `depends:[S01]`
  > After this: `report.Should().MatchSnapshot("name")` creates, reads, and updates JSON snapshot files in .NET; `CUPEL_UPDATE_SNAPSHOTS=1` rewrites snapshots; a test proves the full create→match→fail→update cycle.

- [ ] **S05: Rust budget simulation parity** `risk:medium` `depends:[]`
  > After this: `get_marginal_items` and `find_min_budget_for` are callable on Rust `Pipeline`; `find_min_budget_for` returns `Option<i32>`; monotonicity guard rejects QuotaSlice/CountQuotaSlice inner slicers; unit tests match .NET behavior.

## Boundary Map

### S01 — SelectionReport structural equality

Produces:
- Rust: `PartialEq` (not `Eq` — f64 fields prevent it, D109) on `SelectionReport`, `IncludedItem`, `ExcludedItem`, `TraceEvent`, `CountRequirementShortfall`, `OverflowEvent`, plus `ContextBudget` (transitive dependency, D110)
- .NET: `IEquatable<ContextItem>` (collection-aware), `IEquatable<IncludedItem>`, `IEquatable<ExcludedItem>`, `IEquatable<SelectionReport>` with SequenceEqual for all list properties
- Both: PublicAPI surface updates

Consumes:
- nothing (first slice; builds on existing diagnostic types)

### S02 — PolicySensitivityReport

Produces:
- Rust: `policy_sensitivity(items, &[(label, &Pipeline)])` function in a new `analytics` or `diagnostics` submodule; `PolicySensitivityReport` struct with `variants: Vec<(String, SelectionReport)>` and `diff: PolicySensitivityDiff`; `PolicySensitivityDiff` with per-item status changes across variants
- .NET: `PolicySensitivity(IReadOnlyList<ContextItem>, (string, CupelPipeline)[])` extension method; `PolicySensitivityReport` class with `Variants` and `Diff` properties; `PolicySensitivityDiff` with item change tracking

Consumes from S01:
- `SelectionReport` equality for computing diffs between variant reports

### S03 — IQuotaPolicy + QuotaUtilization

Produces:
- Rust: `QuotaPolicy` trait with per-kind constraint query methods; implemented by `QuotaSlice` and `CountQuotaSlice`; `quota_utilization(report, &dyn QuotaPolicy)` function returning `Vec<KindQuotaUtilization>`
- .NET: `IQuotaPolicy` interface; implemented by `QuotaSlice` and `CountQuotaSlice`; `QuotaUtilization(SelectionReport, IQuotaPolicy)` extension method returning `IReadOnlyList<KindQuotaUtilization>`
- Both: `KindQuotaUtilization` data type with kind, count, cap, require, utilization percentage

Consumes from S01:
- `SelectionReport` equality (not strictly required but establishes the pattern for report-based analytics)

### S04 — Snapshot testing

Produces:
- .NET: `MatchSnapshot(string name)` method on `SelectionReportAssertionChain`; `SnapshotMismatchException` with clear diff output; JSON serialization of `SelectionReport` for snapshot format; `CUPEL_UPDATE_SNAPSHOTS=1` env var support; snapshot file path resolution from `[CallerFilePath]`
- .NET: Test project demonstrating the full create→match→fail→update cycle

Consumes from S01:
- `SelectionReport` equality for determining whether actual matches expected (deserialized from JSON)

### S05 — Rust budget simulation

Produces:
- Rust: `get_marginal_items(&self, items: &[ContextItem], budget: &ContextBudget, slack_tokens: i32)` and `find_min_budget_for(&self, items: &[ContextItem], budget: &ContextBudget, target: &ContextItem, search_ceiling: i32)` methods on `Pipeline`; `find_min_budget_for` returns `Option<i32>`; monotonicity guard via `is_knapsack()` + `is_count_quota()` check on slicer

Consumes:
- nothing (independent of S01; uses existing `dry_run` and `Pipeline` infrastructure)
