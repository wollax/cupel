# Research Synthesis: Cupel v1.1 — Rust Crate Migration & crates.io Publishing

**Date**: 2026-03-14
**Synthesized from**: STACK.md (§12-19), FEATURES.md, ARCHITECTURE-RUST-INTEGRATION.md, PITFALLS-CRATE-MIGRATION.md
**Milestone scope**: Migrate `assay-cupel` from `wollax/assay` into `wollax/cupel`, publish as `cupel-rs` on crates.io, update assay to consume from the registry.

---

## 1. Executive Summary

The v1.1 milestone is a structural migration, not a feature development effort. The Rust crate (`assay-cupel`) already implements the full Cupel specification with 28 passing conformance tests across all required vectors. No algorithm rewrites, no new pipeline stages, and no cross-language feature parity are required. The migration work is entirely about file relocation, Cargo.toml metadata, CI scaffolding, and conformance vector path cleanup — all of which are well-understood with low-to-medium complexity.

The most consequential decisions are all pre-implementation gates: (1) verifying `cupel-rs` is available as a crates.io name before any code is written, (2) choosing a workspace vs standalone crate layout for the `crates/` directory, and (3) deciding whether the conformance test runner uses `include_str!()` embed or `CARGO_MANIFEST_DIR`-relative path reads. Getting these three decisions wrong produces hard-to-reverse consequences (a permanently broken published package or a vendored-copy drift problem). Everything downstream of these three decisions is mechanical.

The CI architecture for the dual-language monorepo is fully solved by the separate-workflow + path-filter pattern, which is the industry standard for polyglot repos and has direct precedent in the ecosystem. Both registries (crates.io and NuGet.org) now support OIDC trusted publishing with comparable workflows, giving a symmetric security model for the release pipeline. The Rust CI adds one new workflow file (`ci-rust.yml`) and one new release workflow (`release-rust.yml`), with no changes needed to the existing .NET workflows other than adding path filters.

---

## 2. Key Findings by Research Dimension

### 2.1 Stack (STACK.md §12-19)

**Rust edition and MSRV**: Rust 2024 edition (`rust-version = "1.85"`) is the correct anchor. This is the edition minimum — the most conservative defensible MSRV for a new crate. Latest stable at research time is 1.94; `1.85` gives an 9-version compatibility window without restricting any feature used by the current implementation.

**Toolchain management**: A single `rust-toolchain.toml` at repo root (channel `"1.85"`, components `rustfmt` and `clippy`) pins the CI toolchain to the MSRV automatically. `actions-rust-lang/setup-rust-toolchain@v1` reads this file without additional configuration and includes `Swatinem/rust-cache@v2` by default, making Rust CI setup nearly zero-config.

**crates.io trusted publishing**: Fully supported as of July 2025 (RFC 3691). The `rust-lang/crates-io-auth-action@v1` action exchanges a GitHub OIDC token for a 30-minute crates.io token. This mirrors the NuGet trusted publishing already used in the .NET release workflow exactly. No long-lived API tokens required for either registry.

**First publish caveat**: Trusted Publishing can only be configured after a crate is already registered on crates.io. The very first `cargo publish` requires a personal API token. This is a one-time bootstrapping step, not an ongoing maintenance concern.

**Release workflow symmetry**: The crates.io release workflow mirrors the existing NuGet `release.yml` in structure: `workflow_dispatch` trigger, dry-run input, `release` GitHub environment for manual approval, OIDC authentication. Versioning diverges by design — NuGet uses MinVer (git tags), crates.io uses an explicit `version` field in `Cargo.toml`. Both anchor to spec compatibility via `[package.metadata.cupel] spec_version = "1.0"`.

**Release tag convention**: `cupel-rs-vX.Y.Z` prefixed tags for Rust releases, separate from the `.NET` `vX.Y.Z` tags, to prevent tag collision in the monorepo.

**Summary table** (new stack additions only):

| Layer | Choice | Confidence |
|---|---|---|
| Rust edition | 2024 | HIGH |
| MSRV | 1.85 (edition minimum) | HIGH |
| Toolchain management | `rust-toolchain.toml` at repo root | HIGH |
| CI action | `actions-rust-lang/setup-rust-toolchain@v1` | HIGH |
| Crate registry | crates.io (OIDC trusted publishing) | HIGH |
| OIDC action | `rust-lang/crates-io-auth-action@v1` | HIGH |
| CI architecture | Separate workflows + path filters | HIGH |
| Workspace layout | `crates/cupel/` standalone or minimal workspace | HIGH |

### 2.2 Features (FEATURES.md)

**The crate is production-ready**: 26 source files, complete trait hierarchies (`Scorer`, `Slicer`, `Placer`), `CompositeScorer` with cycle detection, correct effective-budget computation, `PipelineBuilder`, and 28 conformance tests passing. Migration is purely structural.

**Table stakes (all P0, all required before v1.1 ships)**:

| ID | Feature | Complexity |
|----|---------|------------|
| TS-01 | Compiles in new location (`crates/cupel/`) | Low |
| TS-02 | Published to crates.io as `cupel-rs` | Low-Med |
| TS-03 | assay consumes from crates.io | Low |
| TS-04 | Conformance vectors shared (not duplicated) | Low |
| TS-05 | CI runs Rust tests | Low |

Critical path is linear: TS-01 → TS-04 → TS-02 → TS-03 → TS-05.

**Differentiators (P1-P3, post-table-stakes)**:
- **D-01 Serde feature flag** (P1): `features = ["serde"]` gate on all data types. Highest-value differentiator — most LLM-application consumers need serialization. Medium complexity; `ContextBudget` requires a validated custom deserializer (cannot blind-deserialize around the constructor). Follows ecosystem convention (chrono, uuid, etc.).
- **D-02 docs.rs documentation** (P1): Crate-level quickstart in `lib.rs`, module-level doc comments, `[package.metadata.docs.rs]` configuration. Currently has `///` item-level docs but no module or crate-level landing page.
- **D-03 Examples** (P2): `examples/basic_pipeline.rs` and optionally `examples/composite_scoring.rs`. Single largest factor in new-user conversion on docs.rs.
- **D-04 Builder ergonomics** (P3): `ContextItemBuilder::tag(impl Into<String>)` single-tag append. Fully backward-compatible.

**Explicit anti-features (never in v1.1)**:
- .NET feature parity (no `SelectionReport`, `ITraceCollector`, `CupelPolicy`, `CupelPresets`)
- Async / Tokio integration
- WASM target
- Optional conformance vectors (9 exist; migration ships the same 28 required it has today)
- Policy system / named presets in Rust

**The `cupel-rs` name**: Follows Rust ecosystem convention for `<name>-rs` suffix when the plain name is unavailable. `cupel` is taken on crates.io.

### 2.3 Architecture (ARCHITECTURE-RUST-INTEGRATION.md)

**CI workflow structure**: Separate `ci-rust.yml` and `release-rust.yml` workflow files, path-filtered to `crates/**`. Self-referencing path inclusion (`'.github/workflows/ci-rust.yml'`) is required so workflow changes themselves trigger CI. Using `defaults.run.working-directory: crates/cupel` at job level eliminates per-step `--manifest-path` repetition.

**`.gitignore` pattern**: Add `/crates/cupel/target/` to root `.gitignore` (path-anchored, not bare `target`). The `cargo new`-generated `crates/cupel/.gitignore` with `/target` is complementary and should be committed alongside.

**Conformance vector packaging**: Use an explicit `include` list in `Cargo.toml`. Without it, `.toml` data files under `tests/` are silently excluded from the published crate. `cargo package --list` is the verification gate before every publish.

**docs.rs constraints**: docs.rs builds from the `.crate` tarball in a network-isolated environment. Files not in the published tarball are unavailable. `[package.metadata.docs.rs] all-features = true` ensures the serde feature is documented. Integration tests do not run on docs.rs (only doctests and examples do) — this is normal.

**`cargo publish` from subdirectory**: `working-directory: crates/cupel` in CI with bare `cargo publish`. This mirrors local development and avoids `--manifest-path` path ambiguity.

**Workspace detection**: No root `Cargo.toml` currently exists in the cupel repo (it is .NET-primary). This is an advantage — no accidental workspace detection. If a root `Cargo.toml` is added later, it must explicitly declare `[workspace] members = ["crates/*"]`.

### 2.4 Pitfalls (PITFALLS-CRATE-MIGRATION.md)

**Critical (act before writing any code)**:

1. **Name availability** (pitfall 1.1, CRITICAL): Check `https://crates.io/crates/cupel-rs` on day one. Also check `cupel`, `cupel-core`, `cupel_rs`. If squatted, file a name report; the process takes days to weeks.

2. **Test vectors excluded from published crate** (pitfall 3.2, CRITICAL): `cargo publish` does NOT automatically include `.toml` data files under `tests/`. Without an explicit `include` list, `cargo test` on the published crate fails at runtime. Either use an explicit `include` list with `tests/conformance/**/*.toml`, or use `include_str!()` macros to embed vectors at compile time. Verify with `cargo package --list` before every publish.

3. **Workspace-inherited fields** (pitfall 2.1, HIGH): `assay-cupel/Cargo.toml` uses `version.workspace = true`, `edition.workspace = true`, `license.workspace = true`, `repository.workspace = true`. Every one of these becomes a compile error in the new location. Write a fully explicit standalone `Cargo.toml`.

4. **`repository` field pointing to wrong repo** (pitfall 2.2, HIGH): The inherited `repository` points to `wollax/assay`. The new `Cargo.toml` must have `repository = "https://github.com/wollax/cupel"`. This is permanent for a published version — wrong metadata cannot be corrected without a new publish.

**High-priority (before first publish)**:

5. **`cargo publish --dry-run` does not catch everything** (pitfall 1.3, HIGH): Does not verify `include` file existence, `CARGO_MANIFEST_DIR` path resolution after packaging, or token permissions. `cargo package` → `tar xvf *.crate` → `cargo test` in the unpacked directory is the complete verification sequence.

6. **CI working directory** (pitfall 4.1, HIGH): All Rust CI steps must use `working-directory: crates/cupel` or `--manifest-path`. Running `cargo test` from repo root with no `Cargo.toml` there fails with "could not find Cargo.toml". The workspace-at-root option resolves this cleanly.

7. **Version strategy** (pitfall 1.2, MEDIUM-DECISION): `0.1.0` vs `1.0.0`. Counter-considerations: NuGet ships `1.0.0`; inconsistent sibling versions confuse users. But `1.0.0` locks the API under SemVer without any pre-release validation. Consider `1.0.0-beta.1` as a compromise — visible as pre-release on crates.io, won't appear in `cargo add` without explicit `--version`.

8. **`[patch.crates-io]` in crate's own Cargo.toml** (pitfall 5.1, HIGH): `cargo publish` fails hard if the crate's own `Cargo.toml` contains `[patch]`. Belongs only in the consumer (assay) workspace. Add a CI lint that greps for `[patch` in the crate's Cargo.toml.

**Medium-priority (good practice)**:

9. **Test vector divergence** (pitfall 3.1): Over time the canonical vectors at `conformance/required/` and the crate's vendored copy will drift. A CI diff guard (comparing the two directories) prevents silent divergence. The `CARGO_MANIFEST_DIR`-relative path approach (FEATURES.md TS-04 Option A) eliminates the copy entirely and is cleaner long-term.

10. **Local dev workflow documentation** (pitfall 5.2): The `[patch.crates-io]` pattern for simultaneous cupel-rs + assay development must be documented. Without it, the first developer working across both repos after migration will lose time.

11. **`deny.toml` coverage** (pitfall 4.3): The assay workspace `deny.toml` does not travel with the crate. Add a minimal `deny.toml` (or root-level equivalent) and `cargo-deny check` to Rust CI.

---

## 3. Implications for Roadmap: Suggested Phase Structure

The migration decomposes naturally into four phases, each with a clear exit criterion:

### Phase 13: Migration Scaffold (pre-code gates)

**Goal**: Establish all pre-conditions before touching code. No Rust files moved yet.

**Work**:
- Verify `cupel-rs` name availability on crates.io (day one gate — if squatted, stop and file report)
- Decide workspace vs standalone layout for `crates/` directory (recommendation: minimal workspace root `Cargo.toml` with `members = ["crates/*"]`)
- Decide conformance vector strategy: `include_str!()` embed OR CARGO_MANIFEST_DIR-relative path with `include` list (recommendation: CARGO_MANIFEST_DIR-relative path + explicit `include` list; this enables updating vectors without recompile and keeps the source readable)
- Write complete standalone `Cargo.toml` for `cupel-rs` with all required and recommended fields
- Decide initial version: `0.1.0`, `1.0.0-beta.1`, or `1.0.0`

**Exit criterion**: `cargo check --manifest-path crates/cupel/Cargo.toml` passes on an empty crate placeholder.

### Phase 14: Crate Move + Conformance Verification

**Goal**: Source code lives in the new location and all 28 conformance tests pass.

**Work**:
- Move all 26 `.rs` files from `assay/crates/assay-cupel/src/` to `cupel/crates/cupel/src/`
- Update `load_vector()` path in conformance test runner to navigate to `cupel/conformance/required/` (relative to new `CARGO_MANIFEST_DIR`)
- Add `rust-toolchain.toml` at repo root
- Add `/crates/cupel/target/` to root `.gitignore`
- Verify: `cargo fmt --check`, `cargo clippy -- -D warnings`, `cargo test` all pass
- Run `cargo package --list` and verify `.toml` conformance vectors appear
- Verify: `tar xvf *.crate && cargo test` passes inside the unpacked tarball

**Exit criterion**: `cargo test` passes with 28/28 conformance vectors. `cargo package --list` confirms vectors are in the tarball.

### Phase 15: CI Scaffold (Rust)

**Goal**: Rust CI is wired and passing on PRs. Publish pipeline is verified dry-run.

**Work**:
- Add `ci-rust.yml` with path-filtered trigger (`crates/**`, `conformance/**`, `rust-toolchain.toml`, self-reference)
- Add `release-rust.yml` with `workflow_dispatch` + dry-run input + `release` environment
- Set up crates.io trusted publishing policy (link GitHub repo + workflow file)
- Configure `cargo-deny` and add `cargo-deny check` to Rust CI job
- Run `release-rust.yml` with `dry-run: true` to verify end-to-end packaging
- Add `.github/workflows/ci.yml` path filters for .NET paths (no Rust changes trigger .NET CI)
- Verify GitHub branch protection accepts skipped Rust CI status check on .NET-only PRs

**Exit criterion**: Rust CI passes on a test PR. Dry-run publish completes without error.

### Phase 16: First Publish + Assay Switchover

**Goal**: `cupel-rs` is live on crates.io. assay consumes it from the registry.

**Work**:
- First manual `cargo publish` with personal API token (bootstraps the crate on crates.io)
- Configure trusted publishing on crates.io settings page (links to the `release-rust.yml` workflow)
- Verify crates.io listing: readme rendered, categories visible, docs.rs build triggered
- In assay: replace `assay-cupel` path dependency with `cupel-rs = "VERSION"` from registry
- Rename imports across assay: `assay_cupel::` → `cupel_rs::` (or `extern crate cupel_rs as cupel`)
- Delete `assay/crates/assay-cupel/` directory
- Document `[patch.crates-io]` pattern in assay contributing guide
- Verify assay CI passes with registry dependency

**Exit criterion**: assay builds and tests pass with `cupel-rs` from crates.io. Old `assay-cupel` directory is deleted.

### Differentiators (after Phase 16, ordered by ROI)

**Phase 17 (optional, high ROI)**: Serde feature flag — `features = ["serde"]` on data types. Requires careful `ContextBudget` custom deserializer. Re-publish as a minor version bump.

**Phase 18 (optional)**: docs.rs documentation — crate-level quickstart, module-level doc comments, `examples/basic_pipeline.rs`. Required for `[package.metadata.docs.rs]` config.

---

## 4. Confidence Assessment

**HIGH confidence areas** (verified against official docs / multiple sources):

- crates.io OIDC trusted publishing mechanics (RFC 3691, official blog, action source)
- Cargo.toml required fields and crates.io publish behavior
- GitHub Actions path-filter behavior (including tag push exemption)
- `actions-rust-lang/setup-rust-toolchain@v1` behavior and cache integration
- `cargo publish` test vector exclusion behavior (a well-documented Cargo gotcha)
- `.NET` and `cargo` build tool isolation (they ignore each other's files by design)
- Rust edition 2024 and MSRV 1.85 alignment

**MEDIUM confidence areas** (multiple sources, some inference):

- `cupel-rs` name availability (must be verified live on crates.io — cannot be assumed)
- docs.rs network isolation and its implications for fixture file access
- `[patch.crates-io]` version mismatch behavior (reasoned from Cargo resolver semantics)
- Category slug validity (`algorithms`, `data-structures` — should be verified at `https://crates.io/category_slugs`)

**Decisions requiring human judgment** (not resolvable by research alone):

- Version strategy: `0.1.0` vs `1.0.0-beta.1` vs `1.0.0`. Research documents the tradeoffs; the choice depends on the project's communication goals.
- Conformance vector strategy: `include_str!()` embed vs CARGO_MANIFEST_DIR path vs CI diff guard. Each has valid use cases. Research recommends Option A (CARGO_MANIFEST_DIR-relative path) but the choice affects test ergonomics during development.

---

## 5. Gaps to Address

The following items were identified across the four research documents and are not fully resolved by the research. They must be addressed before or during Phase 13.

### G-01: `cupel-rs` name availability (BLOCKING)

**Gap**: No live check was performed. The name may be squatted.
**Resolution**: Check `https://crates.io/crates/cupel-rs`, `https://crates.io/crates/cupel`, and `https://crates.io/crates/cupel_rs` before starting Phase 13 work.

### G-02: Workspace vs standalone decision (BLOCKING)

**Gap**: Two architectures are documented but no final decision is recorded. The choice affects how `cargo test` is invoked from repo root in CI and whether future Rust crates can be added cleanly.
**Research recommendation**: Minimal workspace `Cargo.toml` at repo root (`members = ["crates/*"]`). This makes `cargo test --workspace` work from root and accommodates future crates.
**Resolution required**: Confirm before writing any Cargo.toml.

### G-03: Version strategy (BLOCKING for publish)

**Gap**: `0.1.0` vs `1.0.0-beta.1` vs `1.0.0` is unresolved. Research documents tradeoffs but the decision is a communication/positioning choice.
**Resolution required**: Decide before Phase 16.

### G-04: Conformance vector strategy (BLOCKING for TS-04)

**Gap**: Three options (Option A: CARGO_MANIFEST_DIR-relative path, Option B: symlink, Option C: CI diff guard with vendored copy). FEATURES.md recommends Option A; PITFALLS.md lists Option C as short-term viable.
**Research recommendation**: Option A (CARGO_MANIFEST_DIR-relative path) for monorepo. No duplication, no CI lint maintenance, clean single source of truth. Requires confirming relative path depth after directory move.
**Resolution required**: Verify the relative path depth (`../../conformance/required/` from `crates/cupel/`) in the new layout before Phase 14.

### G-05: `include_str!()` vs `std::fs::read_to_string` for test runner (MEDIUM priority)

**Gap**: The current test runner uses `std::fs::read_to_string`. If Option A (CARGO_MANIFEST_DIR path) is chosen, `fs::read_to_string` works for tests in the source tree but requires the conformance vectors to be in the `include` list for the published crate. If the vectors are not vendored in the crate (because they are read from the canonical `conformance/` location), they cannot be in the tarball — meaning published crate `cargo test` would fail.
**Implication**: If Option A is chosen (vectors NOT vendored), integration tests in the published crate tarball will fail. This is acceptable if the position is "conformance tests are CI-only, not run from the tarball." Must be documented explicitly.
**Resolution required**: Decide whether published crate tarball `cargo test` is a goal. If yes, vendor the vectors (Option C) and use `include` list. If no, use Option A and document CI-only conformance testing.

### G-06: `Cargo.lock` commit policy (LOW priority)

**Gap**: Library crates conventionally do not commit `Cargo.lock`. In a monorepo CI context, committing it ensures reproducible CI. Decision has implications for `cargo publish --locked` usage.
**Research recommendation**: Do not commit `Cargo.lock` for `cupel-rs` (library crate convention). Add a scheduled CI job (`cargo update && cargo test`) to catch upstream breakage.

### G-07: `deny.toml` configuration

**Gap**: The assay workspace `deny.toml` exists but its configuration is not documented in the research. A new `deny.toml` for `cupel-rs` must be written from scratch or copied and trimmed.
**Resolution**: Review assay's `deny.toml` during Phase 15 CI scaffold and adapt for the smaller dependency tree (`chrono`, `thiserror`, and their transitive deps).
