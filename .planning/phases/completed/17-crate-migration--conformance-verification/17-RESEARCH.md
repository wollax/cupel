# Phase 17: Crate Migration & Conformance Verification - Research

**Researched:** 2026-03-14
**Domain:** Rust crate migration, cargo packaging, conformance testing, git hooks
**Confidence:** HIGH

## Summary

This phase migrates 26 Rust source files and 5 test files from `assay/crates/assay-cupel/` into `cupel/crates/cupel/`, updates imports (`assay_cupel` → `cupel`), copies conformance vectors into the crate, and verifies the result compiles, tests pass, and packages correctly.

The migration is mechanically straightforward. The source files contain zero references to `assay_cupel` — only the 5 test files do (7 occurrences across 4 files). The target crate already has a correct `Cargo.toml` with all dependencies specified. The main complexity lies in getting `cargo package` to include conformance vectors and setting up the CI diff guard and pre-commit hook.

**Primary recommendation:** Copy source files with `cp -R`, copy test files with directory structure, do a straight `sed` find-and-replace of `assay_cupel` → `cupel` in test files, update the vector path from `tests/conformance/required/` to `conformance/required/`, add `"conformance/**/*.toml"` to the `include` array, and verify with `cargo package --list`.

## Standard Stack

No new libraries needed. The crate's dependencies are already correctly specified in the target `Cargo.toml`.

### Core
| Library | Version | Purpose | Status |
|---------|---------|---------|--------|
| chrono | 0.4 | DateTime handling | Already in target Cargo.toml |
| thiserror | 2 | Error derive macros | Already in target Cargo.toml |

### Dev Dependencies
| Library | Version | Purpose | Status |
|---------|---------|---------|--------|
| toml | 0.8 | Parse conformance TOML vectors | Already in target Cargo.toml |
| serde | 1 (derive) | Deserialize test data | Already in target Cargo.toml |
| serde_json | 1 | JSON test assertions | Already in target Cargo.toml |

### No Additions Needed
The target `Cargo.toml` already mirrors the source's dependencies exactly. No workspace inheritance resolution is needed — the target uses explicit versions.

## Architecture Patterns

### Source File Layout (No Changes Needed)
The source files use a standard Rust module hierarchy. The `lib.rs` in the source repo already uses the crate name `cupel` implicitly (no self-references to `assay_cupel` in `src/`).

```
crates/cupel/
├── src/
│   ├── lib.rs              # Module declarations + re-exports
│   ├── error.rs            # CupelError type
│   ├── model/              # Domain types (7 files)
│   ├── pipeline/           # Pipeline stages (7 files)
│   ├── placer/             # Placement strategies (3 files)
│   ├── scorer/             # Scoring strategies (9 files)
│   └── slicer/             # Slicing strategies (4 files)
├── tests/
│   ├── conformance.rs      # Shared test helpers (top-level integration test)
│   └── conformance/        # Test submodules
│       ├── pipeline.rs     # 5 pipeline tests
│       ├── placing.rs      # 4 placing tests
│       ├── scoring.rs      # 13 scoring tests
│       └── slicing.rs      # 6 slicing tests
├── conformance/
│   └── required/           # 28 TOML test vectors (crate-local copy)
│       ├── pipeline/       # 5 vectors
│       ├── placing/        # 4 vectors
│       ├── scoring/        # 13 vectors
│       └── slicing/        # 6 vectors
├── Cargo.toml
├── LICENSE
└── README.md
```

### Pattern 1: Integration Test Module Structure
**What:** Cargo discovers `tests/conformance.rs` as the integration test entry point. It declares `mod conformance { pub mod pipeline; ... }` inline, meaning the submodules live at `tests/conformance/*.rs`.
**When to use:** This is the existing pattern — preserve it exactly.
**Key detail:** The `conformance.rs` file is both the module root AND contains shared helper functions (`load_vector`, `build_items`, `build_scorer`, etc.). Submodules use `super::` to access these helpers.

### Pattern 2: CARGO_MANIFEST_DIR for Test Data
**What:** The test runner uses `env!("CARGO_MANIFEST_DIR")` to locate conformance vectors at compile time. This resolves to the directory containing the crate's `Cargo.toml`.
**Critical change:** Source uses `.join("tests").join("conformance").join("required")` — target must change to `.join("conformance").join("required")`.
**Confidence:** HIGH — verified from source code inspection.

### Pattern 3: cargo package include for Non-Source Files
**What:** The `include` field in `Cargo.toml` controls what goes into the `.crate` tarball. It does NOT affect local `cargo test` — tests always run against the working directory.
**Critical detail:** `Cargo.toml` and `Cargo.lock` are always included automatically. `LICENSE` and `README.md` are already explicitly listed. Conformance vectors need `"conformance/**/*.toml"` added.
**Confidence:** HIGH — verified via Cargo official docs and `cargo package --list` on the current crate.

### Anti-Patterns to Avoid
- **Don't include `tests/` in the `include` array:** Tests are NOT shipped in the crate tarball. They run locally from the working directory regardless of `include`.
- **Don't use `include = ["conformance/"]`:** This would include ALL files, not just `.toml` vectors. Use `"conformance/**/*.toml"` for precision.
- **Don't use workspace-relative paths in the test runner:** `CARGO_MANIFEST_DIR` points to the crate root (`crates/cupel/`), not the repo root. The conformance vectors must exist at `crates/cupel/conformance/required/` for the test runner to find them.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| File copying | Custom script with error handling | `cp -R` with explicit paths | Simple directory copy; rsync overkill for 26 files |
| Import renaming | Regex-based multi-pass replacement | `sed -i '' 's/assay_cupel/cupel/g'` on 4 test files | Only 7 occurrences, all are simple identifier replacements, no ambiguity |
| Vector path update | Manual editing | `sed -i '' 's|.join("tests")\n.*\.join("conformance")|.join("conformance")|'` or single manual edit | Only 1 location in conformance.rs line 23-24 |
| Pre-commit hook | Framework (husky, pre-commit) | Plain bash script in `.githooks/pre-commit` + `git config core.hooksPath .githooks` | Repo has no hook framework; adding one is out of scope |
| Tarball verification | Custom script | `cargo package --list \| grep conformance` | Built-in cargo command |

**Key insight:** This migration is purely mechanical. The source and target have identical dependency sets, identical module structures, and the only code changes are 7 import renames and 1 path adjustment.

## Common Pitfalls

### Pitfall 1: Forgetting to Update the Vector Path in conformance.rs
**What goes wrong:** Tests compile but fail at runtime with "failed to read" because vectors aren't found.
**Why it happens:** Source uses `CARGO_MANIFEST_DIR/tests/conformance/required/` but target needs `CARGO_MANIFEST_DIR/conformance/required/`.
**How to avoid:** After copying `tests/conformance.rs`, change lines 23-25 from:
```rust
let base = Path::new(env!("CARGO_MANIFEST_DIR"))
    .join("tests")
    .join("conformance")
    .join("required");
```
to:
```rust
let base = Path::new(env!("CARGO_MANIFEST_DIR"))
    .join("conformance")
    .join("required");
```
**Warning signs:** Runtime panic mentioning "failed to read" with a path containing `/tests/conformance/required/`.

### Pitfall 2: Conformance Vectors Not in cargo package Tarball
**What goes wrong:** `cargo package --list` doesn't show the `.toml` files, so downstream users who `cargo install` won't have test data.
**Why it happens:** The `include` array in `Cargo.toml` was not updated to include `"conformance/**/*.toml"`.
**How to avoid:** Add the glob pattern to the `include` array before running `cargo package --list`.
**Verification:** `cargo package --list | grep -c '\.toml$'` should return 29 (28 vectors + Cargo.toml.orig).

### Pitfall 3: Stale Crate-Local Vectors After Editing Canonical Source
**What goes wrong:** CI passes but the crate ships old conformance vectors because the canonical repo-root versions were updated but the crate-local copies weren't synced.
**Why it happens:** Two copies of the same 28 files with no automated sync.
**How to avoid:** The CI diff guard (`diff -r conformance/required/ crates/cupel/conformance/required/`) catches this. The pre-commit hook prevents it from being committed.
**Warning signs:** CI step fails with diff output.

### Pitfall 4: sed on macOS vs Linux
**What goes wrong:** `sed -i` behaves differently on macOS (requires `''` as backup extension) vs Linux (no argument needed).
**Why it happens:** macOS ships BSD sed, Linux ships GNU sed.
**How to avoid:** Use `sed -i '' 's/old/new/g' file` on macOS. In CI (Linux), use `sed -i 's/old/new/g' file`. Or just manually edit the 4 files — there are only 7 replacements.
**Recommendation:** Given only 7 occurrences across 4 files, manual editing during the plan execution is safest and most explicit.

### Pitfall 5: Rust 2024 Edition — No Module Changes
**What goes wrong:** Nothing — but worth documenting that Rust 2024 does NOT change module resolution.
**Why it matters:** The source uses `edition.workspace = true` (which resolves to 2024 via the workspace). The target explicitly sets `edition = "2024"`. There are no edition-related module or import differences to worry about.
**Confidence:** MEDIUM — Rust 2024 edition guide was partially inaccessible, but no module changes were found in available documentation or release notes.

### Pitfall 6: cargo package Requires cargo build to Succeed First
**What goes wrong:** `cargo package` fails because the code doesn't compile.
**Why it happens:** `cargo package` runs `cargo verify` which builds the crate from the packaged tarball.
**How to avoid:** Ensure `cargo build`, `cargo test`, `cargo clippy`, and `cargo fmt --check` all pass before running `cargo package`.

### Pitfall 7: Missing Cargo.lock in Standalone Crate
**What goes wrong:** Reproducibility issues.
**Why it happens:** The crate already has a `Cargo.lock` (verified present). `cargo package` includes it automatically.
**How to avoid:** No action needed — just don't delete the existing `Cargo.lock`.

## Code Examples

### Import Replacement (conformance.rs line 13)
```rust
// BEFORE (assay)
use assay_cupel::{
    ChronologicalPlacer, CompositeScorer, ContextItem, ContextItemBuilder,
    ContextKind, FrequencyScorer, GreedySlice, KindScorer, KnapsackSlice, Placer,
    PriorityScorer, QuotaEntry, QuotaSlice, RecencyScorer, ReflexiveScorer, ScaledScorer,
    Scorer, ScoredItem, Slicer, TagScorer, UShapedPlacer,
};

// AFTER (cupel)
use cupel::{
    ChronologicalPlacer, CompositeScorer, ContextItem, ContextItemBuilder,
    ContextKind, FrequencyScorer, GreedySlice, KindScorer, KnapsackSlice, Placer,
    PriorityScorer, QuotaEntry, QuotaSlice, RecencyScorer, ReflexiveScorer, ScaledScorer,
    Scorer, ScoredItem, Slicer, TagScorer, UShapedPlacer,
};
```

### Import Replacement (pipeline.rs — all 4 occurrences)
```rust
// BEFORE
use assay_cupel::{ContextBudget, OverflowStrategy, Pipeline};
let scorer: Box<dyn assay_cupel::Scorer> = ...
let entries: Vec<(Box<dyn assay_cupel::Scorer>, f64)> = ...
Box::new(assay_cupel::CompositeScorer::new(entries).unwrap())

// AFTER
use cupel::{ContextBudget, OverflowStrategy, Pipeline};
let scorer: Box<dyn cupel::Scorer> = ...
let entries: Vec<(Box<dyn cupel::Scorer>, f64)> = ...
Box::new(cupel::CompositeScorer::new(entries).unwrap())
```

### Vector Path Update (conformance.rs lines 22-25)
```rust
// BEFORE
let base = Path::new(env!("CARGO_MANIFEST_DIR"))
    .join("tests")
    .join("conformance")
    .join("required");

// AFTER
let base = Path::new(env!("CARGO_MANIFEST_DIR"))
    .join("conformance")
    .join("required");
```

### Cargo.toml include Update
```toml
# BEFORE
include = [
    "src/**/*.rs",
    "Cargo.toml",
    "LICENSE",
    "README.md",
]

# AFTER
include = [
    "src/**/*.rs",
    "conformance/**/*.toml",
    "Cargo.toml",
    "LICENSE",
    "README.md",
]
```

### Pre-commit Hook Script (.githooks/pre-commit)
```bash
#!/usr/bin/env bash
set -euo pipefail

# Guard: conformance vectors must stay in sync
if ! diff -rq conformance/required/ crates/cupel/conformance/required/ >/dev/null 2>&1; then
    echo "ERROR: Conformance vectors are out of sync."
    echo "  canonical: conformance/required/"
    echo "  crate-local: crates/cupel/conformance/required/"
    echo ""
    diff -r conformance/required/ crates/cupel/conformance/required/ || true
    echo ""
    echo "Copy canonical vectors to crate-local before committing."
    exit 1
fi
```

### Activate Custom Hooks Directory
```bash
git config core.hooksPath .githooks
```

### CI Diff Guard Step (addition to ci.yml)
```yaml
- name: Verify conformance vector sync
  run: diff -r conformance/required/ crates/cupel/conformance/required/
```

### Tarball Verification Commands
```bash
# Verify vectors appear in package listing
cargo package --list | grep 'conformance/.*\.toml'

# Full round-trip verification
cd crates/cupel
cargo package
cd ../../target/package
tar xf cupel-1.0.0.crate
cd cupel-1.0.0
cargo test
```

## State of the Art

| Aspect | Current State | Notes |
|--------|--------------|-------|
| Rust edition | 2024 (1.85.0) | No module system changes from 2021 |
| thiserror | v2 | Major version bump from v1; already in target Cargo.toml |
| cargo package include | Gitignore-style globs | Standard approach, well-documented |
| Pre-commit hooks | Native git hooks | No framework needed for single-check hook |

**No deprecated approaches in use.** The source crate uses current patterns throughout.

## Open Questions

1. **CI workflow structure**
   - What we know: Current CI only runs .NET (`ci.yml`). Rust CI steps need to be added.
   - What's unclear: Should Rust steps be added to the existing `ci.yml` or a separate `rust.yml`?
   - Recommendation: Add to existing `ci.yml` as a separate job, since it already handles multi-step builds. But this may be out of scope for Phase 17 if CI is covered in a different phase.

2. **Pre-commit hook activation**
   - What we know: `git config core.hooksPath .githooks` is per-clone and not automatically applied.
   - What's unclear: Should there be a setup script or documentation for new contributors?
   - Recommendation: Add a one-line note in the crate README or a `Makefile` target. The `.githooks/` directory being committed is sufficient; activation is a one-time developer setup.

## Sources

### Primary (HIGH confidence)
- Source code inspection: `assay/crates/assay-cupel/` (all 26 src files, 5 test files, Cargo.toml)
- Source code inspection: `cupel/crates/cupel/` (Cargo.toml, src/lib.rs placeholder)
- Cargo official docs (doc.rust-lang.org/cargo/reference/manifest) — `include` field behavior
- `cargo package --list` output on current cupel crate — verified automatic includes

### Secondary (MEDIUM confidence)
- Rust 2024 Edition Guide (doc.rust-lang.org/edition-guide) — no module changes found
- Cargo official docs (doc.rust-lang.org/cargo/reference/publishing) — packaging behavior

### Tertiary (LOW confidence)
- None — all findings verified with primary sources

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — dependencies verified in both source and target Cargo.toml
- Architecture: HIGH — direct source code inspection of all files being migrated
- Migration mechanics: HIGH — only 7 import renames and 1 path change verified by grep
- Pitfalls: HIGH — derived from actual code paths, not theoretical concerns
- Pre-commit hook: MEDIUM — standard git pattern but implementation details are discretionary

**Research date:** 2026-03-14
**Valid until:** 2026-04-14 (stable domain, no fast-moving dependencies)
