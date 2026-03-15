# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
