# S03: IQuotaPolicy abstraction + QuotaUtilization — UAT

**Milestone:** M004
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All deliverables are pure data types and analytics functions with no I/O, UI, or runtime dependencies — verification is fully mechanically checkable via test suites, build output, and grep-based API surface checks.

## Preconditions

- Rust toolchain with `cargo` available
- .NET SDK with `dotnet` available
- Repository checked out on the S03 branch or main after merge

## Smoke Test

Run `cargo test --all-targets` in `crates/cupel/` and `dotnet test --configuration Release` at repo root. Both must pass with zero failures.

## Test Cases

### 1. Rust QuotaPolicy trait implementations

1. Run `cargo test quota_utilization --all-targets` in `crates/cupel/`
2. **Expected:** 4 tests pass — percentage mode, count mode, empty report, absent kind

### 2. .NET IQuotaPolicy implementations

1. Run `dotnet test --configuration Release --filter "QuotaUtilization"` at repo root
2. **Expected:** 5 tests pass — QuotaSlice GetConstraints, CountQuotaSlice GetConstraints, percentage utilization, count utilization, empty report utilization

### 3. Rust QuotaSlice returns percentage-mode constraints

1. In a test, create a `QuotaSlice` with two quotas and call `quota_constraints()`
2. **Expected:** Returns `Vec<QuotaConstraint>` with `mode: Percentage` and correct require/cap values

### 4. .NET CountQuotaSlice returns count-mode constraints

1. In a test, create a `CountQuotaSlice` with entries and call `GetConstraints()`
2. **Expected:** Returns `IReadOnlyList<QuotaConstraint>` with `Mode = Count` and correct require/cap values

### 5. Utilization clamping

1. Create a report where actual exceeds cap for a kind
2. Call `quota_utilization` / `QuotaUtilization`
3. **Expected:** Utilization ratio is 1.0 (clamped), not >1.0

## Edge Cases

### Empty report returns zero utilization

1. Pass an empty `SelectionReport` (no included items) to `quota_utilization`
2. **Expected:** All constraints return `actual = 0.0` and `utilization = 0.0`

### Kind in policy but absent from report

1. Define a policy constraint for kind "rare" but include no items of that kind
2. **Expected:** `actual = 0.0`, `utilization = 0.0` for that kind

### Zero cap returns zero utilization

1. Define a policy with `cap = 0.0` for a kind
2. **Expected:** `utilization = 0.0` (not NaN or infinity)

## Failure Signals

- `cargo test` or `dotnet test` failures in quota_utilization tests
- `dotnet build --configuration Release` warnings (PublicAPI analyzer violations)
- `grep -c "IQuotaPolicy" PublicAPI.Unshipped.txt` returning less than 3

## Requirements Proved By This UAT

- R052 — IQuotaPolicy abstraction + QuotaUtilization: QuotaSlice and CountQuotaSlice both implement the shared interface; QuotaUtilization returns correct per-kind data for both percentage and count modes; no breaking changes to existing API

## Not Proven By This UAT

- No runtime/operational verification needed — pure analytics library with no service lifecycle
- Serialization of QuotaConstraint/KindQuotaUtilization not tested (no serde requirement for these types in R052)

## Notes for Tester

All verification is automated. The test cases above are already covered by the integration tests in `crates/cupel/tests/quota_utilization.rs` and `tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs`. Running the full test suites is the complete UAT.
