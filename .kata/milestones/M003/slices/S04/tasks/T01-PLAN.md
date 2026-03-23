---
estimated_steps: 7
estimated_files: 4
---

# T01: Rust + .NET Analytics Extension Methods

**Slice:** S04 — Core Analytics + Cupel.Testing Package
**Milestone:** M003

## Description

Add `BudgetUtilization`, `KindDiversity`, and `TimestampCoverage` as free functions on `&SelectionReport` in Rust and as extension methods on `SelectionReport` in .NET. These belong in the core packages (D045) — zero external dependencies. All three return primitive types. This task is a prerequisite for T02's `HaveBudgetUtilizationAbove` pattern (which delegates to `BudgetUtilization`) and establishes the R021 analytics surface.

## Steps

1. **Create `crates/cupel/src/analytics.rs`**: Write three `pub fn` free functions:
   - `pub fn budget_utilization(report: &SelectionReport, budget: &ContextBudget) -> f64`: sum `report.included.iter().map(|i| i.item.tokens() as f64).sum::<f64>() / budget.max_tokens() as f64`; no division-by-zero needed (ContextBudget validates MaxTokens > 0)
   - `pub fn kind_diversity(report: &SelectionReport) -> usize`: `report.included.iter().map(|i| &i.item.kind).collect::<std::collections::HashSet<_>>().len()`; returns `0` when `included` is empty
   - `pub fn timestamp_coverage(report: &SelectionReport) -> f64`: if `report.included.is_empty()` return `0.0`; else `report.included.iter().filter(|i| i.item.timestamp().is_some()).count() as f64 / report.included.len() as f64`
   - Add 3 unit test functions at the bottom of the file covering basic cases for each function (empty included → 0/0.0, non-empty cases)

2. **Wire into `crates/cupel/src/lib.rs`**: Add `pub mod analytics;` and `pub use analytics::{budget_utilization, kind_diversity, timestamp_coverage};` alongside existing pub uses; place after `pub mod diagnostics`

3. **Run `cargo test --all-targets`** to confirm Rust analytics compile and tests pass; check no clippy issues

4. **Create `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs`**: `public static class SelectionReportExtensions` in `namespace Wollax.Cupel.Diagnostics`:
   - `public static double BudgetUtilization(this SelectionReport report, ContextBudget budget)`: `report.Included.Sum(i => (double)i.Item.Tokens) / budget.MaxTokens`
   - `public static int KindDiversity(this SelectionReport report)`: `report.Included.Select(i => i.Item.Kind).Distinct().Count()`
   - `public static double TimestampCoverage(this SelectionReport report)`: `report.Included.Count == 0 ? 0.0 : report.Included.Count(i => i.Item.Timestamp.HasValue) / (double)report.Included.Count`
   - Use LINQ; no new using directives beyond `System.Linq` (which is available via global usings)

5. **Update `src/Wollax.Cupel/PublicAPI.Unshipped.txt`**: Add entries for the static class and all three extension methods. If `SelectionReportExtensions` is `public`, must list: `static Wollax.Cupel.Diagnostics.SelectionReportExtensions` (class declaration line), and each method signature. Check RS0016 format by looking at existing entries in the file for formatting guidance.

6. **Run `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj`**: Must exit 0 with 0 errors and 0 warnings. If RS0016 fires, add the missing entry to PublicAPI.Unshipped.txt immediately.

7. **Run `dotnet test`** to confirm full suite still passes and new extension methods don't regress anything.

## Must-Haves

- [ ] `budget_utilization`, `kind_diversity`, `timestamp_coverage` exist in `crates/cupel/src/analytics.rs` as `pub fn`
- [ ] All three are `pub use`-d from `crates/cupel/src/lib.rs`
- [ ] `KindDiversity` returns `usize` (count, not ratio); `BudgetUtilization` and `TimestampCoverage` return `f64`
- [ ] `TimestampCoverage` returns `0.0` when `included` is empty (not NaN or panic)
- [ ] `BudgetUtilization`, `KindDiversity`, `TimestampCoverage` exist in `SelectionReportExtensions.cs` as extension methods on `SelectionReport`
- [ ] `KindDiversity` returns `int` in .NET (count, not ratio)
- [ ] `TimestampCoverage` returns `0.0` when `Included.Count == 0`
- [ ] `PublicAPI.Unshipped.txt` updated with all new entries from `SelectionReportExtensions`
- [ ] `cargo test --all-targets` exits 0
- [ ] `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` exits 0 with 0 errors

## Verification

- `cargo test -- analytics --nocapture` → analytics unit tests pass with output visible
- `grep "budget_utilization\|kind_diversity\|timestamp_coverage" crates/cupel/src/lib.rs` → 3 matches
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj 2>&1 | grep " error "` → no output
- `grep "SelectionReportExtensions\|BudgetUtilization\|KindDiversity\|TimestampCoverage" src/Wollax.Cupel/PublicAPI.Unshipped.txt` → matches for all 4
- `dotnet test` → 0 failed (regression check)

## Observability Impact

- Signals added/changed: None — pure computation functions with no side effects or logging
- How a future agent inspects this: `cargo test -- analytics --nocapture` for Rust; `dotnet test --filter "Analytics"` for .NET (if tests added in a later task); `grep` on lib.rs for re-export presence
- Failure state exposed: RS0016 build error if PublicAPI.Unshipped.txt is incomplete — error message names the missing member; Rust compile error if analytics module is not `pub mod`-ed

## Inputs

- `crates/cupel/src/diagnostics/mod.rs` — `SelectionReport`, `IncludedItem` field shapes (`included: Vec<IncludedItem>`, `item: ContextItem`, `item.timestamp()`, `item.tokens()`, `item.kind`)
- `crates/cupel/src/model/context_item.rs` — `timestamp: Option<DateTime<Utc>>` at line 38; `fn timestamp(&self) -> Option<DateTime<Utc>>`; `fn tokens(&self) -> i64`
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — `Included: IReadOnlyList<IncludedItem>`; field shapes
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs` — `Item: ContextItem`, `Score: double`, `Reason: InclusionReason`
- `src/Wollax.Cupel/ContextBudget.cs` — `MaxTokens: int`; validates > 0 at construction so no division-by-zero
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — format of existing entries to match for new ones
- S01/S02/S03 summaries — confirm SelectionReport field names, no surprises

## Expected Output

- `crates/cupel/src/analytics.rs` — new; ~60 lines; 3 pub fns + 3 unit tests
- `crates/cupel/src/lib.rs` — modified; +2 lines (`pub mod analytics` + `pub use analytics::...`)
- `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` — new; ~30 lines; 3 extension methods on SelectionReport
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — modified; ~4 new entries (class + 3 methods)
