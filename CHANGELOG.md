# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2026-03-29

### Added
- `Rust: \`Policy\` struct and \`PolicyBuilder\` — construct a policy from \`Arc<dyn Scorer/Slicer/Placer>\` with deduplication and overflow flags`
- `Rust: \`Pipeline::dry_run_with_policy\` — run a pipeline using a caller-supplied Policy instead of the pipeline's own components`
- `Rust: \`policy_sensitivity\` — fork-diagnostic free function accepting \`&[(label, &Policy)]\` variants, returning \`PolicySensitivityReport\``
- `Rust: \`policy_sensitivity_from_pipelines\` — pipeline-based variant (renamed from \`policy_sensitivity\` for disambiguation)`
- `Rust: \`cupel-otel\` crate — \`CupelOtelTraceCollector\` implementing \`TraceCollector\` with three verbosity tiers (StageOnly, StageAndExclusions, Full); emits \`cupel.pipeline\` root span and five \`cupel.stage.*\` child spans with exact \`cupel.*\` attributes`
- `Rust: \`TraceCollector::on_pipeline_completed\` hook — defaulted no-op method on the trait; called by \`Pipeline::run_traced\` at completion; provides \`StageTraceSnapshot\` slice for structured end-of-run handoff`
- `Rust: \`MetadataKeyScorer\` — absolute scorer returning a configurable multiplier (\`boost\`) when \`metadata[key] == value\`, and \`1.0\` (neutral) otherwise; multiplicative semantics for use in \`CompositeScorer\``
- `Rust: \`CountConstrainedKnapsackSlice\` — 3-phase slicer accepting \`Vec<CountQuotaEntry>\`, \`KnapsackSlice\`, and \`ScarcityBehavior\`; Phase 1 count-satisfy, Phase 2 knapsack-distribute, Phase 3 cap-enforce`
- `.NET: \`CupelPipeline.DryRunWithPolicy\`, \`PolicySensitivity\` overload, \`MetadataKeyScorer\`, \`CountConstrainedKnapsackSlice\``
- `Spec: \`policy-sensitivity.md\`, \`opentelemetry.md\` Rust section, \`count-constrained-knapsack.md\`, \`metadata-key.md\``

## [1.1.0] - 2026-03-15

### Added
- Optional `serde` feature flag with Serialize/Deserialize derives on all public types (ContextItem, ScoredItem, ContextBudget, QuotaEntry, ContextKind, ContextSource, OverflowStrategy)
- Custom ContextBudget deserializer that validates inputs through constructor — prevents bypass of invariants
- Crate-level documentation with quickstart examples wired to docs.rs
- Module-level doc comments with 33 compilable doctests across all public types
- Three standalone runnable examples: basic_pipeline, serde_roundtrip, quota_slicing
- docs.rs metadata with all-features = true and doc_auto_cfg for automatic feature badges

### Fixed
- CI now tests serde feature — closes gap where 40+ serde tests were silently skipped
- Explicit `--features serde` in CI/release workflows for Clippy and Test steps

### Changed
- CI workflows (ci-rust.yml, release-rust.yml) updated to run both default and serde feature test passes

## [1.0.0] - 2026-03-14

### Added
- Pipeline engine with fixed Classify → Score → Deduplicate → Slice → Place stages
- Full scorer suite: Recency, Priority, Kind, Tag, Frequency, Reflexive, Composite, Scaled
- Four slicer strategies: Greedy, Knapsack (0-1 DP), Quota (semantic quotas), Stream (IAsyncEnumerable)
- Pluggable placement with UShapedPlacer (primacy + recency) and ChronologicalPlacer
- Explainability: ContextResult, SelectionReport, DryRun, OverflowStrategy with observer callback
- Fluent builder API via CupelPipeline.CreateBuilder()
- Declarative CupelPolicy with 7 named presets (chat, code-review, rag, document-qa, tool-use, long-running, debugging)
- Intent-based policy lookup via CupelOptions.AddPolicy()
- JSON serialization package (Wollax.Cupel.Json) with source-generated JsonSerializerContext
- DI integration package (Wollax.Cupel.Extensions.DependencyInjection) with keyed services
- Tiktoken token counting companion (Wollax.Cupel.Tiktoken)
- Language-agnostic specification with 28 required conformance test vectors
- Rust crate implementation (assay-cupel) passing all conformance vectors
