---
phase: 16-pre-flight--crate-scaffold
plan: 01
subsystem: build-config
tags: [rust, toolchain, gitignore, editorconfig, crates-io]
dependency-graph:
  requires: []
  provides:
    - ".gitignore with Rust and .NET ignore patterns"
    - "rust-toolchain.toml pinning channel 1.85.0 with rustfmt + clippy"
    - ".editorconfig with Rust and TOML formatting rules"
    - "Crate name 'cupel' verified available on crates.io"
  affects:
    - "16-02 (Cargo.toml rust-version must match 1.85)"
    - "17 (crate migration depends on scaffold)"
tech-stack:
  added: []
  patterns:
    - "rust-toolchain.toml for toolchain pinning (parallels global.json for .NET)"
key-files:
  created:
    - ".gitignore"
    - "rust-toolchain.toml"
  modified:
    - ".editorconfig"
decisions:
  - id: "16-01-D1"
    decision: "Crate name 'cupel' confirmed available — preferred name selected"
    rationale: "Both 'cupel' and 'cupel-rs' are available; 'cupel' is shorter and matches the project name"
  - id: "16-01-D2"
    decision: "Toolchain pinned to 1.85.0 (not 'stable')"
    rationale: "Ensures MSRV alignment with Rust 2024 edition requirement; all contributors build with exact minimum version"
  - id: "16-01-D3"
    decision: "Cargo.lock excluded from git for library crate"
    rationale: "Rust convention — library crates don't commit Cargo.lock so consumers resolve dependencies independently"
metrics:
  duration: "~2 minutes"
  completed: "2026-03-15"
---

# Phase 16 Plan 01: Pre-flight Configuration Summary

**One-liner:** Verified `cupel` crate name availability and created repository-level Rust config files (.gitignore, rust-toolchain.toml, .editorconfig extensions).

## Crate Name Verification

Both names confirmed available on crates.io via authoritative cargo CLI:

**`cupel`** (preferred):
- `cargo search cupel` — empty result (no matching crates)
- `cargo info cupel` — `error: could not find 'cupel' in registry`

**`cupel-rs`** (fallback):
- `cargo search cupel-rs` — empty result (no matching crates)
- `cargo info cupel-rs` — `error: could not find 'cupel-rs' in registry`

**Decision:** Use `cupel` as the crate name. It is shorter, matches the project name, and avoids the `-rs` suffix convention that is falling out of favor in the Rust ecosystem.

## Tasks Completed

### Task 1: Verify crate name and create .gitignore
- Verified both `cupel` and `cupel-rs` available via `cargo search` and `cargo info`
- Created `.gitignore` with Rust (`target/`, `Cargo.lock`), .NET (`**/bin/`, `**/obj/`, `TestResults/`, `BenchmarkDotNet.Artifacts/`), and IDE (`.vs/`, `*.user`) patterns
- Commit: `1a6df1c`

### Task 2: Create rust-toolchain.toml and extend .editorconfig
- Created `rust-toolchain.toml` with `channel = "1.85.0"` and components `rustfmt`, `clippy`
- Extended `.editorconfig` with `[*.rs]` (4-space indent) and `[*.toml]` (2-space indent) sections
- Commit: `6993749`

## Toolchain Strategy

`rust-toolchain.toml` pins `channel = "1.85.0"` directly rather than using `"stable"`. This:
- Ensures all contributors build with the exact MSRV (Minimum Supported Rust Version)
- Aligns with the `rust-version = "1.85"` that Plan 02 will set in `Cargo.toml`
- Parallels the `.NET` approach where `global.json` pins the SDK version

Note: The `RUSTUP_TOOLCHAIN` environment variable (set by RTK proxy) overrides `rust-toolchain.toml` at runtime. In a clean shell, the toolchain file is respected.

## Deviations from Plan

None — plan executed exactly as written.

## Next Phase Readiness

Plan 02 can proceed immediately. All pre-code gates are satisfied:
- `.gitignore` prevents `target/` from being tracked before any `cargo` commands
- `rust-toolchain.toml` ensures correct Rust version before any compilation
- `.editorconfig` ensures consistent formatting for Rust and TOML files
- Crate name `cupel` is confirmed available for `Cargo.toml` creation
