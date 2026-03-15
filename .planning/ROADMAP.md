# Cupel Roadmap

## Milestones

| Milestone | Status | Phases |
|-----------|--------|--------|
| v1.0 Core Library | SHIPPED 2026-03-14 | 1-15 |
| v1.1 Rust Crate Migration & crates.io Publishing | SHIPPED 2026-03-15 | 16-22 |
| v1.2 Rust Parity & Quality Hardening | 🔄 IN PROGRESS | 23-32 |

---

<details>
<summary>✅ v1.0 Core Library — SHIPPED 2026-03-14</summary>

**Goal:** Complete .NET context management library — pipeline engine, scoring, slicing, placement, explainability, fluent API, named policies, serialization, and 4 NuGet packages.

- [x] Phase 1: Project Scaffold & Core Models (5/5 plans, 2026-03-13)
- [x] Phase 2: Interfaces & Diagnostics Infrastructure (2/2 plans, 2026-03-13)
- [x] Phase 3: Individual Scorers (3/3 plans, 2026-03-13)
- [x] Phase 4: Composite Scoring (3/3 plans, 2026-03-13)
- [x] Phase 5: Pipeline Assembly & Basic Execution (3/3 plans, 2026-03-13)
- [x] Phase 6: Advanced Slicers & Quota System (5/5 plans, 2026-03-13)
- [x] Phase 7: Explainability & Overflow Handling (3/3 plans, 2026-03-13)
- [x] Phase 8: Policy System & Named Presets (3/3 plans, 2026-03-13)
- [x] Phase 9: Serialization & JSON Package (3/3 plans, 2026-03-13)
- [x] Phase 10: Companion Packages & Release (3/3 plans, 2026-03-14)
- [x] Phase 11: Language-Agnostic Specification (3/3 plans, 2026-03-14)
- [x] Phase 12: Rust Crate (Assay) (3/3 plans, 2026-03-14)
- [x] Phase 13: Budget Contract Implementation (2/2 plans, 2026-03-14)
- [x] Phase 14: Policy Type Completeness (3/3 plans, 2026-03-14)
- [x] Phase 15: Conformance Hardening (3/3 plans, 2026-03-14)

[Full archive](milestones/v1.0-ROADMAP.md)

</details>

<details>
<summary>✅ v1.1 Rust Crate Migration & crates.io Publishing — SHIPPED 2026-03-15</summary>

**Goal:** Migrate Rust crate from wollax/assay into cupel monorepo, publish to crates.io, add serde feature flag, comprehensive documentation, and CI feature coverage.

- [x] Phase 16: Pre-flight & Crate Scaffold (2/2 plans, 2026-03-14)
- [x] Phase 17: Crate Migration & Conformance Verification (2/2 plans, 2026-03-14)
- [x] Phase 18: Dual-Language CI (2/2 plans, 2026-03-14)
- [x] Phase 19: First Publish & Assay Switchover (2/2 plans, 2026-03-14)
- [x] Phase 20: Serde Feature Flag (3/3 plans, 2026-03-15)
- [x] Phase 21: docs.rs Documentation & Examples (3/3 plans, 2026-03-15)
- [x] Phase 22: CI Feature Coverage (1/1 plan, 2026-03-15)

[Full archive](milestones/v1.1-ROADMAP.md)

</details>

<details open>
<summary>🔄 v1.2 Rust Parity & Quality Hardening — IN PROGRESS</summary>

**Goal:** Close the diagnostics gap between the Rust and .NET implementations, harden the Rust API surface before semver commits, and batch-address 74+ accumulated quality issues.

- [x] Phase 23: API Hardening Foundations (3/3 plans, 2026-03-15) — RAPI-01, 02, 03, 04, 05
  Plans:
  - [x] 23-01-PLAN.md — #[non_exhaustive] on enums + derives on slicer/placer structs
  - [x] 23-02-PLAN.md — ContextKind factory methods, TryFrom<&str>, ParseContextKindError
  - [x] 23-03-PLAN.md — ContextBudget computed properties (Rust + .NET)
- [x] Phase 24: Diagnostics Spec Chapter (2/2 plans, 2026-03-15) — SPEC-01
  Plans:
  - [x] 24-01-PLAN.md — Diagnostics overview, TraceCollector contract, and Events sub-pages
  - [x] 24-02-PLAN.md — Exclusion Reasons, SelectionReport sub-pages, and SUMMARY.md registration
- [ ] Phase 25: Conformance Infrastructure & Drift Guard (~3 plans) — CONF-01, CONF-02, CI-03, SPEC-02
- [ ] Phase 26: Diagnostics Data Types (~2 plans) — DIAG-04, 05, 06
- [ ] Phase 27: TraceCollector Trait & Implementations (~2 plans) — DIAG-01, 02, 03
- [ ] Phase 28: Pipeline Integration & run_traced (~3 plans) — DIAG-07, 08
- [ ] Phase 29: Diagnostics Polish & Serde Integration (~2 plans) — DIAG-09
- [ ] Phase 30: Quality Hardening — CI (~2 plans) — CI-01, CI-02
- [ ] Phase 31: Quality Hardening — .NET Codebase (~2 plans) — QH-01, RAPI-05/.NET, RAPI-06/.NET
- [ ] Phase 32: Quality Hardening — Rust Codebase (~3 plans) — QH-02, QH-03, RAPI-06/Rust

[Full archive](milestones/v1.2-ROADMAP.md)

</details>

---

## Progress Summary

| Phase Range | Milestone | Status | Plans | Requirements |
|-------------|-----------|--------|-------|--------------|
| 1-15 | v1.0 Core Library | SHIPPED 2026-03-14 | 48 | 44 |
| 16-22 | v1.1 Rust Crate Migration | SHIPPED 2026-03-15 | 15 | 22 |
| 23-32 | v1.2 Rust Parity & Quality Hardening | Planned | ~24 | 25 |
