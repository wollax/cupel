# Boundaries & Integration — Consolidated Report

## Summary

This report captures the agreed positions from 3 rounds of explorer/challenger debate on how to move the `assay-cupel` Rust crate from `wollax/assay` to `wollax/cupel`, establish clear ownership boundaries, and define the ongoing integration model.

---

## Agreed Decisions

### 1. Crate Name: `cupel`

**Decision**: Rename from `assay-cupel` to `cupel`.

**Rationale**: The crate implements the Cupel specification, not anything Assay-specific. The name `cupel` is niche enough that collision risk on crates.io is near-zero. Rust ecosystem convention favors short, memorable names for unique terms — owner-prefixed names (`wollax-cupel`) are unnecessary when the name itself is distinctive.

**Pre-requisite**: Verify `cupel` is available on crates.io before committing.

**Impact on assay**: 4 test files change `use assay_cupel::` to `use cupel::`. No other assay crate currently depends on it.

### 2. Repo Structure: `rust/` at Cupel Repo Root

**Decision**: Place the crate at `cupel/rust/` — a single crate, no Cargo workspace.

```
cupel/
├── src/              # .NET implementation
├── spec/             # mdBook specification
├── conformance/      # TOML test vectors (canonical)
├── rust/             # Rust implementation
│   ├── Cargo.toml    # [package] name = "cupel"
│   ├── src/
│   └── tests/
│       └── conformance/  # Vendored copy of ../conformance/ vectors
├── tests/            # .NET tests
└── benchmarks/       # .NET benchmarks
```

**Rationale**: No workspace because there's only one crate (YAGNI). `rust/` clearly signals "Rust implementation" alongside `src/` (.NET) and `spec/`. If a second Rust crate is ever needed, introduce the workspace then.

**Rejected alternative**: `crates/cupel/` with a workspace wrapper — over-engineered for a single crate.

### 3. Conformance Vectors: Single Source of Truth with Vendored Copy + CI Guard

**Decision**: The cupel repo's `conformance/` directory is the ONE canonical source for conformance TOML vectors. The Rust crate vendors a copy into `rust/tests/conformance/`. A CI step (`diff -r conformance/ rust/tests/conformance/`) catches drift.

**Rationale**: This is the simplest approach that solves the drift hazard:
- Zero `build.rs` complexity
- Hermetic published crate (vectors are in the package)
- CI diff check prevents drift — the same problem Phase 12's copy-paste created
- Exactly what the current test harness already expects (load TOML from relative path)

**Rejected alternatives**:
- `build.rs` copy from `../conformance/` — correct but over-engineered; creates dev-vs-publish mode split
- `[package] include` referencing parent directories — doesn't work (Cargo restricts to package root)
- `include_str!` embedding — too much boilerplate for 37+ files
- Separate `cupel-conformance` crate — over-engineered for current needs

**Versioning policy for conformance changes**:
- New vectors added → minor version bump (additive)
- Expected values changed in existing vectors → major version bump (breaking for implementations)
- Vectors removed → major version bump (downstream test harnesses may reference them)

### 4. Spec Ownership: Stays in Cupel, Always

**Decision**: The `spec/` directory (mdBook source + built site) and `conformance/` directory remain exclusively in the cupel repo. Assay is a consumer, not a co-owner.

**Rationale**: The spec defines Cupel's algorithm. It belongs with the reference implementation (.NET) and the conformance suite. Clean separation means the spec can evolve and a new crate version can be published without touching the assay repo. Assay opts into changes by bumping its dependency version.

### 5. Git History: Fresh Start with Provenance Comment

**Decision**: Do NOT preserve git history via `git subtree split`. Copy the source files, start fresh history in the cupel repo.

**Rationale**: Phase 12 was spec-driven — every implementation decision traces back to the spec chapters and planning docs (`12-RESEARCH.md`, `12-01-PLAN.md` through `12-03-PLAN.md`). The spec IS the "why" documentation. `git subtree split` on a workspace crate produces noisy history (workspace Cargo.lock changes, CI config changes) that defeats the purpose.

**Provenance breadcrumb**: Add a comment to the crate's `lib.rs`:
```rust
// Originally implemented in wollax/assay (Phase 12), migrated to cupel repo.
```

### 6. Migration Approach: Single-Session Big-Bang with Verify-Before-Delete

**Decision**: Complete the entire migration in one session, but verify each step before proceeding to the next destructive action.

**Steps**:
1. Check crates.io for `cupel` name availability
2. Create `rust/` directory in cupel repo with crate source (copied from assay)
3. Set up vendored conformance vectors + CI diff check
4. Publish to crates.io as `cupel` (version per spec — see below)
5. In assay: change dependency to `cupel = "1.0"`, update 4 test files (`use cupel::` instead of `use assay_cupel::`)
6. Run `cargo test` in assay — proceed only if green
7. Delete `crates/assay-cupel/` from assay, update workspace `Cargo.toml` members
8. Document `[patch.crates-io]` local dev pattern in assay's `CONTRIBUTING.md`

**Rejected alternative**: Multi-week staged migration with v0.1.0 pre-release — overcomplicated for a solo project with zero published consumers.

### 7. Crate Version: Track Spec Version

**Decision**: The Rust crate version tracks the specification version, not the .NET package version.

**Rationale**: The crate implements spec v1.0. It IS v1.0.0 regardless of whether the .NET package has shipped its v1.0.0 to NuGet yet. The spec is the contract; the crate's version reflects its conformance to that contract.

### 8. Serde Feature Flags: Deferred

**Decision**: Do NOT add feature-flagged serde support during the migration. Defer to a separate PR after migration is complete.

**Rationale**: The migration should be a pure move — rename + relocate + update deps. Architectural changes add diff noise and risk. Feature-flagging serde involves non-trivial Cargo feature-unification with `chrono/serde`, and the optimal feature surface isn't clear until there's a real consumer with real serialization needs.

### 9. Local Dev Workflow: Document `[patch.crates-io]`

**Decision**: Add a section to assay's `CONTRIBUTING.md` documenting how to develop against a local checkout of the cupel crate:

```toml
# In assay's Cargo.toml (do NOT commit this)
[patch.crates-io]
cupel = { path = "../cupel/rust" }
```

**Rationale**: Without this, the first time someone tries to iterate on both repos in tandem, they'll waste time rediscovering the pattern.

---

## Deferred Decisions

| Topic | Reason | When to Revisit |
|-------|--------|-----------------|
| Serde feature flags | Migration should be a pure move | First PR after migration where assay needs serialization |
| Cargo workspace | Only one crate exists | If/when a second Rust crate is needed (e.g., `cupel-conformance`) |
| `wollax-cupel` naming | `cupel` is sufficiently unique | Only if `cupel` is taken on crates.io |

---

## Critical Path

```
[Check crates.io] → [Create rust/ in cupel] → [Vendor conformance + CI guard]
                                                        ↓
                                              [Publish cupel v1.0.0]
                                                        ↓
                                              [Update assay deps]
                                                        ↓
                                              [Verify assay CI green]
                                                        ↓
                                              [Delete from assay]
                                                        ↓
                                              [Document local dev workflow]
```

Estimated effort: ~2-3 hours in a single session.

---

## Open Questions for User

1. **Crate name**: Is `cupel` the preferred name, or does the user want `cupel-rs` or another variant?
2. **Version alignment**: Should v1.0.0 wait until .NET v1.0.0 ships, or publish independently per spec version?
3. **Conformance mechanism preference**: Vendored copy + CI diff check (recommended) vs `build.rs` approach?
