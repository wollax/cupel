---
id: S04
parent: M003
milestone: M003
provides:
  - Rust analytics module: budget_utilization, kind_diversity, timestamp_coverage as free functions on &SelectionReport in crates/cupel/src/analytics.rs
  - All three Rust analytics functions pub use-d from crates/cupel/src/lib.rs with 7 passing unit tests
  - .NET analytics: BudgetUtilization, KindDiversity, TimestampCoverage extension methods on SelectionReport in Wollax.Cupel.Diagnostics namespace
  - Wollax.Cupel.Testing NuGet package (IsPackable=true, csproj at src/Wollax.Cupel.Testing/) with all 13 assertion patterns
  - SelectionReportAssertionChain with fluent chain returning this; SelectionReportAssertionException sealing failure messages
  - SelectionReport.Should() entry point via SelectionReportExtensions.cs in Wollax.Cupel.Testing namespace
  - tests/Wollax.Cupel.Testing.Tests project with 26 TUnit tests covering all 13 patterns (happy + failure path per pattern)
  - Consumption test wiring: Wollax.Cupel.ConsumptionTests references Wollax.Cupel.Testing via PackageReference Version="*-*"
  - nupkg/Wollax.Cupel.Testing.*.nupkg produced; local feed (./packages) populated for consumption test resolution
requires:
  - slice: S01
    provides: Established Rust Scorer trait pattern; ContextItem accessor methods (.kind(), .tokens(), .timestamp())
  - slice: S02
    provides: Established MetadataTrustScorer pattern (no direct consumption in S04, but ContextItem accessor pattern confirmed)
  - slice: S03
    provides: CountRequirementShortfalls field on SelectionReport; confirmed SelectionReport record shape stable for direct construction in tests
affects:
  - S05
  - S06
key_files:
  - crates/cupel/src/analytics.rs
  - crates/cupel/src/lib.rs
  - src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj
  - src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs
  - src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs
  - src/Wollax.Cupel.Testing/SelectionReportExtensions.cs
  - src/Wollax.Cupel.Testing/PublicAPI.Shipped.txt
  - src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj
  - tests/Wollax.Cupel.Testing.Tests/AssertionChainTests.cs
  - tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj
  - tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs
  - Cupel.slnx
key_decisions:
  - D088: S04 verification is contract-level + integration (analytics unit tests + Cupel.Testing consumption reference)
  - D089: Rust analytics are free functions in analytics.rs, pub use-d from lib.rs (not impl SelectionReport block)
  - D090: .NET pattern 6 (HaveExcludedItemWithBudgetExceeded) uses degenerate form — flat enum has no token-detail fields
  - D091: Test fixtures use direct SelectionReport record construction, not DiagnosticTraceCollector
  - D092: ContextBudget::new takes HashMap<ContextKind, i64> directly; Default::default() for empty slots in tests
  - D093: Rust analytics uses .kind() accessor (not field access) on ContextItem — private field
  - D094: SelectionReportAssertionChain constructor is internal; chain created exclusively via Should()
  - D095: Consumption test local NuGet feed is ./packages (not ./nupkg); artifact must be copied between directories
  - D096: PlaceTopNScoredAtEdges uses minTopScore comparison + HashSet membership for tie handling
patterns_established:
  - Analytics module pattern: pure free functions on &SelectionReport in analytics.rs, pub use-d from lib.rs; .NET counterpart as static extension class in Wollax.Cupel.Diagnostics namespace
  - Fluent assertion chain pattern: internal constructor + public methods returning this; single entry point via Should() static extension; dedicated exception type (SelectionReportAssertionException) with structured message
  - Error message format: "{AssertionName}({params}) failed: {expected}. {actual}" — spec-defined, consistent across all 13 patterns
  - New NuGet package project structure: clone Wollax.Cupel.Json.csproj structure; PublicAPI.Shipped.txt empty (only #nullable enable for new packages); PublicAPI.Unshipped.txt populated after initial build surfaces RS0016 errors
  - Failure-path test pattern: try/catch SelectionReportAssertionException with Message assertion (avoids TUnit async complexity)
  - Local feed wiring for consumption tests: dotnet pack → ./nupkg; copy to ./packages (per nuget.config); PackageReference Version="*-*"
observability_surfaces:
  - cargo test -- analytics --nocapture → 7 Rust analytics unit tests with visible output
  - dotnet test --project tests/Wollax.Cupel.Testing.Tests/ → 26 tests (2 per pattern); assertion failure messages in test output
  - SelectionReportAssertionException.Message → structured "{AssertionName}({params}) failed: {expected}. {actual}" on every failure
  - dotnet build src/Wollax.Cupel.Testing/ 2>&1 | grep error → RS0016 for missing PublicAPI entries; no output = compliant
  - dotnet test --project tests/Wollax.Cupel.ConsumptionTests/ → smoke test confirms Should() chains compile and run from installed package
drill_down_paths:
  - .kata/milestones/M003/slices/S04/tasks/T01-SUMMARY.md
  - .kata/milestones/M003/slices/S04/tasks/T02-SUMMARY.md
  - .kata/milestones/M003/slices/S04/tasks/T03-SUMMARY.md
duration: ~60min (3 tasks × ~20min each)
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
---

# S04: Core Analytics + Cupel.Testing Package

**Analytics extension methods (BudgetUtilization, KindDiversity, TimestampCoverage) ship in both languages; Wollax.Cupel.Testing NuGet package installs independently with all 13 fluent assertion patterns proven via 26 tests and a consumption smoke test.**

## What Happened

**T01 — Rust + .NET analytics:**
Created `crates/cupel/src/analytics.rs` with three free functions on `&SelectionReport`:
- `budget_utilization` — sum of included token counts / `budget.max_tokens()` as `f64`
- `kind_diversity` — HashSet dedup of `.kind()` values across included items; returns `usize`
- `timestamp_coverage` — fraction of included items with `timestamp().is_some()`; `0.0` on empty

All three re-exported from `lib.rs` via `pub use analytics::{...}`. Seven unit tests cover empty-report base cases and non-empty cases including partial timestamps and mixed-kind deduplication.

The .NET counterpart uses LINQ in `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` as extension methods on `SelectionReport` in the `Wollax.Cupel.Diagnostics` namespace. `PublicAPI.Unshipped.txt` updated with 4 entries (class + 3 methods).

Key discovery: `ContextBudget::new` takes a `HashMap<ContextKind, i64>` (not `Option`); tests use `Default::default()`. `ContextItem.kind` is private — `.kind()` accessor required throughout.

**T02 — Cupel.Testing package + patterns 1–7:**
Created `src/Wollax.Cupel.Testing/` from scratch, modelled on `Wollax.Cupel.Json.csproj`: `IsPackable=true`, `ProjectReference` to Wollax.Cupel, `PublicApiAnalyzers` with `PrivateAssets="All"`.

`SelectionReportAssertionException` inherits directly from `Exception` (no FluentAssertions, no framework exception types — D041/D066). `SelectionReportAssertionChain` has an `internal` constructor (not in PublicAPI.Unshipped.txt); all 7 assertion methods return `this`. `SelectionReportExtensions.Should()` is the single public entry point.

Patterns 1–7 (IncludeItemWithKind, IncludeItemMatching, IncludeExactlyNItemsWithKind, ExcludeItemWithReason, ExcludeItemMatchingWithReason, HaveExcludedItemWithBudgetExceeded, HaveNoExclusionsForKind) — all error messages follow the spec format exactly. Pattern 6 uses the degenerate .NET form (flat enum, no token-detail fields). `Cupel.slnx` updated to include the new project.

**T03 — Patterns 8–13 + test project + consumption wiring:**
Added patterns 8–13 to `SelectionReportAssertionChain.cs`:
- P8 `HaveAtLeastNExclusions(n)` — count check on Excluded
- P9 `ExcludedItemsAreSortedByScoreDescending()` — adjacent-pair loop
- P10 `HaveBudgetUtilizationAbove(threshold, budget)` — inline token sum / budget.MaxTokens
- P11 `HaveKindCoverageCount(n)` — distinct Kind values in Included
- P12 `PlaceItemAtEdge(predicate)` — position 0 or count−1 check
- P13 `PlaceTopNScoredAtEdges(n)` — alternating lo/hi edge placement with HashSet membership and minTopScore comparison for ties

Created `tests/Wollax.Cupel.Testing.Tests/` with 26 TUnit tests (2 per pattern: happy + failure). Failure-path tests assert on `SelectionReportAssertionException.Message` content to verify spec error format.

Consumption test wiring required a non-obvious step: `nuget.config` declares `./packages` as the local feed (not `./nupkg`). Packed to `./nupkg` then copied to `./packages`. Added `PackageReference Include="Wollax.Cupel.Testing" Version="*-*"` to consumption csproj and added a smoke test proving `Should()` chains compile and run from the installed package.

## Verification

- `cargo test -- analytics --nocapture` → 7 Rust analytics tests pass
- `cargo test --all-targets` → 124 passed, 0 failed
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` → 0 errors, 0 warnings
- `dotnet build src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` → 0 errors, 0 warnings
- `dotnet test --project tests/Wollax.Cupel.Testing.Tests/...` → 26 passed, 0 failed
- `dotnet test` (full solution) → 708 passed, 0 failed
- `dotnet test --project tests/Wollax.Cupel.ConsumptionTests/...` → 6 passed, 0 failed
- `ls ./nupkg/Wollax.Cupel.Testing.*.nupkg` → file exists
- `grep "Wollax.Cupel.Testing" tests/Wollax.Cupel.ConsumptionTests/...csproj` → match
- `grep "budget_utilization\|kind_diversity\|timestamp_coverage" crates/cupel/src/lib.rs` → all 3 exported
- PublicAPI compliance: 0 RS0016 errors on both Wollax.Cupel and Wollax.Cupel.Testing builds

## Requirements Advanced

- R021 (Cupel.Testing package) — fully implemented: all 13 assertion patterns, Should() entry point, dedicated exception, NuGet package builds and installs, 26 tests green, consumption smoke test passes

## Requirements Validated

- R021 — validated: `Wollax.Cupel.Testing` NuGet package installs independently (PackageReference Version="*-*" resolves from local feed); all 13 assertion patterns are callable and produce correct pass/fail behavior verified by 26 TUnit tests; `dotnet test` 708 passed; `dotnet pack` produces `.nupkg`

## New Requirements Surfaced

- None discovered during execution.

## Requirements Invalidated or Re-scoped

- None.

## Deviations

- T03 added a smoke test to `ConsumptionTests.cs` (not explicitly listed in the task plan) to prove `Should()` chains compile and run end-to-end from the installed package. This is additive — it strengthens the slice's installability proof beyond the minimum plan spec.
- nupkg output goes to `./nupkg` by default; consumption tests use `./packages` as local feed. Copy step is required as part of the wiring workflow (discovered at T03, documented as D095).

## Known Limitations

- `HaveBudgetUtilizationAbove` in the assertion chain (P10) recomputes the token sum inline rather than calling the `BudgetUtilization()` extension method from `Wollax.Cupel.Diagnostics`. Both are correct; the inline version avoids an extra project reference dependency from `Wollax.Cupel.Testing` into `Wollax.Cupel.Diagnostics`.
- Rust has no equivalent of `Wollax.Cupel.Testing` — the assertion vocabulary is a .NET-only package. Rust callers use `cargo test` assertions directly against the free functions in `analytics.rs`.
- Pattern 6 (`HaveExcludedItemWithBudgetExceeded`) is a degenerate form in .NET — no token-detail parameters because `ExclusionReason` is a flat enum. The Rust vocabulary spec documents the full form with token-count fields.

## Follow-ups

- S05 can reference `ITraceCollector`/`SelectionReport` from `Wollax.Cupel` core — both are stable and unchanged by this slice.
- The new package project structure (csproj pattern, PublicAPI files, local feed workflow) is now established as a template for S05's `Wollax.Cupel.Diagnostics.OpenTelemetry` package.
- S06 consumes the analytics extension methods (BudgetUtilization is called by GetMarginalItems/FindMinBudgetFor budget simulation).

## Files Created/Modified

- `crates/cupel/src/analytics.rs` — new; 3 pub fn analytics functions + 7 unit tests (~160 lines)
- `crates/cupel/src/lib.rs` — added `pub mod analytics` and `pub use analytics::{...}` (+2 lines)
- `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` — new; 3 extension methods on SelectionReport (~45 lines)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 4 new entries (class declaration + 3 method signatures)
- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — new; NuGet package project with PublicApiAnalyzers
- `src/Wollax.Cupel.Testing/PublicAPI.Shipped.txt` — new; `#nullable enable` header only
- `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — new; all 16 public members (10 from T02 + 6 from T03)
- `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs` — new; sealed Exception subclass
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — new; all 13 assertion patterns (~260 lines)
- `src/Wollax.Cupel.Testing/SelectionReportExtensions.cs` — new; Should() entry point (~15 lines)
- `tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj` — new; ProjectReference to Testing + Cupel
- `tests/Wollax.Cupel.Testing.Tests/AssertionChainTests.cs` — new; 26 TUnit tests covering all 13 patterns
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` — added Wollax.Cupel.Testing PackageReference
- `tests/Wollax.Cupel.ConsumptionTests/ConsumptionTests.cs` — added Testing using + smoke test
- `Cupel.slnx` — added Wollax.Cupel.Testing and Wollax.Cupel.Testing.Tests projects

## Forward Intelligence

### What the next slice should know
- The new package project structure (csproj with IsPackable=true, PublicApiAnalyzers, empty PublicAPI.Shipped.txt, nuget.config local feed workflow) is proven and documented as a reusable template. S05's `Wollax.Cupel.Diagnostics.OpenTelemetry` package should clone the Wollax.Cupel.Testing.csproj structure directly.
- `nuget.config` at the repo root defines `./packages` as the local source feed — NOT `./nupkg`. Any new package that needs consumption-test verification must copy its `.nupkg` from `./nupkg` to `./packages`.
- `PublicAPI.Unshipped.txt` for new packages: create empty (`#nullable enable` only in Shipped), do an initial build, capture all RS0016 errors, populate Unshipped.txt, rebuild to confirm clean. This two-pass workflow is the proven path.
- TUnit test project for a new package: clone the existing `Wollax.Cupel.Tests.csproj`; change `ProjectReference` to the new package; also add a `ProjectReference` to `Wollax.Cupel` if the tests need `SelectionReport` construction.

### What's fragile
- The `./packages` → `./nupkg` split for local feed — if someone runs `dotnet pack` and forgets to copy the `.nupkg` to `./packages`, consumption tests will fail to restore with a cryptic NuGet resolution error (not an obvious "file not found").
- `PlaceTopNScoredAtEdges(n)` tie handling uses `minTopScore` — if all items have the same score, all items qualify as "topN" regardless of `n`. This is a known edge case documented in the spec; the implementation is correct per spec semantics but may surprise callers.

### Authoritative diagnostics
- `dotnet test --project tests/Wollax.Cupel.Testing.Tests/...` — 26 tests; failure output includes structured `SelectionReportAssertionException.Message`
- `SelectionReportAssertionException.Message` is always `"{AssertionName}({params}) failed: {expected}. {actual}"` — no hidden state; all diagnostic information visible in test output
- `dotnet build src/Wollax.Cupel.Testing/ 2>&1 | grep error` — RS0016 = missing PublicAPI entry; CS0161 = missing `return this;` on a chain method

### What assumptions changed
- Original T03 plan said "add `PackageReference Include="Wollax.Cupel.Testing" Version="*-*"` to consumption csproj" without specifying the local feed location. Actual: `nuget.config` declares `./packages` not `./nupkg`; the copy step is mandatory and not obvious from the csproj alone.
