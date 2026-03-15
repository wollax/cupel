# Cupel Roadmap

## Milestones

| Milestone | Status | Phases |
|-----------|--------|--------|
| v1.0 Core Library | SHIPPED 2026-03-14 | 1-15 |
| v1.1 Rust Crate Migration & crates.io Publishing | ○ planned | 16-21 |

---

<details>
<summary>v1.0 Core Library (Phases 1-15) — SHIPPED 2026-03-14</summary>

**Goal:** Complete .NET context management library — pipeline engine, scoring, slicing, placement, explainability, fluent API, named policies, serialization, and 4 NuGet packages.

- [x] Phase 1: Project Scaffold & Core Models (5/5 plans)
- [x] Phase 2: Interfaces & Diagnostics Infrastructure (2/2 plans)
- [x] Phase 3: Individual Scorers (3/3 plans)
- [x] Phase 4: Composite Scoring (3/3 plans)
- [x] Phase 5: Pipeline Assembly & Basic Execution (3/3 plans)
- [x] Phase 6: Advanced Slicers & Quota System (5/5 plans)
- [x] Phase 7: Explainability & Overflow Handling (3/3 plans)
- [x] Phase 8: Policy System & Named Presets (3/3 plans)
- [x] Phase 9: Serialization & JSON Package (3/3 plans)
- [x] Phase 10: Companion Packages & Release (3/3 plans)
- [x] Phase 11: Language-Agnostic Specification (3/3 plans)
- [x] Phase 12: Rust Crate (Assay) (3/3 plans)
- [x] Phase 13: Budget Contract Implementation (2/2 plans)
- [x] Phase 14: Policy Type Completeness (3/3 plans)
- [x] Phase 15: Conformance Hardening (3/3 plans)

[Full archive](milestones/v1.0-ROADMAP.md)

</details>

---

### v1.1 Rust Crate Migration & crates.io Publishing (Planned)

#### Phase 16: Pre-flight & Crate Scaffold

**Goal:** Establish all pre-conditions before moving any Rust source files. Verify the `cupel-rs` crate name is available on crates.io, decide workspace layout and conformance vector strategy, write a complete standalone `Cargo.toml`, and configure Rust toolchain files. This phase is entirely pre-code gates — getting these wrong produces hard-to-reverse consequences.

**Dependencies:** Phase 15 (Conformance Hardening — `quota-basic.toml` promoted to required tier, conformance vector vendoring must account for this)

**Requirements:** MIGRATE-01, MIGRATE-02, MIGRATE-03, MIGRATE-04, MIGRATE-05

**Success Criteria:**
1. `cupel-rs` name availability verified on crates.io (or fallback name selected if squatted)
2. `rust-toolchain.toml` at repo root pins Rust 2024 edition with MSRV 1.85, includes `rustfmt` and `clippy` components
3. `.editorconfig` extended with Rust-specific rules; `.gitignore` includes `/crates/cupel/target/`
4. Standalone `Cargo.toml` at `crates/cupel/` with all required fields (`name`, `version`, `edition`, `rust-version`, `license`, `repository`, `description`, `categories`, `keywords`, `include`) and chosen version strategy — no workspace-inherited fields
5. `cargo check --manifest-path crates/cupel/Cargo.toml` passes on an empty `lib.rs` placeholder

**Plans:** 2 plans

Plans:
- [x] 16-01-PLAN.md — Verify crate name availability, create .gitignore, rust-toolchain.toml, and extend .editorconfig
- [x] 16-02-PLAN.md — Create standalone Cargo.toml and lib.rs placeholder, smoke-test with cargo check

---

#### Phase 17: Crate Migration & Conformance Verification

**Goal:** Move all Rust source files from `wollax/assay` into `crates/cupel/src/`, update conformance test vector paths, and verify the complete crate compiles, passes all conformance tests, and packages correctly for publishing.

**Dependencies:** Phase 16 (crate scaffold exists)

**Requirements:** MIGRATE-06, MIGRATE-07, CONFORM-01, CONFORM-02, CONFORM-03

**Success Criteria:**
1. All 26 `.rs` source files from `assay/crates/assay-cupel/src/` live at `cupel/crates/cupel/src/` and compile cleanly
2. Conformance test runner resolves vectors from `conformance/required/` (including promoted `quota-basic.toml`) via `CARGO_MANIFEST_DIR`-relative path with CI diff guard preventing divergence
3. `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` all pass with 28+ conformance vectors (28 original + promoted quota)
4. `cargo package --list` confirms all `.toml` conformance vectors appear in the tarball
5. Unpacked tarball verification: `tar xvf *.crate && cargo test` passes inside the unpacked directory

**Plans:** TBD

---

#### Phase 18: Dual-Language CI

**Goal:** Wire Rust CI into GitHub Actions alongside the existing .NET workflows. Separate workflow files with path filters ensure Rust changes trigger Rust CI and .NET changes trigger .NET CI. Release pipeline verified with dry-run before first publish.

**Dependencies:** Phase 17 (crate compiles and tests pass locally)

**Requirements:** CI-01, CI-02, CI-04, CI-05

**Success Criteria:**
1. `ci-rust.yml` triggers on PRs touching `crates/**`, `conformance/**`, `rust-toolchain.toml`, and self-referencing workflow path; runs `cargo fmt --check`, `cargo clippy -- -D warnings`, `cargo test`, and `cargo-deny check`
2. `release-rust.yml` with `workflow_dispatch` trigger, `dry-run` input, and `release` GitHub environment completes dry-run without error
3. Existing `.NET` CI workflow has path filters so Rust-only changes do not trigger .NET builds
4. `cargo-deny` configuration (`deny.toml`) exists and passes in CI
5. GitHub branch protection accepts skipped Rust CI status check on .NET-only PRs (and vice versa)

**Plans:** TBD

---

#### Phase 19: First Publish & Assay Switchover

**Goal:** Publish `cupel-rs` to crates.io, configure OIDC trusted publishing for future releases, and update `wollax/assay` to consume the crate from the registry instead of a path dependency. Delete the old `assay-cupel` directory from assay after verification.

**Dependencies:** Phase 18 (CI passing, release workflow verified dry-run)

**Requirements:** CI-03, SWITCH-01, SWITCH-02, SWITCH-03, SWITCH-04

**Success Criteria:**
1. `cupel-rs` is live on crates.io with correct metadata (readme rendered, categories visible, docs.rs build triggered)
2. OIDC trusted publishing configured on crates.io settings page — subsequent publishes use `release-rust.yml` workflow without personal API tokens
3. `wollax/assay` replaces `assay-cupel` path dependency with `cupel-rs = "VERSION"` from registry; imports renamed; all assay tests pass
4. `assay/crates/assay-cupel/` directory deleted from assay repo
5. `[patch.crates-io]` local development pattern documented in assay contributing guide

**Plans:** TBD

---

#### Phase 20: Serde Feature Flag

**Goal:** Add optional `serde` feature flag gating `Serialize`/`Deserialize` derives on all public data types. This is the highest-value differentiator for Rust consumers — most LLM-application users need serialization. Requires careful `ContextBudget` custom deserializer to maintain constructor validation invariants.

**Dependencies:** Phase 19 (crate published — this is a post-publish enhancement)

**Requirements:** ENHANCE-01, ENHANCE-02, ENHANCE-03

**Success Criteria:**
1. `features = ["serde"]` in `Cargo.toml` gates `serde::Serialize` and `serde::Deserialize` derives on all public data types (`ContextItem`, `ContextBudget`, `ContextKind`, `ContextSource`, `ScoredItem`, etc.)
2. `ContextBudget` uses a custom deserializer that validates inputs through the constructor — blind deserialization around validation is not possible
3. `cargo test` passes with `--features serde` and without (feature is additive, not breaking)
4. Crate re-published to crates.io as a minor version bump with the new feature

**Plans:** TBD

---

#### Phase 21: docs.rs Documentation & Examples

**Goal:** Make `cupel-rs` discoverable and approachable on docs.rs with crate-level quickstart documentation, module-level doc comments, and runnable examples. This is the single largest factor in new-user conversion from the docs.rs landing page.

**Dependencies:** Phase 20 (serde feature — docs should document the serde feature)

**Requirements:** ENHANCE-04, ENHANCE-05

**Success Criteria:**
1. Crate-level documentation in `lib.rs` includes a quickstart example that compiles as a doctest
2. Every public module has a `//!` module-level doc comment explaining its purpose; `[package.metadata.docs.rs]` configured with `all-features = true`
3. `examples/basic_pipeline.rs` exists and runs with `cargo run --example basic_pipeline`
4. `cargo doc --no-deps --all-features` builds with zero warnings

**Plans:** TBD

---

## Progress Summary

| Phase | Name | Requirements | Status |
|-------|------|-------------|--------|
| 1-15 | v1.0 Core Library | 44/44 requirements | SHIPPED |
| 16 | Pre-flight & Crate Scaffold | MIGRATE-01-05 | ● complete |
| 17 | Crate Migration & Conformance Verification | MIGRATE-06-07, CONFORM-01-03 | ○ planned |
| 18 | Dual-Language CI | CI-01-02, CI-04-05 | ○ planned |
| 19 | First Publish & Assay Switchover | CI-03, SWITCH-01-04 | ○ planned |
| 20 | Serde Feature Flag | ENHANCE-01-03 | ○ planned |
| 21 | docs.rs Documentation & Examples | ENHANCE-04-05 | ○ planned |
