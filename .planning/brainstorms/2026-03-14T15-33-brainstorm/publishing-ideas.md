# Publishing & CI/CD Ideas — Explorer

## Current State Summary

- **CI**: GitHub Actions (`ci.yml`) — build+test on push/PR to main
- **Release**: `release.yml` — workflow_dispatch, packs NuGet, runs consumption tests, publishes via OIDC trusted publishing, creates GitHub Release with auto-generated notes
- **Versioning**: MinVer 7.0.0 via `Directory.Packages.props`, no `MinVerTagPrefix` set (defaults to `v` prefix on tags)
- **Tags**: No tags currently in repo (fresh project)
- **Spec**: `spec.yml` — path-triggered mdBook deploy to GitHub Pages
- **Packages**: 4 NuGet packages, all versioned identically via MinVer

---

## Idea 1: Unified Version, Prefixed Tags

**What**: Keep a single version number for both .NET and Rust. Use tag format `v{major}.{minor}.{patch}` as the single source of truth. MinVer reads it natively. Rust side uses a pre-publish script or `cargo-set-version` to stamp `Cargo.toml` from the git tag before `cargo publish`. One GitHub Release per version containing both ecosystems.

**Why**: Cupel and assay-cupel implement the *same spec*. Consumers benefit from knowing "v1.2.0 of the .NET library matches v1.2.0 of the Rust crate" — it's a conformance guarantee. Simplifies communication ("what version are you on?") and reduces coordination overhead. The spec itself has a version; the implementations should track it.

**Scope**: Small. Add a step in the release workflow to extract version from git tag, stamp `Cargo.toml`, run `cargo publish`. Maybe 50 lines of workflow YAML.

**Risks**:
- Rust crate may need to ship a patch that doesn't affect .NET (or vice versa). Unified versioning forces a version bump on both even when only one changed.
- If crate and NuGet packages diverge in patch-level fixes, the "same version = same behavior" guarantee weakens.
- `cargo publish` requires `Cargo.toml` version to match — need to either commit the version bump or use `--allow-dirty`.

---

## Idea 2: Independent Versions, Ecosystem-Prefixed Tags

**What**: Each ecosystem gets its own version and tag prefix. .NET uses `dotnet/v1.0.0` tags (set `MinVerTagPrefix` to `dotnet/v`). Rust uses `rust/v1.0.0` tags. Separate release workflows triggered by their respective tag patterns. Independent GitHub Releases (e.g., "Cupel .NET v1.2.0" and "assay-cupel v1.0.3").

**Why**: Decouples release cadence. Rust crate can iterate independently — critical if the Rust ecosystem has different adoption timelines or needs faster patch cycles. Matches how most polyglot monorepos work (e.g., googleapis, aws-sdk). Each ecosystem's versioning follows its own conventions.

**Scope**: Medium. Requires:
- Setting `MinVerTagPrefix` in `Directory.Build.props`
- Separate `release-rust.yml` workflow
- Two sets of tag-based triggers
- Possibly separate changelogs

**Risks**:
- Users must track two version numbers. "Which Rust version corresponds to which .NET version?" becomes a FAQ.
- More operational complexity — two release processes to maintain, test, debug.
- Risk of implementations drifting apart without a forced version sync.

---

## Idea 3: Spec-Anchored Versioning with Independent Patches

**What**: Hybrid approach. Major.Minor tracks the *spec version* (both .NET and Rust share it). Patch is independent per ecosystem. Tag format: `dotnet/v1.2.3` and `rust/v1.2.1`. The spec itself is versioned (e.g., "Cupel Spec 1.2") and both implementations must be at the same major.minor when they claim spec conformance.

**Why**: Best of both worlds. The major.minor communicates spec conformance ("both are Cupel 1.2 implementations"). The independent patch allows bug fixes without forcing cross-ecosystem releases. Users can reason about compatibility: "any 1.2.x .NET package is conformant with any 1.2.x Rust crate."

**Scope**: Medium. Requires spec version tracking, conformance test gates, and clear documentation of the versioning scheme.

**Risks**:
- Unusual versioning scheme may confuse contributors or users who expect standard semver.
- Need a clear process for "who bumps minor?" — presumably when the spec changes.
- Conformance test suite must be robust enough to justify the claim.

---

## Idea 4: Dual CI Matrix with Path Filters

**What**: Expand `ci.yml` into a matrix that conditionally builds/tests each ecosystem based on changed paths. Use GitHub's `paths` filter and `dorny/paths-filter` action for fine-grained control:
- `src/**`, `tests/**`, `*.sln`, `Directory.*` → run .NET jobs
- `rust/**`, `Cargo.*` → run Rust jobs
- `spec/**`, `conformance/**` → run both + spec deploy
- Always run both on `main` push (safety net)

**Why**: Saves CI minutes. A docs-only change shouldn't trigger a full Rust build. A Rust-only fix shouldn't rebuild 4 NuGet packages. Path filtering is the standard pattern for monorepos and GitHub Actions supports it natively.

**Scope**: Small-Medium. Refactor `ci.yml` to use `dorny/paths-filter` or native `paths:` with a matrix strategy. Add Rust toolchain setup (`dtolnay/rust-toolchain`).

**Risks**:
- Path filters can miss transitive changes (e.g., spec change that should trigger both but the path glob is too narrow).
- First-time setup of cross-language matrix can be fiddly.
- Over-filtering may skip necessary builds; under-filtering negates the benefit.

---

## Idea 5: Crates.io Publishing via API Token + GitHub Environments

**What**: Use a GitHub Environment (`crates-io`) with environment protection rules (required reviewers, main-branch-only) to store the `CARGO_REGISTRY_TOKEN`. The release workflow runs `cargo publish` after tests pass. Unlike NuGet's OIDC trusted publishing, crates.io currently requires API tokens.

**Why**: crates.io doesn't support OIDC/trusted publishing yet (as of early 2026). API tokens stored in GitHub Environments with protection rules are the next best thing — they provide approval gates, audit logs, and branch restrictions. GitHub Environments also enable the dry-run pattern already used in the NuGet workflow.

**Scope**: Small. Create a GitHub Environment, store the token, add `cargo publish` step to the release workflow. ~20 lines of YAML.

**Risks**:
- API tokens can be leaked if workflows are misconfigured (but environment protection mitigates this).
- Token rotation is manual (set a calendar reminder).
- If crates.io adds trusted publishing later, migration effort is small.

---

## Idea 6: Conformance-Gated Releases

**What**: Neither .NET nor Rust can publish unless the cross-language conformance test suite passes. The release workflow runs the conformance tests as a required job before the publish jobs. The conformance suite uses the spec's canonical test vectors and validates both implementations produce identical results.

**Why**: The entire point of having two implementations is spec conformance. If they diverge, the dual-language story is worse than having one language. Making conformance tests a publish gate ensures the promise holds. This is the "quality gate" that justifies the monorepo.

**Scope**: Medium-Large. Requires:
- Conformance test infrastructure that can validate both implementations
- Test vectors in a language-neutral format (JSON/TOML)
- CI job that builds both, runs both against the vectors, compares results
- Could reuse the existing `conformance/` directory

**Risks**:
- Conformance tests become a release bottleneck. A bug in one ecosystem blocks the other from releasing.
- False positives in conformance tests create unnecessary friction.
- Maintaining language-neutral test vectors is ongoing work.
- Mitigation: allow independent patch releases to skip cross-conformance if only fixing ecosystem-specific bugs (not spec behavior).
