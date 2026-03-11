# Phase 02 Plan 02: Pipeline Interfaces & ContextResult Summary

Four pipeline stage interfaces (IScorer, ISlicer, IPlacer, IContextSource) and two result types (ContextResult, SelectionReport) implemented with TDD, zero-allocation benchmark verified, and full PublicAPI surface declared.

## Tasks Completed

### Task 1: Pipeline Interfaces (TDD)
- **IScorer**: Synchronous `double Score(ContextItem, IReadOnlyList<ContextItem>)` — receives full candidate set for relative scoring
- **ISlicer**: Synchronous `Slice(IReadOnlyList<ScoredItem>, ContextBudget, ITraceCollector)` — explicit trace propagation (TRACE-04)
- **IPlacer**: Synchronous `Place(IReadOnlyList<ScoredItem>, ITraceCollector)` — explicit trace propagation (TRACE-04)
- **IContextSource**: Async batch (`Task<IReadOnlyList<ContextItem>>`) and streaming (`IAsyncEnumerable<ContextItem>`) with `CancellationToken`
- 11 contract tests verify interface shapes, return types, and synchronicity via stub implementations

### Task 2: ContextResult & SelectionReport (TDD)
- **ContextResult**: Sealed record with required `Items`, computed `TotalTokens` (manual for-loop, no LINQ), nullable `Report`
- **SelectionReport**: Sealed record wrapping `IReadOnlyList<TraceEvent>`
- **TotalTokens**: Computed property with `for` loop — no delegate allocation from `System.Linq.Sum()`
- 8 tests covering construction, TotalTokens computation (empty/single/multiple), with-expressions, and SelectionReport

### Task 3: PublicAPI, AsyncLocal Grep, Benchmark
- **PublicAPI.Unshipped.txt**: All Phase 2 types declared — zero RS0016/RS0017 warnings
- **AsyncLocal grep**: Zero results in `src/` — explicit trace propagation confirmed
- **TraceGatingBenchmark**: `BaselineNoTracing` (NullTraceCollector) vs `WithDiagnosticTracing` — parameterized at 100/500 items, MemoryDiagnoser enabled

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build Cupel.slnx` | Zero errors, zero warnings |
| `dotnet test` | 153 tests passed (134 Phase 1 + 8 ContextResult + 11 interface contract) |
| `dotnet build` benchmarks | Compiles successfully |
| XML doc comments on all interfaces | Present |
| ContextResult.TotalTokens no LINQ | Verified — manual for-loop |
| No AsyncLocal in src/ | Verified — zero matches |
| PublicAPI.Unshipped.txt complete | All Phase 2 entries present |
| Explicit trace propagation | ITraceCollector parameter on ISlicer.Slice and IPlacer.Place |

## Deviations

- **HasCount() obsolete**: TUnit's `HasCount().EqualTo(n)` is obsolete — used `Count().IsEqualTo(n)` instead. Consistent with project convention noted in STATE.md.
- **No copy constructor in PublicAPI**: Sealed records with `required` properties in .NET 10 do not generate public copy constructors — removed erroneous entries that caused RS0017 errors.

## Requirements Satisfied

- SCORE-01: IScorer.Score is synchronous, returns double, receives full candidate set
- SLICE-01: ISlicer.Slice receives ScoredItems, ContextBudget, ITraceCollector
- PLACE-01: IPlacer.Place receives ScoredItems and ITraceCollector
- API-04: IContextSource provides batch and streaming access
- TRACE-01: NullTraceCollector enables zero-cost tracing path
- TRACE-02: DiagnosticTraceCollector captures events with detail level filtering
- TRACE-03: SelectionReport wraps trace events for consumer access
- TRACE-04: Explicit trace propagation via ITraceCollector parameters (no AsyncLocal)

## Key Files

- `src/Wollax.Cupel/IScorer.cs`
- `src/Wollax.Cupel/ISlicer.cs`
- `src/Wollax.Cupel/IPlacer.cs`
- `src/Wollax.Cupel/IContextSource.cs`
- `src/Wollax.Cupel/ContextResult.cs`
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
- `tests/Wollax.Cupel.Tests/Contracts/InterfaceContractTests.cs`
- `tests/Wollax.Cupel.Tests/Models/ContextResultTests.cs`
- `benchmarks/Wollax.Cupel.Benchmarks/TraceGatingBenchmark.cs`

## Duration

~5 minutes
