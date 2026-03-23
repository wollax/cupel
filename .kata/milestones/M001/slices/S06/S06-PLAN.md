# S06: .NET Quality Hardening

**Goal:** Batch-resolve 20 high-signal .NET issues across the `Wollax.Cupel` library and its test suite: add the KnapsackSlice DP table size guard (R002), harden the API surface (naming, error messages, enum anchoring, floating-point correctness), clarify interface contracts in XML docs, and fill verified test gaps.
**Demo:** `dotnet test` passes all 649+ tests; calling `KnapsackSlice.Slice()` with a table larger than 50M cells throws `InvalidOperationException` instead of OOM-crashing; three 33.333...% QuotaBuilder kinds are accepted without spurious rejection; all 20 triage items are resolved.

## Must-Haves

- `KnapsackSlice.Slice()` throws `InvalidOperationException` when `(long)candidateCount * (capacity + 1) > 50_000_000L`, checked after discretization
- Three 33.333...% quota kinds are accepted (QuotaBuilder epsilon fix: `> 100.0 + 1e-9`)
- `CupelPipeline.OverflowStrategyValue` is renamed `OverflowStrategy` (internal — no PublicAPI.txt change)
- `CupelPolicy` Quotas+Stream error message uses caller-facing language (no internal type names)
- `ScorerEntry` InnerScorer error message includes corrective guidance
- `ScorerType.Scaled = 6` and `SlicerType.Stream = 2` are explicit integer assignments
- `PipelineStage` has explicit integer assignments (0–4)
- All `ContextItem` properties have XML `<summary>` comments
- `ITraceCollector.IsEnabled` doc states the constancy requirement (callers may cache; implementations must not toggle mid-run)
- `ISlicer` interface `<summary>` surfaces the sort precondition (candidates sorted score desc)
- `ContextResult.Report` nullability doc uses behavioral terms ("null when tracing is disabled") without referencing `NullTraceCollector` concretely
- `SelectionReport` XML doc references `ITraceCollector` not `DiagnosticTraceCollector`
- KnapsackSliceTests: negative-token items are silently skipped test added
- KnapsackSliceTests: DP guard boundary test added (at 50M cells exactly — passes; above 50M — throws)
- CupelPolicyTests: `Validation_StreamBatchSizeWithKnapsackSlicer_Throws` test added
- QuotaSliceTests: null-slicer and null-quotas constructor argument tests added
- PriorityScorerTests: `ScoresAreInZeroToOneRange` test added
- TagScorerTests: case-insensitive match test and zero-total-weight branch test added
- Duplicate `AddCupelTracing_IsTransient_DifferentInstancesPerResolve` (line 287) removed
- PipelineBuilderTests: `Count > 0` assertion at line 688 changed to `IsEqualTo(3)`
- `dotnet test` passes all tests with zero regressions

## Proof Level

- This slice proves: contract (unit test coverage of all changed behaviors)
- Real runtime required: no (unit tests exercise all paths)
- Human/UAT required: no — all behaviors are machine-verifiable

## Verification

- `dotnet test` — all tests pass (649+ total, zero failures)
- `dotnet test --filter "FullyQualifiedName~KnapsackSlice"` — guard boundary tests and negative-token test pass
- `dotnet test --filter "FullyQualifiedName~CupelPolicyTests"` — Knapsack stream batch size test passes
- `dotnet test --filter "FullyQualifiedName~QuotaSlice"` — null constructor arg tests pass
- `dotnet test --filter "FullyQualifiedName~PriorityScorer|TagScorer"` — new scorer tests pass
- `dotnet build` — zero errors, zero warnings

## Observability / Diagnostics

- Runtime signals: `InvalidOperationException` thrown with a descriptive message from `KnapsackSlice.Slice()` when guard triggers — message includes actual cell count and limit
- Inspection surfaces: `dotnet test --filter` scoped per task; exception message observable in test output on failure
- Failure visibility: test failure output names the failing assertion; `InvalidOperationException.Message` states the cell count that caused the guard to fire
- Redaction constraints: none — no sensitive data involved

## Integration Closure

- Upstream surfaces consumed: none (S06 is standalone per boundary map)
- New wiring introduced in this slice:
  - `KnapsackSlice.Slice()` → `InvalidOperationException` guard (T01)
  - `CupelPipeline.OverflowStrategy` (renamed internal property) consumed by `CupelServiceCollectionExtensions` (T02 verifies all consumers updated)
- What remains before the milestone is truly usable end-to-end: S07 (Rust quality hardening + Rust KnapsackSlice guard)

## Tasks

- [x] **T01: Add KnapsackSlice DP table size guard** `est:45m`
  - Why: R002 requires OOM prevention; `candidateCount × (capacity + 1)` overflows silently without a guard
  - Files: `src/Wollax.Cupel/KnapsackSlice.cs`, `tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs`
  - Do: Insert `(long)candidateCount * (capacity + 1) > 50_000_000L` guard after the `capacity` discretization and before the `ArrayPool.Rent` calls; throw `InvalidOperationException` with a message including actual cell count and limit; add three tests (below-limit passes, at-limit throws, above-limit throws); add negative-token items silently-skipped test
  - Verify: `dotnet test --filter "FullyQualifiedName~KnapsackSlice"` — all new tests pass; `dotnet test` — zero regressions
  - Done when: 4 new tests pass; guard throws on >50M cells (50M exactly passes); all 649+ existing tests still pass

- [x] **T02: Harden API surface (naming, error messages, enum anchoring, epsilon fix)** `est:60m`
  - Why: Closes 7 triage items — naming, floating-point correctness, caller-facing error messages, and enum forward-compatibility
  - Files: `src/Wollax.Cupel/CupelPipeline.cs`, `src/Wollax.Cupel/Extensions/CupelServiceCollectionExtensions.cs` (if consumer), `src/Wollax.Cupel/Slicing/QuotaBuilder.cs`, `src/Wollax.Cupel/CupelPolicy.cs`, `src/Wollax.Cupel/ScorerEntry.cs`, `src/Wollax.Cupel/ScorerType.cs`, `src/Wollax.Cupel/SlicerType.cs`, `src/Wollax.Cupel/Diagnostics/PipelineStage.cs`, `src/Wollax.Cupel/ContextItem.cs`
  - Do: (1) `rg OverflowStrategyValue` to find all consumers; rename `internal OverflowStrategyValue` → `OverflowStrategy` in `CupelPipeline.cs` and update any internal consumer. (2) Change `if (totalRequired > 100)` to `if (totalRequired > 100.0 + 1e-9)` in `QuotaBuilder.cs`. (3) Replace `CupelPolicy.cs` Quotas+Stream error with caller-facing language. (4) Improve `ScorerEntry.cs` InnerScorer error message. (5) Add `= 6` to `ScorerType.Scaled` and `= 2` to `SlicerType.Stream`. (6) Add explicit integers 0–4 to all `PipelineStage` variants. (7) Add XML `<summary>` comments to all `ContextItem` properties using `ContextBudget` as the model.
  - Verify: `dotnet build` — zero errors, zero warnings; `dotnet test` — zero regressions (no test expects the old internal property name or old error messages)
  - Done when: all 7 items applied; `dotnet build` clean; all tests pass

- [x] **T03: Clarify interface contract documentation** `est:30m`
  - Why: Closes 4 doc-clarity items — ITraceCollector constancy, ISlicer sort precondition, ContextResult.Report nullability, SelectionReport concrete-type reference
  - Files: `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs`, `src/Wollax.Cupel/ISlicer.cs`, `src/Wollax.Cupel/ContextResult.cs`, `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`
  - Do: (1) Add constancy note to `ITraceCollector.IsEnabled` doc. (2) Move sort-precondition sentence to `ISlicer` interface `<summary>`. (3) Change `ContextResult.Report` nullability doc from `NullTraceCollector` reference to behavioral: "null when tracing is disabled." (4) Change `SelectionReport` XML doc from `DiagnosticTraceCollector` to `ITraceCollector`.
  - Verify: `dotnet build` — zero errors; `dotnet test` — zero regressions
  - Done when: all 4 docs updated; `dotnet build` clean

- [x] **T04: Fill test coverage gaps and remove test hygiene issues** `est:60m`
  - Why: Closes 8 triage items — real test gaps in scorer, slicer, and pipeline tests; 1 duplicate removal; 1 weak assertion fix
  - Files: `tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs`, `tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs`, `tests/Wollax.Cupel.Tests/Scoring/PriorityScorerTests.cs`, `tests/Wollax.Cupel.Tests/Scoring/TagScorerTests.cs`, `tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs`, `tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs`
  - Do: (1) Add `Validation_StreamBatchSizeWithKnapsackSlicer_Throws` to `CupelPolicyTests`. (2) Add `QuotaSlice_NullSlicer_ThrowsArgumentNull` and `QuotaSlice_NullQuotas_ThrowsArgumentNull` to `QuotaSliceTests`. (3) Add `ScoresAreInZeroToOneRange` to `PriorityScorerTests` (follow RecencyScorer pattern). (4) Add case-insensitive tag match test and zero-total-weight test to `TagScorerTests`. (5) Remove duplicate `AddCupelTracing_IsTransient_DifferentInstancesPerResolve` at line 287 from `CupelServiceCollectionExtensionsTests`. (6) Change `result.Items.Count > 0` to `IsEqualTo(3)` at `PipelineBuilderTests.cs:688`. Use `ThrowsExactly<T>` for all exception assertions. Use the `CreateItem` helper pattern in each test class — do not assume shared fixtures.
  - Verify: `dotnet test` — all new tests pass; zero regressions; total count increases by 9 (8 added − 1 removed + 1 existing assertion tightened with no count change)
  - Done when: all 6 test hygiene / coverage items resolved; `dotnet test` green with 658+ tests

## Files Likely Touched

- `src/Wollax.Cupel/KnapsackSlice.cs`
- `src/Wollax.Cupel/CupelPipeline.cs`
- `src/Wollax.Cupel/Slicing/QuotaBuilder.cs`
- `src/Wollax.Cupel/CupelPolicy.cs`
- `src/Wollax.Cupel/ScorerEntry.cs`
- `src/Wollax.Cupel/ScorerType.cs`
- `src/Wollax.Cupel/SlicerType.cs`
- `src/Wollax.Cupel/Diagnostics/PipelineStage.cs`
- `src/Wollax.Cupel/ContextItem.cs`
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs`
- `src/Wollax.Cupel/ISlicer.cs`
- `src/Wollax.Cupel/ContextResult.cs`
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`
- `tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs`
- `tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs`
- `tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs`
- `tests/Wollax.Cupel.Tests/Scoring/PriorityScorerTests.cs`
- `tests/Wollax.Cupel.Tests/Scoring/TagScorerTests.cs`
- `tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs`
- `tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs`
