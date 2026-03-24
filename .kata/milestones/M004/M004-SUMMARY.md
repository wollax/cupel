---
id: M004
provides:
  - SelectionReport structural equality in both Rust (PartialEq) and .NET (IEquatable)
  - PolicySensitivityReport fork diagnostic with content-keyed diff in both languages
  - QuotaPolicy trait / IQuotaPolicy interface abstraction + QuotaUtilization analytics in both languages
  - MatchSnapshot assertion on SelectionReportAssertionChain with JSON snapshots and CUPEL_UPDATE_SNAPSHOTS env var
  - Pipeline::get_marginal_items and Pipeline::find_min_budget_for in Rust with monotonicity guards
key_decisions:
  - "D103: Exact f64 comparison — no epsilon for report equality"
  - "D109: Rust PartialEq but NOT Eq (f64 fields prevent it)"
  - "D113: Content-keyed matching for diff operations (ContextItem has no Id)"
  - "D105: IQuotaPolicy abstraction over direct quota config"
  - "D119: MatchSnapshot internal *Core method pattern for CallerFilePath testability"
  - "D123: Budget simulation as impl Pipeline methods, not free functions"
  - "D125: Separate is_quota()/is_count_quota() methods following is_knapsack() pattern"
patterns_established:
  - "Clone-and-compare test pattern for Rust #[non_exhaustive] types"
  - "Collection-aware record equality in .NET (IEquatable + SequenceEqual + pragmatic GetHashCode)"
  - "Content-based item matching via HashMap<&str, usize> for diff operations (D113)"
  - "QuotaPolicy trait / IQuotaPolicy as shared abstraction for quota slicers"
  - "__snapshots__/{name}.json convention for snapshot file storage"
  - "Monotonicity guards via defaulted trait methods (is_quota/is_count_quota)"
observability_surfaces:
  - CupelError::PipelineConfig for budget simulation monotonicity guard violations
  - CupelError::InvalidBudget for budget simulation precondition failures
  - SnapshotMismatchException with structured fields (SnapshotName, SnapshotPath, Expected, Actual)
requirement_outcomes:
  - id: R050
    from_status: active
    to_status: validated
    proof: "PartialEq on 6 Rust diagnostic types + ContextBudget; IEquatable on 4 .NET types; 15 Rust + 28 .NET equality tests; cargo test 143 + dotnet test 764 passed"
  - id: R051
    from_status: active
    to_status: validated
    proof: "policy_sensitivity function (Rust) + PolicySensitivity extension method (.NET); content-keyed diff; 2 Rust + 3 .NET integration tests with ≥2 variants; cargo test 145 + dotnet test 767 passed"
  - id: R052
    from_status: active
    to_status: validated
    proof: "QuotaPolicy trait (Rust) + IQuotaPolicy interface (.NET) implemented by both QuotaSlice and CountQuotaSlice; quota_utilization function + QuotaUtilization extension; 4 Rust + 5 .NET tests; PublicAPI clean; no breaking changes"
  - id: R053
    from_status: active
    to_status: validated
    proof: "MatchSnapshot on SelectionReportAssertionChain; JSON serialization via System.Text.Json; CUPEL_UPDATE_SNAPSHOTS=1 env var; 5 lifecycle tests prove create→match→fail→update→no-update cycle; dotnet test 777 passed"
  - id: R054
    from_status: active
    to_status: validated
    proof: "Pipeline::get_marginal_items and Pipeline::find_min_budget_for implemented; is_quota()/is_count_quota() trait methods; 9 integration tests; cargo test 158 passed; clippy clean"
duration: ~3h across 5 slices
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
---

# M004: v1.4 Diagnostics & Simulation Parity

**Structural equality, fork diagnostics, quota utilization analytics, snapshot testing, and Rust budget simulation — completing the brainstormed developer-productivity feature cluster across both languages with 29 validated requirements**

## What Happened

M004 shipped five features as a cohesive cluster. S01 laid the foundation with structural equality on SelectionReport in both languages — PartialEq derives in Rust (with deliberate Eq omission due to f64 fields) and IEquatable implementations in .NET with collection-aware deep comparison. This unblocked three downstream slices.

S02 built the fork diagnostic on top of equality — `policy_sensitivity` / `PolicySensitivity` runs multiple pipeline configurations over the same items and returns a content-keyed diff showing which items change status. S03 extracted the QuotaPolicy / IQuotaPolicy abstraction from both quota slicers and built per-kind utilization analytics on top of it, completing the analytics surface.

S04 added snapshot testing to `Wollax.Cupel.Testing` — `MatchSnapshot` serializes SelectionReport to JSON, stores it alongside test files, and supports `CUPEL_UPDATE_SNAPSHOTS=1` for in-place updates. The implementation required an internal `MatchSnapshotCore` method pattern to work around CallerFilePath testability constraints.

S05 closed the last Rust parity gap — `get_marginal_items` and `find_min_budget_for` on Pipeline, with is_quota()/is_count_quota() trait extensions for monotonicity guards. This completed the budget simulation API matching .NET behavior.

Additionally, a pre-existing OTel PublicAPI gap from M003/S05 was fixed — `CupelActivitySource` and `CupelVerbosity` types were added to PublicAPI.Unshipped.txt to restore clean `dotnet build`.

## Cross-Slice Verification

| Success Criterion | Verification | Result |
|---|---|---|
| SelectionReport `==` in both languages | `cargo test --all-targets` 158 passed; `dotnet test --configuration Release` 777 passed; 15 Rust + 28 .NET equality tests | ✅ PASS |
| PolicySensitivity returns labeled reports + diff for ≥2 configs | S02 tests exercise 2+ variants with items that swap status | ✅ PASS |
| IQuotaPolicy on QuotaSlice + CountQuotaSlice; QuotaUtilization per-kind | S03 tests verify both slicer types through shared abstraction; PublicAPI clean | ✅ PASS |
| MatchSnapshot create→match→fail→update cycle | S04: 5 lifecycle tests in SnapshotTests.cs prove full cycle | ✅ PASS |
| Rust get_marginal_items + find_min_budget_for with monotonicity guards | S05: 9 integration tests; monotonicity guards reject QuotaSlice/CountQuotaSlice | ✅ PASS |
| cargo test + dotnet test both pass | `cargo test --all-targets` 158 passed; `dotnet test --configuration Release` 777 passed | ✅ PASS |

## Requirement Changes

- R050: active → validated — PartialEq on 6 Rust types + ContextBudget; IEquatable on 4 .NET types; 43 equality tests across both languages
- R051: active → validated — Fork diagnostic in both languages; content-keyed diff; 5 integration tests across both languages
- R052: active → validated — QuotaPolicy/IQuotaPolicy implemented by both quota slicers; QuotaUtilization analytics; 9 tests; no breaking changes
- R053: active → validated — MatchSnapshot with JSON serialization, env var update, 5 lifecycle tests proving full cycle
- R054: active → validated — Both budget simulation methods on Rust Pipeline; monotonicity guards; 9 integration tests matching .NET behavior

## Forward Intelligence

### What the next milestone should know
- All 29 requirements across M001-M004 are validated. The project has zero active requirements. A new milestone would start from a fresh brainstorm or user request.
- Test counts: 158 Rust tests, 777 .NET tests. Both are the authoritative verification surfaces.
- The `analytics.rs` module in Rust now contains budget simulation, policy sensitivity, and quota utilization — it's the home for all report-level analytics.

### What's fragile
- OTel companion package PublicAPI management — the type rename from `CupelOpenTelemetryTraceCollector.SourceName` to `CupelActivitySource.SourceName` left a gap that was fixed in this milestone. Future refactors in the OTel package need PublicAPI attention.
- .NET `GetHashCode` on equality types uses pragmatic O(1) collection contributions — hash distribution is adequate for general use but not optimized for hash-heavy workloads.

### Authoritative diagnostics
- `cargo test --all-targets` and `dotnet test --configuration Release` are the definitive verification surfaces.
- `CupelError::PipelineConfig` messages name the violated monotonicity constraint — grep for "monotonic" in error messages.
- `SnapshotMismatchException` carries structured fields for programmatic inspection of snapshot failures.

### What assumptions changed
- OTel PublicAPI file was stale from M003/S05 — new types (`CupelActivitySource`, `CupelVerbosity`) were in source but not declared. Fixed during M004 completion.
- .NET record equality does not need explicit operator ==/ != — the compiler generates them from custom Equals (D111).
- ContextBudget needed PartialEq as a transitive dependency from OverflowEvent (D110) — not originally planned.

## Files Created/Modified

- `crates/cupel/src/diagnostics/mod.rs` — PartialEq derives on 6 diagnostic types
- `crates/cupel/src/model/context_budget.rs` — PartialEq on ContextBudget
- `crates/cupel/src/analytics.rs` — policy_sensitivity, quota_utilization, KindQuotaUtilization
- `crates/cupel/src/slicer/mod.rs` — QuotaPolicy trait, QuotaConstraint, is_quota(), is_count_quota()
- `crates/cupel/src/slicer/quota.rs` — QuotaPolicy impl, is_quota() override
- `crates/cupel/src/slicer/count_quota.rs` — QuotaPolicy impl, is_count_quota() override
- `crates/cupel/src/pipeline/mod.rs` — get_marginal_items, find_min_budget_for
- `crates/cupel/tests/equality.rs` — 15 Rust equality tests
- `crates/cupel/tests/policy_sensitivity.rs` — 2 fork diagnostic tests
- `crates/cupel/tests/quota_utilization.rs` — 4 quota utilization tests
- `crates/cupel/tests/budget_simulation.rs` — 9 budget simulation tests
- `src/Wollax.Cupel/ContextItem.cs` — IEquatable<ContextItem>
- `src/Wollax.Cupel/Diagnostics/` — IncludedItem, ExcludedItem, SelectionReport IEquatable; PolicySensitivity types; KindQuotaUtilization
- `src/Wollax.Cupel/Slicing/` — IQuotaPolicy, QuotaConstraint, QuotaConstraintMode; impls on both slicers
- `src/Wollax.Cupel.Testing/` — SnapshotSerializer, SnapshotMismatchException, MatchSnapshot
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt` — Fixed pre-existing gap for CupelActivitySource + CupelVerbosity
- `tests/Wollax.Cupel.Tests/` — 28 equality + 3 policy sensitivity + 5 quota utilization tests
- `tests/Wollax.Cupel.Testing.Tests/SnapshotTests.cs` — 5 snapshot lifecycle tests
