---
estimated_steps: 7
estimated_files: 7
---

# T02: Wollax.Cupel.Testing csproj + Chain Plumbing + Patterns 1–7

**Slice:** S04 — Core Analytics + Cupel.Testing Package
**Milestone:** M003

## Description

Create the `Wollax.Cupel.Testing` NuGet package project from scratch and implement the first 7 of 13 assertion patterns: the 3 Inclusion group patterns and 4 Exclusion group patterns. This task establishes the package structure, the chain plumbing (`SelectionReportAssertionChain`, `SelectionReportAssertionException`, `Should()` entry point), and the most-used assertion surface. T03 adds the remaining 6 patterns and the test project.

## Steps

1. **Create `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj`**: Copy `Wollax.Cupel.Json.csproj` as the template — `IsPackable=true`, `ProjectReference` to `../Wollax.Cupel/Wollax.Cupel.csproj`, `PublicApiAnalyzers` PackageReference with `PrivateAssets="All"`, `AdditionalFiles` for both `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`; add `Description` element. Do NOT add `InternalsVisibleTo` — this package has no internal test project of its own yet.

2. **Create `src/Wollax.Cupel.Testing/PublicAPI.Shipped.txt`**: Content is exactly `#nullable enable` on the first line (matching the Wollax.Cupel.Json pattern — empty shipped list, new package).

3. **Create `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs`**: `namespace Wollax.Cupel.Testing;` — `public sealed class SelectionReportAssertionException : Exception` with a single constructor `public SelectionReportAssertionException(string message) : base(message) { }`. Must inherit from `Exception` directly (D041, D066) — NOT from `FluentAssertions.AssertionException` or any third-party type.

4. **Create `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs`**: `namespace Wollax.Cupel.Testing;` — `public sealed class SelectionReportAssertionChain` with a `private readonly SelectionReport _report;` field and `internal SelectionReportAssertionChain(SelectionReport report) { _report = report; }`. Implement patterns 1–7 as instance methods, each returning `SelectionReportAssertionChain`:

   **Pattern 1 — `IncludeItemWithKind(ContextKind kind)`**: `if (!_report.Included.Any(i => i.Item.Kind == kind))` → throw with: `$"IncludeItemWithKind({kind}) failed: Included contained 0 items with Kind={kind}. Included had {_report.Included.Count} items with kinds: [{string.Join(", ", _report.Included.Select(i => i.Item.Kind.ToString()).Distinct())}]."`

   **Pattern 2 — `IncludeItemMatching(Func<IncludedItem, bool> predicate)`**: `if (!_report.Included.Any(predicate))` → throw with: `$"IncludeItemMatching failed: no item in Included matched the predicate. Included had {_report.Included.Count} items."`

   **Pattern 3 — `IncludeExactlyNItemsWithKind(ContextKind kind, int n)`**: `var actual = _report.Included.Count(i => i.Item.Kind == kind); if (actual != n)` → throw with: `$"IncludeExactlyNItemsWithKind({kind}, {n}) failed: expected {n} items with Kind={kind} in Included, but found {actual}. Included had {_report.Included.Count} items total."`

   **Pattern 4 — `ExcludeItemWithReason(ExclusionReason reason)`**: `if (!_report.Excluded.Any(e => e.Reason == reason))` → throw with: `$"ExcludeItemWithReason({reason}) failed: no excluded item had reason {reason}. Excluded had {_report.Excluded.Count} items with reasons: [{string.Join(", ", _report.Excluded.Select(e => e.Reason.ToString()).Distinct())}]."`

   **Pattern 5 — `ExcludeItemMatchingWithReason(Func<ContextItem, bool> predicate, ExclusionReason reason)`**: `var predicateMatches = _report.Excluded.Where(e => predicate(e.Item)).ToList(); if (!predicateMatches.Any(e => e.Reason == reason))` → throw with: `$"ExcludeItemMatchingWithReason(reason={reason}) failed: predicate matched {predicateMatches.Count} excluded item(s) but none had reason {reason}. Matched items had reasons: [{string.Join(", ", predicateMatches.Select(e => e.Reason.ToString()).Distinct())}]."`

   **Pattern 6 — `HaveExcludedItemWithBudgetExceeded(Func<ContextItem, bool> predicate)`**: .NET degenerate form (spec language note — flat enum has no token detail fields). `if (!_report.Excluded.Any(e => predicate(e.Item) && e.Reason == ExclusionReason.BudgetExceeded))` → throw with: `$"HaveExcludedItemWithBudgetExceeded failed: no excluded item matching the predicate had reason BudgetExceeded."`

   **Pattern 7 — `HaveNoExclusionsForKind(ContextKind kind)`**: `var matching = _report.Excluded.Where(e => e.Item.Kind == kind).ToList(); if (matching.Any())` → throw with: `$"HaveNoExclusionsForKind({kind}) failed: found {matching.Count} excluded item(s) with Kind={kind}. First: score={matching[0].Score:F4}, reason={matching[0].Reason}."` (highest-scored first because excluded is sorted score-descending)

   All methods use `using Wollax.Cupel.Diagnostics;` and `using System.Linq;` (via usings).

5. **Create `src/Wollax.Cupel.Testing/SelectionReportExtensions.cs`**: `namespace Wollax.Cupel.Testing;` — `public static class SelectionReportExtensions` with: `public static SelectionReportAssertionChain Should(this SelectionReport report) => new SelectionReportAssertionChain(report);` — uses `using Wollax.Cupel.Diagnostics;`

6. **Create `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt`**: Add all public members: `SelectionReportAssertionException`, its constructor, `SelectionReportAssertionChain` class, its constructor (if internal, skip), all 7 assertion method signatures, `SelectionReportExtensions`, and the `Should()` extension method. Run `dotnet build` first and fix any RS0016 errors by adding the missing members.

7. **Run `dotnet build src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj`**: Must exit 0 with 0 errors and 0 warnings. Fix any RS0016 or type-resolution errors before proceeding.

## Must-Haves

- [ ] `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` exists with `IsPackable=true` and `ProjectReference` to Wollax.Cupel
- [ ] `SelectionReportAssertionException` inherits from `Exception` (NOT from any FluentAssertions or framework type)
- [ ] `SelectionReportAssertionChain` wraps `SelectionReport` with `_report` field; all 7 methods return `SelectionReportAssertionChain` (return `this`)
- [ ] `SelectionReport.Should()` extension method exists in `Wollax.Cupel.Testing` namespace and returns `SelectionReportAssertionChain`
- [ ] Patterns 1–7 implemented with error messages matching the spec format exactly
- [ ] Pattern 6 uses the .NET degenerate form: `HaveExcludedItemWithBudgetExceeded(Func<ContextItem, bool>)` (no token-count params per language note)
- [ ] `PublicAPI.Shipped.txt` exists (empty, `#nullable enable` header only)
- [ ] `PublicAPI.Unshipped.txt` lists all public members
- [ ] `dotnet build src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` exits 0 with 0 errors

## Verification

- `dotnet build src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj 2>&1 | grep " error "` → no output
- `grep "IsPackable.*true" src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` → match
- `grep "ProjectReference" src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj | grep Wollax.Cupel.csproj` → match
- `grep "SelectionReportAssertionException : Exception" src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs` → match
- `grep "public.*Should" src/Wollax.Cupel.Testing/SelectionReportExtensions.cs` → match
- `dotnet build 2>&1 | grep " error " | grep -v "^$"` → no output (full solution build clean)

## Observability Impact

- Signals added/changed: `SelectionReportAssertionException.Message` — structured failure message containing assertion name, expected value, and actual value for every failing assertion; error message format is spec-defined
- How a future agent inspects this: `dotnet build src/Wollax.Cupel.Testing/ 2>&1 | grep error` for build failures; test output shows exception message on assertion failures directly
- Failure state exposed: RS0016 errors on missing PublicAPI entries are compiler-visible; wrong return type on assertion methods surfaces as Roslyn error; missing `return this;` causes CS0161 (not all paths return a value)

## Inputs

- `src/Wollax.Cupel.Json/Wollax.Cupel.Json.csproj` — template for new csproj structure (IsPackable, ProjectReference, PublicApiAnalyzers pattern)
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — `Included`, `Excluded` field types
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — flat enum; .NET has no token-detail fields on BudgetExceeded variant → use degenerate form for pattern 6
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs` — fields: `Item`, `Score`, `Reason`
- `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs` — fields: `Item`, `Score`, `Reason`
- `spec/src/testing/vocabulary.md` — exact error message format for each pattern; PD-4 (Should() entry point, SelectionReportAssertionException)
- T01 output: `SelectionReportExtensions.cs` exists in Wollax.Cupel core — the Testing package references Wollax.Cupel, so BudgetUtilization() is available if needed

## Expected Output

- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — new; ~20 lines
- `src/Wollax.Cupel.Testing/PublicAPI.Shipped.txt` — new; 1 line (`#nullable enable`)
- `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — new; ~15–20 entries
- `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs` — new; ~10 lines
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — new; ~150 lines; 7 assertion methods
- `src/Wollax.Cupel.Testing/SelectionReportExtensions.cs` — new; ~12 lines; Should() entry point
