---
estimated_steps: 7
estimated_files: 9
---

# T03: Implement CountQuotaSlice in .NET

**Slice:** S03 — CountQuotaSlice — Rust + .NET Implementation
**Milestone:** M003

## Description

Implements the .NET side of `CountQuotaSlice`: new types `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall`, `CountQuotaSlice`; two new `ExclusionReason` enum values; a non-required `CountRequirementShortfalls` property on `SelectionReport`; all public API entries in `PublicAPI.Unshipped.txt`; and 5+ TUnit tests in `CountQuotaSliceTests.cs` covering all key behavioral scenarios.

Design is fully locked via DI-1 through DI-6, D040, D046, D052–D057. Follow the same structural pattern as `QuotaSlice.cs` for the slicer shape and `QuotaSliceTests.cs` for the test shape.

Key .NET-specific constraints:
- `SelectionReport.CountRequirementShortfalls` MUST be non-required with default `= []` (D057 — adding required would break all existing call sites)
- KnapsackSlice guard: `typeof(innerSlicer) == typeof(KnapsackSlice)` check at construction with the exact guard message from design doc
- `ExclusionReason` is a flat enum — new variants are backward-compatible for property-access callers
- `CountCapExceeded` exclusion reasons are recorded via `traceCollector.RecordExcluded` during Phase 3 of `Slice`

## Steps

1. **Add `CountCapExceeded` and `CountRequireCandidatesExhausted` to `ExclusionReason` enum** in `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs`. Add XML doc comments. `CountCapExceeded` comment: "Item was excluded because its kind reached the configured count cap." `CountRequireCandidatesExhausted` comment: "All candidates of this kind were exhausted before satisfying the required count." Add both as new enum values.

2. **Add `CountRequirementShortfall` record** in `src/Wollax.Cupel/Diagnostics/CountRequirementShortfall.cs`:
   ```csharp
   public sealed record CountRequirementShortfall(
       ContextKind Kind,
       int RequiredCount,
       int SatisfiedCount);
   ```
   XML doc: "Describes an unmet count requirement for a specific ContextKind."

3. **Add `CountRequirementShortfalls` to `SelectionReport`** in `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`: add `public IReadOnlyList<CountRequirementShortfall> CountRequirementShortfalls { get; init; } = [];` — **not** `required`. This is non-breaking for all existing `new SelectionReport { ... }` call sites.

4. **Create `ScarcityBehavior` enum** in `src/Wollax.Cupel/Slicing/ScarcityBehavior.cs`:
   ```csharp
   public enum ScarcityBehavior { Degrade = 0, Throw = 1 }
   ```
   XML doc: "Behavior when candidate pool cannot satisfy a RequireCount constraint at run time."

5. **Create `CountQuotaEntry` class** in `src/Wollax.Cupel/Slicing/CountQuotaEntry.cs`:
   - Properties: `ContextKind Kind`, `int RequireCount`, `int CapCount`
   - Constructor validates: `requireCount >= 0`, `capCount >= 0`, `requireCount <= capCount`, `capCount > 0 if requireCount > 0` — throw `ArgumentException` naming the parameter for each violation. Follow the `QuotaEntry`/`TagScorer` validation pattern.

6. **Create `CountQuotaSlice` class** in `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` implementing `ISlicer`:
   - Constructor `(ISlicer innerSlicer, IReadOnlyList<CountQuotaEntry> entries, ScarcityBehavior scarcity = ScarcityBehavior.Degrade)`:
     - `ArgumentNullException.ThrowIfNull` for innerSlicer and entries
     - KnapsackSlice guard: `if (innerSlicer is KnapsackSlice) throw new ArgumentException("CountQuotaSlice does not support KnapsackSlice as the inner slicer in this version. Use GreedySlice as the inner slicer. A CountConstrainedKnapsackSlice will be provided in a future release.", nameof(innerSlicer));`
   - `Slice(IReadOnlyList<ScoredItem> scoredItems, ContextBudget budget, ITraceCollector traceCollector)`:
     - Phase 1: per-kind count-satisfy (iterate entries with require_count > 0; for each kind, sort candidates by score desc; take top-N; accumulate pre_allocated_tokens; remove committed items from remaining pool; record shortfalls)
     - Phase 2: build residual budget (`new ContextBudget(budget.MaxTokens, Math.Max(0, budget.TargetTokens - preAllocatedTokens), ...)`); call `_innerSlicer.Slice(remainingCandidates, residualBudget, traceCollector)` 
     - Phase 3: cap enforcement — for each item returned by inner slicer, check if `selectedCount[kind] >= capCount[kind]`; if so, call `traceCollector.RecordExcluded(item, ExclusionReason.CountCapExceeded)` and skip; otherwise include and increment selectedCount
     - Assemble: `IReadOnlyList<CountRequirementShortfall>` shortfalls list; CANNOT attach shortfalls to `traceCollector` directly (no method exists for report-level fields); shortfalls are returned via `SelectionReport.CountRequirementShortfalls` — but `Slicer.Slice` returns `IReadOnlyList<ContextItem>`. Since `ReportBuilder` populates `SelectionReport`, shortfalls must be propagated differently. **Resolution**: for v1, `CountRequirementShortfalls` is populated by the slicer returning them through a side-channel. The simplest approach: the `ITraceCollector` does not have a shortfall-recording method in v1; `SelectionReport.CountRequirementShortfalls` will always be `[]` when called through the standard `dry_run` pipeline (shortfalls are not wired to the trace collector in S03). Unit tests verify shortfalls by **calling the slicer directly in a test helper** that inspects the behavior: since `Slice` returns only items, shortfall verification in unit tests must use a custom approach — OR the test verifies `CountRequirementShortfalls` via `Pipeline.DryRun` if the pipeline is extended in a future slice to wire shortfalls. For S03, the TUnit tests verify shortfall behavior by checking that the `result.Count < requiredCount` — the `CountRequirementShortfalls` field on `SelectionReport` will be `[]` until the pipeline is wired to propagate it in a future slice. **Revised scope**: implement `CountRequirementShortfalls` as a field that is populated; test its population via a direct unit test that constructs a slicer and verifies the expected shortfall count via a wrapper that captures the shortfall from an out parameter or a test-accessible `Shortfalls` property on `CountQuotaSlice`. Cleanest .NET approach: add `public IReadOnlyList<CountRequirementShortfall> LastShortfalls { get; private set; } = [];` to `CountQuotaSlice` — populated during each `Slice` call. Unit tests can read it after calling `Slice`. This is an inspection surface for testing; not part of the `ISlicer` contract.

7. **Update `PublicAPI.Unshipped.txt`** with all new public types and members. Entries for: `CountQuotaSlice` class + ctor + `LastShortfalls` property; `CountQuotaEntry` class + ctor + properties; `ScarcityBehavior` enum + values; `CountRequirementShortfall` record + ctor + properties; `ExclusionReason.CountCapExceeded` and `.CountRequireCandidatesExhausted`; `SelectionReport.CountRequirementShortfalls`. Run `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` immediately after updating to confirm 0 errors.

8. **Write `CountQuotaSliceTests.cs`** with at minimum 6 TUnit tests:
   1. `EmptyInput_ReturnsEmpty` — baseline guard
   2. `RequireCount_SelectsTopNByScore` — baseline: require 2 of kind tool, 3 candidates; top 2 selected by score
   3. `CapCount_ExcludesOverCap` — cap 1 of kind tool, 3 candidates; only 1 selected; verify via `traceCollector.RecordExcluded` call count OR via `result.Count`
   4. `ScarcityDegrade_RecordsShortfall` — require 3, only 1 candidate; 1 selected; `slicer.LastShortfalls` has 1 entry with `RequiredCount=3, SatisfiedCount=1`
   5. `TagNonExclusive_MultiTagSatisfiesTwoRequirements` — 1 item tagged `["critical","urgent"]`; require 1 each; only 1 item selected; shortfalls empty
   6. `KnapsackSlice_ThrowsAtConstruction` — guard test

9. **Run `dotnet test`** and verify all tests pass. Run `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` to confirm 0 errors.

## Must-Haves

- [ ] `ExclusionReason.CountCapExceeded` and `ExclusionReason.CountRequireCandidatesExhausted` values added to enum
- [ ] `CountRequirementShortfall` record exists with `Kind`, `RequiredCount`, `SatisfiedCount`
- [ ] `SelectionReport.CountRequirementShortfalls` is non-required with default `= []`
- [ ] `CountQuotaEntry` validates `require_count <= cap_count` at construction (throws `ArgumentException`)
- [ ] `CountQuotaSlice.new(KnapsackSlice, ...)` throws `ArgumentException` with exact message from design doc
- [ ] Phase 1 selects top-N by score per kind; Phase 2 calls inner with residual budget; Phase 3 cap enforcement records `CountCapExceeded` via `traceCollector.RecordExcluded`
- [ ] `CountQuotaSlice.LastShortfalls` populated after each `Slice` call (enables unit test verification)
- [ ] All new public types in `PublicAPI.Unshipped.txt`
- [ ] `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` exits 0
- [ ] `dotnet test` exits 0; ≥6 `CountQuotaSlice` tests pass

## Verification

- `dotnet test 2>&1 | tail -5` — all tests pass, 0 failed
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj 2>&1 | grep " error "` — no output
- `grep -c "CountQuotaSlice\|CountQuotaEntry\|ScarcityBehavior\|CountRequirementShortfall" src/Wollax.Cupel/PublicAPI.Unshipped.txt` — ≥ 6
- `grep -c "CountCapExceeded\|CountRequireCandidatesExhausted" src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — 2
- `grep "CountRequirementShortfalls" src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — present and NOT marked `required`
- `grep -c "\[Test\]" tests/Wollax.Cupel.Tests/Slicing/CountQuotaSliceTests.cs` — ≥ 6

## Observability Impact

- Signals added/changed: `ExclusionReason.CountCapExceeded` emitted via `traceCollector.RecordExcluded` during cap enforcement in Phase 3; `CountQuotaSlice.LastShortfalls` provides post-run shortfall inspection in tests
- How a future agent inspects this: `dotnet test --filter "CountQuota" -- TUnit.Core.Interfaces.ITest.TestName~CountQuota"` runs only CountQuota tests; `SelectionReport.CountRequirementShortfalls` (non-empty = scarcity) for pipeline-level inspection when wired in a future slice
- Failure state exposed: `ArgumentException` at construction with exact message identifies guard violations; `LastShortfalls` count > 0 indicates scarcity; test failure names identify which scenario is broken

## Inputs

- `src/Wollax.Cupel/Slicing/QuotaSlice.cs` — structural model for the slicer class shape
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — flat enum to extend
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — sealed record to extend (non-required field)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — must be updated before `dotnet build` succeeds
- `tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs` — test structure and assertion patterns to follow
- S01-SUMMARY.md Forward Intelligence — RS0016 pattern (protected ctor), construction error types
- `.planning/design/count-quota-design.md` — algorithm, exact guard message, D052–D057

## Expected Output

- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — new; ~160 lines
- `src/Wollax.Cupel/Slicing/CountQuotaEntry.cs` — new; ~50 lines
- `src/Wollax.Cupel/Slicing/ScarcityBehavior.cs` — new; ~15 lines
- `src/Wollax.Cupel/Diagnostics/CountRequirementShortfall.cs` — new; ~15 lines
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — 2 new enum values
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — 1 new non-required property
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — new entries for all new public types/members
- `tests/Wollax.Cupel.Tests/Slicing/CountQuotaSliceTests.cs` — new; ≥6 TUnit tests
- `dotnet test` exits 0; `dotnet build` exits 0
