# Kata State

**Active Milestone:** M002 — v1.3 Design Sprint
**Active Slice:** S01 — Post-v1.2 Brainstorm Sprint (next to start)
**Active Task:** (none — S01 not yet started)
**Phase:** Planning
**Slice Branch:** (to be created: kata/root/M002/S01)
**Active Workspace:** /Users/wollax/Git/personal/cupel
**Next Action:** Start S01 — run post-v1.2 brainstorm (explorer/challenger pairs, 2-3 pairs, following .planning/brainstorms/ format)
**Last Updated:** 2026-03-21 (M002 planned — 6 design-only slices; context, roadmap, requirements written)
**Requirements Status:** 6 active (R040–R045, all M002) · 11 validated (R001–R006, R010–R014) · 3 deferred · 3 out of scope

## Completed Slices This Milestone

- [x] S01 — Diagnostics Data Types (2026-03-17): all 8 types, 5 conformance vectors, zero drift
- [x] S02 — TraceCollector Trait & Implementations (2026-03-17): all 4 types, 12 behavioral contract tests, zero clippy/doc warnings
- [x] S03 — Pipeline run_traced & DryRun (2026-03-21): run_traced + dry_run implemented; all 10 pipeline conformance tests pass; zero clippy/doc warnings
- [x] S04 — Diagnostics Serde Integration (2026-03-21): all diagnostic types serde-complete; 16 new integration tests; internally-tagged wire format; R006 validated
- [x] S05 — CI Quality Hardening (2026-03-21): --all-targets on all 4 clippy steps in both CI workflows; unmaintained = "workspace" in deny.toml; cargo deny check exits 0; R003 validated
- [x] S06 — .NET Quality Hardening (2026-03-21): 20 triage items resolved; KnapsackSlice DP guard; epsilon fix; naming/error/enum hardening; interface contract docs; 6 new tests (net +5); 658 tests pass; R004 validated
- [x] S07 — Rust Quality Hardening (2026-03-21): TableTooLarge guard + Slicer::slice→Result; CompositeScorer DFS + as_any removed; UShapedPlacer explicit left/right vecs; 15 new unit tests; release-rust.yml job-level permissions; R002 + R005 validated; 35 tests pass; all clippy/deny checks clean

## M002 Slices

- [ ] S01 — Post-v1.2 Brainstorm Sprint (next)
- [ ] S02 — Spec Editorial Debt
- [ ] S03 — Count-Based Quota Design (high risk)
- [ ] S04 — Metadata Convention System Spec
- [ ] S05 — Cupel.Testing Vocabulary Design
- [ ] S06 — Future Features Spec Chapters

## Milestone DoD Status

- [x] All 7 slices complete with summaries
- [x] `cargo test` passes (35 passed, 0 failed)
- [x] `cargo clippy --all-targets -- -D warnings` passes (0 warnings)
- [x] `cargo deny check` passes (advisories/bans/licenses/sources ok)
- [x] Diagnostics conformance vectors in `spec/conformance/` and `crates/cupel/conformance/`, all passing in CI
- [x] .NET test suite (663 total across all projects) passes with no regressions
- [x] KnapsackSlice DP guard in both .NET (S06) and Rust (S07)
- [x] M001-SUMMARY.md written with verified success criteria
- [ ] v1.2.0 tag (pending manual publish step)

## Recent Decisions

- D035: `Slicer::slice → Result` semver break accepted for v1.2.0 (all built-in impls in-crate; compile-time-visible)
- D036: `CupelError::CycleDetected` kept as reserved/never-emitted variant (removing it would break downstream match arms)
- D037: `UShapedPlacer` refactored to explicit left/right vecs (no `Vec<Option>` or `.expect()`)
- D038: Scorer unit tests use `std::slice::from_ref(&item)` for single-item `all_items` — required for clippy::cloned_ref_to_slice_refs under -D warnings

## M001 Final Verification Results

- `cargo test` → 35 passed, 0 failed ✅
- `cargo test --features serde` → 35 passed, 0 failed ✅
- `cargo clippy --all-targets -- -D warnings` → 0 warnings, exit 0 ✅
- `cargo clippy --features serde --all-targets -- -D warnings` → 0 warnings, exit 0 ✅
- `cargo deny check` → advisories/bans/licenses/sources ok ✅
- conformance drift → `diff -rq spec/conformance/ crates/cupel/conformance/` → no output ✅
- 5 diagnostics conformance tests pass ✅
- .NET tests → 663 total (583+15+47+13+5), 0 failures ✅
- R001 through R006 all validated ✅

## Blockers

- (none)
