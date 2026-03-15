# Cupel — Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-14)

**Core value:** Optimal context selection with full explainability
**Current focus:** v1.1 Rust Crate Migration & crates.io Publishing

## Current Position

Phase: 22 of 22 — CI Feature Coverage
Milestone: v1.1 Rust Crate Migration & crates.io Publishing
Plan: 0 of 1
Status: Planned
Last activity: 2026-03-15 — Gap closure phase created from milestone audit

Progress: ██████████████████████████░ 14/15 plans (v1.1)

## Phase Overview

NEXT_PHASE=22

| Phase | Status |
|-------|--------|
| **v1.0 Core Library** | SHIPPED 2026-03-14 (15 phases, 48 plans) |
| **v1.1 Rust Crate Migration & crates.io Publishing** | |
| 16. Pre-flight & Crate Scaffold | ● complete (2/2 plans) |
| 17. Crate Migration & Conformance Verification | ● complete (2/2 plans) |
| 18. Dual-Language CI | ● complete (2/2 plans) |
| 19. First Publish & Assay Switchover | ● complete (2/2 plans) |
| 20. Serde Feature Flag | ● complete (3/3 plans) |
| 21. docs.rs Documentation & Examples | ● complete (3/3 plans) |
| 22. CI Feature Coverage | ○ planned (0/1 plans) |

## Accumulated Context

### Decisions
(Carried to PROJECT.md Key Decisions table — see v1.0 archive for full decision log)

| ID | Decision | Phase |
|----|----------|-------|
| 16-01-D1 | Crate name `cupel` confirmed available — preferred name selected | 16-01 |
| 16-01-D2 | Toolchain pinned to 1.85.0 (not 'stable') for MSRV alignment | 16-01 |
| 16-01-D3 | Cargo.lock excluded from git (library crate convention) | 16-01 |
| 17-01-D1 | Applied cargo fmt for edition 2024 formatting (source used edition 2021 style) | 17-01 |
| 17-02-D1 | Added tests/**/*.rs to Cargo.toml include list for tarball round-trip verification | 17-02 |
| 18-01-D1 | conformance/** excluded from ci-rust.yml paths — vendored copy covered by crates/** | 18-01 |
| 19-01-D1 | OIDC auth with continue-on-error fallback to secret for first publish compatibility | 19-01 |

### Roadmap Evolution
- v1.0 shipped 2026-03-14 — 15 phases, 48 plans, 44/44 requirements, 641 tests
- v1.1 active: Rust crate migration to cupel repo + crates.io publishing (6 phases, 24 requirements)

### Blockers
(None)

### Technical Debt
(None — v1.0 tech debt is informational/by-design, documented in milestone audit archive)
