# Boundaries & Integration Ideas

## Idea 1: "cupel-rs" — Cupel's Official Rust SDK

**What**: Rename the crate from `assay-cupel` to `cupel` (or `cupel-rs` if `cupel` is taken on crates.io). Move the full source from `assay/crates/assay-cupel/` into `cupel/crates/cupel/` (or `cupel/rust/`). The crate becomes Cupel's first-party Rust implementation, not an Assay-specific library. Assay references it from crates.io as any other consumer would.

**Why**: The crate implements the *Cupel specification*, not anything Assay-specific. Naming it `assay-cupel` couples identity to the first consumer rather than the spec it implements. `cupel` (or `cupel-rs`) signals language-agnostic ownership and invites adoption by Rust projects beyond Assay. This mirrors how the .NET package is `Wollax.Cupel`, not `Smelt.Cupel`.

**Scope**: Small. Rename `Cargo.toml` name field, update `use` statements in assay (just 4 test files currently reference `assay_cupel`), reserve the crate name on crates.io. ~1-2 hours.

**Risks**:
- `cupel` might already be taken on crates.io (check first; fallback to `cupel-rs`)
- If assay-core later depends on the crate, the rename touches more files — but doing it *now* while only test files reference it is the cheapest possible moment
- Naming divergence from the workspace package naming convention (`assay-*`) — but this is correct because the crate is *leaving* the workspace

---

## Idea 2: Conformance Vectors as Single Source of Truth in Cupel

**What**: The cupel repo's `conformance/` directory is the ONE canonical source for conformance TOML vectors. The Rust crate's test suite loads these vectors via a path relative to the crate root (in-repo) or via a build-time download/submodule (when published to crates.io). The assay repo's copied vectors are deleted entirely.

**Why**: Phase 12 copied the 28 required vectors into `assay/crates/assay-cupel/tests/conformance/required/`. This creates a drift hazard — if Phase 15 promotes `quota-basic.toml` to required, the assay copy becomes stale. Single source of truth eliminates this. The spec already defines the conformance format and vectors; the cupel repo already hosts them.

**Scope**: Medium. Need to solve the "crate published to crates.io can't reference a sibling directory" problem. Options: (a) embed vectors via `include_str!`/`include_bytes!` at compile time, (b) use a `build.rs` to bundle them, (c) include them in the crate's published files via `Cargo.toml` `[package] include`. Option (c) is simplest — just include `conformance/` in the crate package and load at test time.

**Risks**:
- Increases crate download size (28 TOML files, but they're tiny — probably <50KB total)
- `include` path mechanics need testing to ensure `cargo publish` captures the right files
- If conformance vectors are updated in cupel but the crate version isn't bumped, downstream tests won't see updates until they upgrade — but this is actually *correct* behavior (spec-version pinning)

---

## Idea 3: Spec Ownership Stays in Cupel, Assay Gets a Version Pin

**What**: The `spec/` directory (mdBook source + built site) and `conformance/` directory remain exclusively in the cupel repo. Assay's `Cargo.toml` pins to a specific semver version of the crate (e.g., `cupel = "1.0"`). When the spec changes, the crate is re-published and assay bumps its dependency version.

**Why**: The spec defines Cupel's algorithm. It belongs with the reference implementation (.NET) and the conformance suite. Assay is a *consumer* of the spec, not a co-owner. This clean separation means the cupel team (even if it's a team of one) can evolve the spec, update conformance vectors, and publish a new crate version — all without touching the assay repo. Assay opts into changes by bumping the version.

**Scope**: Trivial for assay. Change one line in workspace `Cargo.toml`:
```toml
# Before
assay-cupel = { path = "crates/assay-cupel" }
# After
cupel = "1.0"
```
Remove the `crates/assay-cupel/` directory entirely.

**Risks**:
- Loss of "develop both together" convenience — can't iterate on crate + consumer in a single commit. Mitigated by using `[patch.crates-io]` in assay's `Cargo.toml` during local dev to point at a local checkout of cupel
- Assay tests break if a crate update has breaking changes — but semver protects against this for minor/patch versions

---

## Idea 4: Git History Preservation via `git subtree split`

**What**: Before deleting the crate from assay, use `git subtree split --prefix=crates/assay-cupel` to extract a standalone branch with the crate's full commit history. Graft this onto the cupel repo using `git subtree add`. This preserves `git blame` and `git log` for the Rust code within the cupel repo.

**Why**: Phase 12 was 3 plans across 3 waves — non-trivial implementation work. The git history contains design decisions, conformance debugging, and iterative refinements. Losing this history means future maintainers (including future-you) can't trace *why* certain implementation choices were made. `git subtree` preserves this without merge commits polluting cupel's main branch.

**Scope**: Medium. The `git subtree split/add` dance requires some care:
1. In assay: `git subtree split --prefix=crates/assay-cupel -b cupel-rust-history`
2. In cupel: `git subtree add --prefix=crates/cupel /path/to/assay cupel-rust-history --squash` (or without `--squash` for full history)
3. Verify history, then delete from assay

**Risks**:
- History may include assay-workspace-specific commits (Cargo.lock changes, workspace Cargo.toml edits) that don't make sense in cupel's context
- If the subtree path changes (e.g., `crates/assay-cupel/` → `crates/cupel/`), `git log --follow` is needed to trace across renames
- Could opt for "clean start" instead — the implementation was spec-driven, so the spec itself documents the "why". History is nice-to-have, not load-bearing.

---

## Idea 5: Staged Migration with Temporary Dual Publishing

**What**: Instead of a big-bang switchover, do a staged migration:
1. **Week 1**: Move crate source to cupel repo, publish to crates.io as `cupel` v0.1.0
2. **Week 1**: Update assay's `Cargo.toml` to use crates.io dep + `[patch.crates-io]` pointing to local cupel checkout for dev
3. **Week 2**: Verify assay CI passes with the crates.io dependency (remove `[patch]`)
4. **Week 2**: Delete `crates/assay-cupel/` from assay, remove from workspace members
5. **Week 3**: Publish `cupel` v1.0.0 once Cupel's .NET v1.0 ships and the spec is finalized

**Why**: A big-bang move risks breaking assay if something unexpected happens (e.g., crate feature flags differ, dev-dependency resolution changes). Staged migration lets you validate each step. The `[patch.crates-io]` mechanism gives you the best of both worlds during transition — crates.io dep for CI, local path for iteration.

**Scope**: Spread over 2-3 sessions but each step is small. Total effort ~2-3 hours.

**Risks**:
- Dual maintenance window where the crate exists in both repos (keep it as short as possible)
- `v0.1.0` published to crates.io is "pre-release" but crate names can't be reclaimed once published — make sure the name is right before publishing
- Overcomplicated for a single-person project? Could just do the big-bang move in one session since you control both repos

---

## Idea 6: Feature-Flag-Gated Serde Support

**What**: When moving the crate to cupel, restructure it with a `serde` feature flag (off by default). The core types (`ContextItem`, `ContextBudget`, scorers, slicers, placers) are zero-dependency. `serde::Serialize`/`Deserialize` derives are behind `#[cfg(feature = "serde")]`. Assay enables the feature; other consumers can use the crate without pulling in serde.

**Why**: Currently `serde` is only in dev-dependencies (for parsing conformance TOML). But when assay actually *uses* the crate (not just tests it), it'll need serialization for context items flowing through the MCP pipeline. Making serde opt-in follows the Rust ecosystem convention (e.g., `chrono`, `uuid` all do this) and keeps the crate lightweight for consumers who don't need serialization. This is the right time to design the feature surface — before any real consumers exist.

**Scope**: Small-medium. Add `#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]` to public types. Add `serde = { version = "1", features = ["derive"], optional = true }` to `[dependencies]`. Test with and without the feature.

**Risks**:
- Feature-flag combinatorics increase CI matrix (test with and without `serde`)
- If most consumers need serde anyway, the opt-in is ceremony without benefit — but "off by default" is the Rust convention and keeps compile times minimal
- `chrono` is already a hard dependency (for timestamps) — `chrono`'s own `serde` feature would need to be enabled conditionally too, adding complexity
