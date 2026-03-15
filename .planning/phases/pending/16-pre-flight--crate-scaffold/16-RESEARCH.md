# Phase 16: Pre-flight & Crate Scaffold - Research

**Researched:** 2026-03-14
**Domain:** Rust crate scaffolding, Cargo.toml for crates.io publishing, toolchain configuration
**Confidence:** HIGH

## Summary

Phase 16 establishes all pre-conditions before any Rust source files move into the cupel repository. The phase is entirely configuration and verification gates: crate name availability, `Cargo.toml` authoring, `rust-toolchain.toml`, `.editorconfig` and `.gitignore` extensions, and a `cargo check` smoke test on an empty `lib.rs`.

The existing `assay-cupel` crate uses workspace-inherited fields (`version.workspace = true`, `edition.workspace = true`, etc.) which must ALL be replaced with standalone values. The assay workspace uses `edition = "2024"` (Rust 1.85+), `thiserror = "2"`, and `chrono = "0.4"`. The cupel repo currently has NO `.gitignore`, NO `rust-toolchain.toml`, and NO `crates/` directory.

**Primary recommendation:** Use crate name `cupel` (both `cupel` and `cupel-rs` return 404 on crates.io API, meaning available). Create standalone `Cargo.toml` at `crates/cupel/` with all fields fully specified, no workspace inheritance. Pin `rust-toolchain.toml` with `channel = "1.85.0"` matching the Rust 2024 edition MSRV.

## Standard Stack

### Core Dependencies (from existing assay-cupel)

| Library | Version | Purpose | Notes |
|---------|---------|---------|-------|
| `thiserror` | `2` | Derive `Error` trait for `CupelError` | Used in `error.rs` |
| `chrono` | `0.4` (features: `serde`) | Timestamp handling | Used in model types |

### Dev Dependencies (from existing assay-cupel)

| Library | Version | Purpose | Notes |
|---------|---------|---------|-------|
| `toml` | `0.8` | Parse conformance vector `.toml` files | Test-only |
| `serde` | `1` (features: `derive`) | Deserialize conformance vectors | Test-only |
| `serde_json` | `1` | JSON test assertions | Test-only |

### No New Libraries Needed

Phase 16 is scaffold-only. Dependencies are carried forward from the existing crate. No new libraries to evaluate.

## Architecture Patterns

### Recommended Directory Structure

```
cupel/                          # repo root (already exists)
├── crates/
│   └── cupel/
│       ├── Cargo.toml          # standalone, all fields explicit
│       ├── src/
│       │   └── lib.rs          # empty placeholder for Phase 16
│       └── conformance/        # vendored copy (Phase 17, not 16)
├── rust-toolchain.toml         # at repo root, parallels global.json
├── .editorconfig               # extended with [*.rs] and [*.toml]
├── .gitignore                  # NEW file (does not exist yet)
├── global.json                 # existing .NET SDK pin
└── conformance/                # canonical source of truth (existing)
```

### Pattern: Standalone Cargo.toml (No Workspace)

The existing `assay-cupel` crate inherits 4 fields from the assay workspace. Every one must be replaced with a literal value:

| Workspace Field | Standalone Replacement |
|----------------|----------------------|
| `version.workspace = true` | `version = "1.0.0"` |
| `edition.workspace = true` | `edition = "2024"` |
| `license.workspace = true` | `license = "MIT"` |
| `repository.workspace = true` | `repository = "https://github.com/wollax/cupel"` |

### Pattern: rust-toolchain.toml Pinning

The assay repo uses a minimal `rust-toolchain.toml`:

```toml
[toolchain]
channel = "stable"
components = ["rustfmt", "clippy"]
```

For cupel, pin to a specific version to enforce MSRV:

```toml
[toolchain]
channel = "1.85.0"
components = ["rustfmt", "clippy"]
```

**Why `1.85.0` not `stable`:** The roadmap requires MSRV 1.85 (the release that stabilized Rust 2024 edition). Pinning the channel to `1.85.0` ensures contributors use exactly this version, matching the `rust-version` field in `Cargo.toml`. The `rust-version` field in `Cargo.toml` is the MSRV declaration for consumers; `rust-toolchain.toml` enforces it for developers.

**Note:** The installed toolchain is 1.93.1, which is newer than 1.85. Rustup will install 1.85.0 when `rust-toolchain.toml` is present. Alternatively, `channel = "stable"` can be used if the team prefers latest-stable locally and relies on `rust-version` for MSRV enforcement — this is the more common approach for libraries.

### Pattern: Cargo.toml `include` Field for Publishing

When using `include`, only listed patterns are packaged. This is critical for Phase 17 (conformance vectors) but the field should be present in Phase 16's scaffold:

```toml
include = [
    "src/**/*.rs",
    "Cargo.toml",
    "LICENSE",
    "README.md",
    "conformance/**/*.toml",   # vendored conformance vectors (Phase 17)
]
```

**Max crate size:** 10MB on crates.io. Conformance vectors are TOML files, well within limit.

### Anti-Patterns to Avoid

- **Workspace inheritance in a single-crate repo:** Adds complexity with zero benefit. All fields must be literal.
- **Committing `Cargo.lock` for a library crate:** Rust convention is to NOT commit `Cargo.lock` for libraries. Add to `.gitignore`.
- **Using `edition = "2021"` when source already uses 2024 features:** The existing crate uses `edition = "2024"` in the assay workspace. The standalone crate must match.

## Crate Name Availability

### Verification Results

| Name | crates.io API | Status |
|------|--------------|--------|
| `cupel` | HTTP 404 | **Available** (no crate exists) |
| `cupel-rs` | HTTP 404 | **Available** (no crate exists) |

**Recommendation:** Use `cupel` as the crate name.

Rationale (from brainstorm):
- Short, memorable, matches the project name
- `cupel-rs` is redundant — it's on crates.io, obviously Rust
- `assay-cupel` is misleading — crate is not assay-specific after migration

**The roadmap says "verify `cupel-rs`"** but the brainstorm recommended `cupel`. Both are available. The planner should present this as a decision point or follow the roadmap's `cupel-rs` naming.

**Confidence:** MEDIUM — crates.io API returned 404 for both names, which indicates non-existence, but the API check was done via WebFetch (JS-rendered site returned generic page). The `cargo` CLI can definitively verify: `cargo search cupel` and `cargo info cupel`.

### Cargo-based Verification (for planner to include as task step)

```bash
cargo search cupel           # shows matching crates (empty = available)
cargo info cupel 2>&1        # "error: crate cupel does not exist" = available
cargo info cupel-rs 2>&1     # same check for fallback name
```

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|------------|-------------|-----|
| Error derive macros | Manual `impl Error` | `thiserror = "2"` | Already used, handles `Display` + `Error` |
| TOML conformance vector parsing | Custom parser | `toml = "0.8"` + `serde` | Already used in tests |
| Crate name verification | Manual HTTP requests | `cargo search` / `cargo info` CLI | Authoritative, handles edge cases |
| Toolchain pinning | Manual rustup overrides | `rust-toolchain.toml` | Standard Rust ecosystem mechanism |

## Common Pitfalls

### Pitfall 1: Workspace Field Leakage

**What goes wrong:** Copy-pasting `Cargo.toml` from assay and forgetting to replace `field.workspace = true` entries. `cargo check` fails with "failed to parse manifest" because there's no workspace root.
**Why it happens:** The source `Cargo.toml` has 4 workspace-inherited fields.
**How to avoid:** Write the `Cargo.toml` from scratch, not by copying. Verify with `cargo check --manifest-path crates/cupel/Cargo.toml`.
**Warning signs:** Any `.workspace = true` in the standalone `Cargo.toml`.

### Pitfall 2: Missing `.gitignore` Creates Noise

**What goes wrong:** The cupel repo has NO `.gitignore` file. Running `cargo check` creates `crates/cupel/target/` which shows up as untracked files everywhere.
**Why it happens:** The repo only had .NET artifacts, which are handled by the solution/project structure.
**How to avoid:** Create `.gitignore` BEFORE running any cargo commands.
**Warning signs:** `git status` shows `target/` directories.

### Pitfall 3: `rust-version` vs `rust-toolchain.toml` Channel Mismatch

**What goes wrong:** `rust-version = "1.85"` in `Cargo.toml` but `channel = "stable"` in `rust-toolchain.toml`. Contributors build with latest stable (1.93+) and accidentally use features not available in 1.85.
**Why it happens:** Two different mechanisms serve different audiences (consumers vs developers).
**How to avoid:** Either pin `channel = "1.85.0"` or add CI step that tests with MSRV explicitly. Many libraries use `channel = "stable"` locally and test MSRV in CI only.
**Warning signs:** Code compiles locally but fails `cargo check` on MSRV.

### Pitfall 4: `edition = "2024"` Requires Rust 1.85+

**What goes wrong:** Setting `edition = "2024"` without ensuring MSRV is at least 1.85.
**Why it happens:** The 2024 edition was stabilized in Rust 1.85.0 (February 20, 2025). Earlier versions cannot parse `edition = "2024"`.
**How to avoid:** Set `rust-version = "1.85"` whenever using `edition = "2024"`.

### Pitfall 5: Forgetting `include` Means Everything Ships

**What goes wrong:** Without `include`, Cargo packages everything not in `.gitignore`. This could include planning docs, benchmarks, test results, etc.
**Why it happens:** Default behavior is inclusive.
**How to avoid:** Set `include` explicitly in Phase 16 scaffold. Even though conformance vectors aren't vendored yet, the field structure should be ready.

### Pitfall 6: `description` Field Duplication

**What goes wrong:** Using the same description from `assay-cupel` ("Context window management pipeline for LLM applications") which may not match the standalone crate's positioning.
**Why it happens:** Copy-paste from existing Cargo.toml.
**How to avoid:** Write a description that positions `cupel` as an independent library, not an assay component.

## Code Examples

### Complete Standalone Cargo.toml (Phase 16 Target)

```toml
[package]
name = "cupel"                  # or "cupel-rs" per roadmap
version = "1.0.0"
edition = "2024"
rust-version = "1.85"
license = "MIT"
repository = "https://github.com/wollax/cupel"
description = "Context window management pipeline for LLM applications"
readme = "README.md"
categories = ["algorithms", "text-processing"]
keywords = ["llm", "context-window", "pipeline", "token-budget"]
include = [
    "src/**/*.rs",
    "Cargo.toml",
    "LICENSE",
    "README.md",
]

[dependencies]
chrono = { version = "0.4", default-features = false, features = ["clock"] }
thiserror = "2"

[dev-dependencies]
toml = "0.8"
serde = { version = "1", features = ["derive"] }
serde_json = "1"
```

**Notes:**
- `chrono` features: the existing crate uses `features = ["serde"]` but serde is a dev-dependency concern. For the standalone crate, `default-features = false` with `clock` is lighter. The exact features needed should be verified when source moves in Phase 17.
- `keywords`: max 5, each max 20 chars, ASCII only, no duplicates of category names.
- `categories`: must match crates.io slugs exactly. `algorithms` and `text-processing` are verified slugs. `artificial-intelligence` is also available if preferred.
- `include` will expand in Phase 17 to add `conformance/**/*.toml`.

### Minimal lib.rs Placeholder

```rust
//! Context window management pipeline for LLM applications.
```

This is sufficient for `cargo check` to pass.

### rust-toolchain.toml

```toml
[toolchain]
channel = "1.85.0"
components = ["rustfmt", "clippy"]
```

### .editorconfig Extensions

```ini
[*.rs]
indent_style = space
indent_size = 4

[*.toml]
indent_style = space
indent_size = 2

[Cargo.toml]
indent_style = space
indent_size = 2
```

**Note:** `[*.toml]` covers `Cargo.toml` and `rust-toolchain.toml`. A separate `[Cargo.toml]` section is only needed if Cargo.toml needs different settings (it doesn't).

### .gitignore

```gitignore
# Rust
target/
Cargo.lock

# .NET (existing artifacts already untracked via .gitattributes or convention)
**/bin/
**/obj/
TestResults/
BenchmarkDotNet.Artifacts/

# IDE
.vs/
*.user
```

**Key decisions:**
- `Cargo.lock`: NOT committed for library crates (Rust convention). The roadmap/brainstorm agrees.
- `target/`: covers `crates/cupel/target/` and any future crate targets.
- .NET patterns: the repo currently has NO `.gitignore` — those `bin/`, `obj/`, `TestResults/` directories show as untracked in `git status`. This is an opportunity to clean up.

### Verification Command

```bash
cargo check --manifest-path crates/cupel/Cargo.toml
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|-------------|-----------------|--------------|--------|
| `edition = "2021"` | `edition = "2024"` | Rust 1.85, Feb 2025 | New edition features, unsafe rules |
| `thiserror = "1"` | `thiserror = "2"` | Late 2024 | Already using v2 in assay |
| `license = "MIT/Apache-2.0"` | `license = "MIT OR Apache-2.0"` | SPDX 2.1 | Use SPDX expression syntax |

## Open Questions

1. **Crate name: `cupel` vs `cupel-rs`**
   - What we know: Both are available on crates.io (404 from API). Brainstorm recommends `cupel`. Roadmap says verify `cupel-rs`.
   - What's unclear: User's final preference.
   - Recommendation: Use `cupel` (shorter, matches project). Verify definitively with `cargo search cupel` before committing.

2. **`rust-toolchain.toml` channel: pinned `1.85.0` vs `stable`**
   - What we know: Roadmap says "pins Rust 2024 edition with MSRV 1.85." Assay uses `channel = "stable"`.
   - What's unclear: Whether to enforce MSRV via toolchain file or via CI-only MSRV check.
   - Recommendation: Use `channel = "stable"` locally (matches assay convention, avoids forcing contributors to install 1.85.0), enforce MSRV in CI (Phase 18). Set `rust-version = "1.85"` in `Cargo.toml` for consumer-facing MSRV.

3. **`chrono` feature flags for standalone crate**
   - What we know: Assay workspace uses `features = ["serde"]`. Serde is currently dev-only for cupel.
   - What's unclear: Exact chrono features needed by cupel source code (requires reading model types in Phase 17).
   - Recommendation: Start with `chrono = "0.4"` (default features) in Phase 16. Refine in Phase 17 when source is analyzed.

4. **Should Phase 16 also create the `.gitignore` for .NET artifacts?**
   - What we know: The repo has NO `.gitignore`. `git status` shows 20+ untracked `bin/`, `obj/`, `TestResults/` directories.
   - What's unclear: Whether cleaning up .NET gitignore is in scope for Phase 16 or should be separate.
   - Recommendation: Include basic .NET patterns in the new `.gitignore` since the file must be created anyway. This is low-risk housekeeping.

## Sources

### Primary (HIGH confidence)
- Cargo manifest reference (WebFetch: doc.rust-lang.org/cargo/reference/manifest.html) — required fields for publishing
- Cargo publishing reference (WebFetch: doc.rust-lang.org/cargo/reference/publishing.html) — publish requirements, include field, 10MB limit
- Rust 2024 Edition Guide (WebFetch: doc.rust-lang.org/edition-guide/rust-2024/) — edition 2024 stabilized in Rust 1.85.0
- Rustup overrides documentation (WebFetch: rust-lang.github.io/rustup/overrides.html) — rust-toolchain.toml format
- Existing `assay/Cargo.toml` — workspace field values to de-inherit
- Existing `assay/crates/assay-cupel/Cargo.toml` — current crate metadata
- crates.io categories (WebFetch: raw GitHub categories.toml) — verified category slugs

### Secondary (MEDIUM confidence)
- crates.io API 404 responses for `cupel` and `cupel-rs` — name availability (should verify with `cargo search`)

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — directly read from existing `Cargo.toml` files in assay repo
- Architecture: HIGH — directory structure and toolchain config are well-documented Rust conventions
- Pitfalls: HIGH — derived from analysis of existing code and Cargo documentation
- Crate name availability: MEDIUM — API returned 404 but should be confirmed with `cargo search`

**Research date:** 2026-03-14
**Valid until:** 2026-04-14 (stable domain, Rust edition cycle is 3 years)
