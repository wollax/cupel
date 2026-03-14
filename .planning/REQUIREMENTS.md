# Cupel Requirements

## v1.0 Core Library

### Pipeline Engine

- [x] **PIPE-01**: ContextItem model with Content (non-nullable string), Kind, Tokens, Timestamp, Source, Tags, Priority, Pinned, OriginalTokens, FutureRelevanceHint, and extensible Metadata
- [x] **PIPE-02**: ContextBudget model with MaxTokens, TargetTokens, ReservedSlots, OutputReserve, EstimationSafetyMarginPercent
- [x] **PIPE-03**: Fixed pipeline executing Classify → Score → Deduplicate → Slice → Place stages in order
- [x] **PIPE-04**: Ordinal-only scoring invariant enforced: scorers rank, slicers drop, placers position — no component crosses this boundary
- [x] **PIPE-05**: Pinned items bypass scoring and enter pipeline at Placer stage

### Scorers

- [x] **SCORE-01**: IScorer interface with output conventionally 0.0–1.0 (documented, not enforced by type)
- [x] **SCORE-02**: RecencyScorer — scores items by temporal proximity
- [x] **SCORE-03**: PriorityScorer — scores items by explicit priority value
- [x] **SCORE-04**: KindScorer — scores items by content kind (message, document, tool output, etc.)
- [x] **SCORE-05**: TagScorer — scores items by tag-based categorical boosting
- [x] **SCORE-06**: FrequencyScorer — scores items by reference frequency as relevance signal
- [x] **SCORE-07**: ReflexiveScorer — scores items using caller-supplied FutureRelevanceHint
- [x] **SCORE-08**: CompositeScorer with configurable aggregation (WeightedAverage, nested composites)
- [x] **SCORE-09**: ScaledScorer wrapper that normalizes arbitrary scorer output to 0–1 range

### Slicers

- [x] **SLICE-01**: ISlicer interface for budget-constrained item selection
- [x] **SLICE-02**: GreedySlice — O(N log N) greedy fill by score/token ratio
- [x] **SLICE-03**: KnapsackSlice — 0-1 DP knapsack with token discretization for optimal budget utilization
- [x] **SLICE-04**: QuotaSlice — percentage-based semantic quotas with Require(Kind, minPercent) / Cap(Kind, maxPercent)
- [x] **SLICE-05**: StreamSlice — online/streaming selection for IAsyncEnumerable sources
- [x] **SLICE-06**: Pinned item + quota interaction is specified behavior with clear errors on conflict

### Placement

- [x] **PLACE-01**: IPlacer interface (pluggable, not hardcoded)
- [x] **PLACE-02**: UShapedPlacer as default implementation (primacy + recency attention curve)
- [x] **PLACE-03**: ChronologicalPlacer as alternative implementation (timestamp ordering)

### Explainability

- [x] **TRACE-01**: ContextResult return type from day 1 containing Items and optional ContextTrace
- [x] **TRACE-02**: ITraceCollector with NullTraceCollector (no-op default) and DiagnosticTraceCollector
- [x] **TRACE-03**: Trace event construction gated (IsEnabled check before allocation)
- [x] **TRACE-04**: Explicit trace propagation (no AsyncLocal)
- [x] **TRACE-05**: SelectionReport / DryRun() returning included items with scores, excluded items with ExclusionReason enum
- [x] **TRACE-06**: OverflowStrategy enum (Throw | Truncate | Proceed) with optional observer callback

### API Surface

- [x] **API-01**: CupelPolicy as declarative, serializable config tying pipeline together
- [x] **API-02**: Fluent builder via CupelPipeline.CreateBuilder() over fixed pipeline (no call-next middleware)
- [x] **API-03**: Both explicit policy and intent-based lookup via CupelOptions.AddPolicy("intent", policy)
- [x] **API-04**: IContextSource interface (IAsyncEnumerable<ContextItem>) in core
- [x] **API-05**: Token counting is caller's responsibility — ContextItem.Tokens is required non-nullable int

### Named Policies

- [x] **POLICY-01**: 7+ built-in policies: chat, code-review, rag, document-qa, tool-use, long-running, debugging
- [x] **POLICY-02**: [Experimental] attribute on preset methods
- [x] **POLICY-03**: Policy presets serve as living documentation and test fixtures

### Serialization

- [x] **JSON-01**: [JsonPropertyName] on all public types from day 1
- [x] **JSON-02**: Incremental serialization: ContextBudget + SlicerConfig first, scorer config after CompositeScorer stabilizes
- [x] **JSON-03**: RegisterScorer(string name, Func<IScorer> factory) hook for future serialization extensibility
- [x] **JSON-04**: JSON only (no YAML — minimal dependencies)

### Packaging

- [x] **PKG-01**: Wollax.Cupel — core library with zero external dependencies beyond BCL
- [x] **PKG-02**: Wollax.Cupel.Extensions.DependencyInjection — Microsoft.Extensions.DI integration (separate package)
- [x] **PKG-03**: Wollax.Cupel.Tiktoken — optional token counting companion using Microsoft.ML.Tokenizers
- [x] **PKG-04**: Wollax.Cupel.Json — JSON policy serialization companion with source-generated JsonSerializerContext
- [x] **PKG-05**: Published to nuget.org as public packages

---

## v1.1 Rust Crate Migration & crates.io Publishing

### Migration

- [ ] **MIGRATE-01**: Verify `cupel-rs` name availability on crates.io (day-one gate — if squatted, file name report or select fallback)
- [ ] **MIGRATE-02**: Decide and implement workspace layout for `crates/` directory (standalone or minimal workspace root `Cargo.toml`)
- [ ] **MIGRATE-03**: Write complete standalone `Cargo.toml` for `cupel-rs` with all required/recommended crates.io fields, chosen version strategy, and explicit `include` list — no workspace-inherited fields
- [ ] **MIGRATE-04**: Add `rust-toolchain.toml` at repo root pinning Rust 2024 edition, MSRV 1.85, with `rustfmt` and `clippy` components
- [ ] **MIGRATE-05**: Extend `.editorconfig` with Rust-specific rules; add `/crates/cupel/target/` to root `.gitignore`
- [ ] **MIGRATE-06**: Move all 26 `.rs` source files from `assay/crates/assay-cupel/src/` to `cupel/crates/cupel/src/` — compiles cleanly
- [ ] **MIGRATE-07**: Update conformance test runner `load_vector()` path to resolve from new `CARGO_MANIFEST_DIR`-relative location

### Conformance

- [ ] **CONFORM-01**: Conformance vectors at `conformance/required/` are the single source of truth; crate reads vectors via `CARGO_MANIFEST_DIR`-relative path with CI diff guard preventing divergence
- [ ] **CONFORM-02**: `cargo package --list` confirms all `.toml` conformance vectors appear in the published crate tarball
- [ ] **CONFORM-03**: Unpacked tarball verification: `tar xvf *.crate && cargo test` passes inside the unpacked directory

### CI/CD

- [ ] **CI-01**: `ci-rust.yml` workflow with path-filtered triggers (`crates/**`, `conformance/**`, `rust-toolchain.toml`, self-reference) running `cargo fmt --check`, `cargo clippy -- -D warnings`, `cargo test`, and `cargo-deny check`
- [ ] **CI-02**: `release-rust.yml` workflow with `workflow_dispatch` trigger, `dry-run` input, `release` GitHub environment, and OIDC trusted publishing via `rust-lang/crates-io-auth-action@v1`
- [ ] **CI-03**: First manual `cargo publish` with personal API token to bootstrap crate registration on crates.io (one-time prerequisite for OIDC)
- [ ] **CI-04**: Existing .NET CI workflow updated with path filters so Rust-only changes do not trigger .NET builds (and vice versa)
- [ ] **CI-05**: `cargo-deny` configuration (`deny.toml`) at crate level covering license and advisory checks

### Switchover

- [ ] **SWITCH-01**: `wollax/assay` replaces `assay-cupel` path dependency with `cupel-rs = "VERSION"` from crates.io registry
- [ ] **SWITCH-02**: All assay imports renamed from `assay_cupel::` to `cupel_rs::` (or via `extern crate cupel_rs as cupel`)
- [ ] **SWITCH-03**: `assay/crates/assay-cupel/` directory deleted from assay repo after migration verified
- [ ] **SWITCH-04**: `[patch.crates-io]` local development workflow pattern documented in assay contributing guide

### Enhancements

- [ ] **ENHANCE-01**: `features = ["serde"]` in `Cargo.toml` gates `Serialize`/`Deserialize` derives on all public data types
- [ ] **ENHANCE-02**: `ContextBudget` uses custom serde deserializer that validates inputs through the constructor — no bypass of validation invariants
- [ ] **ENHANCE-03**: Crate re-published to crates.io as minor version bump with serde feature available
- [ ] **ENHANCE-04**: Crate-level documentation in `lib.rs` with quickstart example doctest; every public module has `//!` doc comments; `[package.metadata.docs.rs]` configured with `all-features = true`
- [ ] **ENHANCE-05**: `examples/basic_pipeline.rs` exists and runs with `cargo run --example basic_pipeline`

---

## Future Requirements

(Deferred beyond v1.0)

- Embedding-based semantic similarity scorer (requires external dependency)
- LLM-based reranking scorer (non-deterministic, latency-destroying)
- Hot reload / PolicyWatcher (complex threading, Phase 3+)
- Cross-language SDK / CLI (document algorithm as spec first)
- AdaptiveScorer / ML-based scoring (gradient-boosted on small N is worse than tuned heuristics)

## Out of Scope

- **Storage / persistence** — Cupel is stateless. No conversation history, vector stores, or caches.
- **Retrieval / RAG** — Cupel scores what you give it. No external document fetching.
- **Tokenizer in core** — Caller pre-counts tokens. Optional companion package for convenience.
- **LLM API integration** — Cupel prepares context, does not call models.
- **Compression / summarization** — Cupel scores pre-compressed items. Compression is caller's responsibility.
- **IContextSink** — Cupel selects; consumers convert to their format.
- **YAML serialization** — Contradicts minimal-dependencies constraint.
- **Scorer DAG engine** — CompositeScorer with nesting achieves the same result.
- **LLM-specific adapters** — No Cupel.Adapters.Anthropic/OpenAI packages.

## Traceability

### v1.0 Core Library

| Requirement | Phase | Status |
|---|---|---|
| PIPE-01 | Phase 1: Project Scaffold & Core Models | ● complete |
| PIPE-02 | Phase 1: Project Scaffold & Core Models | ● complete |
| PIPE-03 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| PIPE-04 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| PIPE-05 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| SCORE-01 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| SCORE-02 | Phase 3: Individual Scorers | ● complete |
| SCORE-03 | Phase 3: Individual Scorers | ● complete |
| SCORE-04 | Phase 3: Individual Scorers | ● complete |
| SCORE-05 | Phase 3: Individual Scorers | ● complete |
| SCORE-06 | Phase 3: Individual Scorers | ● complete |
| SCORE-07 | Phase 3: Individual Scorers | ● complete |
| SCORE-08 | Phase 4: Composite Scoring | ● complete |
| SCORE-09 | Phase 4: Composite Scoring | ● complete |
| SLICE-01 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| SLICE-02 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| SLICE-03 | Phase 6: Advanced Slicers & Quota System | ● complete |
| SLICE-04 | Phase 6: Advanced Slicers & Quota System | ● complete |
| SLICE-05 | Phase 6: Advanced Slicers & Quota System | ● complete |
| SLICE-06 | Phase 6: Advanced Slicers & Quota System | ● complete |
| PLACE-01 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| PLACE-02 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| PLACE-03 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| TRACE-01 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| TRACE-02 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| TRACE-03 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| TRACE-04 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| TRACE-05 | Phase 7: Explainability & Overflow Handling | ● complete |
| TRACE-06 | Phase 7: Explainability & Overflow Handling | ● complete |
| API-01 | Phase 8: Policy System & Named Presets | ● complete |
| API-02 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| API-03 | Phase 8: Policy System & Named Presets | ● complete |
| API-04 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| API-05 | Phase 1: Project Scaffold & Core Models | ● complete |
| POLICY-01 | Phase 8: Policy System & Named Presets | ● complete |
| POLICY-02 | Phase 8: Policy System & Named Presets | ● complete |
| POLICY-03 | Phase 8: Policy System & Named Presets | ● complete |
| JSON-01 | Phase 1: Project Scaffold & Core Models | ● complete |
| JSON-02 | Phase 9: Serialization & JSON Package | ● complete |
| JSON-03 | Phase 9: Serialization & JSON Package | ● complete |
| JSON-04 | Phase 9: Serialization & JSON Package | ● complete |
| PKG-01 | Phase 1: Project Scaffold & Core Models | ● complete |
| PKG-02 | Phase 10: Companion Packages & Release | ● complete |
| PKG-03 | Phase 10: Companion Packages & Release | ● complete |
| PKG-04 | Phase 9: Serialization & JSON Package | ● complete |
| PKG-05 | Phase 10: Companion Packages & Release | ● complete |

### v1.1 Rust Crate Migration & crates.io Publishing

| Requirement | Phase | Status |
|---|---|---|
| MIGRATE-01 | Phase 16: Pre-flight & Crate Scaffold | ○ planned |
| MIGRATE-02 | Phase 16: Pre-flight & Crate Scaffold | ○ planned |
| MIGRATE-03 | Phase 16: Pre-flight & Crate Scaffold | ○ planned |
| MIGRATE-04 | Phase 16: Pre-flight & Crate Scaffold | ○ planned |
| MIGRATE-05 | Phase 16: Pre-flight & Crate Scaffold | ○ planned |
| MIGRATE-06 | Phase 17: Crate Migration & Conformance Verification | ○ planned |
| MIGRATE-07 | Phase 17: Crate Migration & Conformance Verification | ○ planned |
| CONFORM-01 | Phase 17: Crate Migration & Conformance Verification | ○ planned |
| CONFORM-02 | Phase 17: Crate Migration & Conformance Verification | ○ planned |
| CONFORM-03 | Phase 17: Crate Migration & Conformance Verification | ○ planned |
| CI-01 | Phase 18: Dual-Language CI | ○ planned |
| CI-02 | Phase 18: Dual-Language CI | ○ planned |
| CI-03 | Phase 19: First Publish & Assay Switchover | ○ planned |
| CI-04 | Phase 18: Dual-Language CI | ○ planned |
| CI-05 | Phase 18: Dual-Language CI | ○ planned |
| SWITCH-01 | Phase 19: First Publish & Assay Switchover | ○ planned |
| SWITCH-02 | Phase 19: First Publish & Assay Switchover | ○ planned |
| SWITCH-03 | Phase 19: First Publish & Assay Switchover | ○ planned |
| SWITCH-04 | Phase 19: First Publish & Assay Switchover | ○ planned |
| ENHANCE-01 | Phase 20: Serde Feature Flag | ○ planned |
| ENHANCE-02 | Phase 20: Serde Feature Flag | ○ planned |
| ENHANCE-03 | Phase 20: Serde Feature Flag | ○ planned |
| ENHANCE-04 | Phase 21: docs.rs Documentation & Examples | ○ planned |
| ENHANCE-05 | Phase 21: docs.rs Documentation & Examples | ○ planned |
