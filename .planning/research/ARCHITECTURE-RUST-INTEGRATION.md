# Architecture Research: Rust Crate Integration into .NET Monorepo

**Context**: Moving `assay-cupel` from `wollax/assay` into `wollax/cupel`.
**Decision already made**: Crate lives at `crates/cupel/`, published as `cupel-rs`, conformance vectors vendored in crate with CI diff guard.

---

## 1. CI Workflow Organization for Dual-Language Repos (Rust + .NET)

### Pattern: Separate workflow files, path-filtered triggers

The industry standard for monorepos with heterogeneous stacks is **separate workflow YAML files per language stack**, each scoped to its directory subtree via `paths:` triggers. This is preferable to a single workflow with conditional jobs because:

- PRs show distinct check names (`Rust CI` vs `CI`) — reviewers can identify failures at a glance
- Path filters prevent unnecessary spend: a pure spec change doesn't rebuild the Rust crate
- Failures are isolated; a broken Rust nightly toolchain doesn't block .NET merges

### Recommended file layout for this repo

```
.github/workflows/
  ci.yml           # existing: .NET build + test (no change needed)
  ci-rust.yml      # new: Rust build + test + clippy + fmt
  release.yml      # existing: NuGet publish (no change needed)
  release-rust.yml # new: cargo publish for cupel-rs
  spec.yml         # existing: mdBook deploy (no change needed)
```

### Path filter on the Rust workflow

```yaml
# .github/workflows/ci-rust.yml
on:
  push:
    branches: [main]
    paths:
      - 'crates/**'
      - '.github/workflows/ci-rust.yml'
  pull_request:
    branches: [main]
    paths:
      - 'crates/**'
      - '.github/workflows/ci-rust.yml'
```

The `.github/workflows/ci-rust.yml` self-reference in `paths:` is critical: without it, changes to the workflow file itself would not trigger the workflow.

### Rust job structure

```yaml
jobs:
  rust:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: crates/cupel
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@stable
        with:
          components: clippy, rustfmt
      - uses: Swatinem/rust-cache@v2
        with:
          workspaces: crates/cupel
      - run: cargo fmt --check
      - run: cargo clippy -- -D warnings
      - run: cargo test
```

`defaults.run.working-directory` is the cleanest way to scope all steps to the crate subdirectory without repeating `--manifest-path` on every command. `Swatinem/rust-cache@v2` with `workspaces` pointing at the crate directory correctly scopes the cache key to `crates/cupel/Cargo.lock`.

### Real-world precedent

The `reifujimura/monorepo-with-csharp-rust` repo demonstrates combined Rust + .NET Core coexistence. The LogRocket and oneuptime.com monorepo CI guides consistently recommend the path-filter + separate-workflow pattern over single-workflow conditional jobs.

---

## 2. .gitignore Pattern for Rust `target/` in a Non-Rust-Primary Repo

### The problem

The GitHub-canonical `Rust.gitignore` uses the bare pattern `target`, which matches **any** directory named `target` anywhere in the repo. This is inappropriate for a repo where Rust is a secondary concern.

### Recommended pattern

Add to the root `.gitignore` (which does not currently exist in this repo):

```gitignore
# Rust build artifacts
/crates/cupel/target/
```

**Why path-anchored?**

- Ignores only the exact build output for this crate
- Does not shadow any .NET artifacts, test output dirs, or spec build dirs
- Does not require relying solely on the `target/` local `.gitignore` that `cargo new` generates in `crates/cupel/`

**Alternative considered**: `/crates/**/target/` — acceptable if multiple crates will live under `crates/` in the future. Given the current single-crate plan, the explicit path is preferable for clarity. Upgrade to the glob form if/when a second crate is added.

**Do not use**:
- Bare `target` — matches too broadly across the whole repo tree
- `/target` — wrong anchoring, misses the `crates/cupel/` prefix entirely

### cargo-generated `.gitignore` inside `crates/cupel/`

`cargo new` places a `/target` line in `crates/cupel/.gitignore`. Commit that file as-is. It correctly anchors to `crates/cupel/target/` relative to itself and provides a self-documenting signal for contributors who open only the crate directory. The root-level entry and the crate-level entry are complementary and non-conflicting.

---

## 3. Conformance Vector Include/Exclude in `Cargo.toml` for `cargo publish`

### The choice: `include` vs `exclude`

Cargo's `include` and `exclude` fields are **mutually exclusive** — setting `include` overrides `exclude`. For a crate with vendored conformance vectors, the `include` field is the right choice because it makes the published file set **explicit and auditable** rather than relying on exclusions.

### File size consideration

crates.io enforces a **10 MB limit** on the `.crate` tarball. Conformance vectors are JSON/text fixtures — typical sizes are well under this limit. The Rust Users Forum consensus is: exclude if large or not needed to build; include if needed for doc tests or to enable `cargo test` from the published tarball.

### Decision for `cupel-rs`

The conformance vectors serve two purposes:
1. Validation during `cargo test` in the local checkout (needed in dev/CI)
2. The vendored copy is the source of truth — downstream consumers of the library do not use them directly

**Recommendation**: Include the vectors in the published crate. Rationale:

- They enable `cargo test` to pass for anyone who downloads and audits the published `.crate`
- Conformance test coverage is a key quality signal; hiding it from the published package would break integration tests when the crate is built from the tarball
- crates.io allows re-running tests from published source; including vectors preserves this capability

### Recommended `Cargo.toml` snippet

```toml
[package]
name = "cupel-rs"
# ...

include = [
  "src/**/*",
  "tests/**/*",
  "conformance/**/*",   # vendored conformance vectors
  "Cargo.toml",
  "README.md",
  "LICENSE*",
  "CHANGELOG.md",
]
```

The `Cargo.toml` itself, a minimized `Cargo.lock`, and any `license-file` are always included by Cargo regardless of this list.

**What to exclude (by omission from `include`)**:
- `benches/` — benchmarks are dev-only
- `.github/` — CI config is not relevant to library users
- Any spec or planning files

---

## 4. docs.rs Build for a Crate with Test Fixtures

### How docs.rs builds crates

docs.rs clones the `.crate` tarball from crates.io (not the GitHub repo) and runs `cargo doc` in a sandboxed, **network-isolated** environment. This means:

- Files not in the published `.crate` (per `include`) are **not available** to docs.rs
- If doc tests (`///` examples) reference fixture files via relative paths, those files must be in the tarball
- The `CARGO_MANIFEST_DIR` env var is set to the crate root during doc builds, enabling `include_str!` and path resolution in examples

### Configuration via `[package.metadata.docs.rs]`

```toml
[package.metadata.docs.rs]
# Enable all features so all public items appear in rendered docs
all-features = true
```

Other options available:
- `rustc-args` — pass additional compiler flags
- `rustdoc-args` — e.g., `["--cfg", "docsrs"]` to enable `#[cfg(docsrs)]` gates on nightly-only doc attributes
- `targets` — restrict to specific platforms if the crate is not cross-platform

### Fixture file access in doctests

If any `///` doctest examples load fixture files, use `CARGO_MANIFEST_DIR` for path resolution:

```rust
/// ```
/// let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
///     .join("conformance/required/pipeline/some_vector.json");
/// ```
```

This works in both local `cargo test` and docs.rs builds, provided the file is in the published tarball (guaranteed by the `include` list above).

### Key constraint

docs.rs does not run integration tests (`tests/` directory) — only doc tests and examples. Integration tests that load conformance vectors will not run on docs.rs. They run in CI via `cargo test` on the full checkout. This is expected and standard.

---

## 5. `cargo publish` Flow for a Crate Not at Repo Root

### Two equivalent approaches

**Option A: `--manifest-path` flag** (run from repo root)

```bash
cargo publish --manifest-path crates/cupel/Cargo.toml
```

Cargo resolves all paths relative to the manifest location. Note: the `target/` directory will be created at `crates/cupel/target/`, not at the repo root. This is documented, intentional behavior (rust-lang/cargo issue #875).

**Option B: `working-directory` in CI** (recommended)

```yaml
- name: Publish to crates.io
  working-directory: crates/cupel
  run: cargo publish
  env:
    CARGO_REGISTRY_TOKEN: ${{ secrets.CARGO_REGISTRY_TOKEN }}
```

This mirrors local development (`cd crates/cupel && cargo publish`) and avoids any path resolution ambiguity. When `defaults.run.working-directory` is set at the job level, all steps automatically use it — no per-step repetition.

### Workspace detection hazard

Cargo walks up the directory tree looking for a `[workspace]` declaration. Because this repo has no `Cargo.toml` at the root (it is a .NET primary repo), there is no workspace root to accidentally pick up. This is an advantage: the crate at `crates/cupel/` is a **standalone crate** with no implicit workspace relationship.

**Future hazard**: If a root `Cargo.toml` is ever added (e.g., for Rust benchmarks), it must either:
- Declare `[workspace]` with `members = ["crates/cupel"]`, OR
- Ensure the standalone crate is not auto-detected as a workspace member

For now, no special configuration is needed.

### Full release workflow sketch

```yaml
# .github/workflows/release-rust.yml
name: Publish cupel-rs to crates.io

on:
  workflow_dispatch:
    inputs:
      dry-run:
        description: 'Dry run — package and verify without publishing'
        type: boolean
        default: false

jobs:
  publish:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: crates/cupel
    steps:
      - uses: actions/checkout@v4

      - uses: dtolnay/rust-toolchain@stable

      - name: Verify package contents
        run: cargo package --list

      - name: Dry run
        if: ${{ inputs.dry-run }}
        run: cargo publish --dry-run

      - name: Publish
        if: ${{ inputs.dry-run != true }}
        run: cargo publish
        env:
          CARGO_REGISTRY_TOKEN: ${{ secrets.CARGO_REGISTRY_TOKEN }}
```

`cargo package --list` before publish is a key sanity check — it prints every file that will be in the tarball, letting you verify conformance vectors are included and no extra files slipped in.

### Versioning and tagging convention

NuGet packages in this repo use version numbers from MSBuild properties, tagged `v1.0.0`, `v1.1.0`, etc. The Rust crate needs its own version in `Cargo.toml`. To avoid collision, use a prefixed tag for Rust releases:

```
cupel-rs-v0.1.0
```

The release workflow should be triggered manually (as the existing NuGet `release.yml` is) and tag after successful publish. This is the standard convention for monorepos publishing multiple artifacts.

---

## Summary Table

| Concern | Decision | Rationale |
|---|---|---|
| CI workflow structure | Separate `ci-rust.yml` + `release-rust.yml`, path-filtered to `crates/**` | Isolation; no cross-stack failures; cheaper CI |
| `.gitignore` pattern | `/crates/cupel/target/` in root `.gitignore` | Precise, non-greedy, safe alongside .NET dirs |
| Conformance vectors in publish | Included via `[package] include = [...]` | Enables `cargo test` from published tarball; audit-friendly |
| docs.rs fixtures | Include conformance vectors in tarball; use `CARGO_MANIFEST_DIR` in doctests | docs.rs is network-isolated; files must be in tarball |
| cargo publish from subdirectory | `defaults.run.working-directory: crates/cupel` in CI; bare `cargo publish` | Mirrors local dev; avoids `--manifest-path` path ambiguity |
| Release tagging | `cupel-rs-vX.Y.Z` prefix | Avoids collision with .NET `vX.Y.Z` tags |
| Workspace detection | No root `Cargo.toml` — standalone crate, no action needed | Non-Rust-primary repo has no workspace to conflict with |
