---
phase: 02-interfaces-diagnostics-infrastructure
plan: 01
subsystem: diagnostics
tags: [tracing, value-types, enums, readonly-record-struct, singleton]
dependency-graph:
  requires: [01-project-scaffold-core-models]
  provides: [ITraceCollector, NullTraceCollector, DiagnosticTraceCollector, TraceEvent, ScoredItem, PipelineStage, TraceDetailLevel, ExclusionReason]
  affects: [02-02-pipeline-interfaces, 03-individual-scorers, 05-pipeline-assembly]
tech-stack:
  added: []
  patterns: [null-object-singleton, detail-level-gating, readonly-record-struct]
key-files:
  created:
    - src/Wollax.Cupel/Diagnostics/PipelineStage.cs
    - src/Wollax.Cupel/Diagnostics/TraceDetailLevel.cs
    - src/Wollax.Cupel/Diagnostics/ExclusionReason.cs
    - src/Wollax.Cupel/Diagnostics/TraceEvent.cs
    - src/Wollax.Cupel/Diagnostics/ITraceCollector.cs
    - src/Wollax.Cupel/Diagnostics/NullTraceCollector.cs
    - src/Wollax.Cupel/Diagnostics/DiagnosticTraceCollector.cs
    - src/Wollax.Cupel/ScoredItem.cs
    - tests/Wollax.Cupel.Tests/Diagnostics/TraceEventTests.cs
    - tests/Wollax.Cupel.Tests/Diagnostics/NullTraceCollectorTests.cs
    - tests/Wollax.Cupel.Tests/Diagnostics/DiagnosticTraceCollectorTests.cs
    - tests/Wollax.Cupel.Tests/Models/ScoredItemTests.cs
  modified:
    - src/Wollax.Cupel/PublicAPI.Unshipped.txt
decisions:
  - ScoredItem lives in root namespace (Wollax.Cupel) since it appears in pipeline interface signatures
  - TraceEvent uses required init properties (not positional constructor) for clarity
  - DiagnosticTraceCollector uses List<TraceEvent> internally, exposed as IReadOnlyList<TraceEvent>
metrics:
  duration: ~5min
  completed: 2026-03-11
  tests-added: 43
  tests-total: 134
---

# Phase 02 Plan 01: Tracing Infrastructure & Value Types Summary

ITraceCollector gated tracing with NullTraceCollector singleton and DiagnosticTraceCollector, plus PipelineStage/TraceDetailLevel/ExclusionReason enums, TraceEvent and ScoredItem readonly record structs.

## Tasks Completed

### Task 1: Supporting enums, TraceEvent, and ScoredItem
- PipelineStage enum: Classify, Score, Deduplicate, Slice, Place
- TraceDetailLevel enum: Stage (0) < Item (1) — integer ordering for filtering
- ExclusionReason enum: LowScore, BudgetExceeded, Duplicate, QuotaExceeded
- TraceEvent readonly record struct with Stage, Duration, ItemCount (zero-allocation on stack)
- ScoredItem readonly record struct pairing ContextItem with double Score (value equality)

### Task 2: ITraceCollector, NullTraceCollector, DiagnosticTraceCollector
- ITraceCollector interface: IsEnabled gate, RecordStageEvent, RecordItemEvent
- NullTraceCollector: sealed singleton (private constructor), IsEnabled=false, empty method bodies — zero-cost disabled path
- DiagnosticTraceCollector: buffered List<TraceEvent>, TraceDetailLevel filtering (Stage ignores item events, Item captures both), optional synchronous Action<TraceEvent> callback

## Deviations

- [Rule 3 - Blocking] PublicAPI.Unshipped.txt required updates for all new public types (PublicApiAnalyzers enforces declaration)
- [Rule 3 - Blocking] TUnit `HasCount()` is obsolete in current version — replaced with `Count().IsEqualTo(n)`
- [Rule 3 - Blocking] TUnit `Assert.That(constant)` analyzer error — restructured no-op tests to assert on non-constant instance property

## Verification

- 134 tests pass (91 Phase 1 + 43 new)
- Zero build warnings, zero errors
- No AsyncLocal in codebase
- NullTraceCollector.Instance.IsEnabled is false (test-verified)
- DiagnosticTraceCollector with Stage detail level ignores item events (test-verified)
- ScoredItem has value equality (test-verified)
- TraceEvent is readonly record struct (test-verified)
