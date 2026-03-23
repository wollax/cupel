---
id: T02
parent: S04
milestone: M003
provides:
  - Wollax.Cupel.Testing NuGet package project (IsPackable, ProjectReference to Wollax.Cupel, PublicApiAnalyzers)
  - SelectionReportAssertionException: sealed class inheriting Exception with single string-message constructor
  - SelectionReportAssertionChain: sealed fluent chain wrapping SelectionReport; 7 assertion methods each returning this
  - SelectionReportExtensions.Should(): extension entry point returning SelectionReportAssertionChain
  - Patterns 1–7: IncludeItemWithKind, IncludeItemMatching, IncludeExactlyNItemsWithKind, ExcludeItemWithReason, ExcludeItemMatchingWithReason, HaveExcludedItemWithBudgetExceeded, HaveNoExclusionsForKind
  - PublicAPI.Shipped.txt (empty, #nullable enable) + PublicAPI.Unshipped.txt (all 10 public members declared)
  - Cupel.slnx updated to include Wollax.Cupel.Testing in /src/ folder
key_files:
  - src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj
  - src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs
  - src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs
  - src/Wollax.Cupel.Testing/SelectionReportExtensions.cs
  - src/Wollax.Cupel.Testing/PublicAPI.Shipped.txt
  - src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt
  - Cupel.slnx
key_decisions:
  - SelectionReportAssertionException inherits directly from Exception (not FluentAssertions.AssertionException), per D041/D066; no third-party dependency in Wollax.Cupel.Testing
  - SelectionReportAssertionChain constructor is internal; not listed in PublicAPI.Unshipped.txt
  - Pattern 6 (HaveExcludedItemWithBudgetExceeded) uses degenerate form: single Func<ContextItem, bool> predicate (no token-count params) because .NET ExclusionReason is a flat enum with no token-detail fields
  - No InternalsVisibleTo added to Testing csproj (no internal test project yet, per task plan)
patterns_established:
  - Fluent assertion chain pattern: internal constructor + public methods returning this; caller entry via Should() static extension
  - Error message format: "{AssertionName}({params}) failed: {what was expected}. {what was actually found}" — spec-defined, structured, all assertion methods follow this shape
  - PublicAPI.Shipped.txt empty for new packages (only #nullable enable header)
observability_surfaces:
  - SelectionReportAssertionException.Message: structured failure message per pattern; contains assertion name, expected, actual on every throw
  - dotnet build src/Wollax.Cupel.Testing/ 2>&1 | grep error — RS0016 for missing PublicAPI entries; CS0161 for missing return on chain methods
duration: 20min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T02: Wollax.Cupel.Testing csproj + Chain Plumbing + Patterns 1–7

**Created Wollax.Cupel.Testing NuGet package with SelectionReportAssertionChain, SelectionReportAssertionException, Should() entry point, and assertion patterns 1–7 (3 Inclusion + 4 Exclusion); build is clean at 0 errors, 0 warnings.**

## What Happened

Created the `Wollax.Cupel.Testing` project directory and all 6 source files from scratch. The csproj follows the `Wollax.Cupel.Json` template: `IsPackable=true`, `ProjectReference` to `../Wollax.Cupel/Wollax.Cupel.csproj`, `PublicApiAnalyzers` with `PrivateAssets="All"`, and `AdditionalFiles` for both PublicAPI files. No `InternalsVisibleTo` was added.

`SelectionReportAssertionException` is a sealed class inheriting directly from `Exception` — no FluentAssertions or framework types involved, matching D041/D066.

`SelectionReportAssertionChain` uses `private readonly SelectionReport _report` with an `internal` constructor (not in PublicAPI.Unshipped.txt). All 7 assertion methods return `this` for chaining. Error messages follow the spec format exactly: `{AssertionName}({params}) failed: {expected}. {actual detail}`. Pattern 6 (`HaveExcludedItemWithBudgetExceeded`) uses the degenerate .NET form since `ExclusionReason` is a flat enum with no token-count payload.

`SelectionReportExtensions.Should()` is a static extension on `SelectionReport` in the `Wollax.Cupel.Testing` namespace that constructs and returns a `SelectionReportAssertionChain`.

`PublicAPI.Shipped.txt` contains only `#nullable enable` (empty shipped list for a new package). `PublicAPI.Unshipped.txt` was populated after an initial build surfaced all 12 RS0016 errors, then revised to list all 10 public members with correct nullability annotations.

`Cupel.slnx` was updated to include the new project under `/src/`.

## Verification

- `dotnet build src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj 2>&1 | grep " error "` → no output (0 errors)
- `grep "IsPackable.*true" src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` → match
- `grep "ProjectReference" ... | grep Wollax.Cupel.csproj` → match
- `grep "SelectionReportAssertionException : Exception" ...` → match
- `grep "public.*Should" src/Wollax.Cupel.Testing/SelectionReportExtensions.cs` → match
- `dotnet build 2>&1 | grep " error " | grep -v "^$"` → no output; full solution 0 errors, 0 warnings
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj 2>&1 | grep " error "` → no output (PublicAPI compliance maintained)

## Diagnostics

- Build failures → `dotnet build src/Wollax.Cupel.Testing/ 2>&1 | grep error`: RS0016 for missing PublicAPI entries; CS0161 for methods with missing `return this;`
- Assertion failures → `SelectionReportAssertionException.Message` carries structured failure text; visible directly in test output without additional tooling
- Pack → `dotnet pack src/Wollax.Cupel.Testing/ --output ./nupkg && ls ./nupkg/Wollax.Cupel.Testing.*.nupkg` (not run in T02; run in T03 or slice verification)

## Deviations

None. All 7 patterns implemented exactly per spec. Pattern 6 degenerate form applied as planned.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — new; NuGet package project with PublicApiAnalyzers
- `src/Wollax.Cupel.Testing/PublicAPI.Shipped.txt` — new; `#nullable enable` header only (empty shipped list)
- `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — new; all 10 public members declared
- `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs` — new; sealed Exception subclass
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — new; ~140 lines; 7 assertion methods
- `src/Wollax.Cupel.Testing/SelectionReportExtensions.cs` — new; ~15 lines; Should() entry point
- `Cupel.slnx` — modified; added Wollax.Cupel.Testing to /src/ folder
