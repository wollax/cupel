# Phase 21 Research: docs.rs Documentation & Examples

## Summary

Phase 21 adds crate-level and module-level documentation, three standalone examples, and docs.rs build configuration to the `cupel` crate. The crate currently has zero `//!` doc comments in `lib.rs` or any module file, no `examples/` directory, no `[package.metadata.docs.rs]` section in Cargo.toml, and the README is a stale placeholder referencing `use cupel::prelude::*` (which does not exist). The public API surface is well-defined: 6 modules, 3 traits, 8 scorers, 3 slicers, 2 placers, 7 model types, 1 error enum, and a builder-based Pipeline.

**Confidence: HIGH** — Based on direct codebase inspection and verified docs.rs/rustdoc documentation.

## Standard Stack

No external libraries are needed for this phase. All work uses:

| Tool | Purpose |
|------|---------|
| `rustdoc` | Documentation generation via `cargo doc` |
| `cargo test --doc` | Doctest compilation and execution |
| `cargo run --example` | Standalone example verification |
| `[package.metadata.docs.rs]` | docs.rs build configuration |

No documentation-generation crates, no proc macros, no additional dependencies.

## Architecture Patterns

### 1. Crate-Level Documentation Pattern (lib.rs)

Use `//!` doc comments at the top of `lib.rs` before any `pub mod` declarations. Structure follows the Tokio-style progressive disclosure model locked in CONTEXT.md:

```
//! # cupel
//!
//! [One-line description]
//!
//! [Conceptual intro paragraph: score -> slice -> place pipeline model and why it matters]
//!
//! ## Quickstart
//!
//! [Minimal hello-world pipeline example — compiles as doctest]
//!
//! ## Multi-Scorer Configuration
//!
//! [Realistic CompositeScorer example — compiles as doctest]
//!
//! ## Key Concepts
//!
//! [Brief glossary: context window, token budget, RAG, scorer, slicer, placer]
//!
//! ## Feature Flags
//!
//! | Feature | Description |
//! |---------|-------------|
//! | `serde` | Enables Serialize/Deserialize on all model types |
//!
//! ## Modules
//!
//! [Brief module-by-module navigation aid]
```

**Confidence: HIGH** — Mirrors Tokio's documented style; CONTEXT.md locks this narrative structure.

### 2. Module Doc Comment Pattern

Every `mod.rs` (or single-file module) gets `//!` comments with:
- Purpose sentence
- Type listing (what's exported)
- Mini code example as doctest

For the four priority modules (scorer, pipeline, model, slicer), add deeper treatment:
- Scorer: comparison table mapping use cases to scorers
- Pipeline: stage-by-stage explanation
- Model: relationship diagram in text
- Slicer: algorithm tradeoff summary

### 3. Struct/Trait Doctest Pattern

Every public struct and trait gets at least one `///` doctest. Pattern:

```rust
/// A slicer selects items from a sorted list to fit within a token budget.
///
/// # Example
///
/// ```
/// use cupel::{ContextBudget, GreedySlice, Slicer};
/// // ... minimal working example
/// ```
```

**Critical constraint:** Doctests must use only the public API (`cupel::*` imports), not `crate::*` internal paths.

### 4. Standalone Examples Pattern

Three files in `crates/cupel/examples/`:
1. `basic_pipeline.rs` — core pipeline flow
2. `serde_roundtrip.rs` — requires `--features serde`
3. `quota_slicing.rs` — QuotaSlice with per-kind budgets

Each uses commented walkthrough style with `println!` output (no asserts). Must be listed in `Cargo.toml` `include` array to ship with the crate.

### 5. README Mirroring Pattern

`crates/cupel/README.md` should mirror the crate-level quickstart from `lib.rs`. Two approaches:
- **Option A (recommended):** Write the quickstart in README.md, then use `#![doc = include_str!("../README.md")]` in lib.rs. Single source of truth; content appears on both crates.io and docs.rs.
- **Option B:** Write in lib.rs, manually sync to README.md.

Option A is the standard Rust ecosystem pattern. The `include_str!` directive works with `cargo doc` and docs.rs. Verified via rustdoc documentation.

**Confidence: HIGH** — `#![doc = include_str!(...)]` is stable and widely used (serde, tokio, etc.).

### 6. docs.rs Metadata Configuration

```toml
[package.metadata.docs.rs]
all-features = true
rustdoc-args = ["--cfg", "docsrs"]
```

The `all-features = true` ensures the serde feature's types/impls appear in docs.rs output. The `--cfg docsrs` flag sets a cfg that can be used in code to conditionally enable documentation-only attributes.

**Confidence: HIGH** — Verified against docs.rs official metadata documentation and chrono's Cargo.toml (a direct dependency).

### 7. Feature-Gate Badge Pattern (doc_auto_cfg)

docs.rs builds with nightly Rust (currently 1.96.0-nightly). On nightly, `doc(auto_cfg)` is enabled by default, meaning items behind `#[cfg(feature = "serde")]` will automatically show a "Available on crate feature serde only" badge without any explicit annotation.

However, for robustness and compatibility with local `cargo doc` builds, add:

```rust
#![cfg_attr(docsrs, feature(doc_auto_cfg))]
```

This is the belt-and-suspenders approach: on docs.rs (nightly + `docsrs` cfg set), `doc_auto_cfg` is explicitly enabled. Locally on stable, the attribute is ignored.

**Confidence: MEDIUM** — `doc_auto_cfg` is enabled by default on recent nightlies, but its stabilization PR (#150055) is still open. The `cfg_attr` guard ensures it works regardless.

## Don't Hand-Roll

| Problem | Use Instead |
|---------|-------------|
| README/lib.rs doc sync | `#![doc = include_str!("../README.md")]` |
| Feature-gate documentation badges | `#![cfg_attr(docsrs, feature(doc_auto_cfg))]` + `[package.metadata.docs.rs]` with `rustdoc-args = ["--cfg", "docsrs"]` |
| Doctest boilerplate reduction | Use `# ` prefix to hide setup lines in doctests |
| Module doc organization | Follow existing `mod.rs` pattern — add `//!` comments at top, don't restructure files |

## Common Pitfalls

### 1. Doctests That Don't Compile

**Risk: HIGH.** The crate's builder pattern requires multiple steps: `ContextItemBuilder::new(...).kind(...).build()?`, `ContextBudget::new(...)`, `Pipeline::builder().scorer(...).slicer(...).placer(...).build()?`. Missing any required field causes a compile error or runtime `Err`.

**Mitigation:** Every doctest must be a complete, compiling example. Use hidden lines (`# `) for imports and setup. Test with `cargo test --doc --all-features` before considering complete.

### 2. Doctests Fail Without Features

Doctests that use serde types will fail without `--features serde`. The `cargo test --doc` command does not enable features by default.

**Mitigation:** Serde-specific doctests must be gated with ````rust,ignore``` or use the `#[cfg(feature = "serde")]` gate in the doc comment. Alternatively, keep serde examples in the standalone `serde_roundtrip.rs` example only, and use `# [cfg(feature = "serde")]` blocks in doc comments.

Actually, the correct approach for feature-gated doctests:
```rust
/// ```
/// # #[cfg(feature = "serde")]
/// # {
/// use cupel::ContextItem;
/// let json = serde_json::to_string(&item)?;
/// # }
/// ```
```
But this makes the doctest a no-op when the feature is off. Better: use ````rust,ignore``` for serde doctests in module docs and point to the `serde_roundtrip` example.

**Confidence: HIGH** — Well-known Rust ecosystem gotcha.

### 3. `include` Array in Cargo.toml Excludes Examples

The current `Cargo.toml` has an explicit `include` array. If `examples/` is not listed, the examples won't be included in the published crate.

**Current include list:**
```toml
include = [
    "src/**/*.rs",
    "tests/**/*.rs",
    "conformance/**/*.toml",
    "Cargo.toml",
    "LICENSE",
    "README.md",
]
```

**Must add:** `"examples/**/*.rs"` to the include array.

**Confidence: HIGH** — Direct codebase observation.

### 4. Stale README References

The current README references `use cupel::prelude::*` which does not exist. The README also says "Coming soon -- source files migrate in Phase 17" which is now complete. Must be fully rewritten.

**Confidence: HIGH** — Direct codebase observation.

### 5. Missing `cargo doc` Warnings

`cargo doc --no-deps --all-features` currently builds with zero warnings (verified). The success criteria requires this to remain true after all documentation is added. Broken intra-doc links (`[`SomeType`]`) are the most common source of new warnings.

**Mitigation:** Use fully-qualified paths in doc links: `[`Pipeline`](crate::Pipeline)` or rely on rustdoc's auto-resolution for types re-exported at crate root.

### 6. Doctest Dependency on `chrono`

Doctests that demonstrate timestamps need `chrono::Utc::now()` or similar. The `chrono` crate is a regular dependency (not just dev), so this works. But doctests should import from `cupel` not from `chrono` directly — except `chrono::Utc` is needed for timestamps. This is acceptable since `chrono` is a public dependency (it appears in the public API via `DateTime<Utc>`).

**Confidence: HIGH** — `chrono` is in `[dependencies]`, not `[dev-dependencies]`.

## Code Examples

### Minimal Quickstart Doctest (for lib.rs / README)

```rust
use std::collections::HashMap;
use cupel::{
    ContextBudget, ContextItemBuilder, ContextKind,
    Pipeline, PriorityScorer, GreedySlice, ChronologicalPlacer,
};

let items = vec![
    ContextItemBuilder::new("System instructions", 50)
        .kind(ContextKind::new("SystemPrompt")?)
        .priority(100)
        .pinned(true)
        .build()?,
    ContextItemBuilder::new("User's latest message", 30)
        .priority(10)
        .build()?,
    ContextItemBuilder::new("Retrieved document about Rust", 200)
        .kind(ContextKind::new("Document")?)
        .build()?,
];

let budget = ContextBudget::new(
    4096,   // max tokens (model context window)
    1000,   // target tokens (aim for this)
    500,    // output reserve
    HashMap::new(),
    5.0,    // safety margin %
)?;

let pipeline = Pipeline::builder()
    .scorer(Box::new(PriorityScorer))
    .slicer(Box::new(GreedySlice))
    .placer(Box::new(ChronologicalPlacer))
    .build()?;

let selected = pipeline.run(&items, &budget)?;
println!("Selected {} items", selected.len());
```

**Note:** This must compile as a doctest. The `?` operator requires either wrapping in `fn main() -> Result<(), Box<dyn Error>>` or using hidden `# fn main() -> ... {` lines.

### Multi-Scorer Configuration Example

```rust
use cupel::{
    CompositeScorer, RecencyScorer, PriorityScorer, KindScorer,
    Pipeline, GreedySlice, UShapedPlacer,
};

let scorer = CompositeScorer::new(vec![
    (Box::new(RecencyScorer), 0.4),
    (Box::new(PriorityScorer), 0.4),
    (Box::new(KindScorer::with_default_weights()), 0.2),
])?;

let pipeline = Pipeline::builder()
    .scorer(Box::new(scorer))
    .slicer(Box::new(GreedySlice))
    .placer(Box::new(UShapedPlacer))
    .build()?;
```

### Scorer Comparison Table (for scorer module docs)

| Use Case | Scorer | Type | Notes |
|----------|--------|------|-------|
| Favor recent messages | `RecencyScorer` | Relative | Rank-based; needs timestamps |
| Explicit priority levels | `PriorityScorer` | Relative | Rank-based; needs priority field |
| Weight by content type | `KindScorer` | Absolute | Configurable weight map |
| Tag-based relevance | `TagScorer` | Absolute | Weighted tag matching |
| Topic clustering | `FrequencyScorer` | Relative | Shared-tag proportion |
| External relevance signal | `ReflexiveScorer` | Absolute | Passes through `future_relevance_hint` |
| Combine multiple strategies | `CompositeScorer` | Composite | Weighted average, normalized |
| Normalize score range | `ScaledScorer` | Decorator | Min-max normalization of inner scorer |

### docs.rs Cargo.toml Configuration

```toml
[package.metadata.docs.rs]
all-features = true
rustdoc-args = ["--cfg", "docsrs"]
```

### lib.rs Crate Attributes

```rust
#![doc = include_str!("../README.md")]
#![cfg_attr(docsrs, feature(doc_auto_cfg))]
```

## State of the Art

### docs.rs Build Environment (verified 2026-03-15)
- **Toolchain:** nightly (`rustc 1.96.0-nightly`)
- **Auto-set cfg:** `docsrs` is automatically set by docs.rs
- **doc_auto_cfg:** Enabled by default on nightly; explicit `feature(doc_auto_cfg)` is belt-and-suspenders
- **Source:** https://docs.rs/about/builds, https://docs.rs/about/metadata

### Doctest Hidden Line Syntax
Lines starting with `# ` (hash + space) in doc examples are hidden in rendered docs but compiled in tests. This is the standard way to hide boilerplate:

```rust
/// ```
/// # use std::collections::HashMap;
/// # fn main() -> Result<(), Box<dyn std::error::Error>> {
/// let budget = cupel::ContextBudget::new(4096, 1000, 0, HashMap::new(), 0.0)?;
/// # Ok(())
/// # }
/// ```
```

### README as Crate Docs Pattern
The `#![doc = include_str!("../README.md")]` pattern is the ecosystem standard for single-source README/crate docs. It:
- Appears as the crate root page on docs.rs
- Appears as the crate description on crates.io
- Can be tested with `cargo test --doc`
- Works with intra-doc links if the README uses them

**Caveat:** Markdown headings in the README become part of the rustdoc output. The README should not start with `# cupel` since rustdoc already shows the crate name. Instead, start with a description paragraph or use the heading to match the crate name (docs.rs handles this gracefully).

### Edition 2024 Considerations
The crate uses edition 2024. No known documentation-specific changes in this edition affect the plan. Doctests run as edition 2024 by default, which means:
- `use` statements follow 2024 path resolution
- No `extern crate` needed in doctests (already true since 2018)

## Open Questions

1. **README heading strategy:** Should `README.md` start with `# cupel` (matching crate name, standard for crates.io) or skip the heading (to avoid duplication in docs.rs)? Most crates include it. Recommend: include `# cupel` — docs.rs renders it fine.

2. **Doctest error handling:** The `?` operator in doctests requires a `main` function returning `Result`. Should doctests use explicit `fn main() -> Result<...>` (hidden), or use `.unwrap()` for brevity? Recommend: hidden `main` returning `Result` for the quickstart; `.unwrap()` for small per-type doctests where the focus is on the type, not error handling.

3. **Repo root README:** CONTEXT.md says the repo root gets a "multi-language project overview." This is out of scope for Phase 21 (which focuses on `crates/cupel/README.md` only). Confirm.

## Sources

| Source | Confidence | Used For |
|--------|------------|----------|
| Codebase inspection (`crates/cupel/src/`) | HIGH | API surface, current doc state, module structure |
| docs.rs metadata documentation (https://docs.rs/about/metadata) | HIGH | `[package.metadata.docs.rs]` configuration |
| docs.rs build environment (https://docs.rs/about/builds) | HIGH | Nightly toolchain, `docsrs` cfg |
| Rustdoc `#[doc]` attribute reference | HIGH | `include_str!`, `doc(cfg)`, hidden lines |
| Rustdoc unstable features page | HIGH | `doc_auto_cfg` status and syntax |
| Chrono Cargo.toml (https://github.com/chronotope/chrono) | HIGH | Real-world `--cfg docsrs` pattern |
| Serde Cargo.toml (https://github.com/serde-rs/serde) | HIGH | Real-world docs.rs metadata pattern |
| Tokio lib.rs documentation structure | MEDIUM | Style reference for progressive disclosure |
| Rust #43781 tracking issue (doc_cfg stabilization) | MEDIUM | `doc_auto_cfg` stabilization status |

## Metadata

- **Phase:** 21 — docs.rs Documentation & Examples
- **Researcher:** Claude
- **Date:** 2026-03-15
- **Confidence:** HIGH overall — all findings verified against codebase and official documentation
- **Blockers:** None identified. Phase 20 (serde feature) is complete.
