# Cupel — Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-15)

**Core value:** Optimal context selection with full explainability
**Current focus:** v1.2 Rust Parity & Quality Hardening

## Current Position

Phase: 23 — API Hardening Foundations
Milestone: v1.2 Rust Parity & Quality Hardening
Plan: —
Status: Roadmap defined, not started
Last activity: 2026-03-15 — v1.2 roadmap created (phases 23-32)

Progress: ████████████████████████████████░░░░░░░░ 22/32 phases shipped (v1.0 + v1.1 complete; v1.2 planned)

## Phase Overview

NEXT_PHASE=23

| Phase | Name | Status |
|-------|------|--------|
| **v1.0 Core Library** | Phases 1-15 | SHIPPED 2026-03-14 (48 plans) |
| **v1.1 Rust Crate Migration & crates.io Publishing** | Phases 16-22 | SHIPPED 2026-03-15 (15 plans) |
| 23 | API Hardening Foundations | Planned |
| 24 | Diagnostics Spec Chapter | Planned |
| 25 | Conformance Infrastructure & Drift Guard | Planned |
| 26 | Diagnostics Data Types | Planned |
| 27 | TraceCollector Trait & Implementations | Planned |
| 28 | Pipeline Integration & run_traced | Planned |
| 29 | Diagnostics Polish & Serde Integration | Planned |
| 30 | Quality Hardening — CI | Planned |
| 31 | Quality Hardening — .NET Codebase | Planned |
| 32 | Quality Hardening — Rust Codebase | Planned |

## Accumulated Context

### Decisions
(Carried to PROJECT.md Key Decisions table — see v1.0 and v1.1 archives for full decision logs)

### Critical Sequencing Constraints (v1.2)
- Phase 23 (RAPI-01: `#[non_exhaustive]`) must complete before Phase 32 (`CupelError::TableTooLarge` variant)
- Phase 24 (spec chapter) must be merged before Phases 25-29 can begin
- Phase 25 (drift guard) must be in place before diagnostic conformance vectors are written
- Phases 30, 31, 32 are fully parallelizable with each other and with the diagnostics track

### Roadmap Evolution
- v1.0 shipped 2026-03-14 — 15 phases, 48 plans, 44/44 requirements, 641 tests
- v1.1 shipped 2026-03-15 — 7 phases, 15 plans, 22/22 requirements, 94 Rust tests
- v1.2 roadmap defined 2026-03-15 — 10 phases (23-32), ~24 plans, 25 requirements

### Blockers
(None)

### Technical Debt
- assay Cargo.toml pins cupel = "1.0.0" — functional via semver but stale after 1.1.0
- 74 open issues in .planning/issues/open/
- deny.toml not documented in contributing guide
- conformance/optional/ vectors not vendored (intentional but undocumented)
- CI uses `--features serde` instead of `--all-features` (functionally equivalent today)
