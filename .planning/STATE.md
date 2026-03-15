# Cupel — Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-15)

**Core value:** Optimal context selection with full explainability
**Current focus:** Planning next milestone

## Current Position

Phase: None active
Milestone: v1.1 complete — next milestone TBD
Plan: N/A
Status: Ready for next milestone
Last activity: 2026-03-15 — v1.1 milestone complete

Progress: ████████████████████████████████ 22/22 phases shipped (v1.0 + v1.1)

## Phase Overview

NEXT_PHASE=none

| Phase | Status |
|-------|--------|
| **v1.0 Core Library** | SHIPPED 2026-03-14 (15 phases, 48 plans) |
| **v1.1 Rust Crate Migration & crates.io Publishing** | SHIPPED 2026-03-15 (7 phases, 15 plans) |

## Accumulated Context

### Decisions
(Carried to PROJECT.md Key Decisions table — see v1.0 and v1.1 archives for full decision logs)

### Roadmap Evolution
- v1.0 shipped 2026-03-14 — 15 phases, 48 plans, 44/44 requirements, 641 tests
- v1.1 shipped 2026-03-15 — 7 phases, 15 plans, 22/22 requirements, 94 Rust tests

### Blockers
(None)

### Technical Debt
- assay Cargo.toml pins cupel = "1.0.0" — functional via semver but stale after 1.1.0
- 74 open issues in .planning/issues/open/
- deny.toml not documented in contributing guide
- conformance/optional/ vectors not vendored (intentional but undocumented)
