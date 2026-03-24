---
estimated_steps: 5
estimated_files: 5
---

# T03: Implement IEquatable on .NET SelectionReport, IncludedItem, ExcludedItem

**Slice:** S01 — SelectionReport structural equality
**Milestone:** M004

## Description

Add custom `Equals`/`GetHashCode` overrides to `SelectionReport`, `IncludedItem`, and `ExcludedItem` with collection-aware equality for their `IReadOnlyList<T>` properties. Depends on T02's `ContextItem` equality being correct — these types nest `ContextItem` via their `Item` property. After this task, `report1 == report2` works correctly for independently constructed reports in .NET.

## Steps

1. In `src/Wollax.Cupel/Diagnostics/IncludedItem.cs`:
   - Add `IEquatable<IncludedItem>` to the record declaration
   - Override `Equals(IncludedItem? other)`: compare `Item` (uses T02's deep equality), `Score` (exact `==` per D103), `Reason` (enum equality)
   - Override `GetHashCode()`: `HashCode.Combine(Item, Score, Reason)`
2. In `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs`:
   - Add `IEquatable<ExcludedItem>` to the record declaration
   - Override `Equals(ExcludedItem? other)`: compare `Item`, `Score`, `Reason`, and `DeduplicatedAgainst` (null-safe via `Equals(DeduplicatedAgainst, other.DeduplicatedAgainst)`)
   - Override `GetHashCode()`: `HashCode.Combine(Item, Score, Reason, DeduplicatedAgainst)`
3. In `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`:
   - Add `IEquatable<SelectionReport>` to the record declaration
   - Override `Equals(SelectionReport? other)`: compare scalar fields (`TotalCandidates`, `TotalTokensConsidered`) + `Events.SequenceEqual(other.Events)` + `Included.SequenceEqual(other.Included)` + `Excluded.SequenceEqual(other.Excluded)` + `CountRequirementShortfalls.SequenceEqual(other.CountRequirementShortfalls)`
   - Override `GetHashCode()`: combine scalars + list counts (pragmatic hash; avoid O(n))
   - Note: `TraceEvent` is a `readonly record struct` with only primitives + enum + nullable string — its auto-generated equality is already correct. `CountRequirementShortfall` is a positional record with `ContextKind` (has IEquatable) + ints — also already correct.
4. Update `src/Wollax.Cupel/PublicAPI.Unshipped.txt` with all new public surface:
   - `IncludedItem.Equals(IncludedItem?)`, `IncludedItem.GetHashCode()`
   - `ExcludedItem.Equals(ExcludedItem?)`, `ExcludedItem.GetHashCode()`
   - `SelectionReport.Equals(SelectionReport?)`, `SelectionReport.GetHashCode()`
   - Any operator entries the analyzer requires
5. Create `tests/Wollax.Cupel.Tests/Diagnostics/SelectionReportEqualityTests.cs`:
   - Two identical `SelectionReport` instances compare equal (full report with events, included, excluded)
   - Reports with different `TotalCandidates` compare unequal
   - Reports with different `Included` items compare unequal
   - Reports with different `Excluded` items compare unequal
   - Reports with different `Events` compare unequal
   - Reports with different `CountRequirementShortfalls` compare unequal
   - Empty reports (all empty lists) compare equal
   - `IncludedItem` equality: same item+score+reason → equal
   - `ExcludedItem` equality: same item+score+reason+null DeduplicatedAgainst → equal
   - `ExcludedItem` with different `DeduplicatedAgainst` → unequal
   - `ExcludedItem` with null vs non-null `DeduplicatedAgainst` → unequal
   - GetHashCode: equal reports produce same hash
6. Run full verification:
   - `dotnet test --configuration Release`
   - `dotnet build --configuration Release` (0 warnings)
   - `cargo test --all-targets` (ensure no Rust regressions)

## Must-Haves

- [ ] `IEquatable<T>` with custom `Equals`/`GetHashCode` on `SelectionReport`, `IncludedItem`, `ExcludedItem`
- [ ] `SelectionReport.Equals` uses `SequenceEqual` for all four list properties
- [ ] `ExcludedItem.Equals` handles nullable `DeduplicatedAgainst` correctly
- [ ] `PublicAPI.Unshipped.txt` updated — build produces 0 warnings
- [ ] ≥12 test cases in `SelectionReportEqualityTests.cs`
- [ ] `dotnet test --configuration Release` passes
- [ ] `cargo test --all-targets` passes (no Rust regressions)

## Verification

- `dotnet build --configuration Release` — 0 errors, 0 warnings
- `dotnet test --configuration Release` — all existing + new equality tests pass
- `cargo test --all-targets` — Rust still green (no cross-language regressions)

## Observability Impact

- Signals added/changed: None
- How a future agent inspects this: `dotnet test` exercises all equality paths
- Failure state exposed: None

## Inputs

- T02 output: `ContextItem` with working deep equality (Tags, Metadata)
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — current sealed record, no custom equality
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs` — current sealed record
- `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs` — current sealed record with nullable `DeduplicatedAgainst`
- S01-RESEARCH.md — confirms `TraceEvent` and `CountRequirementShortfall` auto-generated equality is correct

## Expected Output

- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — custom equality with SequenceEqual for lists
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs` — custom equality
- `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs` — custom equality with null-safe DeduplicatedAgainst
- `tests/Wollax.Cupel.Tests/Diagnostics/SelectionReportEqualityTests.cs` — ≥12 equality tests
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — updated with all equality surface
