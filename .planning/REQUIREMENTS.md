# Cupel v1.2 Requirements: Rust Parity & Quality Hardening

## Milestone Requirements

### Rust API Hardening

- [ ] **RAPI-01**: `CupelError` and `OverflowStrategy` enums have `#[non_exhaustive]` attribute
- [ ] **RAPI-02**: Concrete slicer/placer structs (`GreedySlice`, `KnapsackSlice`, `UShapedPlacer`, `ChronologicalPlacer`) derive `Debug`, `Clone`, and `Copy`
- [ ] **RAPI-03**: `ContextKind` provides factory methods for all known kinds (`message()`, `system_prompt()`, `document()`, `tool_output()`, `memory()`)
- [ ] **RAPI-04**: `ContextKind` implements `TryFrom<&str>` for idiomatic error-propagation usage
- [ ] **RAPI-05**: `ContextBudget` exposes `unreserved_capacity()` computed property in both .NET and Rust (`MaxTokens - OutputReserve - sum(ReservedSlots)`)
- [ ] **RAPI-06**: `KnapsackSlice` validates DP table size before allocation and returns error if capacity × items exceeds 50M cells (both .NET and Rust)

### Rust Diagnostics — Specification

- [ ] **SPEC-01**: Language-agnostic diagnostics spec chapter exists in `/spec/` covering TraceCollector contract, event types, exclusion reasons, report structure, and ownership model
- [ ] **SPEC-02**: Diagnostics conformance vectors exist with `[expected.diagnostics]` schema for cross-language verification of exclusion reasons and counts

### Rust Diagnostics — Implementation

- [ ] **DIAG-01**: `TraceCollector` trait exists with `is_enabled()` gate and default no-op methods for event recording
- [ ] **DIAG-02**: `NullTraceCollector` is a zero-sized type that compiles to zero overhead when monomorphized
- [ ] **DIAG-03**: `DiagnosticTraceCollector` accumulates pipeline events and produces a `SelectionReport`
- [ ] **DIAG-04**: `TraceEvent` enum represents pipeline stage events (Classify, Score, Deduplicate, Sort, Slice, Place) with stage timing
- [ ] **DIAG-05**: `ExclusionReason` enum covers all exclusion cases (BudgetExceeded, Deduplicated, QuotaExceeded, etc.) with `#[non_exhaustive]`
- [ ] **DIAG-06**: `SelectionReport` struct carries per-item inclusion/exclusion reasons with scores
- [ ] **DIAG-07**: Pipeline exposes `run_traced()` or `run_with_trace()` method accepting a `TraceCollector` implementation
- [ ] **DIAG-08**: `DryRun` capability exists — run pipeline without committing, returning only the report
- [ ] **DIAG-09**: Diagnostic types (`SelectionReport`, `TraceEvent`, `ExclusionReason`) support serde serialization behind the `serde` feature flag

### Spec Conformance

- [ ] **CONF-01**: Incorrect/misleading comments in 5 conformance vector TOML files are fixed (both spec/ and vendored copies)
- [ ] **CONF-02**: CI drift guard exists that diffs `spec/conformance/` against `crates/cupel/tests/conformance/` and fails on divergence

### Quality Hardening — CI

- [ ] **CI-01**: Rust CI runs clippy with `--all-targets` flag
- [ ] **CI-02**: `cargo-deny` checks include unmaintained advisory warnings
- [ ] **CI-03**: Conformance vector drift guard runs in CI

### Quality Hardening — Codebase

- [ ] **QH-01**: Mechanical .NET issues resolved: XML doc comments, naming inconsistencies, defensive coding improvements
- [ ] **QH-02**: Mechanical Rust issues resolved: doc comments, naming consistency, test gaps
- [ ] **QH-03**: Test coverage gaps identified in open issues are addressed

## Future Requirements (Deferred)

- **DecayScorer** — built-in time-decay scorer with `TimeProvider` injection (v1.3)
- **Cupel.Testing package** — fluent assertion chains over SelectionReport (v1.3)
- **OpenTelemetry integration** — bridge ITraceCollector to ActivitySource (v1.3)
- **Budget simulation APIs** — `GetMarginalItems`, `FindMinBudgetFor` (v1.3)
- **Count-based quotas** — design phase needed before implementation (v1.3+)
- **Metadata convention system** — `cupel:trust` namespace and MetadataTrustScorer (v1.3)
- **ProfiledPlacer** — caller-provided attention profiles companion package (v1.3+)
- **Context fork diagnostic** — policy sensitivity analysis developer tool (v1.3+)

## Out of Scope

- **`tracing` crate integration** — belongs in a companion crate, not core (zero-dep constraint)
- **`Arc<dyn TraceCollector>`** — wrong ownership model for sync pipeline; per-call injection instead
- **Serializable pipeline config** — requires `typetag` or similar, contradicts zero-dep constraint
- **`IDecayCurve` public interface** — `IScorer` is the extensibility contract
- **`SweepBudget`** — belongs in Smelt, not Cupel
- **Inverse context synthesis** — Assay/Smelt concern, not context selection

## Traceability

| REQ-ID | Phase |
|--------|-------|
| *(filled by roadmapper)* | |
