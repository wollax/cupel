# M004: v1.4 Diagnostics & Simulation Parity — Context

**Gathered:** 2026-03-23
**Status:** Ready for planning

## Project Description

Cupel is a dual-language (.NET + Rust) context management library for coding agents. M004 adds the last cluster of brainstormed features: structural equality for reports, a policy fork diagnostic tool, quota utilization analytics, snapshot testing in Cupel.Testing, and Rust budget simulation parity.

## Why This Milestone

M003 shipped all designed features — scorers, slicers, analytics, testing vocabulary, OTel bridge, budget simulation. But three capabilities from the brainstorm sessions remain: fork diagnostics (`PolicySensitivityReport`), quota utilization (`QuotaUtilization`), and Rust budget simulation parity. Additionally, snapshot testing was blocked until M003/S06 shipped the tiebreaker rule, and structural equality on `SelectionReport` was identified as a prerequisite for both.

These five items form a natural cluster: equality enables fork diagnostics and snapshot testing; quota utilization completes the analytics surface; Rust budget simulation closes the last Rust parity gap.

## User-Visible Outcome

### When this milestone is complete, the user can:

- Compare `SelectionReport` instances with `==` in both languages (equality)
- Run `PolicySensitivity(items, [(label, pipeline), ...])` and get labeled reports + a structured diff showing which items moved between variants
- Call `QuotaUtilization(report, policy)` to see per-kind cap/require utilization with an `IQuotaPolicy` abstraction shared across `QuotaSlice` and `CountQuotaSlice`
- Write snapshot-based tests in .NET via `report.Should().MatchSnapshot("name")` with JSON snapshot files and `CUPEL_UPDATE_SNAPSHOTS=1` update workflow
- Call `get_marginal_items` and `find_min_budget_for` in Rust matching the .NET budget simulation API

### Entry point / environment

- Entry point: Library API (NuGet packages + crates.io crate)
- Environment: local dev / CI (test harnesses)
- Live dependencies involved: none (pure library)

## Completion Class

- Contract complete means: all new APIs have unit tests, existing tests stay green, PublicAPI analyzers pass, conformance vectors where applicable
- Integration complete means: snapshot testing exercises real `SelectionReport` serialization + file I/O; fork diagnostic exercises real `dry_run` across multiple pipeline configs
- Operational complete means: none (library — no service lifecycle)

## Final Integrated Acceptance

To call this milestone complete, we must prove:

- `cargo test --all-targets` passes with new budget simulation tests
- `dotnet test --configuration Release` passes with all new feature tests
- Fork diagnostic produces a meaningful diff across at least two different pipeline configurations in a test harness
- Snapshot test round-trips: create snapshot → verify match → modify report → verify failure → update snapshot → verify match
- `QuotaUtilization` returns correct per-kind data for both `QuotaSlice` and `CountQuotaSlice` configurations via `IQuotaPolicy`

## Risks and Unknowns

- `SelectionReport` equality with f64 scores — exact comparison is correct for deterministic pipelines, but callers may misuse it for cross-run comparison where floating-point non-determinism matters. Must document clearly.
- `IQuotaPolicy` extraction — must not break `QuotaSlice` or `CountQuotaSlice` public API surface. Both already implement `ISlicer`; adding a second interface is additive but needs PublicAPI analyzer approval.
- Snapshot file I/O in test context — `Wollax.Cupel.Testing` is currently pure (no file system access). Adding snapshot support means the package gains `System.IO` usage. This is acceptable for a testing package.
- Rust budget simulation — must match .NET behavior exactly, including the temporary-budget execution seam in Pipeline. The .NET implementation uses an internal `DryRunWithBudget` method.

## Existing Codebase / Prior Art

- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — sealed record, no IEquatable yet
- `crates/cupel/src/diagnostics/mod.rs` — `SelectionReport` derives `Debug, Clone` but not `PartialEq`
- `src/Wollax.Cupel/BudgetSimulationExtensions.cs` — .NET GetMarginalItems + FindMinBudgetFor
- `spec/src/analytics/budget-simulation.md` — spec chapter for budget simulation
- `src/Wollax.Cupel/Slicing/QuotaSlice.cs` + `CountQuotaSlice.cs` — existing slicers that will implement IQuotaPolicy
- `crates/cupel/src/slicer/quota.rs` + `count_quota.rs` — Rust equivalents
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — existing assertion chain (13 patterns)
- `crates/cupel/src/pipeline/mod.rs` — `dry_run` method used by fork diagnostic

> See `.kata/DECISIONS.md` for all architectural and pattern decisions — it is an append-only register; read it during planning, append to it during execution.

## Relevant Requirements

- R050 — SelectionReport structural equality (prerequisite for R051, R053)
- R051 — PolicySensitivityReport fork diagnostic
- R052 — IQuotaPolicy abstraction + QuotaUtilization
- R053 — Snapshot testing in Cupel.Testing
- R054 — Rust budget simulation parity

## Scope

### In Scope

- `PartialEq`/`Eq` on `SelectionReport`, `IncludedItem`, `ExcludedItem` in Rust; `IEquatable<T>` in .NET
- `PolicySensitivityReport` with labeled reports + structured diff in both languages
- `IQuotaPolicy` interface extraction from QuotaSlice/CountQuotaSlice + `QuotaUtilization` extension method in both languages
- `MatchSnapshot(name)` assertion in Cupel.Testing with JSON format and env-var update workflow
- `get_marginal_items` + `find_min_budget_for` in Rust crate

### Out of Scope / Non-Goals

- Epsilon-based approximate equality (callers build their own)
- `DryRunWithPolicy` convenience API (deferred — R056)
- Rust snapshot testing (Rust has `insta` crate)
- `ProfiledPlacer` companion package (deferred — R055)
- Any new pipeline stages, scorers, or slicers

## Technical Constraints

- Zero-dependency constraint on `cupel` core crate preserved
- `Wollax.Cupel.Testing` may add `System.IO` and `System.Text.Json` for snapshot file I/O
- PublicAPI analyzers must pass for all new public surface
- `#[non_exhaustive]` audit not needed — no new enum variants in this milestone
- Exact f64 equality — no epsilon, documented clearly

## Integration Points

- `Wollax.Cupel.Testing` package — gains snapshot assertion methods (additive)
- `Wollax.Cupel` core package — gains IQuotaPolicy interface, equality implementations, fork diagnostic API
- `cupel` Rust crate — gains budget simulation, equality derives, fork diagnostic

## Open Questions

- None — all design choices locked during discussion phase.
