# Repo Structure Proposals — Dual-Language Cupel Monorepo

## Context

Cupel is currently a .NET-only monorepo. The `assay-cupel` Rust crate lives in `wollax/assay` as a workspace member. The goal is to pull `assay-cupel` into the cupel repo, publish to crates.io, and have `wollax/assay` consume it as a crates.io dependency.

### Current Cupel Layout
```
Cupel.slnx
Directory.Build.props / Directory.Packages.props
global.json
src/Wollax.Cupel{,.Json,.Tiktoken,.Extensions.DI}/
tests/Wollax.Cupel{,.Json,.Tiktoken,.Extensions.DI}.Tests/
benchmarks/Wollax.Cupel.Benchmarks/
conformance/{required,optional}/  ← TOML test vectors
spec/                              ← mdBook specification
```

### Current assay-cupel Layout (in wollax/assay)
```
crates/assay-cupel/
  Cargo.toml (workspace member, deps: chrono, thiserror)
  src/{lib.rs, model/, scorer/, slicer/, placer/, pipeline/}
  tests/conformance.rs
  tests/conformance/required/  ← 28 TOML files (COPIES of cupel's vectors)
```

---

## Proposal 1: Language-Partitioned Top-Level (`dotnet/` + `rust/`)

### What
Introduce two top-level directories `dotnet/` and `rust/` that fully partition language-specific code. Shared assets (spec, conformance vectors) stay at root.

```
Cupel.slnx → dotnet/Cupel.slnx
dotnet/src/Wollax.Cupel/
dotnet/tests/Wollax.Cupel.Tests/
dotnet/benchmarks/
dotnet/Directory.Build.props
dotnet/global.json
rust/Cargo.toml          ← standalone (not workspace)
rust/assay-cupel/src/
rust/assay-cupel/tests/
rust/rust-toolchain.toml
conformance/             ← shared, single source of truth
spec/
```

### Why
- Clean separation: contributors in one language never touch the other's tree
- Shared assets (spec, conformance) are obviously cross-cutting at root level
- Scales naturally if a third language implementation appears (e.g., `python/`)
- IDE projects open cleanly from their respective subdirectories

### Scope
Medium — requires moving all .NET files one level deeper, updating CI paths, .editorconfig scoping, and the .slnx project paths.

### Risks
- **Breaking change for .NET contributors**: all paths shift, muscle memory disrupted
- **`Directory.Build.props` walk-up**: MSBuild auto-discovers props by walking up; moving to `dotnet/` requires re-anchoring or explicit imports
- **Git history**: `git log --follow` handles renames, but tools like GitHub blame may lose context
- **Root clutter stays**: `global.json` at root could confuse Rust-only devs (though it can move into `dotnet/`)

---

## Proposal 2: Rust Crate as Peer Under `crates/` (Minimal Disruption)

### What
Add a `crates/` directory at the repo root for Rust crates. The .NET structure stays untouched. Conformance vectors remain at `conformance/` and are referenced by both test suites.

```
Cupel.slnx                        ← unchanged
src/Wollax.Cupel/                  ← unchanged
tests/Wollax.Cupel.Tests/          ← unchanged
crates/assay-cupel/                ← NEW
  Cargo.toml (standalone, not workspace)
  src/
  tests/conformance.rs             ← reads from ../../conformance/
crates/Cargo.lock                  ← or root-level
conformance/                       ← shared, single source of truth
spec/
rust-toolchain.toml                ← root or crates/
```

### Why
- **Zero disruption to .NET side**: no path changes, no broken workflows
- `crates/` is the Rust ecosystem convention (mirrors assay's own layout)
- Clear namespace: "if it's Rust, it's in `crates/`"
- Conformance vectors already at `conformance/` — Rust tests just `../..` to reach them

### Scope
Small — copy `assay-cupel` into `crates/`, add `rust-toolchain.toml` and `Cargo.lock`, update CI.

### Risks
- **Naming confusion**: `crates/` is typically inside a Cargo workspace; here it's a standalone crate inside a .NET repo — may confuse Rust contributors expecting a workspace
- **Orphan Cargo.toml**: no root `Cargo.toml` means `cargo` commands must be run from `crates/assay-cupel/`; slightly awkward DX
- **Scale concern**: if more Rust crates appear, you'd want a workspace — so you might refactor later anyway

---

## Proposal 3: Root-Level Cargo Workspace Alongside .NET Solution

### What
Place a `Cargo.toml` workspace at the repo root, mirroring how `Cupel.slnx` already lives at root. Rust crates go under `crates/`. Both build systems coexist at the top level.

```
Cupel.slnx                        ← .NET entry point
Cargo.toml                         ← Rust workspace entry point
Cargo.lock
rust-toolchain.toml
crates/assay-cupel/
  Cargo.toml
  src/
  tests/
src/Wollax.Cupel/                  ← .NET sources
tests/Wollax.Cupel.Tests/          ← .NET tests
conformance/
spec/
```

### Why
- **Both ecosystems feel first-class**: `cargo build` from root Just Works, `dotnet build` from root Just Works
- Workspace allows future Rust crates (e.g., `assay-cupel-serde`, `assay-cupel-ffi`) without restructuring
- `Cargo.toml` + `Cupel.slnx` at root signals "this is a polyglot project" immediately
- Standard Rust workspace conventions apply — Rust contributors feel at home

### Scope
Small-Medium — add root `Cargo.toml` workspace + `rust-toolchain.toml`, place crate under `crates/`. No .NET changes.

### Risks
- **Root clutter**: `Cargo.toml`, `Cargo.lock`, `rust-toolchain.toml`, `rustfmt.toml`, `clippy.toml` all at root alongside .NET's `global.json`, `Directory.Build.props`, etc. Could feel noisy
- **Editor confusion**: opening root in VS Code may trigger both C# and Rust extensions; Rider may index Cargo files unnecessarily
- **Workspace assumptions**: Rust contributors might expect `cargo test` from root to run all tests, but conformance tests need the vectors — paths work fine from workspace root actually

---

## Proposal 4: Top-Level `assay-cupel/` as Self-Contained Crate

### What
Place the Rust crate directly at the repo root as `assay-cupel/`, treating it as a self-contained package (no workspace). Minimal ceremony, maximum simplicity.

```
Cupel.slnx
assay-cupel/
  Cargo.toml
  Cargo.lock
  rust-toolchain.toml
  src/
  tests/conformance.rs   ← reads from ../conformance/
src/Wollax.Cupel/
tests/
conformance/
spec/
```

### Why
- **Simplest possible change**: one new directory, no workspace overhead
- The crate name IS the directory name — zero ambiguity
- Self-contained: `cd assay-cupel && cargo test` works immediately
- Clear parallel: `src/` = .NET libraries, `assay-cupel/` = Rust library

### Scope
Small — copy crate, add it as a directory, done.

### Risks
- **Doesn't scale**: if a second Rust crate appears, you need restructuring
- **Inconsistent hierarchy**: .NET sources are under `src/`, Rust is a top-level sibling — asymmetric
- **Toolchain files buried**: `rust-toolchain.toml` inside a subdirectory means contributors must know to `cd` first, or you duplicate it at root

---

## Proposal 5: Conformance-Centric Layout (Spec as Hub)

### What
Reorganize around the specification as the central artifact. Implementations are peers under `impl/`. Conformance vectors move into `spec/conformance/` (if not already) to emphasize they're part of the spec, not implementation tests.

```
spec/
  src/                      ← mdBook source
  book/                     ← built book
  conformance/
    required/               ← TOML test vectors
    optional/
impl/
  dotnet/
    Cupel.slnx
    src/Wollax.Cupel/
    tests/
    benchmarks/
  rust/
    Cargo.toml
    assay-cupel/src/
    assay-cupel/tests/
```

### Why
- **Spec-first philosophy**: the specification is the product; implementations are just realizations of it
- Makes it obvious that conformance vectors belong to the spec, not any implementation
- Adding a new language implementation is just `impl/<lang>/` — perfectly uniform
- Contributors see the hierarchy: spec governs, implementations conform

### Scope
Large — requires moving ALL .NET files under `impl/dotnet/`, updating every CI path, every MSBuild reference, solution paths. Essentially a repo reorganization.

### Risks
- **Massive disruption**: every path in the repo changes
- **MSBuild complexity**: `Directory.Build.props` and package resolution need careful re-anchoring
- **Over-engineering for two languages**: this layout shines with 3+ implementations; with just two, it's ceremony
- **`impl/` is non-standard**: neither .NET nor Rust communities use this convention

---

## Proposal 6: Git Submodule for Conformance Vectors

### What
Instead of choosing where vectors live, extract them into a standalone `cupel-conformance` repo and include it as a git submodule in both `cupel` and `assay`. Each consumer pins a version.

```
cupel/
  Cupel.slnx
  src/
  tests/
  crates/assay-cupel/        ← Rust crate (any layout from above)
  conformance/                ← git submodule → cupel-conformance
  spec/

cupel-conformance/            ← separate repo
  required/
  optional/
  README.md
```

### Why
- **Single source of truth**: conformance vectors are versioned independently
- **Decoupled releases**: spec version can advance independently of implementations
- Both cupel and assay pin specific conformance versions — no drift
- Third-party implementations can consume the same submodule
- Semantic versioning of the test suite itself

### Scope
Medium — create new repo, move vectors, set up submodule in both cupel and assay.

### Risks
- **Submodule DX pain**: `git clone --recursive`, forgotten `git submodule update`, CI needs explicit checkout
- **Over-engineering for now**: with only two implementations, a simple directory is simpler than a whole repo
- **Release coordination**: updating vectors requires bumping the submodule ref in each consumer repo
- **Developer friction**: new contributors consistently struggle with submodules

---

## Summary Comparison

| Proposal | .NET Disruption | Rust DX | Scales to 3+ langs | Conformance Strategy | Complexity |
|----------|----------------|---------|--------------------|--------------------|------------|
| 1. `dotnet/`+`rust/` | High | Good | Excellent | Root shared dir | Medium |
| 2. `crates/` peer | None | OK | Fair | Root shared dir | Low |
| 3. Root workspace | None | Excellent | Good | Root shared dir | Low-Med |
| 4. Top-level `assay-cupel/` | None | Good | Poor | Root shared dir | Low |
| 5. Spec-centric `impl/` | Very High | Good | Excellent | Under `spec/` | High |
| 6. Conformance submodule | Varies | Varies | Excellent | Submodule | Medium |

**My top picks**: Proposal 3 (root workspace) for best DX balance, or Proposal 2 (`crates/` peer) if minimal disruption is paramount. Proposal 6 (submodule) is orthogonal and could combine with any of 1-5.
