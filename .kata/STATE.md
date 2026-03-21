# Kata State

**Active Milestone:** M001 — v1.2 Rust Parity & Quality Hardening
**Active Slice:** S05 — CI Quality Hardening (next)
**Active Task:** (none — starting S05)
**Phase:** Planning
**Slice Branch:** kata/root/M001/S04 (to be squash-merged; S05 branches from main)
**Active Workspace:** /Users/wollax/Git/personal/cupel
**Next Action:** Begin S05 (CI Quality Hardening) — add `cargo clippy --all-targets -- -D warnings` and `cargo-deny` unmaintained warning to CI
**Last Updated:** 2026-03-21
**Requirements Status:** 5 active · 6 validated · 3 deferred · 3 out of scope

## Completed Slices This Milestone

- [x] S01 — Diagnostics Data Types (2026-03-17): all 8 types, 5 conformance vectors, zero drift
- [x] S02 — TraceCollector Trait & Implementations (2026-03-17): all 4 types, 12 behavioral contract tests, zero clippy/doc warnings
- [x] S03 — Pipeline run_traced & DryRun (2026-03-21): run_traced + dry_run implemented; all 10 pipeline conformance tests pass; zero clippy/doc warnings
- [x] S04 — Diagnostics Serde Integration (2026-03-21): all diagnostic types serde-complete; 16 new integration tests; internally-tagged wire format; R006 validated

## Recent Decisions

- D027: ExclusionReason/InclusionReason serde uses `#[serde(tag = "reason")]` (internally-tagged); `_Unknown` with `#[serde(other)]` on ExclusionReason for graceful unknown handling
- D028: SelectionReport custom Deserialize via RawSelectionReport with deny_unknown_fields + total_candidates validation — mirrors ContextBudget Raw pattern
- D029: Vec<(T, usize)> hidden-index fields: serialize_with/deserialize_with free functions strip/reconstruct index, keeping wire format clean

## Blockers

- (none)
