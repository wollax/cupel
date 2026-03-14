# Pitfalls: Rust Crate Migration & crates.io Publishing

**Date**: 2026-03-14
**Scope**: Moving `assay-cupel` from `wollax/assay` into `wollax/cupel` and publishing as `cupel-rs` on crates.io
**Context**: Subsequent milestone after v1.0 gap closure. The crate currently lives at `assay/crates/assay-cupel/`, uses workspace-inherited fields, and embeds 28 conformance test vectors under `tests/conformance/required/` loaded via `env!("CARGO_MANIFEST_DIR")`.
**Confidence key**: HIGH = verified against official docs / crates.io behavior, MEDIUM = verified via multiple sources, LOW = single source or reasoned inference

---

## 1. First Publish to crates.io

### 1.1 Name squatting — `cupel-rs` may already be taken [HIGH]

**What goes wrong**: crates.io names are first-come-first-served and are permanent. If `cupel-rs` is already registered (even by a squatter with an empty placeholder crate), you cannot use that name. The `cargo publish` command fails with a 422 error rather than a useful message. Reversing this requires contacting the crates.io team, which takes time.

**Warning signs**: No prior check was done. Running `cargo search cupel-rs` returns a result from an unrelated author.

**Prevention**:
- Run `cargo search cupel-rs` and visit `https://crates.io/crates/cupel-rs` before any other migration work begins. Do this on day one of the migration milestone.
- Simultaneously check adjacent names: `cupel`, `cupel-core`, `cupel_rs`. The name with the hyphen and the name with the underscore are treated as the same crate on crates.io (hyphens and underscores are equivalent in the registry index, but `Cargo.toml` must use the exact registered form).
- If the name is taken by a squatter, file a name-squatting report at `https://crates.io/policies` before starting implementation work. The process can take days to weeks.
- Consider whether `cupel` (no suffix) is a better name. The `-rs` suffix is a common convention when a name is taken in other ecosystems, but it is not required for Rust crates.

**Which phase**: Crate migration scaffold — first action, before writing any code.

---

### 1.2 Version 1.0.0 on first publish [MEDIUM]

**What goes wrong**: Choosing `1.0.0` on first publish signals stability that the crate may not yet have. In the Rust ecosystem, `1.0.0` communicates "stable, backward-compatible API". The SemVer rules enforced by `cargo` then prevent you from making breaking changes without a major version bump. More concretely: if any API decision made during migration turns out to be wrong, a 1.0.0 fixes you into it unless you publish 2.0.0, which is a bad look for a brand-new crate.

Additionally, `Cargo.lock` in downstream consumers treats pre-1.0 and 1.0+ differently with respect to automatic updates. Starting at 0.x gives you more flexibility.

**Counter-argument**: The C# package is publishing `1.0.0` simultaneously. Inconsistent versioning between sibling packages (NuGet `1.0.0` vs crates.io `0.1.0`) may confuse consumers who expect parity.

**Decision guidance**:
- If the Rust crate API is intentionally a strict subset or differs from the C# API (which it does — no DI, no JSON package, no policy system), starting at `0.1.0` is defensible and honest.
- If the project communicates "the Rust crate implements the same specification as v1.0 of the C# library", starting at `1.0.0` is coherent.
- Either way: document the version choice explicitly in the crate's CHANGELOG before publishing.

**Warning signs**: Shipping 1.0.0 without a public pre-release (`0.x` or `1.0.0-beta.1`). No consumers have tried the API in its new `cupel-rs` form before 1.0.0.

**Prevention**:
- Consider a `0.1.0` publish first, announce in the C# package README ("Rust: `cupel-rs` 0.1.0 on crates.io"), then bump to `1.0.0` after a short bake period.
- Alternatively, publish `1.0.0-beta.1` first. crates.io allows pre-release versions and they do not appear in `cargo add` without an explicit `--version` flag.

**Which phase**: Crate migration scaffold — version strategy decision.

---

### 1.3 `cargo publish` dry-run does not catch everything [HIGH]

**What goes wrong**: `cargo publish --dry-run` validates the package metadata and checks that the crate compiles, but it does NOT:
- Verify that all files listed in `include` actually exist
- Verify that `CARGO_MANIFEST_DIR` paths inside tests resolve correctly after packaging
- Check that the crates.io token has write permissions for the crate name
- Check that the `repository` URL is reachable or correct

The result is a dry-run that passes locally but a live publish that fails or produces a broken package.

**Warning signs**: Dry-run succeeds but you have never actually unpacked the `.crate` tarball to inspect it. You skipped `cargo package --list` to review the file manifest.

**Prevention**:
- Run `cargo package` (without `--dry-run`) to produce the `.crate` file locally, then `tar xvf *.crate` and inspect the tree.
- Run `cargo package --list` to see exactly which files will be included.
- After `cargo package`, run `cargo test` inside the unpacked directory to verify that test vector paths still resolve.
- In CI: produce the package on every PR, upload as an artifact, and only publish on tag push.

**Which phase**: CI scaffold for the Rust crate publish workflow.

---

### 1.4 Crates.io API token management in CI [HIGH]

**What goes wrong**: The `CARGO_REGISTRY_TOKEN` secret is added to the repository's CI environment but scoped incorrectly (e.g., available on pull requests from forks), or it is a token with overly broad permissions (all crates the account owns rather than just `cupel-rs`).

Additionally, crates.io now supports trusted publishing via OIDC (similar to NuGet Trusted Publishing used in the C# workflow). Using a static token when OIDC is available is a security downgrade.

**Warning signs**: Token is stored as a repository secret instead of an environment secret. The GitHub Actions workflow runs `cargo publish` on every push to `main`, not only on tag events.

**Prevention**:
- Use crates.io's trusted publishing (OIDC) if the account is enrolled. As of late 2024, crates.io supports GitHub Actions OIDC for `cargo publish`. This eliminates the need for a static token entirely.
- If using a static token: create a scoped token at `crates.io/settings/tokens` with "Publish new versions" scope restricted to `cupel-rs` only.
- Store the token as a GitHub Actions environment secret (not a repository secret), restricted to the `release` environment which requires manual approval.
- `cargo publish` should only run on `push: tags: ['v*']` — never on branch pushes.

**Which phase**: CI scaffold for publish workflow.

---

## 2. Moving Code Between Repos

### 2.1 `Cargo.toml` fields that were workspace-inherited must be filled in explicitly [HIGH]

**What goes wrong**: The current `assay-cupel/Cargo.toml` inherits nearly all metadata from the workspace:
```toml
version.workspace = true
edition.workspace = true
license.workspace = true
repository.workspace = true
```

When the crate moves to `wollax/cupel`, there is no workspace to inherit from (unless a Rust workspace is created inside the cupel repo). Every `field.workspace = true` becomes a compile error. More subtly, the `repository` field in the assay workspace points to `wollax/assay` — the new crate must point to `wollax/cupel`.

**Warning signs**: Running `cargo build` in the migrated crate directory immediately after copying the files fails with `error: no `workspace.package` entry in workspace manifest found`.

**Prevention**:
- Create a standalone `Cargo.toml` for `cupel-rs` with all fields explicit: `name`, `version`, `edition`, `license`, `repository`, `description`, `keywords`, `categories`, `homepage`, `readme`, `documentation`.
- The `repository` field must be `https://github.com/wollax/cupel`, NOT `https://github.com/wollax/assay`.
- The `documentation` field should point to `https://docs.rs/cupel-rs` (matches the crate name).
- `readme = "README.md"` must refer to a file that exists at the crate root (not the workspace root).
- Verify all fields pass `cargo publish --dry-run` validation.

**Which phase**: Crate migration scaffold.

---

### 2.2 `repository` field pointing to wrong repo [HIGH]

**What goes wrong**: This is a specific, high-visibility instance of 2.1. If the crate is published with `repository = "https://github.com/wollax/assay"`, then:
- The docs.rs page shows "Repository" linking to the wrong repo
- crates.io shows the wrong source link
- The Cargo.lock in consumer projects encodes the wrong repository
- This is permanent for the published version — you must publish a new version to fix it

**Warning signs**: Not verifying the crates.io page for the published crate immediately after the first publish.

**Prevention**:
- Explicit checklist before first publish: confirm `cargo metadata --format-version 1 | jq '.packages[] | select(.name == "cupel-rs") | .repository'` returns `https://github.com/wollax/cupel`.
- Add a CI lint step that greps `Cargo.toml` for the `assay` repository URL and fails if found.

**Which phase**: Crate migration scaffold + CI lint.

---

### 2.3 Path references in source code that assume assay workspace layout [MEDIUM]

**What goes wrong**: Any `include_str!()`, `include_bytes!()`, or hardcoded relative paths that assume the crate lives at `assay/crates/assay-cupel/` will silently break or produce wrong behavior. The conformance test runner already uses `env!("CARGO_MANIFEST_DIR")` correctly, but any other path construction that uses `../../` style relative paths to reach workspace-level files will break.

**Warning signs**: A test that includes a shared fixture from the workspace root (`../../schemas/something.json`) passes in the assay repo but panics in the migrated crate.

**Prevention**:
- Grep the entire `assay-cupel` source tree for `"../"` and `"../../"` string literals before migration. Any such path that escapes the crate root is a migration hazard.
- `env!("CARGO_MANIFEST_DIR")` is the correct pattern for test fixture paths — the conformance runner already uses it correctly.
- After migration, run `cargo test` from a clean checkout of the cupel repo (no assay repo present) to verify all paths resolve.

**Which phase**: Crate migration scaffold (pre-migration audit).

---

## 3. Conformance Test Vectors Across Repo Boundaries

### 3.1 Test vectors are duplicated, not shared — they will diverge [HIGH]

**What goes wrong**: Currently, the 28 required conformance test vectors exist in two places:
- `cupel/conformance/required/**/*.toml` (the canonical source, part of Phase 11)
- `assay/crates/assay-cupel/tests/conformance/required/**/*.toml` (copies made during Phase 12)

After migration, there will be a third copy inside `cupel-rs` within the `wollax/cupel` repo. Any time a conformance vector is updated (e.g., during Phase 15 gap closure which moves `quota-basic.toml` from optional to required), the copies must be updated in sync. This is a maintenance hazard that compounds over time.

**Warning signs**: Phase 15 moves `quota-basic.toml` to required tier in the spec, updates the C# conformance suite, but the Rust crate's embedded copies are not updated. The Rust crate passes its own tests but fails the actual spec conformance test.

**Prevention**:
- Long-term canonical solution: the `cupel-rs` crate should read test vectors from the `cupel/conformance/` directory via a `build.rs` script that copies them at build time, or via a Git submodule. This is the same pattern used in projects like `wasmparser` and `cranelift` that share test vectors across crates.
- Short-term (v1.1 migration): copy vectors during migration and add a CI step in `wollax/cupel` that diffs `conformance/required/` against `crates/cupel-rs/tests/conformance/required/` and fails if they differ.
- Medium-term: move vectors to a separate subdirectory within `wollax/cupel` and have the Rust test runner reference them via `CARGO_MANIFEST_DIR/../../../conformance/required/` (this works because the crate lives in the same repo as the spec).
- Document the source-of-truth policy explicitly: "The canonical test vectors are in `conformance/`. The Rust crate copies at `crates/cupel-rs/tests/conformance/` must be kept in sync."

**Which phase**: Crate migration scaffold + ongoing CI enforcement.

---

### 3.2 `cargo publish` excludes test fixtures unless explicitly included [HIGH]

**What goes wrong**: By default, `cargo publish` excludes the `tests/` directory content (`.rs` files are included; data files under `tests/` are NOT automatically included). The TOML test vectors at `tests/conformance/required/**/*.toml` will be **silently excluded** from the published crate unless the `Cargo.toml` has an explicit `include` list or the files are not in a path that cargo excludes.

The result: the published crate compiles fine (no `.toml` in `src/`), but `cargo test` on the published crate fails at runtime because `std::fs::read_to_string(path)` can't find the TOML files.

Concretely, `cargo` includes: `src/**`, `Cargo.toml`, `README.md`, `LICENSE`, and any files referenced by `[package] readme` / `license-file`. It does NOT automatically include arbitrary `tests/` data files.

**Warning signs**: `cargo package --list` does not show the `.toml` files. Running `cargo test` inside the unpacked `.crate` fails with "No such file or directory" on the first conformance test.

**Prevention**:
- Add an explicit `include` list to `Cargo.toml`:
  ```toml
  [package]
  include = [
      "src/**/*",
      "tests/**/*.rs",
      "tests/conformance/**/*.toml",
      "README.md",
      "LICENSE",
  ]
  ```
- OR: keep the default include behavior but move test vectors to a location cargo always includes (e.g., reference them via `include_str!()` macro, which forces inclusion of the referenced file). This is the most robust approach — `include_str!("conformance/required/scoring/recency-basic.toml")` guarantees the file is in the crate.
- Verify with `cargo package --list` before every publish.

**Which phase**: Crate migration scaffold — highest priority packaging concern.

---

### 3.3 `include_str!` vs `std::fs::read_to_string` for test vectors [MEDIUM]

**What goes wrong**: The current conformance runner uses `std::fs::read_to_string` with a path constructed from `env!("CARGO_MANIFEST_DIR")`. This works during development and `cargo test` in the source tree, but has two risks:
1. Files excluded from the published crate (see 3.2)
2. If the crate is ever compiled in an environment where the source tree is not present at the manifest directory (e.g., some cross-compilation setups, doctests in unusual configurations)

Using `include_str!()` instead embeds the content at compile time, avoiding both issues — but makes vectors harder to update without recompiling.

**Prevention**:
- For the published crate: use `include_str!()` for all 28 required test vectors. The test functions can call `include_str!()` via a macro that maps vector names to their content.
- For development ergonomics: keep the `load_vector(path)` helper as an alternative that reads from the filesystem, used only in tests that are `#[cfg(test)]` and gated behind `#[cfg(not(feature = "embedded-vectors"))]` or similar.
- A simpler approach: use `include_str!()` for all vectors and accept the recompile cost. The test suite is small (28 vectors) and compile times are fast.

**Which phase**: Crate migration scaffold.

---

## 4. CI Configuration for Cargo Publish

### 4.1 Running `cargo test` from repo root vs crate directory [HIGH]

**What goes wrong**: The cupel repo currently has a .NET solution at its root. Adding a Rust crate creates a scenario where `cargo` commands run from the repo root fail unless a Cargo workspace is configured at the repo root. If a `Cargo.toml` is placed at the repo root with `[workspace]`, it must include `crates/cupel-rs` as a member. If there is no root `Cargo.toml`, then all `cargo` commands in CI must be run with `--manifest-path crates/cupel-rs/Cargo.toml` or executed from the `crates/cupel-rs/` directory.

Running from the wrong directory produces one of two outcomes:
- `cargo build` at repo root with no `Cargo.toml` → "could not find Cargo.toml"
- `cargo test` that inadvertently finds a parent directory `Cargo.toml` from a different project → unexpected test failures

**Warning signs**: CI step uses `cargo test` without specifying the manifest path. A developer checks out the cupel repo and runs `cargo test` from the root expecting it to work.

**Prevention**:
- Decision required: is there a Rust workspace at `cupel/` root, or is `crates/cupel-rs/` a standalone crate?
  - **Standalone crate**: All CI steps must use `cargo test --manifest-path crates/cupel-rs/Cargo.toml`. Use `working-directory: crates/cupel-rs` in GitHub Actions steps.
  - **Workspace**: Add a minimal `Cargo.toml` at repo root with `[workspace] members = ["crates/cupel-rs"]`. Future Rust crates (if any) join the workspace naturally.
- The workspace approach is recommended: it allows `cargo test --workspace` to run all Rust tests from the root, and accommodates future crates.
- Add a `justfile` or `Makefile` target for running Rust tests so developers have a discoverable entry point.

**Which phase**: CI scaffold for Rust crate.

---

### 4.2 `cargo publish` without `--locked` can silently upgrade dependencies [MEDIUM]

**What goes wrong**: If CI runs `cargo publish` without `--locked`, cargo may resolve newer dependency versions than the ones in `Cargo.lock`. The published crate's dependencies in `Cargo.toml` are ranges, so consumers may get a different transitive tree than what was tested. More critically, if a new version of `chrono` or `thiserror` introduced a breaking change that isn't in your test matrix, your published crate may break for consumers who pull it fresh.

**Warning signs**: CI does not commit `Cargo.lock` (for library crates, `Cargo.lock` is typically not committed). The publish step uses a different dependency resolution than the test step.

**Prevention**:
- For library crates, `Cargo.lock` should generally NOT be committed (per Rust conventions). However, CI should use a pinned lockfile for testing.
- Add a scheduled CI job that runs `cargo update && cargo test` to catch breakage from upstream dependency updates.
- Use `cargo publish --locked` if the lockfile IS committed. Otherwise rely on `cargo publish` resolving ranges normally.
- Pin `chrono` and `thiserror` to minimum versions known to work, not ranges that could pick up breaking semver-incompatible changes.

**Which phase**: CI scaffold.

---

### 4.3 `deny.toml` from assay workspace does not travel with the crate [MEDIUM]

**What goes wrong**: The assay repo has a `deny.toml` at its root that configures `cargo-deny` for license checking and duplicate dependency detection. This file is at the workspace root and applies to the entire workspace. When `assay-cupel` is extracted into `cupel-rs`, the `deny.toml` does not come with it. The cupel CI has no equivalent configuration for the Rust crate.

**Warning signs**: `cargo-deny check` is not in the Rust CI workflow for cupel. A dependency with a disallowed license sneaks in.

**Prevention**:
- Copy a minimal `deny.toml` from the assay repo into `crates/cupel-rs/` (or the repo root if using a workspace).
- Add `cargo-deny check` to the CI workflow that runs `cargo test`.
- The cupel-rs crate's `deny.toml` only needs to cover its own dependency tree, which is small: `chrono` and `thiserror` (direct), with their transitive deps.

**Which phase**: CI scaffold.

---

## 5. Dependency Switchover (Path → crates.io)

### 5.1 `[patch.crates-io]` must be removed before publishing [HIGH]

**What goes wrong**: During development of the migrated crate, you may use:
```toml
[patch.crates-io]
cupel-rs = { path = "../" }
```
to test the crate locally in the assay project before it is published. If this `[patch]` section is accidentally left in any `Cargo.toml` that gets published, `cargo publish` will error:

```
error: published crates cannot contain [patch] tables
```

This is a hard error from cargo, not a silent failure. However, the risk is that the patch is in the _consumer_ project's `Cargo.toml`, which is fine — but developers may accidentally put it in the crate's own `Cargo.toml`, which is not allowed.

**Warning signs**: A `[patch.crates-io]` block appears in `crates/cupel-rs/Cargo.toml`. CI dry-run step is skipped.

**Prevention**:
- The `[patch.crates-io]` block belongs ONLY in the workspace root `Cargo.toml` of the consumer project (assay), never in the library crate itself.
- Add a CI lint that greps `crates/cupel-rs/Cargo.toml` for `[patch` and fails if found.
- Document the local dev workflow explicitly: "to test `cupel-rs` locally from the assay project, add `[patch.crates-io]` to assay's workspace `Cargo.toml`."

**Which phase**: Crate migration scaffold + CI lint.

---

### 5.2 `[patch.crates-io]` in assay breaks when cupel-rs version doesn't match [MEDIUM]

**What goes wrong**: After `cupel-rs 0.1.0` is published, the assay workspace uses:
```toml
[dependencies]
cupel-rs = "0.1"

[patch.crates-io]
cupel-rs = { path = "../cupel/crates/cupel-rs" }
```
If the local path version is bumped to `0.2.0` during active development, cargo resolves the patch because `0.1` matches `0.2` under SemVer (same major, higher minor). However, if the local version is bumped to `1.0.0` and the dependency spec says `"0.1"`, the patch is silently ignored because `1.0.0` is not compatible with `"0.1"`. Cargo logs a warning but the build succeeds with the registry version.

**Warning signs**: You change code in `cupel-rs`, run `cargo test` in assay, and the tests use the old registry version because the patch was silently dropped.

**Prevention**:
- Keep the local dev version and the dependency version synchronized. The simplest convention: during active development, the local version is always higher than the latest published version (e.g., `0.2.0-dev`). Set the dependency in assay to `"0"` (matches any `0.x`) to ensure the patch always applies.
- Run `cargo metadata | jq '.packages[] | select(.name == "cupel-rs") | .manifest_path'` to verify the patch is active (should show a local path, not a registry path).
- Add a CI step to assay that tests with both the patched (local) version and the published (registry) version.

**Which phase**: Dependency switchover (after first crates.io publish).

---

### 5.3 Coordinating version bumps between cupel-rs and assay [MEDIUM]

**What goes wrong**: `cupel-rs` publishes `0.2.0` with a breaking API change. The assay crate has `cupel-rs = "0.1"` in its `Cargo.toml`. The dependency spec does not auto-update. If a developer updates the local cupel-rs to 0.2.0 but forgets to update assay's `Cargo.toml` and publish the change, the assay CI fails on a fresh checkout where the local path patch is not available.

This is the standard library version coordination problem, but it matters more here because the two repos are developed by the same person with no external consumer pressure to keep the API stable.

**Prevention**:
- Maintain a clear policy: breaking API changes to `cupel-rs` require a version bump AND a simultaneous PR to assay updating the dependency spec.
- Use a semver-aware dependency spec in assay: `cupel-rs = "0"` rather than `cupel-rs = "0.1"` to allow minor/patch auto-updates.
- Add an issue template or checklist for `cupel-rs` releases that includes "update assay dependency".

**Which phase**: Post-publish, ongoing maintenance.

---

## 6. Cargo.toml Metadata Completeness

### 6.1 Missing required metadata for crates.io publication [HIGH]

**What goes wrong**: `cargo publish` requires certain fields and strongly recommends others. Missing fields that are required cause publish failures; missing recommended fields reduce discoverability.

Required by `cargo publish`:
- `name`
- `version`
- `edition` (technically optional but warns without it)
- `license` OR `license-file`

Strongly recommended for discoverability:
- `description` (required if publishing to crates.io — actually a hard requirement)
- `documentation`
- `homepage`
- `repository`
- `keywords` (up to 5, affects crates.io search)
- `categories` (must be from the official category list at `https://crates.io/category_slugs`)
- `readme`

The current `assay-cupel/Cargo.toml` has `description` but lacks `keywords`, `categories`, `homepage`, `documentation`, and `readme` because these were intended as workspace-level fields that haven't been set.

**Warning signs**: `cargo publish --dry-run` succeeds (only `description` is a hard error) but the published crate appears on crates.io with no categories and no readme rendered.

**Prevention**:
- Complete checklist for `cupel-rs/Cargo.toml`:
  ```toml
  [package]
  name = "cupel-rs"
  version = "0.1.0"
  edition = "2021"
  license = "MIT OR Apache-2.0"
  description = "Context window management pipeline for LLM applications"
  repository = "https://github.com/wollax/cupel"
  homepage = "https://github.com/wollax/cupel"
  documentation = "https://docs.rs/cupel-rs"
  readme = "README.md"
  keywords = ["llm", "context", "context-window", "ai", "pipeline"]
  categories = ["algorithms", "data-structures"]
  ```
- Verify `categories` entries against `https://crates.io/category_sluggs` — invalid category slugs are silently dropped.
- Write a `README.md` at the crate root (distinct from the repo-level README) before first publish.

**Which phase**: Crate migration scaffold.

---

### 6.2 `exclude` vs `include` — unexpected files in published crate [MEDIUM]

**What goes wrong**: Without an explicit `include` list, `cargo package` uses a default include heuristic: everything tracked by git, minus files matching `exclude` patterns, minus common build artifacts. This can result in:
- Development fixtures, internal notes, or `TODO.md` files being published
- Large test snapshots bloating the download size
- Files that reveal internal structure unnecessarily

Conversely, using `exclude` without `include` means you must maintain an exclusion list that grows as the repo grows.

**Warning signs**: `cargo package --list` shows unexpected files. The `.crate` tarball is larger than expected.

**Prevention**:
- Prefer an explicit `include` list over `exclude` for library crates. The list is small for `cupel-rs`:
  ```toml
  include = [
      "src/**/*",
      "tests/**/*.rs",
      "tests/conformance/**/*.toml",
      "README.md",
      "LICENSE",
      "CHANGELOG.md",
  ]
  ```
- Run `cargo package --list` before every publish and review the output.

**Which phase**: Crate migration scaffold.

---

## Summary: Phase Assignment

| Pitfall | Severity | Which Phase |
|---------|----------|-------------|
| 1.1 Name squatting — check `cupel-rs` availability first | CRITICAL | Migration scaffold: day one |
| 1.2 Version 1.0.0 vs 0.1.0 strategy | DECISION | Migration scaffold: before first publish |
| 1.3 `cargo publish --dry-run` does not catch everything | HIGH | CI scaffold: publish workflow |
| 1.4 API token management / OIDC | HIGH | CI scaffold: publish workflow |
| 2.1 Workspace-inherited fields need explicit values | HIGH | Migration scaffold: Cargo.toml |
| 2.2 `repository` field pointing to wrong repo | HIGH | Migration scaffold + CI lint |
| 2.3 Path references escaping the crate root | MEDIUM | Migration scaffold: pre-migration audit |
| 3.1 Test vectors duplicated — will diverge from spec | HIGH | Migration scaffold + ongoing CI |
| 3.2 `cargo publish` excludes test TOML files | CRITICAL | Migration scaffold: highest priority |
| 3.3 `include_str!()` vs `fs::read_to_string` for vectors | MEDIUM | Migration scaffold |
| 4.1 `cargo test` from wrong directory in CI | HIGH | CI scaffold: Rust crate workflow |
| 4.2 `cargo publish` without `--locked` | LOW | CI scaffold |
| 4.3 `deny.toml` not traveling with crate | MEDIUM | CI scaffold |
| 5.1 `[patch.crates-io]` in crate's own Cargo.toml | HIGH | Migration scaffold + CI lint |
| 5.2 `[patch.crates-io]` version mismatch silently ignored | MEDIUM | Post-publish: dev workflow docs |
| 5.3 Version bump coordination between repos | LOW | Post-publish: maintenance policy |
| 6.1 Missing required/recommended Cargo.toml metadata | HIGH | Migration scaffold: Cargo.toml |
| 6.2 Unexpected files in published crate | MEDIUM | Migration scaffold: packaging |

### Highest-priority items (act before writing migration code)

1. **Check `cupel-rs` name availability on crates.io** (pitfall 1.1)
2. **Verify `cargo publish` will include `.toml` test vectors** (pitfall 3.2) — use `include` list or `include_str!()`
3. **Decide whether to create a Rust workspace at cupel repo root** (pitfall 4.1)
4. **Write explicit Cargo.toml metadata** rather than relying on workspace inheritance (pitfall 2.1, 6.1)

---

*Research completed 2026-03-14. Sources: Cargo Book (https://doc.rust-lang.org/cargo/), crates.io policies, live inspection of `assay/crates/assay-cupel/` source tree.*
