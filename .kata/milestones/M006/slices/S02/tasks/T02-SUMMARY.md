---
id: T02
parent: S02
milestone: M006
provides:
  - 5 CountQuota conformance integration tests covering baseline, cap exclusion, require+cap, scarcity-degrade, and tag non-exclusive scenarios
  - End-to-end pipeline wiring proof via CupelPipeline.DryRun() for CountQuotaSlice
  - Regression coverage for CountRequirementShortfalls and ExclusionReason.CountCapExceeded
key_files:
  - tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs
key_decisions:
  - "Added WithScorer(new ReflexiveScorer()) to pipeline builder — scorer is mandatory even when CountQuotaSlice uses its own ranking; FutureRelevanceHint drives score via ReflexiveScorer"
patterns_established:
  - "Run() helper pattern: static helper that builds a CountQuotaSlice pipeline and calls DryRun() — mirrors existing ExplainabilityIntegrationTests SC helpers"
  - "Content-based membership assertions via .Select(i => i.Item.Content).ToList() + .Contains() — avoids fragile index-based assertions on U-shaped placer output"
observability_surfaces:
  - "5 tests serve as regression proof; TUnit failure messages expose exact assertion, expected, and actual — no parsing required to localize regressions"
duration: 15min
verification_result: passed
completed_at: 2026-03-24T17:00:00Z
blocker_discovered: false
---

# T02: Write 5 conformance integration tests

**5 integration tests mirroring Rust count-quota conformance vectors all pass, proving end-to-end pipeline wiring of CountRequirementShortfalls and ExclusionReason.CountCapExceeded through CupelPipeline.DryRun().**

## What Happened

Created `CountQuotaIntegrationTests.cs` with 5 test methods, each mapping to one Rust conformance vector from `crates/cupel/conformance/required/slicing/count-quota-*.toml`. Tests use a `Run()` static helper that builds a `CountQuotaSlice(new GreedySlice(), entries, scarcity)` pipeline with `ReflexiveScorer` and calls `DryRun()`.

Key discovery: `PipelineBuilder.Build()` requires `WithScorer()` even when the slicer drives its own ranking — added `new ReflexiveScorer()` which reads `FutureRelevanceHint`. Tests use `ContextKind.ToolOutput` for tool items and `new ContextKind("critical")` / `new ContextKind("urgent")` for the non-exclusive multi-kind scenario.

All assertions use content-based membership checks (`Select(i => i.Item.Content).Contains(...)`) rather than index-based checks, correctly handling the U-shaped placer ordering.

## Verification

- `dotnet test --project tests/Wollax.Cupel.Tests/ 2>&1 | tail -5` → `total: 669, failed: 0, succeeded: 669`
- `grep -c "public async Task" ...CountQuotaIntegrationTests.cs` → `5`
- `grep -c "CountCapExceeded\|CountRequirementShortfalls" ...CountQuotaIntegrationTests.cs` → `12`

## Diagnostics

Test failures will be reported by TUnit with the exact assertion that failed, expected and actual values, and the test name identifying the conformance scenario. No additional diagnostic surfaces needed.

## Deviations

None — all 5 conformance vectors covered as planned. Scenario 3 (pinned-count decrement) was correctly skipped per plan constraints; the 5th test is `count-quota-tag-nonexclusive.toml`.

## Known Issues

None.

## Files Created/Modified

- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — new file, 5 integration test methods, ~165 lines
