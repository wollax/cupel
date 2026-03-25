# S02: CountConstrainedKnapsackSlice — .NET implementation — UAT

**Milestone:** M009
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: `CountConstrainedKnapsackSlice` is a pure library function with no I/O dependencies. Correctness is fully verifiable by running the automated integration test suite against real `CupelPipeline.DryRun()` calls. No live runtime, UI, or human-observable behavior exists.

## Preconditions

- .NET SDK installed (`dotnet --version`)
- All dependencies restored (`dotnet restore`)
- Solution builds with 0 warnings (`dotnet build`)

## Smoke Test

```bash
dotnet test --solution Cupel.slnx
```

Expected: `797 total, 797 succeeded, 0 failed` (or higher if new tests added; 0 failed is the invariant).

## Test Cases

### 1. Baseline — all items included, no shortfalls, no cap exclusions

```bash
dotnet test --solution Cupel.slnx
# Wollax.Cupel.Tests: CountConstrainedKnapsackTests.Baseline_AllItemsIncluded_NoShortfalls_NoCap
```

1. Pipeline uses `CountConstrainedKnapsackSlice` with `require=2, cap=4` for tool kind.
2. 3 items (2 tool, 1 msg) all fit within budget.
3. **Expected:** `result.Report.Included.Count == 3`, `CountRequirementShortfalls.Count == 0`, `CountCapExceeded` exclusions == 0.

### 2. Cap exclusion — over-cap items dropped from knapsack output

```bash
# CountConstrainedKnapsackTests.CapExclusion_TwoCapExcluded
```

1. Pipeline uses `require=1, cap=2` for tool kind.
2. 4 tool items submitted; all fit in budget.
3. **Expected:** 2 items in `Included` (tool-a and tool-b, highest scores), 2 items in `Excluded` with `Reason == CountCapExceeded`.

### 3. Scarcity degrade — shortfall recorded when candidates below require count

```bash
# CountConstrainedKnapsackTests.ScarcityDegrade_ShortfallRecorded
```

1. Pipeline uses `require=3` for tool kind; only 1 tool item available.
2. `ScarcityBehavior.Degrade` (default).
3. **Expected:** `CountRequirementShortfalls.Count == 1`; shortfall entry has `Kind == ContextKind.ToolResult`, `RequiredCount == 3`, `SatisfiedCount == 1`.

### 4. Tag non-exclusive — multiple kinds required independently

```bash
# CountConstrainedKnapsackTests.TagNonExclusive_MultipleKindsRequiredIndependently
```

1. Pipeline constrains both tool (`require=1`) and memory (`require=1`) kinds independently.
2. 3 items: 1 dual-tagged (tool+memory), 1 tool-only, 1 memory-only.
3. **Expected:** All 3 in `Included`; 0 shortfalls; 0 cap exclusions.

### 5. Require and cap — no residual cap exclusions when Phase 1 fills cap

```bash
# CountConstrainedKnapsackTests.RequireAndCap_NoResidualExcluded
```

1. Pipeline uses `bucket_size=1` knapsack, `require=2, cap=2` for tool kind.
2. 5 items: 2 tool (tool-a, tool-b) + 3 msg; all fit.
3. **Expected:** All 5 in `Included`; 0 cap exclusions (tool-a and tool-b committed in Phase 1 exhaust cap; Phase 3 has nothing to drop).

## Edge Cases

### ScarcityBehavior.Throw

```csharp
var slicer = new CountConstrainedKnapsackSlice(
    new[] { new CountQuotaEntry(ContextKind.ToolResult, requireCount: 3, capCount: 5) },
    new KnapsackSlice(new ContextBudget(maxTokens: 1000)),
    ScarcityBehavior.Throw
);
// Pass 1 tool item — should throw InvalidOperationException
```

**Expected:** `InvalidOperationException` thrown with message matching: `"CountConstrainedKnapsackSlice: candidate pool for kind 'tool_result' has 1 items but RequireCount is 3."`

### Construction validation (require > cap)

```csharp
// Should throw at construction time
new CountQuotaEntry(ContextKind.ToolResult, requireCount: 5, capCount: 2);
```

**Expected:** `ArgumentException` at construction (require > cap is invalid).

## Failure Signals

- `dotnet build` exits with warnings → `PublicAPI.Unshipped.txt` is out of sync or code quality regression
- `CountConstrainedKnapsackTests.CapExclusion_TwoCapExcluded` fails → D180 regression (score-descending re-sort not applied before Phase 3 cap loop)
- `CountConstrainedKnapsackTests.RequireAndCap_NoResidualExcluded` fails → D181 regression (Phase 3 selectedCount not seeded from Phase 1 committed counts)
- `CountConstrainedKnapsackTests.ScarcityDegrade_ShortfallRecorded` fails → shortfall wiring regression in `CupelPipeline.cs` Change 1
- `CapExclusion` test shows wrong items excluded → score-descending sort order broken; check `scoreByContent` dict construction in `CountConstrainedKnapsackSlice.Slice()`

## Requirements Proved By This UAT

- R062 (.NET half) — `CountConstrainedKnapsackSlice` exists in .NET, is constructable via public API, implements `ISlicer` and `IQuotaPolicy`, passes 5 conformance integration tests through `CupelPipeline.DryRun()`, and `PublicAPI.Unshipped.txt` is complete. Combined with S01 Rust validation, both language implementations are proven.

## Not Proven By This UAT

- R062 spec chapter — S03 must write `spec/src/slicers/count-constrained-knapsack.md`; this UAT does not verify spec accuracy or completeness
- R063 (`MetadataKeyScorer`) — out of scope for this slice
- `find_min_budget_for` interaction with `CountConstrainedKnapsackSlice` via `IQuotaPolicy.GetConstraints()` — no dedicated simulation test written for this slicer; deferred to S03/S04 scope
- `CountConstrainedKnapsackSlice` + `QuotaSlice` composition — not tested in this slice; `CountQuotaSlice` + `QuotaSlice` composition is proven (R061), and `CountConstrainedKnapsackSlice` uses the same entry types, but no explicit composition test exists

## Notes for Tester

- TUnit's `--filter` flag syntax differs from xUnit/NUnit; use `--treenode-filter` or run the full suite and visually confirm 0 failures
- The 5 conformance tests are in `tests/Wollax.Cupel.Tests/Pipeline/CountConstrainedKnapsackTests.cs`
- `CountConstrainedKnapsackSlice.Entries` is `internal` — not accessible from test project directly; use `LastShortfalls` and pipeline report for inspection
