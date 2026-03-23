---
id: T03
parent: S03
milestone: M003
provides:
  - CountQuotaSlice .NET class implementing ISlicer with two-phase COUNT-DISTRIBUTE-BUDGET algorithm
  - CountQuotaEntry class with require_count/cap_count validation and construction guards
  - ScarcityBehavior enum (Degrade=0, Throw=1)
  - CountRequirementShortfall sealed record (Kind, RequiredCount, SatisfiedCount)
  - ExclusionReason.CountCapExceeded and .CountRequireCandidatesExhausted enum values
  - SelectionReport.CountRequirementShortfalls non-required property (default = [])
  - CountQuotaSlice.LastShortfalls property for post-run shortfall inspection
  - 13 TUnit tests in CountQuotaSliceTests.cs covering all behavioral scenarios
key_files:
  - src/Wollax.Cupel/Slicing/CountQuotaSlice.cs
  - src/Wollax.Cupel/Slicing/CountQuotaEntry.cs
  - src/Wollax.Cupel/Slicing/ScarcityBehavior.cs
  - src/Wollax.Cupel/Diagnostics/CountRequirementShortfall.cs
  - src/Wollax.Cupel/Diagnostics/ExclusionReason.cs
  - src/Wollax.Cupel/Diagnostics/SelectionReport.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Slicing/CountQuotaSliceTests.cs
key_decisions:
  - "LastShortfalls property on CountQuotaSlice (not ISlicer) is the test-accessible inspection surface — shortfalls cannot be propagated via ITraceCollector in v1 (no RecordShortfall method); SelectionReport.CountRequirementShortfalls stays [] until pipeline wiring in a future slice"
  - "Cap enforcement in Phase 3 uses RecordItemEvent (not a non-existent RecordExcluded method) — ITraceCollector only exposes RecordStageEvent and RecordItemEvent; actual ExcludedItem.CountCapExceeded reason will surface when pipeline wires slicer outputs to ReportBuilder in a future slice"
  - "Existing ExclusionReason tests (TraceEventTests + OverflowEventTests) hard-coded enum count at 8; updated to 10 to account for two new values"
patterns_established:
  - "CountQuotaSlice two-phase decorator: Phase 1 commits required items (removed from residual pool), Phase 2 delegates residual to inner slicer with reduced TargetTokens, Phase 3 cap-filters inner output using selectedCount map"
  - "ReferenceEqualityComparer.Instance for committedSet in Phase 1 — ContextItem identity is reference-based"
observability_surfaces:
  - "slicer.LastShortfalls after Slice() call — count > 0 = scarcity detected"
  - "RecordItemEvent messages for CountCapExceeded (Phase 3) and shortfall detection (Phase 1) when traceCollector.IsEnabled"
  - "ArgumentException at construction for KnapsackSlice guard and CountQuotaEntry constraint violations"
  - "dotnet test --treenode-filter '/*/*/CountQuotaSliceTests/*' — runs all 13 CountQuota tests"
duration: 45min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T03: Implement CountQuotaSlice in .NET

**.NET CountQuotaSlice with two-phase COUNT-DISTRIBUTE-BUDGET algorithm, 6 new types/extensions, and 13 TUnit tests — 682 total tests pass, build clean.**

## What Happened

Implemented all six required .NET artifacts:

1. **`ExclusionReason` extended** with `CountCapExceeded` (= 8) and `CountRequireCandidatesExhausted` (= 9). Two existing enum-count tests in `TraceEventTests` and `OverflowEventTests` hard-coded `8`; updated to `10`.

2. **`CountRequirementShortfall`** — sealed positional record with `Kind`, `RequiredCount`, `SatisfiedCount`. Added `init` accessors to PublicAPI.Unshipped.txt (required by the RS0016 analyzer for positional record properties).

3. **`SelectionReport.CountRequirementShortfalls`** — non-required `IReadOnlyList<CountRequirementShortfall>` property with default `= []`. Added both `.get` and `.init` entries to PublicAPI since `init` is part of the record surface.

4. **`ScarcityBehavior` enum** — `Degrade = 0`, `Throw = 1`.

5. **`CountQuotaEntry`** — validates require ≥ 0, cap > 0, require ≤ cap at construction with descriptive `ArgumentException`.

6. **`CountQuotaSlice`** — implements `ISlicer` with three phases:
   - Phase 1: for each entry with `RequireCount > 0`, takes top-N candidates by score and commits them. Shortfalls recorded if candidates < require.
   - Phase 2: builds residual budget (`TargetTokens -= preAllocatedTokens`) and delegates to inner slicer.
   - Phase 3: iterates inner slicer output; checks `selectedCount[kind] >= entry.CapCount`; cap-exceeded items are skipped with a `RecordItemEvent` trace.
   - `LastShortfalls` property populated at end of each Slice call for test inspection.
   - KnapsackSlice construction guard with exact message from design doc.

**ITraceCollector limitation**: The interface only has `RecordStageEvent` / `RecordItemEvent` — there is no `RecordExcluded` method. Cap exclusions are signalled via `RecordItemEvent` messages. Actual `ExcludedItem.CountCapExceeded` reason will appear in `SelectionReport.Excluded` once the pipeline wires slicer outputs through `ReportBuilder` (future slice).

## Verification

- `dotnet test` → 682 passed, 0 failed ✓
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` → 0 errors ✓
- `grep -c "CountQuotaSlice\|CountQuotaEntry\|ScarcityBehavior\|CountRequirementShortfall" PublicAPI.Unshipped.txt` → 22 (≥ 6) ✓
- `grep -c "CountCapExceeded\|CountRequireCandidatesExhausted" ExclusionReason.cs` → 2 ✓
- `grep "CountRequirementShortfalls" SelectionReport.cs` → present and not `required` ✓
- `grep -c "\[Test\]" CountQuotaSliceTests.cs` → 13 (≥ 6) ✓
- Rust: `cargo test` → 40 passed (Rust side unaffected) ✓
- Conformance drift guard: `diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing` → no output ✓

## Diagnostics

- `dotnet test --treenode-filter "/*/*/CountQuotaSliceTests/*"` — runs only CountQuota tests
- `slicer.LastShortfalls.Count > 0` after `Slice()` — scarcity detected; entries name `Kind`, `RequiredCount`, `SatisfiedCount`
- `ArgumentException` at construction identifies: KnapsackSlice guard (exact message), require > cap, cap ≤ 0
- `RecordItemEvent` messages with `CountCapExceeded` and shortfall text when `traceCollector.IsEnabled`

## Deviations

- `ITraceCollector.RecordExcluded` does not exist. Task plan says "call `traceCollector.RecordExcluded`"; actual implementation uses `RecordItemEvent` with a structured message. `ExcludedItem.CountCapExceeded` won't appear in `SelectionReport.Excluded` until the pipeline is extended to pass slicer exclusions through `ReportBuilder` (deferred to future slice).
- Two existing enum-count tests were updated from 8 → 10 to accommodate new variants (not mentioned in task plan, but straightforward fix).

## Known Issues

None. `SelectionReport.CountRequirementShortfalls` always returns `[]` when accessed via standard `Pipeline.DryRun` because the pipeline populates `SelectionReport` through `ReportBuilder`, which does not currently have a path for shortfalls. This is expected per the task plan ("shortfalls not wired to trace collector in S03"); the property exists and `LastShortfalls` is the test surface.

## Files Created/Modified

- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — new; ~175 lines; ISlicer implementation
- `src/Wollax.Cupel/Slicing/CountQuotaEntry.cs` — new; ~50 lines; entry with validation
- `src/Wollax.Cupel/Slicing/ScarcityBehavior.cs` — new; ~10 lines; enum
- `src/Wollax.Cupel/Diagnostics/CountRequirementShortfall.cs` — new; ~8 lines; sealed positional record
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — 2 new enum values
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — 1 new non-required property
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 22 new API entries
- `tests/Wollax.Cupel.Tests/Slicing/CountQuotaSliceTests.cs` — new; 13 TUnit tests
- `tests/Wollax.Cupel.Tests/Diagnostics/TraceEventTests.cs` — updated enum count 8→10
- `tests/Wollax.Cupel.Tests/Diagnostics/OverflowEventTests.cs` — updated enum count 8→10
