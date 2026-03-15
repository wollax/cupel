# Repo Structure — Consolidated Report

**Date**: 2026-03-14
**Participants**: explorer-structure (explorer), challenger-structure (challenger)
**Status**: Consensus reached after 2 rounds of debate

---

## Decision

**Proposal 2: `crates/` Peer (Standalone Crate, Minimal Disruption)** with specific amendments from debate.

## Recommended Layout

```
cupel/
├── Cupel.slnx                              ← .NET solution (unchanged)
├── Directory.Build.props                    ← .NET build config (unchanged)
├── Directory.Packages.props                 ← .NET package versions (unchanged)
├── global.json                              ← .NET SDK pin (unchanged)
├── rust-toolchain.toml                      ← NEW — Rust toolchain pin (at root)
├── .editorconfig                            ← unchanged
├── .gitignore                               ← updated with Rust patterns
│
├── src/                                     ← .NET source packages (unchanged)
│   ├── Wollax.Cupel/
│   ├── Wollax.Cupel.Json/
│   ├── Wollax.Cupel.Tiktoken/
│   └── Wollax.Cupel.Extensions.DependencyInjection/
│
├── tests/                                   ← .NET tests (unchanged)
│   ├── Wollax.Cupel.Tests/
│   ├── Wollax.Cupel.Json.Tests/
│   ├── Wollax.Cupel.Tiktoken.Tests/
│   ├── Wollax.Cupel.Extensions.DependencyInjection.Tests/
│   └── Wollax.Cupel.ConsumptionTests/
│
├── benchmarks/                              ← .NET benchmarks (unchanged)
│   └── Wollax.Cupel.Benchmarks/
│
├── crates/                                  ← NEW — Rust crates
│   └── assay-cupel/
│       ├── Cargo.toml                       ← standalone (no workspace)
│       ├── src/
│       │   ├── lib.rs
│       │   ├── model/
│       │   ├── scorer/
│       │   ├── slicer/
│       │   ├── placer/
│       │   ├── pipeline/
│       │   └── error.rs
│       └── tests/
│           └── conformance.rs               ← references ../../conformance/
│
├── conformance/                             ← shared test vectors (unchanged)
│   ├── required/
│   │   ├── scoring/
│   │   ├── slicing/
│   │   ├── placing/
│   │   └── pipeline/
│   ├── optional/
│   └── README.md
│
├── spec/                                    ← mdBook specification (unchanged)
│   ├── src/
│   ├── book/
│   └── book.toml
│
└── .github/workflows/                       ← CI (updated)
    ├── ci.yml                               ← add Rust job
    ├── release.yml                          ← add crates.io publish
    └── spec.yml                             ← unchanged
```

## Key Design Decisions

### 1. `rust-toolchain.toml` at repo root (not inside `crates/`)

**This is a correctness requirement, not a style preference.** `rustup` discovers toolchain files by walking up from the current working directory. CI workflows that invoke `cargo --manifest-path crates/assay-cupel/Cargo.toml` from the repo root would not find a toolchain file scoped inside `crates/`. Root placement ensures the correct Rust toolchain is selected regardless of working directory.

Parallels `global.json` for .NET — both are repo-wide SDK version pins.

### 2. No `Cargo.lock` committed (library convention)

`assay-cupel` is a library crate published to crates.io. The Rust ecosystem convention is that libraries do **not** commit `Cargo.lock` — consumers generate their own lock file based on their dependency resolution. With only 2 runtime dependencies (chrono, thiserror), there's minimal risk from unpinned transitive deps.

Add `crates/**/Cargo.lock` to `.gitignore`.

**Alternative**: If reproducible CI builds are a priority, commit `Cargo.lock` at `crates/assay-cupel/Cargo.lock`. This is acceptable but non-standard for libraries.

### 3. Conformance vectors: single source of truth at `conformance/`

The Rust conformance test harness (`tests/conformance.rs`) references vectors via `CARGO_MANIFEST_DIR`-relative paths:
```rust
let vectors_dir = Path::new(env!("CARGO_MANIFEST_DIR"))
    .join("../../conformance");
```

`CARGO_MANIFEST_DIR` is set at compile time to the crate's `Cargo.toml` directory, making this path stable regardless of where `cargo` is invoked from. No copies, no symlinks, no submodules.

### 4. `.NET solution unchanged`

`Cupel.slnx` is not modified. The Rust crate is invisible to the .NET build system. MSBuild's `Directory.Build.props` walk-up behavior is unaffected — no re-anchoring needed.

### 5. `.editorconfig` extended for Rust

Add a Rust section:
```ini
[*.rs]
indent_style = space
indent_size = 4

[*.toml]
indent_style = space
indent_size = 2
```

The existing `*.toml` rule may need to be added if not present (`.toml` currently falls under the generic `[*]` rule).

## Proposals Eliminated and Why

| Proposal | Reason for elimination |
|----------|----------------------|
| 1. `dotnet/`+`rust/` partitioning | Requires moving all .NET files. MSBuild `Directory.Build.props` walk-up becomes a landmine. Massive disruption for speculative future-proofing. |
| 3. Root Cargo workspace | Sound architecture, but YAGNI for one crate. Adds 3+ config files to root. Promotion from Proposal 2→3 is trivial when needed. |
| 4. Top-level `assay-cupel/` | Creates naming asymmetry (.NET in `src/`, Rust at root). Looks accidental, not intentional. Doesn't scale. |
| 5. Spec-centric `impl/` | Requires moving every file in the repo. Over-engineered for two language implementations. |
| 6. Git submodule for conformance | Disproportionate ceremony for 37 TOML files. Submodule DX tax (forgotten `--recursive`, CI checkout steps, version pinning) outweighs benefits with only 2 consumers. |

## Workspace Promotion Plan

**Trigger**: When a second Rust crate needs to live in this repo.

**Steps** (single mechanical PR, ~10 minutes):
1. Create root `Cargo.toml`:
   ```toml
   [workspace]
   resolver = "2"
   members = ["crates/*"]
   ```
2. Move `Cargo.lock` from `crates/assay-cupel/` to root (if committed)
3. Move shared workspace dependencies into `[workspace.dependencies]`
4. Update CI `--manifest-path` references to use workspace root
5. Optionally move `rustfmt.toml`/`clippy.toml` to root for workspace-wide linting

**What doesn't change**: crate source paths, conformance vector paths, .NET layout, `rust-toolchain.toml` location.

## Open Questions for Implementation

1. **`.gitignore` strategy**: Add Rust patterns (`target/`, `Cargo.lock`) to the existing root `.gitignore`, or create `crates/.gitignore`? Root is simpler; scoped is cleaner.
2. **CI workflow structure**: Add Rust jobs to existing `ci.yml`, or create a separate `rust-ci.yml`? Separate allows independent triggering on `crates/**` path changes.
3. **`Cargo.toml` metadata**: The crate's `repository` field currently points to `wollax/assay`. Must update to `wollax/cupel`. The `version` was workspace-inherited — needs to be set directly.
