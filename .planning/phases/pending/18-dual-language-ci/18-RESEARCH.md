# Phase 18: Dual-Language CI - Research

**Researched:** 2026-03-14
**Domain:** GitHub Actions CI/CD for dual Rust/.NET monorepo
**Confidence:** HIGH

## Summary

This phase wires Rust CI into GitHub Actions alongside the existing .NET workflows. The existing `.NET` CI (`ci.yml`) has no path filters — it triggers on all pushes/PRs to `main`. The existing `release.yml` uses `workflow_dispatch` with a `dry-run` boolean, OIDC for NuGet, and `softprops/action-gh-release@v2`. A third workflow (`spec.yml`) already uses path filters for `spec/**` and `conformance/**`.

The standard Rust CI stack on GitHub Actions is well-established: `dtolnay/rust-toolchain` for toolchain setup, `Swatinem/rust-cache@v2` for build caching, and `EmbarkStudios/cargo-deny-action@v2` for dependency linting. The main complexity is handling branch protection when path-filtered workflows are skipped — GitHub does not natively mark skipped workflows as "passed", leaving required checks in a "Pending" state.

**Primary recommendation:** Use a gate job pattern where the required branch protection check is a lightweight gate job that always runs, depends on the actual CI jobs (which use job-level `if` conditionals based on changed files), and succeeds when those jobs either pass or are correctly skipped. Alternatively, since this is a small project, simply don't make the individual CI workflows required checks — make them informational and rely on the gate pattern only if branch protection becomes necessary.

## Standard Stack

### Core Actions

| Action | Version | Purpose | Why Standard |
|--------|---------|---------|--------------|
| `dtolnay/rust-toolchain` | `@1.85.0` (pin to MSRV) | Install Rust toolchain | De facto standard, 1.5k stars, reads components from input |
| `Swatinem/rust-cache` | `@v2` (v2.9.1) | Cache cargo registry + target | Standard Rust CI caching, workspace-aware |
| `EmbarkStudios/cargo-deny-action` | `@v2` (v2.0.15) | Run cargo-deny checks | Official action from cargo-deny maintainers |
| `actions/checkout` | `@v4` | Checkout code | Already used by existing workflows |
| `softprops/action-gh-release` | `@v2` | Create GitHub Release | Already used by `release.yml` |

### Supporting Actions

| Action | Version | Purpose | When to Use |
|--------|---------|---------|-------------|
| `actions/upload-artifact` | `@v4` | Upload build artifacts | Already used by `release.yml` for nupkg |
| `actions/download-artifact` | `@v4` | Download build artifacts | Already used by `release.yml` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `dtolnay/rust-toolchain` | `actions-rust-lang/setup-rust-toolchain` | More features (problem matchers, auto-cache) but less widespread; dtolnay's is simpler and sufficient |
| `EmbarkStudios/cargo-deny-action` | Manual `cargo install cargo-deny && cargo deny check` | Action handles caching and version management; manual approach wastes CI time |
| `Swatinem/rust-cache` | No caching | Acceptable for small crate but wastes ~1-2 min per run; cache is free |

### Installation (local development)

```bash
cargo install cargo-deny
cargo deny init   # generates deny.toml scaffold
```

## Architecture Patterns

### Recommended Workflow Structure

```
.github/workflows/
├── ci.yml              # .NET CI (existing, add path filters)
├── ci-rust.yml         # NEW: Rust CI
├── release.yml         # .NET release (existing, unchanged)
├── release-rust.yml    # NEW: Rust release
└── spec.yml            # Spec deploy (existing, unchanged)

crates/cupel/
├── deny.toml           # NEW: cargo-deny configuration
├── Cargo.toml          # Existing
└── ...
```

Note: `deny.toml` lives inside `crates/cupel/` (next to `Cargo.toml`) since there is no workspace-level `Cargo.toml`. The `cargo-deny-action` accepts a `manifest-path` input to point to this location.

### Pattern 1: Rust CI Workflow (`ci-rust.yml`)

**What:** Separate workflow for Rust CI triggered by path filters.
**When to use:** Every PR touching Rust code.

```yaml
name: Rust CI

on:
  push:
    branches: [main]
    paths:
      - 'crates/**'
      - 'rust-toolchain.toml'
      - '.github/workflows/ci-rust.yml'
  pull_request:
    branches: [main]
    paths:
      - 'crates/**'
      - 'rust-toolchain.toml'
      - '.github/workflows/ci-rust.yml'

jobs:
  check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@1.85.0
        with:
          components: rustfmt, clippy
      - uses: Swatinem/rust-cache@v2
        with:
          workspaces: "crates/cupel -> target"
      - name: Format check
        run: cargo fmt --check --manifest-path crates/cupel/Cargo.toml
      - name: Clippy
        run: cargo clippy --manifest-path crates/cupel/Cargo.toml -- -D warnings
      - name: Test
        run: cargo test --manifest-path crates/cupel/Cargo.toml
      - name: Deny check
        uses: EmbarkStudios/cargo-deny-action@v2
        with:
          manifest-path: crates/cupel/Cargo.toml
```

### Pattern 2: .NET CI with Path Filters (modified `ci.yml`)

**What:** Add path filters to existing .NET CI so Rust-only changes don't trigger .NET builds.
**Key detail:** Use positive `paths` filters listing .NET-relevant paths.

```yaml
on:
  push:
    branches: [main]
    paths:
      - 'src/**'
      - 'tests/**'
      - 'benchmarks/**'
      - '*.slnx'
      - '*.props'
      - 'global.json'
      - '.github/workflows/ci.yml'
  pull_request:
    branches: [main]
    paths:
      - 'src/**'
      - 'tests/**'
      - 'benchmarks/**'
      - '*.slnx'
      - '*.props'
      - 'global.json'
      - '.github/workflows/ci.yml'
```

### Pattern 3: Rust Release Workflow (`release-rust.yml`)

**What:** Manual release workflow mirroring the .NET `release.yml` pattern.
**Structure:** Full test suite -> dry-run pack -> conditional publish -> GitHub Release.

```yaml
name: Publish to crates.io

on:
  workflow_dispatch:
    inputs:
      dry-run:
        description: 'Dry run - test and pack without publishing'
        required: false
        type: boolean
        default: false

permissions:
  contents: write

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@1.85.0
        with:
          components: rustfmt, clippy
      - name: Format check
        run: cargo fmt --check --manifest-path crates/cupel/Cargo.toml
      - name: Clippy
        run: cargo clippy --manifest-path crates/cupel/Cargo.toml -- -D warnings
      - name: Test
        run: cargo test --manifest-path crates/cupel/Cargo.toml
      - name: Deny check
        uses: EmbarkStudios/cargo-deny-action@v2
        with:
          manifest-path: crates/cupel/Cargo.toml
      - name: Dry-run package
        run: cargo package --manifest-path crates/cupel/Cargo.toml

  publish:
    runs-on: ubuntu-latest
    needs: test
    if: ${{ inputs.dry-run != true }}
    environment: release
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@1.85.0
      - name: Verify running from main
        if: github.ref != 'refs/heads/main'
        run: |
          echo "::error::Release workflow must be run from the main branch"
          exit 1
      - name: Publish
        run: cargo publish --manifest-path crates/cupel/Cargo.toml
        env:
          CARGO_REGISTRY_TOKEN: ${{ secrets.CARGO_REGISTRY_TOKEN }}
      - name: Get version
        id: version
        run: |
          VERSION=$(cargo metadata --manifest-path crates/cupel/Cargo.toml --format-version 1 --no-deps | jq -r '.packages[0].version')
          echo "version=$VERSION" >> $GITHUB_OUTPUT
      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          tag_name: rust-v${{ steps.version.outputs.version }}
          name: cupel (Rust) v${{ steps.version.outputs.version }}
          generate_release_notes: true
```

### Pattern 4: Conformance Vector Path Overlap

**Analysis of `conformance/**` trigger scope:**

The Rust crate has its own **vendored copy** of conformance vectors at `crates/cupel/conformance/` (created in Phase 17). The root `conformance/` directory is the source of truth used by `spec.yml`. The .NET tests do NOT reference `conformance/` directly.

Therefore:
- `conformance/**` (root) changes should trigger `spec.yml` (already configured) but NOT Rust or .NET CI
- `crates/cupel/conformance/**` changes are covered by the `crates/**` glob in `ci-rust.yml`
- No overlap issue exists because the Rust crate vendors its own copy

### Anti-Patterns to Avoid

- **Using `paths-ignore` instead of `paths`:** Negative filters are fragile — new directories trigger workflows unexpectedly. Positive `paths` filters are self-documenting and explicit.
- **Making path-filtered workflows required checks in branch protection:** GitHub leaves skipped workflows in "Pending" state, blocking PRs. Either use a gate job or keep these checks informational.
- **Running `cargo deny` without pinning the action version:** The action bundles its own cargo-deny version; pin `@v2` to avoid surprise breakage.
- **Caching with wrong workspace path:** `Swatinem/rust-cache` defaults to `. -> target`. For this repo, must use `crates/cupel -> target`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|------------|-------------|-----|
| Rust toolchain installation | Shell script to install rustup | `dtolnay/rust-toolchain@1.85.0` | Handles components, caching, and version pinning |
| Dependency auditing | `cargo audit` + manual advisory DB | `EmbarkStudios/cargo-deny-action@v2` | Covers licenses, bans, advisories, sources in one tool |
| Cargo build caching | Manual `actions/cache` with cargo paths | `Swatinem/rust-cache@v2` | Knows which paths to cache, handles key rotation |
| GitHub Release creation | Manual `gh release create` in script | `softprops/action-gh-release@v2` | Already used by .NET workflow, handles edge cases |

## Common Pitfalls

### Pitfall 1: Skipped Workflows Block Branch Protection

**What goes wrong:** A workflow with `paths` filters is made a required status check. When a PR doesn't touch those paths, the workflow never runs, and the check stays in "Pending" forever — blocking merge.
**Why it happens:** GitHub does not report path-skipped workflows as "skipped" or "success"; they simply never start.
**How to avoid:** Two approaches:
1. **Simple (recommended for this project):** Don't make `ci-rust.yml` or `ci.yml` required checks individually. Instead, either keep them informational or create a single gate workflow that uses `dorny/paths-filter` or `tj-actions/changed-files` internally to decide which checks to run, and make only the gate job required.
2. **Gate job pattern:** A single always-running workflow with a final gate job that checks outcomes of conditional jobs. The gate job is the only required check.
**Warning signs:** PRs stuck in "Waiting for status to be reported" state.

### Pitfall 2: `--manifest-path` Required for Non-Root Crates

**What goes wrong:** `cargo fmt --check`, `cargo clippy`, `cargo test` all default to the current directory. Without `--manifest-path crates/cupel/Cargo.toml`, they fail or silently do nothing.
**Why it happens:** No `Cargo.toml` at repository root.
**How to avoid:** Every cargo command must include `--manifest-path crates/cupel/Cargo.toml`.
**Warning signs:** "could not find `Cargo.toml` in `/home/runner/work/...`" error.

### Pitfall 3: `cargo fmt` Needs Nightly for Some Edition 2024 Features

**What goes wrong:** `cargo fmt --check` may behave differently between stable 1.85 and nightly for edition 2024 code.
**Why it happens:** Some formatting rules for edition 2024 may not be fully stable in 1.85.
**How to avoid:** The toolchain is pinned to 1.85.0 in `rust-toolchain.toml` which includes `rustfmt` component. Use whatever rustfmt ships with 1.85.0. If formatting issues arise, they were already resolved in Phase 17 (17-01-D1 applied `cargo fmt` for edition 2024).
**Warning signs:** CI format failures that don't reproduce locally.

### Pitfall 4: `cargo-deny` Config Location

**What goes wrong:** `cargo-deny` looks for `deny.toml` in the same directory as `Cargo.toml` by default, or in the workspace root.
**Why it happens:** No workspace root `Cargo.toml` exists.
**How to avoid:** Place `deny.toml` at `crates/cupel/deny.toml`. The `EmbarkStudios/cargo-deny-action@v2` accepts `manifest-path` input which tells it where to find the crate (and its `deny.toml`).
**Warning signs:** "failed to load deny configuration" error.

### Pitfall 5: Rust Release Tag Collision with .NET

**What goes wrong:** Both .NET and Rust releases use `v{version}` tags, potentially colliding.
**Why it happens:** Same version numbering (e.g., both could be `v1.0.0`).
**How to avoid:** Use a prefix for Rust tags: `rust-v{version}` (e.g., `rust-v1.0.0`). The .NET workflow uses plain `v{version}`.
**Warning signs:** Tag already exists error during release.

## Code Examples

### cargo-deny Configuration for Permissive Open-Source Crate

Recommended `deny.toml` for a MIT-licensed library crate:

```toml
# deny.toml — cargo-deny configuration for cupel
# https://embarkstudios.github.io/cargo-deny/

[graph]
targets = []
all-features = false
no-default-features = false

[advisories]
version = 2
yanked = "deny"

[licenses]
version = 2
allow = [
    "MIT",
    "Apache-2.0",
    "Apache-2.0 WITH LLVM-exception",
    "BSD-2-Clause",
    "BSD-3-Clause",
    "ISC",
    "Unicode-3.0",
    "Unicode-DFS-2016",
    "Zlib",
]
confidence-threshold = 0.93

[bans]
multiple-versions = "warn"
wildcards = "deny"
highlight = "simplest-path"

[sources]
unknown-registry = "deny"
unknown-git = "deny"
allow-registry = ["https://github.com/rust-lang/crates.io-index"]
allow-org = { github = [] }
```

**License strategy rationale:** The cupel crate is MIT-licensed. The allowlist includes all common permissive licenses that are compatible with MIT. Copyleft licenses (GPL, LGPL, MPL) are excluded by omission — any dependency using them will fail CI. This is appropriate for a library crate that downstream consumers need to integrate freely.

**Advisory strategy:** `yanked = "deny"` fails CI on yanked crates (security measure). The default RustSec advisory database is used. No advisories need to be ignored for a new crate.

**Bans strategy:** `multiple-versions = "warn"` rather than `"deny"` because duplicate transitive dependencies are common and not always avoidable (e.g., `syn` 1.x vs 2.x). `wildcards = "deny"` prevents `*` version specs.

**Sources strategy:** Only crates.io is allowed. No git dependencies. This ensures reproducible builds and prevents supply chain attacks via git source substitution.

### Swatinem/rust-cache with Non-Root Workspace

```yaml
- uses: Swatinem/rust-cache@v2
  with:
    workspaces: "crates/cupel -> target"
```

### dtolnay/rust-toolchain with Pinned Version

```yaml
- uses: dtolnay/rust-toolchain@1.85.0
  with:
    components: rustfmt, clippy
```

Note: Using `@1.85.0` pins to the exact toolchain version matching `rust-toolchain.toml`. This is preferred over `@stable` for MSRV compliance (per decision 16-01-D2).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|-------------|-----------------|--------------|--------|
| `actions-rs/toolchain` | `dtolnay/rust-toolchain` | 2023 | actions-rs is unmaintained; dtolnay is the standard |
| `cargo audit` standalone | `cargo-deny` (superset) | 2022+ | cargo-deny covers licenses, bans, advisories, sources |
| Manual `actions/cache` for Rust | `Swatinem/rust-cache@v2` | 2023+ | Smart cache invalidation, knows Rust-specific paths |
| `EmbarkStudios/cargo-deny-action@v1` | `@v2` (v2.0.15) | 2024 | Bundles cargo-deny 0.19.0, improved defaults |

**Deprecated/outdated:**
- `actions-rs/*` actions: Unmaintained since 2022. Do not use.
- `cargo audit` alone: cargo-deny is a superset. Use cargo-deny.

## Open Questions

1. **Branch protection strategy**
   - What we know: Path-filtered workflows that are skipped stay in "Pending" state and block PRs if made required checks. Job-level `if` conditionals report as "Success" when skipped.
   - What's unclear: Whether the project currently has branch protection rules configured, and whether adding required checks is desired.
   - Recommendation: Start without making CI workflows required checks. Add branch protection later if needed, using the gate job pattern at that point. For a personal project this is pragmatic. If branch protection IS desired now, use a gate workflow pattern (single always-running workflow with conditional jobs based on changed files, and a final gate job as the only required check).

2. **`jq` availability on GitHub Actions runners**
   - What we know: `jq` is pre-installed on `ubuntu-latest` runners.
   - What's unclear: Whether `cargo metadata` + `jq` is the best way to extract version for release tags.
   - Recommendation: Use `cargo metadata --format-version 1 --no-deps | jq -r '.packages[0].version'` — this is reliable and standard.

## Sources

### Primary (HIGH confidence)
- Existing workflows: `.github/workflows/ci.yml`, `release.yml`, `spec.yml` — read directly
- `crates/cupel/Cargo.toml`, `rust-toolchain.toml` — read directly
- GitHub Actions docs (Context7 `/websites/github_en_actions`) — path filter syntax, event triggers
- cargo-deny docs (https://embarkstudios.github.io/cargo-deny/) — check categories, configuration fields

### Secondary (MEDIUM confidence)
- `EmbarkStudios/cargo-deny-action` README (https://github.com/EmbarkStudios/cargo-deny-action) — v2.0.15, inputs, usage
- `dtolnay/rust-toolchain` README (https://github.com/dtolnay/rust-toolchain) — inputs, pinning approach
- `Swatinem/rust-cache` README (https://github.com/Swatinem/rust-cache) — v2.9.1, workspace configuration
- GitHub community discussions on path filter + branch protection — multiple threads confirm the problem and workarounds

### Tertiary (LOW confidence)
- Pantsbuild blog on gate job pattern — describes approach but specific to their workflow generator

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — well-established Rust CI tooling, verified via official READMEs
- Architecture: HIGH — existing .NET workflows provide clear template to mirror
- Pitfalls: HIGH — path filter + branch protection issue is well-documented across multiple GitHub community threads
- cargo-deny config: MEDIUM — license allowlist is based on common patterns for permissive crates, may need tuning based on actual dependency tree

**Research date:** 2026-03-14
**Valid until:** 2026-04-14 (30 days — stable domain, actions versions may update)
