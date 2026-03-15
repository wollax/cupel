# Brainstorm Summary: Pull assay-cupel into Cupel Repo

**Date**: 2026-03-14
**Pairs**: 3 (structure, publishing/CI, boundaries/integration)
**Rounds**: 2-3 per pair

---

## Key Findings

### Divergence: Two Pairs Disagreed on Fundamentals

The **publishing pair** operated under the assumption that the repos stay separate (challenger caught this and steered the debate). Their recommendations are still valuable for the cross-repo coordination *after* migration. The **structure** and **boundaries** pairs both assume the crate moves into cupel — which is the actual goal.

The **structure** and **boundaries** pairs diverged on directory naming:
- Structure: `crates/assay-cupel/` (keeps existing crate name, `crates/` convention)
- Boundaries: `rust/` with rename to `cupel` (cleaner, language-parallel with `src/`)

### Cross-Cutting Themes

1. **No Cargo workspace yet** — unanimous. One crate doesn't justify workspace overhead. Promotion path is trivial when needed.
2. **Conformance vectors need ONE source of truth** — all pairs agree. The `conformance/` dir in cupel is canonical. Mechanism debated (relative path vs vendored copy + CI guard).
3. **Git history preservation is not worth it** — boundaries pair argues spec + planning docs provide full provenance. `git subtree split` produces noisy history.
4. **Version tracks the spec, not .NET** — the crate is v1.0.0 because it implements Spec 1.0, independent of .NET package version.
5. **Migration is a single-session operation** — no staged rollout needed for a solo project with zero published consumers.

---

## Surviving Proposals

### Repo Structure

| Aspect | Recommendation | Confidence |
|--------|---------------|------------|
| Directory | `crates/assay-cupel/` (structure) OR `rust/` (boundaries) — **user decision** | Medium |
| Workspace | No workspace; standalone `Cargo.toml` | High |
| `rust-toolchain.toml` | At repo root (parallels `global.json`) | High |
| `.editorconfig` | Add `[*.rs]` and `[*.toml]` sections | High |
| `.gitignore` | Add `target/`, `Cargo.lock` patterns | High |
| `.NET solution` | Unchanged — Rust is invisible to MSBuild | High |

[Full report](structure-report.md)

### Crate Naming

| Option | Pros | Cons |
|--------|------|------|
| `cupel` | Short, memorable, matches the project | Must verify crates.io availability |
| `assay-cupel` | No rename work, current name | Misleading — crate isn't assay-specific |
| `cupel-rs` | Explicit language signal | Redundant — it's on crates.io, obviously Rust |

**Recommendation**: `cupel` (verify availability first)

### Conformance Test Vectors

| Approach | Pros | Cons |
|----------|------|------|
| Relative path (`../../conformance/`) | Zero duplication | Breaks `cargo publish` (can't include parent dirs) |
| Vendored copy + CI diff guard | Hermetic crate, simple | Duplication exists but CI catches drift |
| `build.rs` copy | Automated | Dev/publish mode split, added complexity |

**Recommendation**: Vendored copy + CI diff check (boundaries pair). Structure pair's relative path works for dev but breaks `cargo publish`.

### Publishing & Versioning

| Aspect | Recommendation |
|--------|---------------|
| Version scheme | Track spec version (v1.0.0 = Spec 1.0) |
| .NET version coordination | Independent — spec version is the anchor |
| Conformance vectors as release asset | Include in GitHub Release for cross-repo consumption |
| Cross-repo CI gating | Deferred to v1.1 — document the spec-change flow now |

[Full report](publishing-report.md)

### Migration Path

| Step | Action |
|------|--------|
| 1 | Check crates.io for `cupel` availability |
| 2 | Copy crate source into cupel repo |
| 3 | Set up conformance vector sharing + CI |
| 4 | Publish `cupel` v1.0.0 to crates.io |
| 5 | Update assay: `cupel = "1.0"`, rename `use` imports |
| 6 | Verify assay CI green |
| 7 | Delete `crates/assay-cupel/` from assay |
| 8 | Document `[patch.crates-io]` local dev pattern |

**Estimated effort**: ~2-3 hours single session

[Full report](boundaries-report.md)

---

## Decisions Requiring User Input

1. **Directory name**: `crates/assay-cupel/` vs `rust/` — structure vs boundaries pair disagreed
2. **Crate name**: `cupel` vs `assay-cupel` vs other
3. **Version timing**: Publish crate v1.0.0 now (spec-aligned) or wait for .NET v1.0.0?

## Deferred Items

| Item | Reason | Revisit When |
|------|--------|-------------|
| Cargo workspace | YAGNI — one crate | Second Rust crate needed |
| Serde feature flags | Migration should be pure move | First serialization need in assay |
| Cross-repo CI gating | Complex, premature | v1.1 or second implementation |
| `Cargo.lock` commit | Library convention says no | If reproducible CI builds become priority |

---

## Recommended Sequencing

1. **Phase N**: Crate migration (copy source, set up CI, publish to crates.io)
2. **Phase N+1**: Assay integration (update dependency, verify, delete old source)
3. *(Deferred)*: Cross-repo conformance gating, serde features
