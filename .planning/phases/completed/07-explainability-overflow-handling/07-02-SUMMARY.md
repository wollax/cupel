# 07-02 Summary: Pipeline Integration — ExecuteCore, Overflow, DryRun

## Tasks Completed: 2/2

### Task 1: Pipeline refactor — ExecuteCore(), ReportBuilder integration, diff-based exclusion tracking
- **Commit:** `7350453` — `feat(07-02): extract ExecuteCore(), wire ReportBuilder with diff-based exclusion tracking`
- Extracted `ExecuteCore()` from `Execute()` — shared by both `Execute()` and `DryRun()`
- Wired `ReportBuilder` into pipeline gated on `DiagnosticTraceCollector`
- Classify stage: tracks `NegativeTokens` exclusions, sets `TotalCandidates` and `TotalTokensConsidered`
- Deduplicate stage: tracks `Deduplicated` exclusions with `DeduplicatedAgainst` reference to surviving item
- Slice stage: diff-based `BudgetExceeded` exclusion tracking (no ISlicer changes needed)
- Merge stage: tracks `Included` items with `Pinned`/`Scored`/`ZeroToken` reasons
- Report built via `ReportBuilder.Build()` for proper score-descending excluded ordering
- 10 new report-population tests added to `CupelPipelineTests.cs`

### Task 2: OverflowStrategy handling, DryRun(), and builder integration
- **Commit:** `5f83d21` — `feat(07-02): add OverflowStrategy handling, DryRun(), and WithOverflowStrategy builder API`
- Overflow detection post-merge/pre-place: computes total merged tokens vs `TargetTokens`
- `OverflowStrategy.Throw`: raises `OverflowException` with descriptive message including token counts
- `OverflowStrategy.Truncate`: removes lowest-scored non-pinned items from merged set; uses `PinnedOverride` when pinned items present, `BudgetExceeded` when no pinned items involved
- `OverflowStrategy.Proceed`: invokes optional `Action<OverflowEvent>` callback, continues with all items
- Default strategy is `Throw` (fail-fast)
- Pinned-only overflow remains `InvalidOperationException` (unchanged)
- `DryRun()` creates its own `DiagnosticTraceCollector` — always produces `SelectionReport`
- `DryRun()` is idempotent and sync-only
- `WithOverflowStrategy()` on `PipelineBuilder` with enum validation
- 10 overflow tests + 6 dry-run tests added

## Decisions
- Overflow detection uses `_budget.TargetTokens` (not effective/adjusted target) as the overflow threshold — this is the user's stated budget goal
- Truncate iterates merged array from end (lowest scored) removing non-pinned items until within budget
- `DryRun()` passes `isDryRun: true` to `ExecuteCore()` — parameter reserved for future side-effect gating
- `PassAllSlicer` test helper created in `OverflowStrategyTests` to force overflow scenarios

## Deviations
- None — plan executed as specified

## Files Created
- `tests/Wollax.Cupel.Tests/Pipeline/OverflowStrategyTests.cs`
- `tests/Wollax.Cupel.Tests/Pipeline/DryRunTests.cs`

## Files Modified
- `src/Wollax.Cupel/CupelPipeline.cs` — ExecuteCore(), DryRun(), overflow handling, ReportBuilder integration
- `src/Wollax.Cupel/PipelineBuilder.cs` — WithOverflowStrategy(), overflow fields, Build() updated
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — DryRun, WithOverflowStrategy entries
- `tests/Wollax.Cupel.Tests/Pipeline/CupelPipelineTests.cs` — 10 report-population tests

## Test Results
- Total: 429 tests (was 403)
- Failed: 0
- All existing tests pass (zero regression)
