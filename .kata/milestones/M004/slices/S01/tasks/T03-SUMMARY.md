---
id: T03
parent: S01
milestone: M004
provides:
  - IEquatable<IncludedItem> with custom Equals/GetHashCode
  - IEquatable<ExcludedItem> with custom Equals/GetHashCode and null-safe DeduplicatedAgainst
  - IEquatable<SelectionReport> with SequenceEqual for all four list properties
  - PublicAPI.Unshipped.txt updated for all new equality surface
  - 14 equality test cases in SelectionReportEqualityTests.cs
key_files:
  - src/Wollax.Cupel/Diagnostics/IncludedItem.cs
  - src/Wollax.Cupel/Diagnostics/ExcludedItem.cs
  - src/Wollax.Cupel/Diagnostics/SelectionReport.cs
  - tests/Wollax.Cupel.Tests/Diagnostics/SelectionReportEqualityTests.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
key_decisions:
  - "ExcludedItem uses Equals(DeduplicatedAgainst, other.DeduplicatedAgainst) for null-safe comparison of optional ContextItem"
  - "SelectionReport.GetHashCode uses list counts only (pragmatic O(1)) — consistent with T02 ContextItem pattern"
  - "TraceEvent and CountRequirementShortfall auto-generated equality confirmed correct — no custom overrides needed"
patterns_established:
  - "Collection-aware record equality pattern (from T02) extended to nested types: IEquatable<T> + custom Equals with SequenceEqual for lists + pragmatic O(1) GetHashCode"
observability_surfaces:
  - none — equality is a pure function; test assertions show expected vs actual on failure
duration: 10min
verification_result: passed
completed_at: 2026-03-23T12:00:00Z
blocker_discovered: false
---

# T03: Implement IEquatable on .NET SelectionReport, IncludedItem, ExcludedItem

**Custom deep equality on SelectionReport, IncludedItem, and ExcludedItem with SequenceEqual for all list properties and null-safe DeduplicatedAgainst handling**

## What Happened

Added `IEquatable<T>` with custom `Equals`/`GetHashCode` overrides to all three diagnostic types. `IncludedItem` compares `Item` (leveraging T02's deep ContextItem equality), `Score` (exact `==` per D103), and `Reason`. `ExcludedItem` adds null-safe comparison for `DeduplicatedAgainst` using `Equals(a, b)`. `SelectionReport` compares two scalar fields plus `SequenceEqual` on all four list properties (`Events`, `Included`, `Excluded`, `CountRequirementShortfalls`). `GetHashCode` uses pragmatic O(1) list contributions (count only) consistent with the T02 pattern.

`TraceEvent` (readonly record struct with primitives + enum + nullable string) and `CountRequirementShortfall` (positional record with ContextKind + ints) were confirmed to have correct auto-generated equality — no custom overrides needed.

Updated `PublicAPI.Unshipped.txt` with all six new public members (Equals + GetHashCode for each type).

## Verification

- `dotnet build --configuration Release` — 0 errors, 0 warnings (14 projects)
- `dotnet test --configuration Release` — 764 tests passed, 0 failed (includes 14 new equality tests)
- `cargo test --all-targets` — 143 tests passed (no Rust regressions)

## Diagnostics

None — equality is a pure function with no runtime observability surface. Test assertions show expected vs actual values on failure.

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs` — Added IEquatable<IncludedItem> with custom Equals/GetHashCode
- `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs` — Added IEquatable<ExcludedItem> with null-safe DeduplicatedAgainst comparison
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — Added IEquatable<SelectionReport> with SequenceEqual for all four list properties
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Added 6 new public API entries for equality members
- `tests/Wollax.Cupel.Tests/Diagnostics/SelectionReportEqualityTests.cs` — 14 equality test cases
