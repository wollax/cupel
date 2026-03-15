# Project Milestones: Cupel

## v1.1 Rust Crate Migration & crates.io Publishing (Shipped: 2026-03-15)

**Delivered:** Migrated the Rust conformance implementation from wollax/assay into the cupel monorepo, published to crates.io with OIDC trusted publishing, added optional serde serialization, comprehensive docs.rs documentation, and CI feature coverage.

**Phases completed:** 16-22 (15 plans total)

**Key accomplishments:**

- Rust crate scaffold with Edition 2024 compliance, MSRV 1.85, and standalone Cargo.toml for crates.io publishing
- Full crate migration (32 source files, 28 conformance vectors) with tarball round-trip verification
- Dual-language CI/CD with path-filtered GitHub Actions, cargo-deny supply chain checks, and OIDC-based release workflow
- Published cupel 1.0.0 to crates.io; assay switched from path dependency to registry consumer (714 assay tests passing)
- Optional serde feature flag with validation-on-deserialize for ContextBudget — constructor bypass impossible
- docs.rs documentation suite: crate README, module docs, 33 compilable doctests, 3 standalone runnable examples

**Stats:**

- 11 Rust source files, 4,550 lines of Rust (source + tests + examples)
- 94 tests (28 conformance + 33 serde + 33 doctests)
- 7 phases, 15 plans, 22 requirements (all passed)
- 2 days (2026-03-14 → 2026-03-15)

**What's next:** TBD — next milestone via `/kata-add-milestone`

---

## v1.0 Core Library (Shipped: 2026-03-14)

**Delivered:** Complete .NET context management library for coding agents — pipeline engine, scoring, slicing, placement, explainability, fluent API, named policies, serialization, and 4 NuGet packages.

**Phases completed:** 1-15 (48 plans total)

**Key accomplishments:**

- Pipeline engine with fixed 5-stage architecture (Classify → Score → Deduplicate → Slice → Place) and zero-allocation hot paths
- Full scorer suite (8 implementations) with CompositeScorer weighted aggregation and ScaledScorer normalization
- Four slicer strategies including optimal 0-1 knapsack with configurable discretization and semantic quota enforcement
- Complete explainability framework — every inclusion and exclusion carries a traceable reason
- Language-agnostic specification with 28 required conformance test vectors and mdBook publication
- Rust crate implementation (assay-cupel) passing all conformance vectors — first cross-language validation

**Stats:**

- 56 source files, 4,303 lines of C#
- 49 test files, 11,175 lines of test code
- 641 tests across 4 test assemblies, 0 failures
- 15 phases, 48 plans
- 4 NuGet packages: Core, DI Extensions, Tiktoken, Json

**What's next:** v1.1 Rust crate migration to cupel monorepo and crates.io publishing
