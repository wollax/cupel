# S04: Core Analytics + Cupel.Testing Package — UAT

**Milestone:** M003
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All must-haves are mechanically verifiable via `cargo test`, `dotnet test`, `dotnet pack`, and `grep`-based artifact checks. No service is running; no human interaction is required to exercise the feature surface. The 26-test test project and consumption smoke test together constitute a complete integration proof for the assertion vocabulary.

## Preconditions

- Repo is on the `kata/M003/S04` branch (or main after merge)
- `dotnet` SDK available (net10.0)
- Rust toolchain available; `cargo` in PATH
- `./packages` local NuGet feed directory exists and contains `Wollax.Cupel.Testing.*.nupkg`

## Smoke Test

```bash
# From repo root
cd /Users/wollax/Git/personal/cupel
dotnet test --project tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj
# Expected: 26 passed, 0 failed
```

## Test Cases

### 1. Rust analytics functions are callable

```bash
cd crates/cupel
cargo test -- analytics --nocapture
```
**Expected:** 7 tests pass (budget_utilization_empty_is_zero, budget_utilization_full_budget, kind_diversity_empty_is_zero, kind_diversity_counts_distinct_kinds, timestamp_coverage_empty_is_zero, timestamp_coverage_all_have_timestamps, timestamp_coverage_partial); all assertions hold.

### 2. Rust analytics re-exported from lib.rs

```bash
grep "budget_utilization\|kind_diversity\|timestamp_coverage" crates/cupel/src/lib.rs
```
**Expected:** One line matching all three function names via `pub use analytics::{...}`.

### 3. .NET analytics build clean with no public API errors

```bash
dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj
```
**Expected:** 0 errors, 0 warnings. No RS0016 output.

### 4. Wollax.Cupel.Testing package builds

```bash
dotnet build src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj
```
**Expected:** 0 errors, 0 warnings.

### 5. Pack produces .nupkg

```bash
dotnet pack src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj --output ./nupkg
ls ./nupkg/Wollax.Cupel.Testing.*.nupkg
```
**Expected:** At least one `.nupkg` file exists.

### 6. All 26 assertion chain tests pass (one per pattern × 2 paths)

```bash
dotnet test --project tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj --verbosity normal
```
**Expected:** 26 tests pass; names include happy-path and failure-path variants for all 13 patterns; 0 failed.

### 7. Consumption test verifies standalone installability

```bash
dotnet test --project tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj
```
**Expected:** 6 tests pass (including `Testing_Package_Should_Extension_Compiles_And_Works`); 0 failed.

### 8. Full dotnet test suite is green

```bash
dotnet test
```
**Expected:** 708 tests passed, 0 failed.

### 9. Consumption csproj references Wollax.Cupel.Testing

```bash
grep "Wollax.Cupel.Testing" tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj
```
**Expected:** Match found (`PackageReference Include="Wollax.Cupel.Testing"`).

## Edge Cases

### Empty SelectionReport returns 0.0 / 0 for all analytics

Write a short script or test:
```csharp
var report = new SelectionReport { Events = [], Included = [], Excluded = [], TotalCandidates = 0, TotalTokensConsidered = 0 };
var budget = new ContextBudget(100, 0, new Dictionary<ContextKind, int>());
Assert.Equal(0.0, report.BudgetUtilization(budget));
Assert.Equal(0, report.KindDiversity());
Assert.Equal(0.0, report.TimestampCoverage());
```
**Expected:** All three return their zero-value without throwing.

### Assertion chain failure produces structured message

```csharp
var report = MakeReport(included: []);
try {
    report.Should().IncludeItemWithKind(ContextKind.SystemPrompt());
} catch (SelectionReportAssertionException ex) {
    Console.WriteLine(ex.Message); // must start with "IncludeItemWithKind("
}
```
**Expected:** `SelectionReportAssertionException` is thrown; `ex.Message` matches `"IncludeItemWithKind({kind}) failed: expected at least one included item with kind {kind}. Found 0 items with that kind."` format.

### PlaceItemAtEdge passes for first or last position

Pattern 12 passes when the matched item is at index 0 OR index count−1 — both are valid "edges."

## Failure Signals

- `dotnet test` reports any failed test → analytics or assertion behavior mismatch
- `dotnet build ... | grep error` produces output → PublicAPI.Unshipped.txt out of sync (RS0016) or missing `return this;` (CS0161)
- `ls ./nupkg/Wollax.Cupel.Testing.*.nupkg` produces no output → pack failed
- `dotnet test --project tests/Wollax.Cupel.ConsumptionTests/...` fails on smoke test → local feed copy missing from `./packages`
- `cargo test -- analytics` fails → analytics function logic error or ContextItem accessor change

## Requirements Proved By This UAT

- R021 (Cupel.Testing package) — all 13 assertion patterns are callable; `SelectionReport.Should()` entry point works; `SelectionReportAssertionException` carries structured failure messages; `Wollax.Cupel.Testing` NuGet package installs independently and runs against a real SelectionReport; `dotnet pack` produces a `.nupkg`; 26 TUnit tests and 1 consumption smoke test pass

## Not Proven By This UAT

- Rust equivalent of Wollax.Cupel.Testing — no assertion vocabulary in Rust; Rust callers use `cargo test` assertions directly
- OTel bridge integration (S05 concern) — `ITraceCollector` wiring to ActivitySource is out of scope for this UAT
- Budget simulation (S06 concern) — `GetMarginalItems` and `FindMinBudgetFor` extension methods not yet implemented
- Production NuGet feed publishing — local feed is sufficient for R021; release pipeline publishing is a release process concern

## Notes for Tester

- The local NuGet feed for consumption tests is `./packages` — if you re-pack and the consumption tests fail to restore, copy the latest `.nupkg` from `./nupkg` to `./packages` and re-run.
- `Version="*-*"` in the consumption csproj is a float-version selector that picks the latest pre-release; this requires the local feed to contain a matching package.
- All 26 tests in `Wollax.Cupel.Testing.Tests` are named; pass `--verbosity normal` to see them individually.
