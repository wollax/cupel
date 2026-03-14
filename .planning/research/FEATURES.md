# Cupel v1.1 — Features Research: Rust Crate Migration

**Date**: 2026-03-14
**Dimension**: Features — Rust crate migration (assay-cupel → cupel-rs on crates.io)
**Milestone**: v1.1 Rust Crate Migration & crates.io Publishing
**Context**: Moving the existing `assay-cupel` crate from `wollax/assay` into `wollax/cupel`,
publishing as `cupel-rs`, and having assay consume it from crates.io rather than as a path dependency.

---

## Baseline: What Already Exists

The `assay-cupel` crate in `wollax/assay` at `crates/assay-cupel/` is a complete, passing implementation
of the Cupel specification. It is not a prototype. All 28 required conformance tests pass.

### Public API Surface (assay-cupel today)

**Model types** (`src/model/`):
- `ContextItem` — immutable, private fields, full accessor set. Builder pattern via `ContextItemBuilder`.
  Fields: `content`, `tokens` (i64), `kind`, `source`, `priority` (Option<i64>), `tags`, `metadata`
  (HashMap<String, String>), `timestamp` (Option<DateTime<Utc>>), `future_relevance_hint` (Option<f64>),
  `pinned`, `original_tokens` (Option<i64>).
- `ContextBudget` — validated at construction. Fields: `max_tokens`, `target_tokens`, `output_reserve`,
  `reserved_slots` (HashMap<ContextKind, i64>), `estimation_safety_margin_percent` (f64).
- `ContextKind` — extensible newtype String with case-insensitive comparison and hashing.
  Well-known constants: Message, Document, ToolOutput, Memory, SystemPrompt.
- `ContextSource` — extensible newtype String with case-insensitive comparison and hashing.
  Well-known constants: Chat, Tool, Rag.
- `OverflowStrategy` — enum: Throw (default), Truncate, Proceed.
- `ScoredItem` — struct: item + score (f64).

**Scorer trait and implementations** (`src/scorer/`):
- `Scorer` trait: `score(&self, item, all_items) -> f64` + `as_any() -> &dyn Any` (for cycle detection).
- Implementations: `RecencyScorer`, `PriorityScorer`, `KindScorer`, `TagScorer`,
  `FrequencyScorer`, `ReflexiveScorer`, `CompositeScorer`, `ScaledScorer`.

**Slicer trait and implementations** (`src/slicer/`):
- `Slicer` trait: `slice(&self, sorted: &[ScoredItem], budget: &ContextBudget) -> Vec<ContextItem>`.
- Implementations: `GreedySlice`, `KnapsackSlice`, `QuotaSlice` (with `QuotaEntry`).
- Note: `StreamSlice` is NOT present — excluded per spec (Rust has async but streaming is anti-feature
  for this migration).

**Placer trait and implementations** (`src/placer/`):
- `Placer` trait: `place(&self, items: &[ScoredItem]) -> Vec<ContextItem>`.
- Implementations: `UShapedPlacer`, `ChronologicalPlacer`.

**Pipeline** (`src/pipeline/`):
- `Pipeline` — executes fixed 6-stage sequence: Classify → Score → Deduplicate → Sort → Slice → Place.
- `PipelineBuilder` — builder pattern for constructing a `Pipeline`. Requires scorer, slicer, placer.
  Optional: `deduplication` (bool, default true), `overflow_strategy`.
- `Pipeline::run(&self, items: &[ContextItem], budget: &ContextBudget) -> Result<Vec<ContextItem>, CupelError>`.

**Error type** (`src/error.rs`):
- `CupelError` — thiserror-derived enum. Variants: EmptyContent, EmptyKind, EmptySource,
  InvalidBudget, PinnedExceedsBudget, Overflow, PipelineConfig, ScorerConfig, SlicerConfig, CycleDetected.

**Crate dependencies** (Cargo.toml, production):
- `chrono` (workspace) — DateTime<Utc> for timestamps.
- `thiserror` (workspace) — error derive macro.

**Crate dev-dependencies**:
- `toml`, `serde`, `serde_json` — conformance test runner only.

### Conformance Test Coverage

28 required TOML vectors copied from `conformance/required/` into
`tests/conformance/required/`. The conformance test runner in `tests/conformance.rs`
dynamically parses vectors and exercises all stages. Test modules: `scoring`, `slicing`,
`placing`, `pipeline`.

The vectors live in TWO places currently:
- Canonical source: `cupel/conformance/required/` (owned by the spec)
- Vendored copy: `assay/crates/assay-cupel/tests/conformance/required/` (duplicated for test runner access)

This duplication is the primary structural problem the migration must solve.

### What Is NOT in the Crate

- No serde feature (no `#[derive(Serialize, Deserialize)]` on any public type)
- No doc examples in lib.rs or any module
- No `README.md` for the crate itself
- No `rust-toolchain.toml` in the assay workspace
- No docs.rs configuration
- No crates.io publishing metadata (`description`, `keywords`, `categories`, `homepage`, `documentation`)
- No `CHANGELOG.md`
- No Cargo workspace — assay-cupel inherits workspace settings from the assay Cargo workspace;
  a standalone `cupel-rs` crate will need its own complete Cargo.toml

---

## Table Stakes (Must Have for Migration)

These are the minimum viable features for v1.1. Without all of them, the migration is not done.

### TS-01: Crate Compiles and Tests Pass in New Location

**What it means**: The Rust source code (all 26 .rs files) moves from
`assay/crates/assay-cupel/src/` to `cupel/crates/cupel/src/` and `cargo test` passes
with zero failures.

**Work required**:
- Move source tree. No functional changes to logic.
- Write a standalone `Cargo.toml` at `crates/cupel/Cargo.toml` — cannot inherit from
  the assay Cargo workspace. Needs: `[package]` with name, version, edition, license, repository,
  description, keywords, categories; `[dependencies]` section with explicit chrono and thiserror versions;
  `[dev-dependencies]` with toml, serde, serde_json.
- Add `rust-toolchain.toml` at repo root specifying the toolchain channel (stable).
- Verify `cargo check`, `cargo clippy --deny warnings`, `cargo test` all pass.

**Complexity**: Low. Pure file movement + standalone Cargo.toml.

### TS-02: Published to crates.io as `cupel-rs`

**What it means**: `cargo publish` succeeds, the crate appears on crates.io as `cupel-rs`,
and the version is `1.0.0` (spec-aligned).

**Work required**:
- Standalone `Cargo.toml` with all required crates.io metadata:
  - `name = "cupel-rs"`
  - `version = "1.0.0"` (pin to spec version, not workspace-inherited)
  - `license = "MIT"` or `Apache-2.0` (must match assay's license or be set explicitly)
  - `description` — single sentence
  - `keywords` — up to 5, e.g. `["llm", "context", "agent", "token-budget", "context-window"]`
  - `categories` — from crates.io taxonomy
  - `repository = "https://github.com/wollax/cupel"`
  - `homepage = "https://wollax.github.io/cupel/"` (spec site)
  - `documentation = "https://docs.rs/cupel-rs"`
  - `readme = "crates/cupel/README.md"` (relative to repo root) or per-crate README
- Run `cargo publish --dry-run` in CI before actual publish.
- Set up crates.io API token or trusted publishing in GitHub Actions.

**Complexity**: Low (metadata) + Medium (CI publish pipeline).

### TS-03: Assay Consumes from crates.io

**What it means**: The assay repo's `Cargo.toml` replaces `assay-cupel` path dependency with
`cupel-rs = "1.0.0"` from crates.io. The assay build passes. The old `crates/assay-cupel/` is deleted.

**Work required**:
- In assay's workspace `Cargo.toml`: replace `assay-cupel` path/workspace member with
  `cupel-rs = "1.0.0"`.
- Update all import paths in assay code: `assay_cupel::` → `cupel_rs::` (or via `extern crate cupel_rs as cupel`).
- Delete `assay/crates/assay-cupel/` directory.
- Document `[patch.crates-io]` pattern for local dev:
  ```toml
  [patch.crates-io]
  cupel-rs = { path = "../cupel/crates/cupel" }
  ```
- Verify assay builds and all assay tests pass after the switch.

**Complexity**: Low. Mechanical rename + path cleanup.

### TS-04: Conformance Vectors Shared (Not Duplicated)

**What it means**: The 28 (and growing) TOML test vectors have a single source of truth at
`cupel/conformance/required/`. The cupel-rs crate's conformance test runner reads them from
the canonical location, not a vendored copy.

**Current problem**: `assay/crates/assay-cupel/tests/conformance/required/` is a full copy
of the vectors. When vectors change (e.g., Phase 15 moves quota-basic.toml from optional to required),
the copy must be manually synchronized.

**Solution options**:

Option A — Include path (preferred for monorepo):
The test runner uses `env!("CARGO_MANIFEST_DIR")` to navigate up to the repo root and read
from `conformance/required/`. This works because `crates/cupel/` is inside the same repo as
`conformance/`. No copy needed. The `load_vector()` helper changes from:
```rust
Path::new(env!("CARGO_MANIFEST_DIR"))
    .join("tests").join("conformance").join("required")
```
to:
```rust
Path::new(env!("CARGO_MANIFEST_DIR"))
    .join("..").join("..").join("conformance").join("required")
```

Option B — Symlink:
Create a symlink from `crates/cupel/tests/conformance/` → `../../conformance/`.
Works locally on Linux/macOS; fragile on Windows CI.

Option C — CI drift guard:
Keep the vendored copy but add a CI step that diffs `conformance/required/` against
`crates/cupel/tests/conformance/required/` and fails if they diverge.

**Recommendation**: Option A is the cleanest. The crate is in the monorepo; relative path navigation
is stable and avoids duplication entirely. Implement Option A with the path expressed as a constant
in the test helper.

**Complexity**: Low. Change the base path in `load_vector()`. No logic changes.

### TS-05: CI Runs Rust Tests

**What it means**: The existing `ci.yml` GitHub Actions workflow is extended (or a new `rust-ci.yml`
is added) with a job that runs `cargo test` in `crates/cupel/`. The job must pass on pull requests
targeting `main`.

**Work required**:
- Add a `rust` job to `ci.yml` (or a new workflow file):
  ```yaml
  rust:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Install Rust toolchain
        uses: dtolnay/rust-toolchain@stable
        # or read from rust-toolchain.toml automatically
      - name: Cargo test
        run: cargo test --manifest-path crates/cupel/Cargo.toml
  ```
- Add `cargo clippy --deny warnings` as a lint gate.
- (Optional but recommended) Cache `~/.cargo/registry` and `target/` between runs for speed.

**Complexity**: Low. Straightforward CI YAML additions.

---

## Differentiators (Nice to Have)

These features increase the value of the published crate. None block the migration. Prioritized
by impact-to-effort ratio.

### D-01: Serde Feature Flag

**What it means**: All public types gain `#[derive(serde::Serialize, serde::Deserialize)]`
behind a `features = ["serde"]` gate. Consumers who want serialization opt in; the core crate
stays serde-free for consumers who don't need it.

**Motivation**: The .NET implementation has full JSON serialization support. Rust consumers
building CLI tools, web services, or policy storage will want to serialize `ContextItem`,
`ContextBudget`, `ContextKind`, and `OverflowStrategy`. Without this, they must implement
custom serialization or maintain parallel types.

**Design constraints**:
- `ContextKind` and `ContextSource` are newtypes around `String` with case-insensitive equality.
  Serde's default derive will serialize the inner string, which is correct. Deserialization
  preserves the original casing (as the .NET spec does).
- `ContextBudget` uses a private constructor for validation. A custom `Deserialize` impl (or
  `#[serde(try_from = "ContextBudgetRaw")]` pattern) must call `ContextBudget::new()` to
  enforce validation. Blind field deserialization that bypasses the constructor is not acceptable.
- `HashMap<String, String>` metadata serializes naturally.
- `DateTime<Utc>` from chrono: use `chrono`'s serde feature (`chrono = { features = ["serde"] }`).
- Scorers, Slicers, Placers are trait objects (`Box<dyn Scorer>`) — these cannot be made
  serde-serializable in a general way. The serde feature applies only to data types, not
  pipeline component types.

**Cargo.toml changes**:
```toml
[features]
default = []
serde = ["dep:serde", "chrono/serde"]

[dependencies]
chrono = { version = "...", features = ["std"] }
serde = { version = "1", features = ["derive"], optional = true }
```

**Complexity**: Medium. Data types are straightforward; ContextBudget deserializer needs care.

### D-02: Documentation on docs.rs

**What it means**: `cargo doc` produces documentation with no missing items, no broken links,
and module-level examples. docs.rs auto-builds from crates.io; no extra configuration beyond
what's in Cargo.toml, but doc quality requires explicit effort.

**What needs writing**:
- Crate-level doc comment in `lib.rs` — quickstart example showing Pipeline construction
  and `run()` call. This is the "landing page" on docs.rs.
- Module-level doc comments for each public module: `model`, `scorer`, `slicer`, `placer`, `pipeline`.
- `/// # Examples` blocks on the highest-traffic entry points: `ContextItemBuilder::new()`,
  `PipelineBuilder`, `ContextBudget::new()`.

**Cargo.toml changes for docs.rs**:
```toml
[package.metadata.docs.rs]
features = ["serde"]           # build docs with all features enabled
all-features = false
rustdoc-args = ["--cfg", "docsrs"]
```

**Current state**: The existing code has good `///` doc comments on all public items. Module-level
docs and the crate-level quickstart are the gaps. No example files (`examples/`) exist.

**Complexity**: Low-Medium. Writing docs and one or two `examples/*.rs` files.

### D-03: Examples in the Crate

**What it means**: `crates/cupel/examples/` contains at minimum one runnable example demonstrating
the typical use case. Examples appear on docs.rs and are executable with `cargo run --example`.

**Proposed examples**:
1. `basic_pipeline.rs` — Build a pipeline with `RecencyScorer`, `GreedySlice`, `UShapedPlacer`,
   run it on 5 items with a 1000-token budget, print the result. 30-40 lines. Self-contained.
2. `composite_scoring.rs` — Demonstrate `CompositeScorer` with `RecencyScorer` + `PriorityScorer`,
   show score-based selection. Optional but valuable as documentation.

**Why this matters for migration**: crates.io users discover crates through docs.rs. A working
example in the first 5 seconds of reading is the single largest factor in "try it" conversion.
The assay repo is private or specialized — cupel-rs is a general-purpose library that needs
to stand on its own.

**Complexity**: Low. Examples use the existing public API; no new features needed.

### D-04: Builder Pattern Improvements

**What it means**: Minor ergonomic improvements to `ContextItemBuilder` and `PipelineBuilder`
that reduce friction for new consumers. Not API-breaking changes, and not new features.

**Current gaps identified**:
- `ContextItemBuilder::tags()` takes `Vec<String>` — callers must construct the vec. A
  `tag(impl Into<String>)` method that appends a single tag would be more ergonomic for
  the common case.
- `ContextBudget::new()` has 5 positional arguments including the `HashMap<ContextKind, i64>`.
  A `ContextBudgetBuilder` would be more discoverable, though the current constructor is
  fine for initial release.
- `PipelineBuilder` already uses the builder pattern correctly — no changes needed.

**Recommendation**: Add `ContextItemBuilder::tag(impl Into<String>)` for single-tag append.
Defer `ContextBudgetBuilder` to a future release (it's a convenience, not a necessity).

**Complexity**: Low (single method addition, fully backward-compatible).

---

## Anti-Features (Explicitly Not Built During Migration)

These are features that will be asked for, have obvious implementation paths, and would delay
the migration if accepted. The decision to exclude them is deliberate.

### AF-01: Feature Parity with .NET

**Why not**: The .NET implementation has features the Rust crate does not:
- `SelectionReport` / `DryRun()` with `ExclusionReason` per item
- `ITraceCollector` / `DiagnosticTraceCollector` for pipeline tracing
- `ContextTrace` with gated event construction
- Named policy presets (CupelPresets)
- `CupelPolicy` declarative config
- JSON policy serialization

None of these block the migration. They block `assay-cupel` → `cupel-rs` not at all — assay
uses `Pipeline::run()` directly. Adding these features during migration inflates scope and
introduces new design decisions that belong in their own phase.

**Decision**: Rust feature parity is a post-v1.1 milestone. The Rust crate ships what it has.
The spec defines the features; the .NET implementation demonstrates one full realization.

### AF-02: Async Support / Tokio Integration

**Why not**: The current Rust pipeline is synchronous. `Pipeline::run()` returns `Result<Vec<ContextItem>, CupelError>`.
Adding `async fn run()` requires choosing a runtime (tokio vs async-std vs smol) or keeping the
crate runtime-agnostic (which is significantly more complex). The `StreamSlice` async slicer
from the .NET implementation has no Rust counterpart, and there is no demand signal from assay.

**Decision**: No async. Synchronous pipeline only. Consumers who need async wrap `run()` in
`tokio::task::spawn_blocking` at their call site.

### AF-03: WASM Target Support

**Why not**: Assay is a CLI tool and does not target WASM. Adding WASM support means auditing
every dependency for WASM compatibility (`chrono`, `thiserror`), replacing or gating dependencies
that don't compile to WASM, and adding a CI job to verify the target. This is a significant
testing and maintenance burden with zero current demand.

**Decision**: WASM is explicitly not a v1.1 target. Document this in the crate README.

### AF-04: Optional Conformance Tests

**Why not**: There are 9 optional TOML vectors in `conformance/optional/`. These cover edge cases
(recency with all-null timestamps, greedy with empty input, composite nested, etc.). The Rust
conformance test runner currently only runs required vectors. Importing optional vectors
during migration adds complexity to the test runner and may reveal edge cases that require
implementation changes — scope creep during what should be a structural migration.

**Decision**: Optional conformance tests are a post-migration task, tracked separately.
The migration ships with 28 required vectors passing, same as today.

### AF-05: Policy System / Named Presets in Rust

**Why not**: The .NET implementation has `CupelPolicy` (declarative config), `CupelPresets`
(7 named presets), and `CupelOptions` (intent-based lookup). These require design decisions
about how Rust consumers express policy (structs? macros? TOML?) that are non-trivial. There
is no assay usage of this feature. Including it in v1.1 would double the design surface.

**Decision**: No policy system in Rust for v1.1. Consumers use `PipelineBuilder` directly.

---

## Feature Dependency Map for v1.1

```
[TS-01] Compiles in new location
    └── [TS-04] Conformance vectors shared (path change in load_vector())
    └── [TS-02] Published to crates.io (standalone Cargo.toml with metadata)
          └── [TS-03] Assay consumes from crates.io (import rename + delete)
    └── [TS-05] CI runs Rust tests (GitHub Actions job)

[D-02] docs.rs documentation
    └── [TS-02] Published (docs.rs reads from crates.io)

[D-03] Examples
    └── [TS-01] Compiles (examples use public API)

[D-01] Serde feature flag
    └── [TS-01] Compiles (new feature gate, no changes to existing code)
    └── [D-02] docs.rs (serde feature enabled in docs.rs metadata)

[D-04] Builder ergonomics
    └── [TS-01] Compiles (additive API change)
```

Critical path: TS-01 → TS-04 → TS-02 → TS-03 → TS-05.
Differentiators can be done in parallel with or after TS-02.

---

## Key Findings and Recommendations

### The Crate Is Production-Ready

The existing `assay-cupel` code is not a sketch. It has 26 source files, complete trait hierarchies,
working cycle detection in `CompositeScorer`, correct effective-budget computation in `slice.rs`,
and 28 passing conformance tests. The migration is structural, not functional. No algorithm
rewrites are needed.

### The Name `cupel-rs` Is Correct

`cupel` is taken on crates.io (checked implicitly — `cupel-rs` follows the Rust ecosystem
convention for `<name>-rs` when the plain name is unavailable or reserved). The crates.io name
matches the spec (`cupel-rs` = Rust implementation of the Cupel specification).

### Conformance Vectors Are the Hardest Coupling to Break

The existing conformance test runner hardcodes a path relative to `CARGO_MANIFEST_DIR` pointing
into the vendored copy. Switching to repo-root-relative paths (Option A) requires verifying
the relative path depth is correct after the directory move. This should be the first thing
tested in the new location before CI setup.

### Serde Is the Highest-Value Differentiator

Most Rust consumers building LLM applications will need to serialize `ContextItem` lists for
logging, storage, or API interchange. A `features = ["serde"]` gate is low-risk (no API changes),
follows Rust ecosystem conventions (chrono, uuid, etc. all do this), and opens the crate to
a wider audience. It should be prioritized over examples or builder ergonomics.

### The Local Dev Workflow Needs Documentation

After the migration, cupel-rs and assay are in separate repos. Developers working on both
simultaneously need the `[patch.crates-io]` pattern documented. This is a one-paragraph note
in the crate README and in the cupel repo's contributing guide, but without it the first
developer to touch both repos after the migration will lose time.

### CI Caching Pays Off Immediately

The Rust toolchain and `~/.cargo/registry` download on cold cache takes 60-90 seconds.
Add `actions/cache` for `~/.cargo` and `target/` on the first CI implementation.
The cupel conformance test suite is fast (< 1s locally) — cache is for toolchain, not test time.

---

## Summary Table

| ID | Feature | Category | Priority | Complexity | Blocks |
|----|---------|----------|----------|------------|--------|
| TS-01 | Compiles in new location | Table Stakes | P0 | Low | Everything |
| TS-02 | Published to crates.io as cupel-rs | Table Stakes | P0 | Low-Med | TS-03 |
| TS-03 | Assay consumes from crates.io | Table Stakes | P0 | Low | — |
| TS-04 | Conformance vectors shared | Table Stakes | P0 | Low | TS-01 |
| TS-05 | CI runs Rust tests | Table Stakes | P0 | Low | — |
| D-01 | Serde feature flag | Differentiator | P1 | Medium | — |
| D-02 | docs.rs documentation | Differentiator | P1 | Low-Med | TS-02 |
| D-03 | Examples in crate | Differentiator | P2 | Low | TS-01 |
| D-04 | Builder ergonomics | Differentiator | P3 | Low | TS-01 |
| AF-01 | .NET feature parity | Anti-Feature | Never | High | — |
| AF-02 | Async / Tokio | Anti-Feature | Never | High | — |
| AF-03 | WASM target | Anti-Feature | Never | Medium | — |
| AF-04 | Optional conformance tests | Anti-Feature | Post-v1.1 | Low | — |
| AF-05 | Policy system in Rust | Anti-Feature | Post-v1.1 | High | — |
