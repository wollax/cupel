# S03: IQuotaPolicy abstraction + QuotaUtilization

**Goal:** Extract a shared `QuotaPolicy` trait (Rust) / `IQuotaPolicy` interface (.NET) from `QuotaSlice` and `CountQuotaSlice`, then add a `quota_utilization` analytics function that computes per-kind utilization against the policy. Both languages, no breaking changes.
**Demo:** `quota_utilization(report, &quota_slice)` and `quota_utilization(report, &count_quota_slice)` both return `Vec<KindQuotaUtilization>` with kind, constraint values, actual counts/percentages, and utilization ratio — in both Rust and .NET.

## Must-Haves

- `QuotaPolicy` trait in Rust with `quota_constraints(&self) -> Vec<QuotaConstraint>` method
- `IQuotaPolicy` interface in .NET with `IReadOnlyList<QuotaConstraint> GetConstraints()` method
- `QuotaSlice` and `CountQuotaSlice` implement the trait/interface in both languages without breaking existing public API
- `QuotaConstraint` data type: kind, mode (percentage or count), require value, cap value
- `KindQuotaUtilization` data type: kind, mode, require, cap, actual, utilization ratio
- `quota_utilization(report, policy)` free function in Rust `analytics.rs` returning `Vec<KindQuotaUtilization>`
- `QuotaUtilization(SelectionReport, IQuotaPolicy)` extension method in .NET returning `IReadOnlyList<KindQuotaUtilization>`
- Utilization for percentage mode: `actual_pct = sum(included tokens for kind) / budget.target_tokens * 100`; for count mode: `actual_count = count(included items of kind)`
- Utilization ratio: `actual / cap` (clamped to [0.0, 1.0]) — how close to the cap
- PublicAPI analyzers clean; `dotnet build` 0 warnings
- `cargo test --all-targets` and `dotnet test --configuration Release` both pass
- No breaking changes to existing QuotaSlice or CountQuotaSlice API surface

## Proof Level

- This slice proves: contract + integration (unit tests exercise both quota types through the full utilization pipeline)
- Real runtime required: no
- Human/UAT required: no

## Verification

- `cargo test --all-targets` — all tests pass including new `quota_utilization` tests
- `cargo clippy --all-targets -- -D warnings` — clean
- `dotnet test --configuration Release` — all tests pass including new `QuotaUtilizationTests`
- `dotnet build --configuration Release` — 0 errors, 0 warnings
- `grep -c "IQuotaPolicy" src/Wollax.Cupel/PublicAPI.Unshipped.txt` — confirms interface is in PublicAPI
- QuotaSlice + CountQuotaSlice both exercise `quota_utilization` in tests

## Observability / Diagnostics

- Runtime signals: none — pure analytics function with no side effects
- Inspection surfaces: `KindQuotaUtilization` struct/record is the structured inspection surface — each entry shows kind, constraint mode, thresholds, actual, and utilization ratio
- Failure visibility: none — no error paths beyond standard Rust Result/panics and .NET exceptions for null args
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `SelectionReport` (from S01), `QuotaSlice`/`CountQuotaSlice` existing public API, `analytics.rs` module pattern (from M003/S04)
- New wiring introduced in this slice: `QuotaPolicy` trait implemented on `QuotaSlice` and `CountQuotaSlice`; `IQuotaPolicy` implemented on .NET equivalents; `quota_utilization` function added to `analytics.rs` and `SelectionReportExtensions`
- What remains before the milestone is truly usable end-to-end: S04 (snapshot testing), S05 (Rust budget simulation)

## Tasks

- [x] **T01: Rust QuotaPolicy trait, QuotaConstraint, and implementations** `est:25m`
  - Why: Establishes the shared abstraction and makes both Rust slicer types implement it — the prerequisite for the analytics function
  - Files: `crates/cupel/src/slicer/mod.rs`, `crates/cupel/src/slicer/quota.rs`, `crates/cupel/src/slicer/count_quota.rs`, `crates/cupel/src/lib.rs`
  - Do: Define `QuotaConstraintMode` enum (Percentage/Count), `QuotaConstraint` struct, `QuotaPolicy` trait; implement on `QuotaSlice` and `CountQuotaSlice`; re-export from lib.rs
  - Verify: `cargo test --all-targets` passes; `cargo clippy --all-targets -- -D warnings` clean
  - Done when: Both slicers implement `QuotaPolicy`, new types are re-exported, existing tests still pass

- [x] **T02: Rust quota_utilization function + tests** `est:20m`
  - Why: Completes the Rust half of R052 — callers can compute per-kind utilization against any quota policy
  - Files: `crates/cupel/src/analytics.rs`, `crates/cupel/src/lib.rs`, `crates/cupel/tests/quota_utilization.rs`
  - Do: Add `KindQuotaUtilization` struct, `quota_utilization` free function to analytics.rs; re-export from lib.rs; write integration tests covering both QuotaSlice and CountQuotaSlice policies
  - Verify: `cargo test --all-targets` passes; new tests exercise both policy types with correct utilization values
  - Done when: `quota_utilization` returns correct per-kind data for both QuotaSlice and CountQuotaSlice; all Rust tests green

- [x] **T03: .NET IQuotaPolicy, QuotaConstraint, implementations, QuotaUtilization + tests** `est:25m`
  - Why: Completes the .NET half of R052 — same abstraction and analytics as Rust
  - Files: `src/Wollax.Cupel/Slicing/IQuotaPolicy.cs`, `src/Wollax.Cupel/Slicing/QuotaConstraint.cs`, `src/Wollax.Cupel/Slicing/QuotaSlice.cs`, `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs`, `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`, `tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs`
  - Do: Define `QuotaConstraintMode` enum, `QuotaConstraint` record, `IQuotaPolicy` interface, `KindQuotaUtilization` record; implement `IQuotaPolicy` on `QuotaSlice` and `CountQuotaSlice`; add `QuotaUtilization` extension method; update PublicAPI; write tests
  - Verify: `dotnet test --configuration Release` passes; `dotnet build --configuration Release` 0 warnings; tests exercise both quota types
  - Done when: Both .NET slicer types implement `IQuotaPolicy`; `QuotaUtilization` returns correct per-kind data; PublicAPI clean; all .NET tests green

## Files Likely Touched

- `crates/cupel/src/slicer/mod.rs`
- `crates/cupel/src/slicer/quota.rs`
- `crates/cupel/src/slicer/count_quota.rs`
- `crates/cupel/src/analytics.rs`
- `crates/cupel/src/lib.rs`
- `crates/cupel/tests/quota_utilization.rs`
- `src/Wollax.Cupel/Slicing/IQuotaPolicy.cs`
- `src/Wollax.Cupel/Slicing/QuotaConstraint.cs`
- `src/Wollax.Cupel/Slicing/QuotaSlice.cs`
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs`
- `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs`
- `src/Wollax.Cupel/Diagnostics/KindQuotaUtilization.cs`
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
- `tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs`
