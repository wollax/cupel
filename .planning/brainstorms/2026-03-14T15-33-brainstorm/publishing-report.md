# Publishing & CI/CD — Consolidated Report

**Topic**: Version coordination, CI/CD, and publishing for a dual-language spec (C#/.NET in `wollax/cupel`, Rust in `wollax/assay`)
**Explorer**: explorer-publishing | **Challenger**: challenger-publishing
**Date**: 2026-03-14
**Rounds**: 3

---

## Critical Context: Two Repos, Not a Monorepo

The Rust crate (`assay-cupel`) lives in `wollax/assay`, a separate GitHub repository. It does NOT live in the cupel repo. This fact — confirmed by Phase 12's context doc — reshapes every publishing and CI/CD proposal. Monorepo patterns (path filters, shared tags, unified CI matrices) do not apply.

---

## Recommendations (v1.0)

### 1. Independent Semver Per Repo

Each repo versions independently using its ecosystem's standard tooling:
- **cupel**: MinVer 7.0.0, git tags (`v1.x.x`), NuGet publishing via OIDC trusted publishing
- **assay**: `Cargo.toml` version field, standard `cargo publish` workflow

No cross-repo tagging, no shared version numbers, no coordination automation. This is the natural default for separate repos and requires zero implementation effort.

**Confidence**: High — this is how separate repos work. No debate.

### 2. Spec Version as the Compatibility Anchor

The spec version (e.g., "Cupel Spec 1.0") is a first-class field embedded in the conformance test vectors. Each implementation declares which spec version it targets:
- .NET: in NuGet package description and README
- Rust: in `Cargo.toml` metadata (`[package.metadata.cupel] spec_version = "1.0"`) and README

The user-facing compatibility rule: **implementations targeting the same spec major.minor are conformance-equivalent.** A one-line README note is sufficient for the target audience (coding agent developers).

Major.minor tracks the spec. Patch is independent per implementation. `cupel 1.2.7` and `assay-cupel 1.2.3` both implement Cupel Spec 1.2 and are interchangeable at the spec level.

**Confidence**: High — the challenger's refinement (spec version in vectors, not in tag schemes) is simpler and more robust than the original proposal.

### 3. Conformance Vectors as a Versioned Artifact

When cupel publishes a release, conformance test vectors are published as an accessible, versioned artifact. Two viable mechanisms:

| Mechanism | Pros | Cons |
|-----------|------|------|
| **GitHub Release asset** | Explicit versioning, downloadable via API, tied to release | Requires GitHub API or `gh` CLI to download |
| **GitHub Pages URL** | Public URL, already deployed by `spec.yml`, no API needed | URL structure must be designed; less explicit version pinning |

**Recommendation**: Start with GitHub Release assets (simpler, more explicit). Consider Pages URLs later if more implementations emerge and want frictionless vector access.

**Vector pinning**: Assay pins to a specific vector release version and bumps it deliberately — like a dependency version. This prevents cupel spec updates from silently breaking assay's CI. The pin lives in assay's CI workflow or a config file (e.g., `CUPEL_SPEC_VERSION=1.0.0` in the workflow).

### 4. Per-Repo Conformance Testing

Each implementation runs conformance tests independently in its own CI:
- cupel: .NET conformance tests already exist and run in CI
- assay: Downloads pinned vector version, runs Rust conformance tests

No cross-repo gating, no cross-repo triggers, no shared CI infrastructure. Each repo is self-contained.

**Confidence**: High — minimal complexity, validates the spec claim, no cross-repo coordination needed.

### 5. Cupel CI/CD Stays As-Is

The existing cupel CI/CD is well-structured:
- `ci.yml`: Build + test on push/PR to main
- `release.yml`: Workflow dispatch, pack, consumption tests, OIDC publish, GitHub Release with auto-generated notes
- `spec.yml`: Path-triggered mdBook deploy to GitHub Pages

No changes needed for v1.0. The only addition is including conformance vectors as a release asset in `release.yml` (a few lines of YAML).

---

## Recommendations (v1.1)

### 6. Cross-Repo Conformance Gating — Essential, Deferred

**Why essential**: Two implementations of one spec without conformance guarantees are just two different libraries. The conformance story is what makes the dual-language offering valuable.

**Why deferred**: Cross-repo CI gating is complex (repository_dispatch, cross-repo checkout, dual-toolchain builds) and premature for v1.0 with one maintainer and two implementations.

**The spec-change flow** (document explicitly):
1. Spec change lands in cupel (new/modified conformance vector)
2. Cupel releases with updated vectors
3. Assay's CI detects new vectors when the pin is bumped
4. Assay tests fail (expected — new spec behavior not yet implemented)
5. Assay implements the new behavior
6. Assay releases

This flow is **by design** — spec changes in cupel are inherently breaking for assay's conformance CI. This is a feature, not a bug: it ensures implementations track the spec. But it must be documented so it's not a surprise.

**v1.1 implementation sketch**: A GitHub Actions workflow in cupel that, on release, sends a `repository_dispatch` event to assay. Assay's CI runs conformance tests against the new vectors and opens an issue if they fail. This is notification-based, not blocking.

---

## Rejected Ideas

| Idea | Reason for Rejection |
|------|---------------------|
| **Unified versioning** (single version for both) | Cross-repo atomic tagging is overhead with no payoff for a single-maintainer project. Forces coupled releases. |
| **Ecosystem-prefixed tags** (`dotnet/v1.0.0`, `rust/v1.0.0`) | Separate repos already have independent tag namespaces. Prefixed tags solve a monorepo problem that doesn't exist. |
| **Dual CI matrix with path filters** | No Rust code in cupel repo. Path filters are a monorepo pattern. |
| **Crates.io publishing in cupel repo** | Publishing belongs in `wollax/assay`. Out of scope for this brainstorm. |
| **Git submodules for conformance vectors** | Brittle, poor DX, version pinning via release artifacts is simpler. |

---

## Out of Scope (Assay Repo Concerns)

These are valid topics but belong in `wollax/assay`'s planning, not cupel's:
- Crates.io publishing workflow (scoped API token + GitHub Environment)
- Assay CI/CD setup (Rust toolchain, clippy, tests)
- MSRV (minimum supported Rust version) policy
- `Cargo.toml` metadata (description, license, repository URL)

---

## Action Items for Cupel

1. **Add spec version field to conformance vectors** — small schema change
2. **Include conformance vectors as GitHub Release asset** — a few lines in `release.yml`
3. **Document the spec-version coordination scheme** — README section or spec chapter
4. **Document the spec-change → implementation-update flow** — for future contributors

All are small scope. The main insight is that cupel's CI/CD is already well-structured and needs minimal changes to support the dual-language story.

---

## Debate Summary

| Round | Key Shift |
|-------|-----------|
| 1 (Explorer) | 6 proposals assuming monorepo structure |
| 1 (Challenger) | Flagged critical assumption: repos are separate, not monorepo. Rejected 3 ideas outright. |
| 2 (Explorer) | Conceded monorepo assumption. Accepted independent versioning. Defended spec-anchored coordination and conformance-gating as essential. |
| 2 (Challenger) | Conceded "do nothing" was too dismissive. Accepted "essential but deferred" for conformance gating. Added vector pinning and sequencing details. |
| 3 (Converged) | Agreed on all 6 recommendations. Final refinement on artifact mechanism and spec-change flow documentation. |
