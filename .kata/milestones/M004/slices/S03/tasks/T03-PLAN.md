---
estimated_steps: 5
estimated_files: 8
---

# T03: .NET IQuotaPolicy, QuotaConstraint, implementations, QuotaUtilization + tests

**Slice:** S03 — IQuotaPolicy abstraction + QuotaUtilization
**Milestone:** M004

## Description

Implement the full .NET side: `IQuotaPolicy` interface, `QuotaConstraint` record, `QuotaConstraintMode` enum, `KindQuotaUtilization` record; implement `IQuotaPolicy` on `QuotaSlice` and `CountQuotaSlice`; add `QuotaUtilization` extension method; update PublicAPI.Unshipped.txt; write comprehensive tests.

## Steps

1. Create new files in `src/Wollax.Cupel/Slicing/`:
   - `QuotaConstraintMode.cs` — `public enum QuotaConstraintMode { Percentage = 0, Count = 1 }`
   - `QuotaConstraint.cs` — `public sealed record QuotaConstraint(ContextKind Kind, QuotaConstraintMode Mode, double Require, double Cap)` — same semantics as Rust (percentage values 0-100; count values as doubles)
   - `IQuotaPolicy.cs` — `public interface IQuotaPolicy { IReadOnlyList<QuotaConstraint> GetConstraints(); }`
2. Implement `IQuotaPolicy` on `QuotaSlice` — iterate `Quotas.Kinds` and build `QuotaConstraint` entries with `Mode = Percentage`, `Require = GetRequire(kind)`, `Cap = GetCap(kind)`. Implement on `CountQuotaSlice` — iterate entries and build constraints with `Mode = Count`.
3. Create `src/Wollax.Cupel/Diagnostics/KindQuotaUtilization.cs` — `public sealed record KindQuotaUtilization(ContextKind Kind, QuotaConstraintMode Mode, double Require, double Cap, double Actual, double Utilization)`. Add `QuotaUtilization(this SelectionReport report, IQuotaPolicy policy, ContextBudget budget)` extension method in `SelectionReportExtensions.cs` — same logic as Rust: iterate constraints, compute actual per kind, compute utilization = actual / cap clamped to [0.0, 1.0].
4. Update `src/Wollax.Cupel/PublicAPI.Unshipped.txt` with all new public surface: `IQuotaPolicy`, `QuotaConstraint`, `QuotaConstraintMode`, `KindQuotaUtilization`, the `GetConstraints` methods on both slicers, and the `QuotaUtilization` extension method.
5. Create `tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs`:
   - Test `QuotaSlice` implements `IQuotaPolicy` and `GetConstraints` returns correct entries
   - Test `CountQuotaSlice` implements `IQuotaPolicy` and `GetConstraints` returns correct entries
   - Test `QuotaUtilization` with percentage-mode policy returns correct per-kind utilization
   - Test `QuotaUtilization` with count-mode policy returns correct per-kind utilization
   - Test empty report returns utilization 0.0

## Must-Haves

- [ ] `IQuotaPolicy` interface with `GetConstraints()` method
- [ ] `QuotaConstraintMode` enum with `Percentage` and `Count`
- [ ] `QuotaConstraint` sealed record with Kind, Mode, Require, Cap
- [ ] `KindQuotaUtilization` sealed record with Kind, Mode, Require, Cap, Actual, Utilization
- [ ] `QuotaSlice` implements `IQuotaPolicy` (additive — no breaking changes)
- [ ] `CountQuotaSlice` implements `IQuotaPolicy` (additive — no breaking changes)
- [ ] `QuotaUtilization` extension method on `SelectionReport`
- [ ] PublicAPI.Unshipped.txt updated with all new surface
- [ ] `dotnet build --configuration Release` — 0 errors, 0 warnings
- [ ] `dotnet test --configuration Release` — all tests pass including new QuotaUtilizationTests
- [ ] Tests cover both percentage and count mode policies

## Verification

- `dotnet build --configuration Release` — 0 errors, 0 warnings
- `dotnet test --configuration Release` — all tests pass
- `grep "IQuotaPolicy" src/Wollax.Cupel/PublicAPI.Unshipped.txt` — confirms interface is declared
- Tests exercise both QuotaSlice and CountQuotaSlice through the IQuotaPolicy abstraction

## Observability Impact

- None — pure analytics; no runtime behavior change

## Inputs

- T01/T02 Rust implementation for API shape parity reference
- `src/Wollax.Cupel/Slicing/QuotaSlice.cs` — QuotaSet with GetRequire/GetCap/Kinds
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — IReadOnlyList<CountQuotaEntry> with Kind/RequireCount/CapCount
- `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` — existing extension method pattern

## Expected Output

- `src/Wollax.Cupel/Slicing/IQuotaPolicy.cs` — interface
- `src/Wollax.Cupel/Slicing/QuotaConstraintMode.cs` — enum
- `src/Wollax.Cupel/Slicing/QuotaConstraint.cs` — record
- `src/Wollax.Cupel/Diagnostics/KindQuotaUtilization.cs` — record
- `src/Wollax.Cupel/Slicing/QuotaSlice.cs` — implements IQuotaPolicy
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — implements IQuotaPolicy
- `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` — QuotaUtilization method added
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — all new entries
- `tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs` — 5+ tests
