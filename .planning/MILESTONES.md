# Project Milestones: Cupel

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
