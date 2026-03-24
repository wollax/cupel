---
id: S03
parent: M006
milestone: M006
provides:
  - crates/cupel/tests/count_quota_composition.rs — Rust integration test proving CountQuotaSlice(QuotaSlice(GreedySlice)) via real dry_run()
  - CountCapExceeded now emitted by Rust pipeline (pipeline Stage 5 count_cap_map() fix)
  - Slicer::count_cap_map() default method on Slicer trait; CountQuotaSlice implements it
  - tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs — .NET integration test proving same composition via DryRun()
  - R061 validated in .kata/REQUIREMENTS.md with full Rust + .NET proof note
  - .kata/milestones/M006/slices/S01/S01-SUMMARY.md (retrospective)
  - .kata/milestones/M006/M006-SUMMARY.md (full milestone summary)
  - PublicAPI.Unshipped.txt confirmed complete — dotnet build 0 errors, 0 warnings
  - .kata/STATE.md updated to M006 complete
requires:
  - slice: S01
    provides: CountQuotaSlice Rust implementation; QuotaPolicy trait; 5 conformance vectors; ExclusionReason::CountCapExceeded definition
  - slice: S02
    provides: CountQuotaSlice .NET implementation; CountCapExceeded .NET enum value; SelectionReport.QuotaViolations wiring; PublicAPI.Unshipped.txt base entries
affects: []
key_files:
  - crates/cupel/tests/count_quota_composition.rs
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/slicer/count_quota.rs
  - tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs
  - .kata/REQUIREMENTS.md
  - .kata/milestones/M006/slices/S01/S01-SUMMARY.md
  - .kata/milestones/M006/M006-SUMMARY.md
  - .kata/STATE.md
key_decisions:
  - "D145: Slicer::count_cap_map() default method added to Slicer trait (returns empty HashMap); CountQuotaSlice overrides it to expose per-kind caps to the pipeline without widening the trait's minimal surface"
  - "D146: Pipeline Stage 5 CountCapExceeded classification: when is_count_quota() is true, reconstruct selectedKindCounts from actual sliced output; classify slicer-excluded items that fit the budget as CountCapExceeded{kind,cap,count} when kind count >= cap"
  - "D147: Composition test budget set to 600 tokens (not 400 as planned) — 5 items × 100 = 500 total; 600 ensures budget exhaustion never fires before the count cap, making the count cap the sole binding constraint"
patterns_established:
  - "Pipeline Stage 5 CountCapExceeded pattern: reconstruct per-kind selected counts from sliced output; emit CountCapExceeded for budget-fitting excluded items when kind cap is saturated — mirrors .NET D141 pattern"
  - "CountQuotaCompositionTests.Run() helper: CupelPipeline.CreateBuilder().WithBudget().WithScorer(ReflexiveScorer).WithSlicer(new CountQuotaSlice(new QuotaSlice(new GreedySlice(), quotaSet), countEntries)).Build().DryRun(items)"
observability_surfaces:
  - "cargo test -- --nocapture count_quota_composition — prints full assertion detail including excluded reasons on failure"
  - "report.excluded.iter().filter(|e| matches!(e.reason, ExclusionReason::CountCapExceeded { .. })) — now populated for real Rust pipeline runs with CountQuotaSlice"
  - "dotnet test --solution Cupel.slnx — includes CountQuotaCompositionTests with pass/fail per method and assertion detail on failure"
  - "dotnet build Cupel.slnx 2>&1 | grep -E 'error|warning' — 0 lines = green (PublicAPI audit)"
drill_down_paths:
  - .kata/milestones/M006/slices/S03/tasks/T01-SUMMARY.md
  - .kata/milestones/M006/slices/S03/tasks/T02-SUMMARY.md
  - .kata/milestones/M006/slices/S03/tasks/T03-SUMMARY.md
duration: 50min
verification_result: passed
completed_at: 2026-03-24T19:00:00Z
---

# S03: Integration proof + summaries

**CountQuotaSlice+QuotaSlice composition proven end-to-end in both languages; CountCapExceeded now emitted by Rust pipeline; R061 validated; M006 complete.**

## What Happened

S03 closed all remaining M006 gates across three tasks.

**T01 — Rust composition integration test:** Created `crates/cupel/tests/count_quota_composition.rs` with a single integration test `count_quota_composition_quota_slice_inner` chaining `CountQuotaSlice(inner: QuotaSlice(inner: GreedySlice))`. The test exposed a gap in S01's deliverable: the Rust pipeline never emitted `ExclusionReason::CountCapExceeded` — items dropped by the slicer's count-cap were always classified as `BudgetExceeded` by the pipeline's post-slice pass. A minimal production fix was applied: `Slicer::count_cap_map()` default method (returns empty HashMap) added to the Slicer trait; `CountQuotaSlice` implements it to expose per-kind caps; pipeline Stage 5 reconstructs `selectedKindCounts` from actual sliced output and classifies budget-fitting excluded items as `CountCapExceeded` when the kind's cap is saturated. Budget adjusted from 400→600 tokens (5×100=500 total; 600 ensures budget exhaustion never fires before the count cap). 159 Rust tests pass; clippy clean.

**T02 — .NET composition integration test:** Created `CountQuotaCompositionTests.cs` mirroring the T01 test structure. Single test `CompositionWithQuotaSlice_CountCapAndPercentageConstraintsBothActive` asserts all three observable outcomes: count cap ≤2 ToolOutput included, `CountCapExceeded` in excluded list, require=1 satisfied (no shortfalls). Passed on first run with no production code changes. `dotnet test --solution Cupel.slnx` exits 0; `dotnet build Cupel.slnx` 0 warnings.

**T03 — PublicAPI audit, R061 validation, M006 summaries:** Confirmed all 8 M006 public types in `PublicAPI.Unshipped.txt`; `dotnet build` returned 0 errors, 0 warnings. Updated `.kata/REQUIREMENTS.md` — R061 `Status: active` → `Status: validated` with full proof note. Wrote `S01-SUMMARY.md` retrospectively (S01 ran before the current planning session). Wrote `M006-SUMMARY.md` covering all three slices. Updated `STATE.md` to M006 complete.

## Verification

| Check | Status | Evidence |
|-------|--------|----------|
| `cargo test --all-targets` exits 0 | ✓ PASS | 159 tests passed, 0 failed |
| `cargo clippy --all-targets -- -D warnings` exits 0 | ✓ PASS | No warnings emitted |
| `dotnet test --solution Cupel.slnx` exits 0 | ✓ PASS | All tests passed, 0 failed |
| `dotnet build Cupel.slnx` 0 errors, 0 warnings | ✓ PASS | 14 projects, 0 errors, 0 warnings |
| `count_quota_composition_quota_slice_inner` listed as passing | ✓ PASS | `cargo test` output includes test |
| `CountQuotaCompositionTests` listed as passing | ✓ PASS | `dotnet test` output includes test class |
| `report.excluded` contains `CountCapExceeded` in Rust | ✓ PASS | `matches!()` assertion passes |
| R061 Status: validated in REQUIREMENTS.md | ✓ PASS | `grep -A3 "R061" REQUIREMENTS.md \| grep "validated"` returns match |
| All 8 M006 PublicAPI entries confirmed | ✓ PASS | Read of `PublicAPI.Unshipped.txt` confirms all entries |
| `M006-SUMMARY.md` exists and non-empty | ✓ PASS | 9657 bytes |
| `S01-SUMMARY.md` exists | ✓ PASS | File confirmed |

## Requirements Advanced

- R061 — final validation gates confirmed: composition tests in both languages, CountCapExceeded in Rust pipeline, PublicAPI complete

## Requirements Validated

- R061 — CountQuotaSlice: count-based quota enforcement. Validated by: 5 conformance integration tests in both languages; CountCapExceeded in excluded list from real dry_run()/DryRun(); count_requirement_shortfalls / CountRequirementShortfalls from real pipeline; CountQuotaSlice+QuotaSlice composition in both languages; cargo test 159 passed; dotnet test full solution passes; dotnet build 0 warnings; PublicAPI.Unshipped.txt complete

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

1. **Production code added in T01** (minor): Task plan said "no production code." The required assertion (`CountCapExceeded` in `report.excluded`) was permanently impossible without the fix because the Rust pipeline classified all slicer-excluded items as `BudgetExceeded`. Fix was minimal: ~20 lines across 3 files (`Slicer::count_cap_map()` default + `CountQuotaSlice` impl + pipeline Stage 5 classification).

2. **Budget 600 tokens, not 400 (T01)**: Task plan specified 400-token budget. At 400 tokens, 2 items were budget-excluded before the count cap could fire (5×100=500 > 400). Changed to 600 so budget exhaustion never fires before the count cap.

## Known Limitations

- None for M006. The milestone is complete.

## Follow-ups

- None. No deferred work discovered.

## Files Created/Modified

- `crates/cupel/tests/count_quota_composition.rs` — new Rust integration test for CountQuotaSlice(QuotaSlice(GreedySlice)) composition
- `crates/cupel/src/slicer/mod.rs` — added `count_cap_map()` default method to Slicer trait
- `crates/cupel/src/slicer/count_quota.rs` — implemented `count_cap_map()` on CountQuotaSlice
- `crates/cupel/src/pipeline/mod.rs` — Stage 5: emit CountCapExceeded when is_count_quota() and kind cap saturated
- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs` — new .NET integration test (~75 lines)
- `.kata/REQUIREMENTS.md` — R061 validated; traceability table updated; Coverage Summary updated to 31 validated
- `.kata/milestones/M006/slices/S01/S01-SUMMARY.md` — new file (retrospective S01 summary)
- `.kata/milestones/M006/M006-SUMMARY.md` — new file (full milestone summary)
- `.kata/STATE.md` — updated to M006 complete

## Forward Intelligence

### What the next slice should know
- `Slicer::count_cap_map()` is now part of the Slicer trait API — any new slicer implementations must at minimum accept the default (empty HashMap). No breaking change, but new slicers with per-kind caps should implement it.
- The pipeline Stage 5 CountCapExceeded classification pattern is now established in both languages. Both are driven by reconstructing selectedKindCounts from the actual slicer output rather than relying on slicer internals.

### What's fragile
- Pipeline Stage 5 CountCapExceeded classification assumes `is_count_quota()` is the right gate — if a future slicer has count caps without being a `CountQuotaSlice`, it would need its own `is_*` gate or a more general mechanism.

### Authoritative diagnostics
- `cargo test -- --nocapture count_quota_composition` — prints full assertion detail for the composition test, including all excluded reasons
- `dotnet test --solution Cupel.slnx --filter "CountQuotaComposition"` — runs only the .NET composition tests with detailed output

### What assumptions changed
- Original assumption (S01 roadmap): `CountCapExceeded` appears in `dry_run()` output after S01. Actual: S01 only wired the slicer-level cap; the pipeline Stage 5 classification was missing. Fixed in S03/T01.
