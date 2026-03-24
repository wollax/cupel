---
id: M006
provides:
  - CountQuotaSlice in Rust (crates/cupel/src/slicer/count_quota.rs, 783 lines) — full two-phase implementation
  - CountQuotaSlice in .NET (src/Wollax.Cupel/Slicing/CountQuotaSlice.cs) — full two-phase implementation
  - ExclusionReason::CountCapExceeded (Rust) / ExclusionReason.CountCapExceeded = 8 (.NET) — cap-excluded items visible in SelectionReport
  - SelectionReport::count_requirement_shortfalls / SelectionReport.CountRequirementShortfalls — scarcity shortfalls visible in SelectionReport
  - QuotaPolicy trait (Rust) / IQuotaPolicy interface (.NET) implemented on CountQuotaSlice — quota_utilization() works without regression
  - CountQuotaEntry / CountQuotaSlice.GetConstraints() in PublicAPI.Unshipped.txt — full public API surface declared
  - 5 conformance TOML vectors in crates/cupel/conformance/required/slicing/ (baseline, cap-exclusion, require-and-cap, scarcity-degrade, tag-nonexclusive)
  - 10 conformance integration tests total (5 Rust in conformance.rs, 5 .NET in CountQuotaIntegrationTests.cs)
  - CountQuotaSlice+QuotaSlice composition proven end-to-end in both languages (count_quota_composition.rs, CountQuotaCompositionTests.cs)
  - R061 validated — all proof criteria met in both languages
key_files:
  - crates/cupel/src/slicer/count_quota.rs
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/tests/conformance.rs
  - crates/cupel/tests/count_quota_composition.rs
  - src/Wollax.Cupel/Slicing/CountQuotaSlice.cs
  - src/Wollax.Cupel/Diagnostics/ReportBuilder.cs
  - src/Wollax.Cupel/CupelPipeline.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs
  - tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs
key_decisions:
  - "D086 gap: Rust pipeline did not emit CountCapExceeded — fixed by adding Slicer::count_cap_map() default method and per-kind count reconstruction in pipeline Stage 5"
  - "D140: CountQuotaSlice.Entries exposed as internal property (not public) to give CupelPipeline access without extending public API"
  - "D141: Cap-classification guard: budget constraint wins (Tokens > adjustedBudget.TargetTokens → BudgetExceeded); cap only fires when item fits budget AND selectedKindCounts[kind] >= entry.CapCount"
  - "D143: S03 verification strategy — integration-level with real dry_run() composition tests in both languages"
  - "D144: .NET QuotaSlice requires QuotaSet built via QuotaBuilder — no direct list constructor; QuotaSet has internal constructor"
  - "D142: Integration tests require WithScorer(new ReflexiveScorer()) — scorer is mandatory in PipelineBuilder"
patterns_established:
  - "Pipeline Stage 5 CountCapExceeded pattern: if slicer.is_count_quota(), build selectedKindCounts from sliced output; classify slicer-excluded items fitting budget as CountCapExceeded when kind count >= cap"
  - "count_cap_map() trait method: empty HashMap default on Slicer; CountQuotaSlice overrides to expose cap limits"
  - "Run() helper pattern in integration tests: static helper builds CountQuotaSlice pipeline and calls DryRun() — mirrors ExplainabilityIntegrationTests SC helpers"
  - "CountQuotaSlice cast pattern (.NET): _slicer is CountQuotaSlice cqs — mirrors existing _slicer is QuotaSlice quotaSlicer"
observability_surfaces:
  - "dry_run().report.excluded (Rust) / DryRun().Report.Excluded (.NET) — CountCapExceeded items machine-readable"
  - "dry_run().report.count_requirement_shortfalls (Rust) / DryRun().Report.CountRequirementShortfalls (.NET) — shortfall list"
  - "cargo test -- --nocapture count_quota_composition — Rust composition test output"
  - "dotnet test --solution Cupel.slnx — includes CountQuotaCompositionTests pass/fail"
  - "dotnet build Cupel.slnx 2>&1 | grep -E 'error|warning' — PublicAPI audit"
duration: multi-session
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
---

# M006: Count-Based Quotas

**CountQuotaSlice implemented end-to-end in both Rust and .NET; ExclusionReason::CountCapExceeded and count_requirement_shortfalls wired through real pipeline runs; 10+ integration tests and composition proof in both languages; R061 validated.**

## What Happened

M006 implemented `CountQuotaSlice` — a decorator slicer enforcing absolute item-count requirements and per-kind caps — in both the `cupel` Rust crate and `Wollax.Cupel` .NET library.

### S01 — Rust CountQuotaSlice

The Rust implementation (`count_quota.rs`, 783 lines) realized the two-phase COUNT-DISTRIBUTE-BUDGET algorithm from `.planning/design/count-quota-design.md`. Phase 1 pre-allocates top-N candidates by score descending per required kind and records shortfalls in `SelectionReport::count_requirement_shortfalls`. Phase 2 runs residual budget through the inner slicer with cap enforcement.

A critical wiring gap was discovered and fixed: the Rust pipeline Stage 5 did not initially emit `ExclusionReason::CountCapExceeded`. This was resolved by adding `count_cap_map()` as a default method on the `Slicer` trait (returning empty HashMap) and overriding it in `CountQuotaSlice`. Pipeline Stage 5 uses `is_count_quota()` to reconstruct `selectedKindCounts` from sliced output and reclassify cap-excluded items correctly — mirroring the .NET pattern established in D141.

Five TOML conformance vectors were created (baseline, cap-exclusion, require-and-cap, scarcity-degrade, tag-nonexclusive) and all five pass through the `conformance.rs` harness.

### S02 — .NET CountQuotaSlice

The .NET implementation mirrored the Rust design. Two structural wiring gaps were closed:

1. `ReportBuilder.SetCountRequirementShortfalls()` was added and wired from `CupelPipeline.Execute()` after `_slicer.Slice()` returns — reading `CountQuotaSlice.LastShortfalls` and propagating them into `SelectionReport.CountRequirementShortfalls`.

2. The re-association loop's unconditional `BudgetExceeded` classification was replaced with the two-condition rule: budget constraint wins when tokens exceed adjusted budget; `CountCapExceeded` fires when item fits budget but its kind's `selectedKindCounts` meets or exceeds the entry's `CapCount`.

An `internal Entries` property on `CountQuotaSlice` exposes `_entries` to `CupelPipeline` within the same assembly without adding public API surface. Five integration tests in `CountQuotaIntegrationTests.cs` prove all conformance scenarios. Full solution: 782 tests passed, 0 warnings.

### S03 — Composition proof + summaries

Both Rust and .NET cross-language composition tests were created, each chaining `CountQuotaSlice(inner: QuotaSlice(inner: GreedySlice))` in a real pipeline via `dry_run()` / `DryRun()`. The tests assert that count constraints (cap enforcement, CountCapExceeded in excluded) and percentage constraints (QuotaSlice fractions) are both simultaneously active and visible in `SelectionReport`.

The PublicAPI audit confirmed `dotnet build Cupel.slnx` exits with 0 errors and 0 warnings, and all eight M006 types are present in `PublicAPI.Unshipped.txt`. R061 was marked validated with full proof citations.

## Requirements Validated

- **R061** — CountQuotaSlice: count-based quota enforcement
  - Rust: 5 conformance integration tests in `conformance.rs`; `CountCapExceeded` in `report.excluded` + `count_requirement_shortfalls` via `dry_run()` proven; `CountQuotaSlice+QuotaSlice` composition in `count_quota_composition.rs`; `cargo test --all-targets` passes
  - .NET: 5 conformance integration tests in `CountQuotaIntegrationTests.cs`; `CountCapExceeded` + `CountRequirementShortfalls` in `DryRun()` proven; `CountQuotaSlice+QuotaSlice` composition in `CountQuotaCompositionTests.cs`; `dotnet test --solution Cupel.slnx` passes; `PublicAPI.Unshipped.txt` complete; `dotnet build` 0 warnings

## Files Created/Modified

### Rust
- `crates/cupel/src/slicer/count_quota.rs` — full CountQuotaSlice implementation (783 lines)
- `crates/cupel/src/diagnostics/mod.rs` — ExclusionReason::CountCapExceeded variant
- `crates/cupel/src/slicer/mod.rs` — count_cap_map() default, is_count_quota() flag
- `crates/cupel/src/pipeline/mod.rs` — Stage 5 CountCapExceeded classification, shortfall propagation
- `crates/cupel/conformance/required/slicing/count-quota-baseline.toml` — new
- `crates/cupel/conformance/required/slicing/count-quota-cap-exclusion.toml` — new
- `crates/cupel/conformance/required/slicing/count-quota-require-and-cap.toml` — new
- `crates/cupel/conformance/required/slicing/count-quota-scarcity-degrade.toml` — new
- `crates/cupel/conformance/required/slicing/count-quota-tag-nonexclusive.toml` — new
- `crates/cupel/tests/conformance.rs` — count_quota slicer branch
- `crates/cupel/tests/count_quota_composition.rs` — composition integration test (S03/T01)

### .NET
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — full implementation + internal Entries property
- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — SetCountRequirementShortfalls(), CountRequirementShortfalls in Build()
- `src/Wollax.Cupel/CupelPipeline.cs` — shortfall read, selectedKindCounts reconstruction, cap classification
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — all 8 M006 types declared
- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — 5 conformance integration tests (S02/T02)
- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs` — composition integration test (S03/T02)

## Drill-Down Paths

- `.kata/milestones/M006/slices/S01/S01-SUMMARY.md`
- `.kata/milestones/M006/slices/S02/S02-SUMMARY.md`
- `.kata/milestones/M006/slices/S03/tasks/T01-SUMMARY.md`
- `.kata/milestones/M006/slices/S03/tasks/T02-SUMMARY.md`
