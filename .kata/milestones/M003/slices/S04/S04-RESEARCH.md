# S04: Core Analytics + Cupel.Testing Package — Research

**Researched:** 2026-03-23
**Domain:** .NET extension methods, Rust free functions, NuGet package creation
**Confidence:** HIGH

## Summary

S04 has two distinct workstreams: (1) analytics extension methods (`BudgetUtilization`, `KindDiversity`, `TimestampCoverage`) in both .NET and Rust, and (2) the `Wollax.Cupel.Testing` NuGet package with 13 assertion patterns. Both are fully spec-locked with no design ambiguity remaining.

**Analytics extension methods** go in `Wollax.Cupel` core (D045) — not a separate package — as extension methods on `SelectionReport` in .NET and as free functions on `&SelectionReport` in Rust. The semantics are straightforward: `BudgetUtilization` divides included token sum by `budget.MaxTokens`; `KindDiversity` counts distinct `ContextKind` values in `included`; `TimestampCoverage` returns the fraction of `included` items with non-null `Timestamp`. All three return `double`/`f64`.

**Cupel.Testing** is a new `src/Wollax.Cupel.Testing/` csproj with `IsPackable=true` and a `ProjectReference` to `Wollax.Cupel`. The chain plumbing is ~100 lines: `SelectionReportAssertionChain` wrapping `SelectionReport`, `SelectionReportAssertionException`, and the 13 assertion methods. There is no dependency on FluentAssertions (D041). The release workflow uses `dotnet pack` with wildcard glob, so the new package picks up automatically without workflow changes.

The critical risk: the new package must be **standalone-installable**. The consumption tests (`tests/Wollax.Cupel.ConsumptionTests/`) are the installability proof. S04 must add a `Wollax.Cupel.Testing` reference to the consumption tests project to retire the NuGet wiring risk.

## Recommendation

**Split execution into 3 tasks:**
- **T01 (Rust + .NET analytics extension methods):** Add `BudgetUtilization`, `KindDiversity`, `TimestampCoverage` to both languages. Short task (~40 LOC Rust, ~40 LOC C#). Update PublicAPI.Unshipped.txt.
- **T02 (Wollax.Cupel.Testing csproj + chain plumbing + patterns 1–7):** Create project, `SelectionReportAssertionChain`, `SelectionReportAssertionException`, and implement the Inclusion group (3 patterns) + Exclusion group (4 patterns). Verify `dotnet build` clean.
- **T03 (Patterns 8–13 + tests + consumption test installability):** Implement Aggregate (2), Budget (1), Coverage (1), Ordering (2) patterns. Write unit tests in a new `tests/Wollax.Cupel.Testing.Tests/` project. Add the `Wollax.Cupel.Testing` package reference to consumption tests. Run `dotnet test` end-to-end.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Extension methods on sealed record | C# `static class` with `this SelectionReport` parameter — standard C# extension method pattern | `SelectionReport` is `sealed record`; caller sees it as a method without modification |
| Rust free functions on non-owned type | `pub fn budget_utilization(report: &SelectionReport, budget: &ContextBudget) -> f64` in a new `analytics` module | Rust doesn't have extension methods; module-level free functions are idiomatic |
| Chain plumbing boilerplate | ~100 lines hand-rolled (D041 mandates no FluentAssertions) | FluentAssertions adds a runtime dependency; FA's chain is modeled from `SelectionReportAssertionChain` returning `this` |
| NuGet package wiring | Copy the `Wollax.Cupel.Json.csproj` template — it's a companion package with identical structure | Json package already has `IsPackable=true`, `ProjectReference` to core, `PublicAPI.*.txt`, `Microsoft.CodeAnalysis.PublicApiAnalyzers` |

## Existing Code and Patterns

### .NET Side

- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — `sealed record` with `Included: IReadOnlyList<IncludedItem>`, `Excluded: IReadOnlyList<ExcludedItem>`, `TotalCandidates`, `TotalTokensConsidered`, `CountRequirementShortfalls`. The analytics extension methods live in a new static class in the same namespace (`Wollax.Cupel.Diagnostics` or `Wollax.Cupel`).
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs` — `sealed record` with `Item: ContextItem`, `Score: double`, `Reason: InclusionReason`. Analytics method for `KindDiversity` reads `Item.Kind`; `TimestampCoverage` reads `Item.Timestamp`. `BudgetUtilization` reads `Item.Tokens`.
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — flat enum with 10 values (0–9, including `CountCapExceeded=8` and `CountRequireCandidatesExhausted=9` added in S03). The assertion chain's `ExcludeItemWithReason(ExclusionReason reason)` does a direct enum equality comparison.
- `src/Wollax.Cupel/ContextBudget.cs` — `sealed class` with `MaxTokens: int`. `BudgetUtilization` uses `budget.MaxTokens` as denominator (PD-2, D045). `ContextBudget` validates `MaxTokens > 0` at construction; no division-by-zero possible.
- `src/Wollax.Cupel/ContextItem.cs` — `sealed record` with `Timestamp: DateTimeOffset?`. `TimestampCoverage` checks non-null `Timestamp` on each `IncludedItem.Item`.
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Must be updated with all new public members from analytics extension methods. RS0016 analyzer blocks build if any public/protected member is missing.
- `src/Wollax.Cupel.Json/Wollax.Cupel.Json.csproj` — **Template for the new Cupel.Testing csproj.** Has `IsPackable=true`, `ProjectReference` to `Wollax.Cupel`, `PublicApiAnalyzers`, `PublicAPI.Shipped.txt` + `PublicAPI.Unshipped.txt`. The `Wollax.Cupel.Testing` project should be identical in structure.
- `Directory.Build.props` — `TreatWarningsAsErrors=true`. All new code must be warning-free.
- `Directory.Packages.props` — Central package management. `TUnit` is already in the central package versions. Any new `PackageVersion` entries (none expected for Cupel.Testing — it has no third-party deps) would go here.
- `.github/workflows/release.yml` — Uses `dotnet pack --output ./nupkg` which packs **all** projects with `IsPackable=true`. No workflow changes needed for Wollax.Cupel.Testing to be packed and published. The OIDC publish step uses `./nupkg/*.nupkg` wildcard.
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` — References all 4 existing packages by version `*-*` (MinVer, so any pre-release). Must add `Wollax.Cupel.Testing` reference here to prove standalone installability. Uses `ManagePackageVersionsCentrally=false` — version must be specified inline (not in `Directory.Packages.props`).

### Rust Side

- `crates/cupel/src/diagnostics/mod.rs` — `SelectionReport` struct with `included: Vec<IncludedItem>` and `excluded: Vec<ExcludedItem>`. Analytics free functions go in a new `crates/cupel/src/analytics.rs` module (or directly in `diagnostics/mod.rs` as an `impl` block — prefer separate module for clarity).
- `crates/cupel/src/lib.rs` — `pub use` pattern established. New analytics functions/module must be re-exported here.
- `crates/cupel/src/model/context_item.rs` — `timestamp: Option<DateTime<Utc>>` at line 38, accessor `fn timestamp(&self) -> Option<DateTime<Utc>>`. `TimestampCoverage` filters `included` for `item.timestamp().is_some()`.
- `crates/cupel/src/scorer/decay.rs` — Uses `chrono` via `DateTime<Utc>`. `chrono` is already in `Cargo.toml`.

### Test Pattern

- `tests/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs` — Established pattern for TUnit test classes. Copy structure for `tests/Wollax.Cupel.Testing.Tests/`.
- `tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` — `OutputType=Exe` with `TUnit` reference and `ProjectReference` to core. `Wollax.Cupel.Testing.Tests` csproj follows this exactly but references `Wollax.Cupel.Testing` instead.

## Constraints

- **Zero external deps in Wollax.Cupel core (D045):** `BudgetUtilization`, `KindDiversity`, `TimestampCoverage` are extension methods in the core package — no new PackageReference needed.
- **No FluentAssertions (D041):** Cupel.Testing chain is hand-rolled. `SelectionReportAssertionException` is a dedicated type, not `InvalidOperationException`.
- **TreatWarningsAsErrors:** All new files must be warning-free on first compile. Check `PublicAPI.Unshipped.txt` entries immediately after adding new public members.
- **ExclusionReason is a flat enum in .NET:** `ExcludeItemWithBudgetDetails` (pattern 6) carries `item_tokens` and `available_tokens` in Rust's `BudgetExceeded` variant, but .NET `ExclusionReason` is a flat enum with no associated data. The spec explicitly notes .NET may omit the token-detail parameters and degenerate to a `BudgetExceeded` reason check. Implement as `HaveExcludedItemWithBudgetExceeded(Func<ContextItem, bool> predicate)` in .NET.
- **`TimestampCoverage` denominator:** `Included.Count` — not `TotalCandidates`. Returns `0.0` when `Included` is empty.
- **`KindDiversity` denominator:** None — returns count of distinct `ContextKind` values, not a ratio. Return type is `int` in both languages.
- **`BudgetUtilization` denominator:** `budget.MaxTokens` — confirmed PD-2. Return type is `double`/`f64`.
- **RS0016:** New `static class SelectionReportExtensions` (or equivalent) with extension methods must have its public members listed in `PublicAPI.Unshipped.txt`. Extension method classes themselves do not need to be listed if `internal`; if `public`, the class AND all methods need entries.
- **`#[non_exhaustive]` on `SelectionReport`:** Rust struct is `#[non_exhaustive]` — free functions taking `&SelectionReport` are fine; struct field access (`report.included`, `report.excluded`) is stable for in-crate code.

## Common Pitfalls

- **Forgetting `PublicAPI.Unshipped.txt` update for analytics extension class:** If the analytics extension class is `public`, RS0016 requires listing `static Wollax.Cupel.Diagnostics.SelectionReportExtensions.BudgetUtilization(...)` etc. Check with `dotnet build` immediately after adding the class.
- **`TimestampCoverage` returning 1.0 when Included is empty instead of 0.0:** Division-by-zero guard: return `0.0` when `Included.Count == 0`.
- **`KindDiversity` returning a ratio instead of a count:** The spec (and brainstorm decisions) define this as `distinct kind count` (an integer), not a ratio. Do NOT return `distinctCount / included.Count`. Return the raw `int`/`usize`.
- **`SelectionReportAssertionException` inheriting from wrong base:** Must NOT inherit from `FluentAssertions.AssertionException` or any framework-specific type. Inherit from `Exception` directly. Test runners display it based on whether it's a known assertion failure type — this will show as a test failure, not a system error, since TUnit catches all exceptions and reports them.
- **Consumption test project `ManagePackageVersionsCentrally=false`:** The consumption tests project opts out of central package management — version must be specified inline: `<PackageReference Include="Wollax.Cupel.Testing" Version="*-*" />`. Do NOT add to `Directory.Packages.props`.
- **`dotnet pack` glob picks up test projects:** Only projects with `IsPackable=true` are packed. `Wollax.Cupel.Testing` source project needs `IsPackable=true`; the test project must have `IsPackable=false` (default) or explicitly `false`.
- **Missing `PublicAPI.Shipped.txt` in new Testing project:** Copy the pattern from `Wollax.Cupel.Json` — needs both `PublicAPI.Shipped.txt` (empty header) and `PublicAPI.Unshipped.txt` with all public members.
- **Rust `analytics` module not re-exported from lib.rs:** Free functions in `crates/cupel/src/analytics.rs` must be `pub use analytics::{budget_utilization, kind_diversity, timestamp_coverage}` in `lib.rs` to be accessible to callers.

## Open Risks

- **Consumption test project wiring:** The consumption tests use local package references from `./nupkg`. In local dev (not CI), `dotnet pack` must be run first before the consumption tests can reference `Wollax.Cupel.Testing`. This is the established pattern for all 4 existing packages — same workflow applies.
- **`SelectionReportAssertionChain` method return type inference:** All 13 methods return `SelectionReportAssertionChain` (not `void`, not `this`). The implementation must explicitly `return this;` in each method — Roslyn will error if a branch returns `void` or if `TreatWarningsAsErrors` catches a missing return.
- **Rust `KindDiversity` return type:** Returns `usize` (count of distinct kinds). Must import or iterate `included` items via `report.included.iter().map(|i| &i.item.kind).collect::<HashSet<_>>().len()`. `ContextKind` implements `Hash + Eq` (confirmed in model/mod.rs).

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| .NET NuGet packaging | none found | none found |
| xUnit/TUnit assertion patterns | none found | none found |

## Sources

- `spec/src/testing/vocabulary.md` — Complete 13-pattern spec; all patterns fully specified with error message formats, edge cases, predicate types (HIGH confidence)
- `spec/src/analytics/budget-simulation.md` — Confirms `BudgetUtilization` denominator is `budget.MaxTokens`; analytics methods belong in core (D045)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` — `TimestampCoverage()` confirmed as fourth analytics method; return type `double` / `f64`; returns `0.0` when `Included` is empty (HIGH confidence)
- `src/Wollax.Cupel.Json/Wollax.Cupel.Json.csproj` — Template for new Wollax.Cupel.Testing csproj structure (HIGH confidence)
- `.github/workflows/release.yml` — Wildcard `dotnet pack` confirms no workflow changes needed (HIGH confidence)
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` — Opt-out of central package management; `Version="*-*"` pattern for adding new package reference (HIGH confidence)
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — Current field types confirmed; `CountRequirementShortfalls` is non-required with default `[]` (S03 output) (HIGH confidence)
- `crates/cupel/src/model/context_item.rs` — `timestamp: Option<DateTime<Utc>>` at line 38 (HIGH confidence)
