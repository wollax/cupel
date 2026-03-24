# S01: SelectionReport structural equality — UAT

**Milestone:** M004
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: Equality is a pure function with no I/O, UI, or runtime behavior — correctness is fully proven by unit tests exercising `==` on constructed types in both languages

## Preconditions

- Rust toolchain installed (`cargo` available)
- .NET SDK installed (`dotnet` available)
- Repository checked out with all changes from S01

## Smoke Test

Run `cargo test --all-targets` in `crates/cupel/` and `dotnet test --configuration Release` at repo root. Both should pass with zero failures.

## Test Cases

### 1. Rust SelectionReport equality

1. Run `cargo test -p cupel --test equality selection_report_clone_equals`
2. **Expected:** Test passes — cloned SelectionReport equals original

### 2. Rust IncludedItem inequality

1. Run `cargo test -p cupel --test equality included_item_different_content_not_equal`
2. **Expected:** Test passes — IncludedItems with different content are not equal

### 3. .NET ContextItem metadata equality

1. Run `dotnet test --configuration Release --filter "ContextItemEqualityTests"`
2. **Expected:** All 14 tests pass — identical metadata compares equal, different metadata compares unequal, null metadata values handled correctly

### 4. .NET SelectionReport end-to-end equality

1. Run `dotnet test --configuration Release --filter "SelectionReportEqualityTests"`
2. **Expected:** All 14 tests pass — full reports with nested ContextItems compare correctly via `==`

## Edge Cases

### NaN scores in Rust

1. Run `cargo test -p cupel --test equality` and look for the NaN-related test
2. **Expected:** Two reports with NaN scores compare as NOT equal (IEEE 754: NaN != NaN)

### Empty reports in .NET

1. The SelectionReportEqualityTests include empty-list edge cases
2. **Expected:** Empty SelectionReports compare equal; report with items != empty report

### Null DeduplicatedAgainst in .NET

1. The SelectionReportEqualityTests include null DeduplicatedAgainst cases
2. **Expected:** ExcludedItem with null DeduplicatedAgainst equals another with null; differs from one with non-null

## Failure Signals

- `cargo test` failures in `equality.rs` — Rust equality broken
- `dotnet test` failures in `ContextItemEqualityTests` or `SelectionReportEqualityTests` — .NET equality broken
- `dotnet build` warnings about PublicAPI — surface not declared correctly
- `cargo clippy` warnings — derive issues

## Requirements Proved By This UAT

- R050 — SelectionReport, IncludedItem, and ExcludedItem structural equality in both languages with exact f64 comparison; PartialEq (Rust) + IEquatable (.NET); all existing tests pass

## Not Proven By This UAT

- Runtime performance of equality comparison under high-frequency usage (not a requirement)
- Hash distribution quality of GetHashCode implementations (pragmatic O(1) — sufficient for general use)

## Notes for Tester

- Rust `PartialEq` but not `Eq` is intentional (f64 fields) — do not try to use reports as HashMap keys
- .NET records auto-generate `operator ==`/`!=` from the custom `Equals` — no separate operator test needed
- All tests are deterministic (no random data, no time dependency)
