---
estimated_steps: 8
estimated_files: 1
---

# T02: Write 5 conformance integration tests

**Slice:** S02 — .NET CountQuotaSlice — audit, complete, and test
**Milestone:** M006

## Description

Create `CountQuotaIntegrationTests.cs` with 5 integration tests, each mirroring one of the 5 Rust conformance vectors from `crates/cupel/conformance/required/slicing/count-quota-*.toml`. Tests exercise the pipeline wiring from T01 end-to-end through `DryRun()`, asserting that `SelectionReport.CountRequirementShortfalls` is populated for scarcity scenarios and `ExclusionReason.CountCapExceeded` appears on cap-excluded items.

**Constraints:**
- Tests use real `CupelPipeline.DryRun()` — not `slicer.Slice()` directly
- Scenario 3 (pinned-count decrement) is explicitly out of scope per the research (pinned items are excluded before the slicer runs; `ISlicer` has no pinned parameter) — skip it
- Each conformance vector maps to one test; the 5th Rust vector (`count-quota-tag-nonexclusive.toml`) is the 5th test
- Use `TUnit.Core`, `TUnit.Assertions`, and `TUnit.Assertions.Extensions` (existing project patterns)
- `ContextKind` factory methods available: `ContextKind.Tool`, `ContextKind.Message`, or `ContextKind.FromString("critical")` etc.
- `CountRequirementShortfall` has `Kind`, `RequiredCount`, `SatisfiedCount` properties
- Item ordering in `Report.Included` follows the placer (U-shaped by default); use content-based membership assertions, not index-based

## Steps

1. **Create the test file** `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` with standard TUnit using statements and `namespace Wollax.Cupel.Tests.Pipeline`.

2. **Add helper methods**:
   - `static ContextItem Item(string content, int tokens, double score, ContextKind kind)` — creates a `ContextItem` with `Content`, `Tokens`, `FutureRelevanceHint = score`, `Kind = kind`
   - `static ContextResult Run(IReadOnlyList<CountQuotaEntry> entries, IReadOnlyList<ContextItem> items, int budgetTokens, ScarcityBehavior scarcity = ScarcityBehavior.Degrade)` — builds pipeline with `CountQuotaSlice(new GreedySlice(), entries, scarcity)`, calls `pipeline.DryRun(items, new ContextBudget(budgetTokens, budgetTokens))`

3. **Test 1 — Baseline** (`count-quota-baseline.toml`): 3 tool items (tool-a 0.9, tool-b 0.7, tool-c 0.5, each 100t), budget 1000, require=2 cap=4.
   - Assert: `Report.Included.Count == 3`
   - Assert: included contents contain `"tool-a"`, `"tool-b"`, `"tool-c"` (all 3)
   - Assert: `Report.CountRequirementShortfalls.Count == 0`
   - Assert: `Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded) == 0`

4. **Test 2 — Cap exclusion** (`count-quota-cap-exclusion.toml`): 3 tool items (tool-a 0.9, tool-b 0.7, tool-c 0.5, each 100t), budget 1000, require=0 cap=1.
   - Assert: `Report.Included.Count == 1`
   - Assert: included contents contain `"tool-a"`
   - Assert: `Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded) == 2`
   - Assert: cap-excluded items contain `"tool-b"` and `"tool-c"` (check `ExcludedItem.Item.Content`)
   - Assert: `Report.CountRequirementShortfalls.Count == 0`

5. **Test 3 — Require+cap combined** (`count-quota-require-and-cap.toml`): 4 tool items (tool-a 0.9, tool-b 0.7, tool-c 0.6, tool-d 0.4, each 100t), budget 1000, require=2 cap=2.
   - Assert: `Report.Included.Count == 2`
   - Assert: included contents contain `"tool-a"` and `"tool-b"`
   - Assert: `Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded) == 2`
   - Assert: cap-excluded contents contain `"tool-c"` and `"tool-d"`
   - Assert: `Report.CountRequirementShortfalls.Count == 0`

6. **Test 4 — Scarcity degrade** (`count-quota-scarcity-degrade.toml`): 1 tool item (tool-a 0.9, 100t), budget 1000, require=3 cap=5.
   - Assert: `Report.Included.Count == 1`
   - Assert: included contents contain `"tool-a"`
   - Assert: `Report.CountRequirementShortfalls.Count == 1`
   - Assert: `Report.CountRequirementShortfalls[0].RequiredCount == 3`
   - Assert: `Report.CountRequirementShortfalls[0].SatisfiedCount == 1`
   - Assert: `Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded) == 0`

7. **Test 5 — Tag non-exclusive (multi-kind)** (`count-quota-tag-nonexclusive.toml`): 3 items (item-critical kind=critical 0.9 100t, item-urgent kind=urgent 0.8 100t, item-extra kind=critical 0.5 100t), budget 1000, require=1 cap=4 for "critical", require=1 cap=4 for "urgent". Use `ContextKind.FromString("critical")` and `ContextKind.FromString("urgent")` or appropriate factory methods.
   - Assert: `Report.Included.Count == 3` (all pass — cap is 4, only 1-2 of each kind selected)
   - Assert: `Report.CountRequirementShortfalls.Count == 0`
   - Assert: `Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded) == 0`

8. **Run tests**: `dotnet test --project tests/Wollax.Cupel.Tests/` — all 5 new tests pass; total 669 tests, 0 failed.

## Must-Haves

- [ ] Test file `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` created with 5 test methods
- [ ] All 5 tests use real `pipeline.DryRun()` (not `slicer.Slice()` directly)
- [ ] Test 2 asserts `ExclusionReason.CountCapExceeded` appears exactly 2 times in `Report.Excluded`
- [ ] Test 4 asserts `CountRequirementShortfalls.Count == 1` with correct `RequiredCount` and `SatisfiedCount`
- [ ] All 5 tests pass with `dotnet test --project tests/Wollax.Cupel.Tests/`
- [ ] Total test count increases from 664 to 669

## Verification

- `dotnet test --project tests/Wollax.Cupel.Tests/ 2>&1 | grep -E "total:|failed:"` — total: 669, failed: 0
- `grep -c "public async Task" tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — output is 5
- `grep -c "CountCapExceeded\|CountRequirementShortfalls" tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — non-zero (multiple assertions)

## Observability Impact

- Signals added/changed: none — this task only adds tests; it validates the signals wired in T01
- How a future agent inspects this: the 5 tests serve as regression proof; a failing test immediately identifies which conformance scenario regressed and which assertion broke
- Failure state exposed: TUnit test failure messages include the exact assertion that failed, the expected and actual values, enabling rapid localization without re-reading the pipeline

## Inputs

- T01 completed — `CupelPipeline.Execute()` wires `LastShortfalls` and classifies `CountCapExceeded`; `ReportBuilder.Build()` populates `CountRequirementShortfalls`
- `crates/cupel/conformance/required/slicing/count-quota-*.toml` — 5 vector shapes (preloaded via research); use as reference for item configurations, budgets, and expected outcomes
- Existing test helpers in `tests/Wollax.Cupel.Tests/Pipeline/ExplainabilityIntegrationTests.cs` — `CreateItem()` pattern to mirror
- `CountQuotaEntry(ContextKind kind, int requireCount, int capCount)` constructor from `src/Wollax.Cupel/Slicing/CountQuotaEntry.cs`

## Expected Output

- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — new file, 5 test methods, ~120 lines
- All 5 tests pass; test count 664 → 669
