# S01: SelectionReport structural equality

**Goal:** `SelectionReport`, `IncludedItem`, and `ExcludedItem` support `==` comparison in both Rust and .NET with exact f64 equality; all existing tests pass; downstream slices can compare reports programmatically.
**Demo:** `report1 == report2` returns `true` for structurally identical reports and `false` for differing ones in both languages.

## Must-Haves

- Rust: `PartialEq` derived on `IncludedItem`, `ExcludedItem`, `SelectionReport`, `TraceEvent`, `CountRequirementShortfall`, and `OverflowEvent`
- .NET: `IEquatable<T>` with custom `Equals`/`GetHashCode` overrides on `ContextItem`, `SelectionReport`, `IncludedItem`, and `ExcludedItem` — collection-aware (ordered `SequenceEqual` for lists, element-wise for dictionaries)
- Exact f64 comparison in both languages (D103) — no epsilon
- `PublicAPI.Unshipped.txt` updated with all new public surface (IEquatable implementations, operator overloads)
- `cargo test --all-targets` passes (including serde feature)
- `dotnet test --configuration Release` passes
- R050 fully satisfied

## Proof Level

- This slice proves: contract (structural equality correctness via unit tests in both languages)
- Real runtime required: no (unit tests exercise equality directly on constructed types)
- Human/UAT required: no

## Verification

- `cargo test --all-targets` — all existing + new equality tests pass
- `cargo test --all-targets --features serde` — serde round-trip still works
- `dotnet test --configuration Release` — all existing + new equality tests pass
- `cargo clippy --all-targets -- -D warnings` — no new warnings
- `dotnet build --configuration Release` — 0 errors, 0 warnings (PublicAPI analyzer clean)

## Observability / Diagnostics

- Runtime signals: none (equality is a pure function with no side effects or logging)
- Inspection surfaces: none (library code — unit test output is the inspection surface)
- Failure visibility: test assertion messages show expected vs actual values
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: existing `ContextItem`, `SelectionReport`, `IncludedItem`, `ExcludedItem`, `TraceEvent`, `CountRequirementShortfall` types in both languages
- New wiring introduced in this slice: `PartialEq` derives (Rust), `IEquatable<T>` + `Equals`/`GetHashCode`/`operator ==`/`operator !=` overrides (.NET)
- What remains before the milestone is truly usable end-to-end: S02 (fork diagnostic), S03 (IQuotaPolicy + QuotaUtilization), S04 (snapshot testing), S05 (Rust budget simulation)

## Tasks

- [x] **T01: Add PartialEq derives to Rust diagnostic types** `est:20m`
  - Why: Rust side is nearly complete — `ContextItem` and enums already have `PartialEq`; only the 6 structs (`IncludedItem`, `ExcludedItem`, `SelectionReport`, `TraceEvent`, `CountRequirementShortfall`, `OverflowEvent`) need it added
  - Files: `crates/cupel/src/diagnostics/mod.rs`, `crates/cupel/tests/equality.rs`
  - Do: Add `PartialEq` to derive macros on 6 structs; do NOT add `Eq` (f64 fields prevent it); create equality test file with tests for all types including edge cases (empty reports, NaN scores)
  - Verify: `cargo test --all-targets` and `cargo test --all-targets --features serde` and `cargo clippy --all-targets -- -D warnings`
  - Done when: `report1 == report2` compiles and returns correct results for identical and differing `SelectionReport` instances in Rust tests

- [x] **T02: Implement IEquatable on .NET ContextItem with collection-aware equality** `est:40m`
  - Why: `ContextItem` is the base type nested inside `IncludedItem` and `ExcludedItem`; its `Tags` (IReadOnlyList) and `Metadata` (IReadOnlyDictionary) need deep equality before the diagnostic types can be correct
  - Files: `src/Wollax.Cupel/ContextItem.cs`, `tests/Wollax.Cupel.Tests/Models/ContextItemEqualityTests.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
  - Do: Override `Equals(ContextItem?)`, `Equals(object?)`, `GetHashCode()`, add `operator ==`/`!=` on `ContextItem`; use `SequenceEqual` for Tags; element-wise key-value comparison for Metadata using `object.Equals()`; update PublicAPI.Unshipped.txt
  - Verify: `dotnet test --configuration Release` and `dotnet build --configuration Release` (0 warnings)
  - Done when: Two `ContextItem` instances with identical content, tags, metadata compare equal; instances with different tags/metadata compare unequal

- [x] **T03: Implement IEquatable on .NET SelectionReport, IncludedItem, ExcludedItem** `est:45m`
  - Why: The three diagnostic record types need custom equality overrides for their collection properties; depends on T02's ContextItem equality being correct
  - Files: `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`, `src/Wollax.Cupel/Diagnostics/IncludedItem.cs`, `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs`, `tests/Wollax.Cupel.Tests/Diagnostics/SelectionReportEqualityTests.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
  - Do: Override `Equals`/`GetHashCode` on all three types with `SequenceEqual` for list properties; null-safe comparison for `ExcludedItem.DeduplicatedAgainst`; use `==` for double Score fields (consistent with D103); update PublicAPI.Unshipped.txt; test identical vs differing reports, edge cases (empty lists, null DeduplicatedAgainst)
  - Verify: `dotnet test --configuration Release` and `dotnet build --configuration Release` (0 warnings) and `cargo test --all-targets`
  - Done when: Full `SelectionReport` equality works end-to-end including nested ContextItem comparison; all existing tests still pass in both languages

## Files Likely Touched

- `crates/cupel/src/diagnostics/mod.rs`
- `crates/cupel/tests/equality.rs`
- `src/Wollax.Cupel/ContextItem.cs`
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs`
- `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs`
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
- `tests/Wollax.Cupel.Tests/Models/ContextItemEqualityTests.cs`
- `tests/Wollax.Cupel.Tests/Diagnostics/SelectionReportEqualityTests.cs`
