# S04: Core Analytics + Cupel.Testing Package

**Goal:** Add `BudgetUtilization`, `KindDiversity`, and `TimestampCoverage` extension methods/functions to `SelectionReport` in both Rust and .NET; create the `Wollax.Cupel.Testing` NuGet package with all 13 assertion patterns; prove standalone installability via consumption tests.
**Demo:** After this slice, a test author can write:
```csharp
report.Should()
    .HaveKindCoverageCount(2)
    .HaveBudgetUtilizationAbove(0.8, budget)
    .ExcludeItemWithReason(ExclusionReason.BudgetExceeded);
```
and have it pass or fail with structured error messages; all three analytics extension methods are callable in both languages; `dotnet test` is green.

## Must-Haves

- `BudgetUtilization(ContextBudget)` callable on `SelectionReport` in both .NET and Rust; returns `double`/`f64`; denominator is `budget.MaxTokens`
- `KindDiversity()` callable on `SelectionReport` in both languages; returns `int`/`usize`; returns count of distinct `ContextKind` values in `Included`/`included`; return type is a count, not a ratio
- `TimestampCoverage()` callable on `SelectionReport` in both languages; returns `double`/`f64`; denominator is `Included.Count`; returns `0.0` when `Included` is empty
- `Wollax.Cupel.Testing` NuGet package builds (`dotnet pack` produces `.nupkg`); csproj at `src/Wollax.Cupel.Testing/`; `IsPackable=true`
- `SelectionReport.Should()` returns `SelectionReportAssertionChain`; all 13 assertion methods are implemented and return `this`; failures throw `SelectionReportAssertionException`
- All 13 assertion patterns match the vocabulary spec error message formats exactly
- `tests/Wollax.Cupel.Testing.Tests/` passes `dotnet test` green with at least one test per pattern (13+ tests)
- Consumption tests project references `Wollax.Cupel.Testing` (proving standalone installability)
- `cargo test --all-targets` passes; `dotnet test` passes (both zero failures)
- `PublicAPI.Unshipped.txt` updated for all new public members in `Wollax.Cupel` and `Wollax.Cupel.Testing`

## Proof Level

- This slice proves: integration (Cupel.Testing package installs and runs assertions against a real SelectionReport; analytics methods run against real SelectionReport data)
- Real runtime required: no (test harness is sufficient; no running service)
- Human/UAT required: no (all verification is mechanically checkable)

## Verification

```bash
# Rust
cargo test --all-targets
# Expected: all tests pass, including analytics unit tests

# .NET core build
dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj
# Expected: 0 errors, 0 warnings

# .NET Testing package build
dotnet build src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj
# Expected: 0 errors, 0 warnings

# Full test suite
dotnet test
# Expected: all tests pass, 0 failed

# Specific test project
dotnet test tests/Wollax.Cupel.Testing.Tests/
# Expected: ≥13 tests pass (one per pattern), 0 failed

# Pack verification
dotnet pack src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj --output ./nupkg
ls ./nupkg/Wollax.Cupel.Testing.*.nupkg
# Expected: file exists

# PublicAPI compliance (Wollax.Cupel analytics methods)
dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj 2>&1 | grep " error " | grep -v "^$"
# Expected: no output

# PublicAPI compliance (Wollax.Cupel.Testing)
dotnet build src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj 2>&1 | grep " error " | grep -v "^$"
# Expected: no output

# Rust analytics in lib.rs
grep "budget_utilization\|kind_diversity\|timestamp_coverage" crates/cupel/src/lib.rs
# Expected: all three functions re-exported

# Consumption tests reference Wollax.Cupel.Testing
grep "Wollax.Cupel.Testing" tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj
# Expected: match found
```

## Observability / Diagnostics

- Runtime signals: `SelectionReportAssertionException.Message` carries structured assertion name + expected + actual for every failure; analytics return primitive values observable in test output
- Inspection surfaces: `dotnet test` test output; `cargo test -- analytics --nocapture` for Rust analytics tests
- Failure visibility: exception message format is spec-defined — `{AssertionName}({params}) failed: {what was expected}. {what was actually found}`; no hidden state
- Redaction constraints: none (test library; no secrets or PII)

## Integration Closure

- Upstream surfaces consumed: `SelectionReport` (stable through M001/M002), `IncludedItem`, `ExcludedItem`, `ExclusionReason`, `ContextBudget`, `ContextKind`, `CountRequirementShortfalls` (S03)
- New wiring introduced in this slice: `Wollax.Cupel.Testing` NuGet package with `ProjectReference` to `Wollax.Cupel`; `SelectionReport.Should()` extension method (in `Wollax.Cupel.Testing`); `SelectionReportExtensions` static class (in `Wollax.Cupel` core); Rust `analytics` module re-exported from `lib.rs`; consumption tests reference `Wollax.Cupel.Testing`
- What remains before the milestone is truly usable end-to-end: S05 (OTel bridge) and S06 (budget simulation + tiebreaker) — neither depends on this slice's internal structure; S05 consumes `ITraceCollector`/`SelectionReport` from core

## Tasks

- [x] **T01: Rust + .NET analytics extension methods** `est:45m`
  - Why: Analytics extension methods are in Wollax.Cupel core (D045); must ship before Cupel.Testing can reference them (pattern 10 uses BudgetUtilization internally); zero external dependencies
  - Files: `crates/cupel/src/analytics.rs`, `crates/cupel/src/lib.rs`, `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
  - Do: Create `crates/cupel/src/analytics.rs` with three `pub fn` free functions on `&SelectionReport`; `BudgetUtilization(report, budget)` returns `f64` = sum of `included[i].item.tokens()` / `budget.max_tokens() as f64`; `KindDiversity(report)` returns `usize` = count of distinct `item.kind` in `included` using `HashSet`; `TimestampCoverage(report)` returns `f64` = fraction of `included` items where `item.timestamp().is_some()`; return `0.0` when `included` is empty; add `pub mod analytics;` and `pub use analytics::{budget_utilization, kind_diversity, timestamp_coverage};` in `lib.rs`; add unit tests in `analytics.rs`; create `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` as `public static class` in `Wollax.Cupel.Diagnostics` namespace with three extension methods on `SelectionReport`; `BudgetUtilization(this SelectionReport, ContextBudget)` returns `double`; `KindDiversity(this SelectionReport)` returns `int`; `TimestampCoverage(this SelectionReport)` returns `double` with `Included.Count == 0` → `0.0` guard; update `PublicAPI.Unshipped.txt` with the extension class and all three method signatures immediately after writing the file
  - Verify: `cargo test -- analytics --nocapture` → all analytics tests pass; `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` → 0 errors; `grep budget_utilization crates/cupel/src/lib.rs` → match
  - Done when: `cargo test --all-targets` exits 0; `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` exits 0 with 0 errors; all three analytics functions exported from lib.rs; all three extension methods in PublicAPI.Unshipped.txt

- [x] **T02: Wollax.Cupel.Testing csproj + chain plumbing + patterns 1–7** `est:60m`
  - Why: The NuGet package structure and core chain plumbing must exist before any assertion patterns can be implemented or tested; patterns 1–7 (Inclusion group + Exclusion group) form the most-used assertion surface
  - Files: `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj`, `src/Wollax.Cupel.Testing/PublicAPI.Shipped.txt`, `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt`, `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs`, `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs`, `src/Wollax.Cupel.Testing/SelectionReportExtensions.cs`
  - Do: Create `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` by copying the `Wollax.Cupel.Json.csproj` structure: `IsPackable=true`, `ProjectReference` to `../Wollax.Cupel/Wollax.Cupel.csproj`, `PublicApiAnalyzers`, `PublicAPI.Shipped.txt` + `PublicAPI.Unshipped.txt`; create empty `PublicAPI.Shipped.txt` with `#nullable enable` header; create `SelectionReportAssertionException.cs` inheriting from `Exception` directly (not from any framework type per D041); create `SelectionReportAssertionChain.cs` wrapping `SelectionReport` with constructor and `_report` field; create `SelectionReportExtensions.cs` with `public static SelectionReportAssertionChain Should(this SelectionReport report)` extension method; implement patterns 1–7 in `SelectionReportAssertionChain.cs`: `IncludeItemWithKind`, `IncludeItemMatching`, `IncludeExactlyNItemsWithKind`, `ExcludeItemWithReason`, `ExcludeItemMatchingWithReason`, `HaveExcludedItemWithBudgetExceeded` (.NET degenerate form per language note), `HaveNoExclusionsForKind`; each method returns `this`; each throws `SelectionReportAssertionException` with exact error message from spec; update `PublicAPI.Unshipped.txt` with all public members; build must pass
  - Verify: `dotnet build src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` → 0 errors, 0 warnings; `grep "Should\b" src/Wollax.Cupel.Testing/SelectionReportExtensions.cs` → match
  - Done when: `dotnet build src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` exits 0 with 0 errors/warnings; all 7 patterns implemented and returning `this`; `PublicAPI.Unshipped.txt` complete

- [x] **T03: Patterns 8–13 + test project + consumption test wiring** `est:60m`
  - Why: Completes the 13-pattern vocabulary; tests prove correctness and serve as the integration proof; consumption test wiring retires the NuGet standalone-installability risk (the primary S04 risk per research)
  - Files: `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs`, `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt`, `tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj`, `tests/Wollax.Cupel.Testing.Tests/AssertionChainTests.cs`, `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj`
  - Do: Add patterns 8–13 to `SelectionReportAssertionChain.cs`: `HaveAtLeastNExclusions`, `ExcludedItemsAreSortedByScoreDescending`, `HaveBudgetUtilizationAbove` (calls `BudgetUtilization()` extension from Wollax.Cupel or recomputes inline), `HaveKindCoverageCount`, `PlaceItemAtEdge`, `PlaceTopNScoredAtEdges`; implement exact spec error messages; update `PublicAPI.Unshipped.txt`; create `tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj` (clone of Wollax.Cupel.Tests.csproj with `ProjectReference` to `Wollax.Cupel.Testing` instead of `Wollax.Cupel`; also add `ProjectReference` to `Wollax.Cupel` for SelectionReport construction); create `AssertionChainTests.cs` with ≥13 TUnit test methods (one happy-path + one failure-path per pattern minimum); add `<PackageReference Include="Wollax.Cupel.Testing" Version="*-*" />` to `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj`; run `dotnet pack` then `dotnet test` end-to-end
  - Verify: `dotnet test tests/Wollax.Cupel.Testing.Tests/` → ≥13 tests pass, 0 failed; `grep "Wollax.Cupel.Testing" tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` → match; `dotnet test` → all tests pass, 0 failed; `cargo test --all-targets` → 0 failed
  - Done when: `dotnet test` exits 0 (all projects); ≥13 tests in `Wollax.Cupel.Testing.Tests` pass; consumption test csproj references `Wollax.Cupel.Testing`; `dotnet pack src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj --output ./nupkg` produces a `.nupkg` file

## Files Likely Touched

- `crates/cupel/src/analytics.rs` — new
- `crates/cupel/src/lib.rs` — add analytics module + re-exports
- `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` — new
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — add analytics extension class + 3 methods
- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — new
- `src/Wollax.Cupel.Testing/PublicAPI.Shipped.txt` — new (empty header)
- `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — new (all public members)
- `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs` — new
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — new (all 13 patterns)
- `src/Wollax.Cupel.Testing/SelectionReportExtensions.cs` — new (Should() entry point)
- `tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj` — new
- `tests/Wollax.Cupel.Testing.Tests/AssertionChainTests.cs` — new (≥13 tests)
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` — add Testing package reference
