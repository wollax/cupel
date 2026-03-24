# S03: Integration proof + summaries

**Goal:** Prove `CountQuotaSlice + QuotaSlice` composition end-to-end in both Rust and .NET; confirm `PublicAPI.Unshipped.txt` is complete and correct; validate R061 in `REQUIREMENTS.md`; write M006 summaries; confirm the full test suite is green.
**Demo:** A real Rust integration test and a real .NET integration test each chain `CountQuotaSlice(inner: QuotaSlice(inner: GreedySlice))`, run a real pipeline via `dry_run()` / `DryRun()`, and assert that both count constraints and percentage constraints have visible effects on the `SelectionReport`. All 160+ Rust tests and 782+ .NET tests pass. R061 is marked validated. M006 summaries exist.

## Must-Haves

- `crates/cupel/tests/count_quota_composition.rs` exists with ≥1 test proving `CountQuotaSlice(QuotaSlice(GreedySlice))` via `dry_run()` and asserting visible count-cap and percentage-allocation effects
- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs` exists with ≥1 test proving the same composition via `DryRun()` with `WithScorer(new ReflexiveScorer())`
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` audit confirms all M006 public surface is declared (`dotnet build Cupel.slnx` exits 0 after audit)
- `cargo test --all-targets` passes with 0 failures and `cargo clippy --all-targets -- -D warnings` is clean
- `dotnet test --solution Cupel.slnx` passes with 0 failures
- R061 status changed to `validated` in `.kata/REQUIREMENTS.md` with a Validation proof note
- `.kata/milestones/M006/slices/S01/S01-SUMMARY.md` exists (written in this slice as M006 has no S01 summary)
- `.kata/milestones/M006/M006-SUMMARY.md` exists summarising all three slices

## Proof Level

- This slice proves: final-assembly
- Real runtime required: yes — real `dry_run()` / `DryRun()` calls in both languages via actual pipeline
- Human/UAT required: no

## Verification

- `rtk cargo test --all-targets` exits 0 with count_quota_composition test file listed as passing
- `rtk cargo clippy --all-targets -- -D warnings` exits 0
- `rtk dotnet test --solution Cupel.slnx` exits 0 with CountQuotaCompositionTests listed
- `rtk dotnet build Cupel.slnx` exits 0 with 0 warnings (PublicAPI audit)
- `grep -i "validated" .kata/REQUIREMENTS.md | grep R061` returns a match
- `.kata/milestones/M006/M006-SUMMARY.md` exists and is non-empty

## Observability / Diagnostics

- Runtime signals: `dry_run()` / `DryRun()` returns `SelectionReport` with `Excluded`, `Included`, `CountRequirementShortfalls` — all machine-readable
- Inspection surfaces: `cargo test -- --nocapture count_quota_composition` for Rust; `dotnet test --filter "count_quota_composition"` for .NET (TUnit tree filter)
- Failure visibility: Test failure output names the specific assertion, kind, and count that diverged; `cargo clippy` errors include file:line references
- Redaction constraints: none — no secrets or PII in test fixtures

## Integration Closure

- Upstream surfaces consumed: `CountQuotaSlice` (S01 Rust + S02 .NET), `QuotaSlice`, `GreedySlice`, `Pipeline::dry_run`, `CupelPipeline.DryRun()`, `SelectionReport` fields (`Excluded`, `CountRequirementShortfalls`)
- New wiring introduced in this slice: `CountQuotaSlice` wrapping `QuotaSlice` wrapping `GreedySlice` — the only novel composition not yet exercised in S01 or S02
- What remains before the milestone is truly usable end-to-end: nothing — M006 is complete after this slice

## Tasks

- [x] **T01: Rust composition integration test** `est:30m`
  - Why: Proves `CountQuotaSlice(QuotaSlice(GreedySlice))` runs end-to-end in Rust via `dry_run()` — the only composition not yet covered by S01 tests. This is the primary open risk from S03 research.
  - Files: `crates/cupel/tests/count_quota_composition.rs`, `crates/cupel/Cargo.toml`
  - Do: Create `crates/cupel/tests/count_quota_composition.rs`. Add `[[test]]` entry in `Cargo.toml` (or rely on auto-discovery if already configured). Write one integration test: construct `CountQuotaSlice::new(QuotaSlice::new(entries_pct, Box::new(GreedySlice)).unwrap(), count_entries, ScarcityBehavior::Degrade).unwrap()` as the slicer. Use `Pipeline::builder().scorer(Box::new(ReflexiveScorer)).slicer(Box::new(slicer)).placer(Box::new(ChronologicalPlacer)).overflow_strategy(OverflowStrategy::Throw).build().unwrap()`. Prepare items: 3 ToolOutput items (100 tokens each, scores 0.9/0.7/0.5) + 2 Message items (100 tokens each, scores 0.8/0.6). Budget: 400 tokens target (forces cap). Count entry: `ToolOutput require=1 cap=2`. Percentage entry for QuotaSlice: `ToolOutput require=10% cap=60%`. Run `pipeline.dry_run(&items, &b).unwrap()`. Assert: `report.included` has ≤2 ToolOutput items (count cap); `report.excluded` contains at least one item with `ExclusionReason::CountCapExceeded { .. }` matched via `matches!()`. Run `cargo test --all-targets` to confirm pass. Run `cargo clippy --all-targets -- -D warnings` to confirm clean.
  - Verify: `cargo test --all-targets 2>&1 | grep count_quota_composition` shows `test count_quota_composition_quota_slice_inner ... ok`; `cargo clippy --all-targets -- -D warnings` exits 0
  - Done when: The composition test passes, clippy is clean, and `report.excluded` contains a `CountCapExceeded` variant

- [x] **T02: .NET composition integration test** `est:30m`
  - Why: Proves `CountQuotaSlice(QuotaSlice(GreedySlice))` runs end-to-end in .NET via `DryRun()`. The `Run()` helper from `CountQuotaIntegrationTests.cs` is the model — adapt it to swap the inner slicer.
  - Files: `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs`
  - Do: Create `CountQuotaCompositionTests.cs`. Define a `Run()` static helper that builds: `new CountQuotaSlice(new QuotaSlice(new GreedySlice(), quotaSet), countEntries)` where `quotaSet` is built via `new QuotaBuilder().Require(kind, pct).Cap(kind, pct).Build()` — `QuotaSet` has no public constructor. Register `WithScorer(new ReflexiveScorer())` — mandatory. Call `pipeline.DryRun(items)`. Write one `[Test]` method: 3 ToolOutput items (100 tokens, scores 0.9/0.7/0.5) + 2 Message items (100 tokens, scores 0.8/0.6). Budget 400 tokens. Count entry: `new CountQuotaEntry(ContextKind.ToolOutput, requireCount: 1, capCount: 2)`. Quota set: `new QuotaBuilder().Require(ContextKind.ToolOutput, 10).Cap(ContextKind.ToolOutput, 60).Build()`. Assert: `result.Report!.Included.Count(i => i.Item.Kind == ContextKind.ToolOutput) <= 2`; `result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded) >= 1`. Run `dotnet test --solution Cupel.slnx` to confirm 0 failures.
  - Verify: `dotnet test --solution Cupel.slnx 2>&1 | grep -E "CountQuotaComposition|failed"` shows the test passing and "failed: 0"
  - Done when: Composition test passes, `dotnet build Cupel.slnx` still 0 warnings

- [x] **T03: PublicAPI audit, R061 validation, and M006 summaries** `est:30m`
  - Why: Closes all documentation and requirement gates for M006: confirms the public surface is complete, marks R061 validated, and writes the milestone summaries needed for any future agent to understand what shipped.
  - Files: `src/Wollax.Cupel/PublicAPI.Unshipped.txt`, `.kata/REQUIREMENTS.md`, `.kata/milestones/M006/slices/S01/S01-SUMMARY.md`, `.kata/milestones/M006/M006-SUMMARY.md`, `.kata/STATE.md`
  - Do: (1) PublicAPI audit: run `dotnet build Cupel.slnx` and confirm 0 warnings — the PublicAPI analyzer will surface any missing declarations. Cross-check `PublicAPI.Unshipped.txt` contains all M006 types: `CountQuotaSlice`, `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall`, `ExclusionReason.CountCapExceeded`, `ExclusionReason.CountRequireCandidatesExhausted`, `SelectionReport.CountRequirementShortfalls`, `CountQuotaSlice.GetConstraints()`. (2) R061 validation: update `.kata/REQUIREMENTS.md` — change `Status: active` to `Status: validated` for R061; add a Validation line: `Validation: validated — Rust: 5 conformance integration tests pass in crates/cupel/tests/conformance.rs; CountCapExceeded + count_requirement_shortfalls proven in dry_run(); composition with QuotaSlice tested in count_quota_composition.rs; cargo test --all-targets passes. .NET: 5 conformance integration tests pass in CountQuotaIntegrationTests.cs; CountCapExceeded + CountRequirementShortfalls proven in DryRun(); composition with QuotaSlice tested in CountQuotaCompositionTests.cs; dotnet test --solution Cupel.slnx passes; PublicAPI.Unshipped.txt complete`. (3) Write S01 summary stub at `.kata/milestones/M006/slices/S01/S01-SUMMARY.md` — S01 was completed before this planning session; write a compact summary capturing what it produced (Rust CountQuotaSlice implementation, 5 conformance tests, ExclusionReason::CountCapExceeded, count_requirement_shortfalls). (4) Write `.kata/milestones/M006/M006-SUMMARY.md` summarising all three slices. (5) Update `.kata/STATE.md` to reflect M006 complete.
  - Verify: `grep -A3 "R061" .kata/REQUIREMENTS.md | grep "validated"` returns a match; `dotnet build Cupel.slnx 2>&1 | grep -E "error|warning" | wc -l` returns 0; `.kata/milestones/M006/M006-SUMMARY.md` exists
  - Done when: R061 is validated in REQUIREMENTS.md, PublicAPI audit passes with 0 warnings, both summary files exist

## Files Likely Touched

- `crates/cupel/tests/count_quota_composition.rs` (new)
- `crates/cupel/Cargo.toml` (if `[[test]]` entry needed)
- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs` (new)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` (audit only — likely no changes)
- `.kata/REQUIREMENTS.md`
- `.kata/milestones/M006/slices/S01/S01-SUMMARY.md` (new)
- `.kata/milestones/M006/M006-SUMMARY.md` (new)
- `.kata/STATE.md`
