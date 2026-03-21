---
id: M001
provides:
  - Rust diagnostics parity: TraceCollector trait + NullTraceCollector ZST + DiagnosticTraceCollector; run_traced() + dry_run() pipeline methods; full SelectionReport with per-item inclusion/exclusion reasons
  - All diagnostic types serde-complete under the serde feature flag (internally-tagged wire format)
  - 5 diagnostics conformance vectors in spec/conformance/ and crates/cupel/conformance/, all passing in CI
  - KnapsackSlice DP table size guard in both Rust (CupelError::TableTooLarge) and .NET (InvalidOperationException)
  - Slicer::slice returns Result<Vec<ContextItem>, CupelError> (semver-intentional for v1.2.0)
  - cargo clippy --all-targets -- -D warnings across all CI jobs (ci-rust.yml + release-rust.yml)
  - cargo-deny unmaintained advisory coverage in deny.toml
  - 20 .NET quality items resolved (naming, epsilon, error messages, enum anchors, XML docs, interface contracts, test gaps)
  - 15 new Rust unit tests + CompositeScorer DFS removed + Scorer::as_any eliminated + UShapedPlacer panic-free
  - release-rust.yml permissions scoped to job level
key_decisions:
  - D001–D038 (see DECISIONS.md) — full register for all architectural, API, and convention decisions
  - D001: TraceCollector per-invocation ownership (not stored on pipeline)
  - D002: run_traced coexists with run() — additive, zero breaking change for non-diagnostic callers
  - D003: NullTraceCollector ZST — monomorphization eliminates all diagnostic code paths; zero runtime cost
  - D027: Internally-tagged serde on ExclusionReason/InclusionReason (#[serde(tag = "reason")])
  - D035: Slicer::slice semver break accepted for v1.2.0 — required for Result propagation
patterns_established:
  - is_enabled() guard pattern — wrap entire diagnostic block (not just TraceEvent construction) in if collector.is_enabled() to preserve NullTraceCollector zero-cost invariant
  - RawSelectionReport Deserialize pattern — mirrors ContextBudget Raw pattern; deny_unknown_fields + total_candidates validation
  - Vec<(T, usize)> serde — serialize_with strips index; deserialize_with reconstructs sequential indices
  - Scorer unit tests use std::slice::from_ref(&item) for single-item all_items (clippy::cloned_ref_to_slice_refs)
  - OOM-bound guard: compute cell count with promotion arithmetic, return error before allocation
observability_surfaces:
  - CupelError::TableTooLarge { candidates, capacity, cells } — structured error; inspect via `cargo test -- knapsack_table_too_large`
  - CupelError::Display via thiserror — callers see full message with field values
  - pipeline.dry_run(&items, &budget) → SelectionReport — full explainability without side effects
  - cargo test --test conformance -- pipeline::diag --nocapture — 5 diagnostics tests with field-level assertions
  - diff -rq spec/conformance/ crates/cupel/conformance/ — canonical drift detection; exit 0 required
requirement_outcomes:
  - id: R001
    from_status: active
    to_status: validated
    proof: "TraceCollector trait + NullTraceCollector + DiagnosticTraceCollector in crate root; Pipeline::run_traced() and Pipeline::dry_run() implemented in pipeline/mod.rs; all 5 diagnostics conformance vectors pass (cargo test --test conformance -- pipeline::diag → 5/5 ok); cargo test --features serde → 35 passed including serde round-trips for SelectionReport"
  - id: R002
    from_status: active
    to_status: validated
    proof: "Rust: KnapsackSlice::slice returns Err(CupelError::TableTooLarge) when (capacity as u64)*(n as u64) > 50_000_000; knapsack_table_too_large unit test passes. .NET: InvalidOperationException thrown when (long)candidateCount*(capacity+1) > 50_000_000L; 4 boundary tests pass in KnapsackSliceTests.cs. Both guards verified in CI."
  - id: R003
    from_status: active
    to_status: validated
    proof: "All 4 cargo clippy invocations in ci-rust.yml and release-rust.yml updated with --all-targets; deny.toml has unmaintained = workspace; local cargo clippy --all-targets and cargo deny check exit 0"
  - id: R004
    from_status: active
    to_status: validated
    proof: "20 .NET triage items resolved: OverflowStrategyValue → OverflowStrategy, QuotaBuilder epsilon fix, caller-facing error messages, enum integer anchors, ContextItem XML docs, interface contract docs; 6 new tests (net +5); dotnet test → 663 total tests, 0 failures"
  - id: R005
    from_status: active
    to_status: validated
    proof: "CompositeScorer DFS cycle detection removed; Scorer::as_any eliminated from trait and all 8 impls; UShapedPlacer refactored to explicit left/right vecs (no Vec<Option> or .expect()); 15 new unit tests; cargo clippy --all-targets -- -D warnings exits 0"
  - id: R006
    from_status: active
    to_status: validated
    proof: "cargo test --features serde → 35 passed including wire-format assertions for all ExclusionReason/InclusionReason variants, SelectionReport round-trip, validation-rejection test, graceful unknown-variant test; serde_roundtrip example Part 4 produces spec-compliant JSON"
duration: ~7 days across 7 slices (S01: 2026-03-17, S02: 2026-03-17, S03–S07: 2026-03-21)
verification_result: passed
completed_at: 2026-03-21
---

# M001: v1.2 Rust Parity & Quality Hardening

**Closed the diagnostics gap between Rust and .NET with `run_traced`/`dry_run`/`SelectionReport`, hardened both codebases against OOM and panic paths, and shipped clean CI baselines — 6 requirements validated across 7 slices.**

## What Happened

M001 executed as 7 slices in three logical phases.

**Phase 1 — Diagnostics foundation (S01–S04):** S01 built all 8 diagnostic data types (`TraceEvent`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, and supporting structs) with serde stubs, then authored 4 new conformance vectors in `spec/conformance/`. S02 layered the `TraceCollector` trait over those types — `NullTraceCollector` ZST (zero runtime cost via monomorphization) and `DiagnosticTraceCollector` (buffered recording, `into_report`, optional callback). S03 wired both into `Pipeline::run_traced<C: TraceCollector>` and `Pipeline::dry_run` — the full 5-stage trace instrumentation with timing, per-item inclusion/exclusion reasons, and stage-level events. The diagnostics conformance harness (`run_pipeline_diagnostics_test`) was added and all 5 vectors verified. S04 completed the serde story: `#[serde(tag = "reason")]` internally-tagged format on both reason enums, a `_Unknown` forward-compat variant, and a custom `Deserialize` for `SelectionReport` via the `RawSelectionReport` pattern. All 6 requirements for this phase proved out cleanly.

**Phase 2 — CI hardening (S05):** Three targeted config changes — `--all-targets` on all 4 clippy invocations across both CI workflow files, `unmaintained = "workspace"` in `deny.toml` — established the clean lint baseline that S07 would depend on. Zero pre-existing warnings surfaced. R003 validated.

**Phase 3 — Quality hardening (S06–S07):** S06 addressed 20 .NET triage items: the KnapsackSlice OOM guard (`(long)candidateCount * (capacity+1) > 50M`), QuotaBuilder epsilon fix, `OverflowStrategyValue` → `OverflowStrategy` rename, caller-facing error messages without internal type names, enum integer anchors, comprehensive `ContextItem` XML docs, interface contract docs for `ITraceCollector`/`ISlicer`/`ContextResult`, and 6 new tests. S07 mirrored the Rust side: `CupelError::TableTooLarge` + `KnapsackSlice` guard with flat `Vec<bool>` keep table, `Slicer::slice → Result` propagation through `QuotaSlice` and pipeline, removal of dead `CompositeScorer` DFS cycle detection, elimination of `Scorer::as_any` from all 8 impls, panic-free `UShapedPlacer` with explicit `left`/`right` vecs, and 15 new unit tests.

## Cross-Slice Verification

**Success criterion 1 — `pipeline.run_traced(&mut collector)` returns `SelectionReport` with per-item reasons:**
```
cargo test --manifest-path crates/cupel/Cargo.toml --test conformance -- pipeline::diag
# 5 passed (diag_negative_tokens, diag_deduplicated, diag_pinned_override, diag_scored_inclusion, diagnostics_budget_exceeded)
```
Each test asserts `summary.total_candidates`, `summary.total_tokens_considered`, per-item `score`, `reason` variant tag, and variant-specific data fields (e.g. `NegativeTokens.tokens`, `Deduplicated.deduplicated_against`, `BudgetExceeded.item_tokens/available_tokens`). ✅

**Success criterion 2 — `pipeline.dry_run(items)` works without side effects:**
```
grep "pub fn dry_run" crates/cupel/src/pipeline/mod.rs
# → pub fn dry_run(&self, items: Vec<ContextItem>, budget: &ContextBudget) -> Result<SelectionReport, CupelError>
cargo run --example serde_roundtrip --features serde --manifest-path crates/cupel/Cargo.toml
# Part 4 output: dry_run → SelectionReport → pretty JSON with correct wire format
```
✅

**Success criterion 3 — All new diagnostic types serialize/deserialize correctly under `serde` feature:**
```
cargo test --features serde --manifest-path crates/cupel/Cargo.toml
# 35 passed; 0 failed (includes all serde round-trip tests from S04)
```
Wire-format spot checks: `ExclusionReason::BudgetExceeded` → `{"reason":"BudgetExceeded","item_tokens":...,"available_tokens":...}` (internally-tagged, not externally-tagged). ✅

**Success criterion 4 — New diagnostics conformance vectors pass in CI:**
```
diff -rq spec/conformance/ crates/cupel/conformance/
# (no output — zero drift)
ls spec/conformance/required/pipeline/diag*.toml | wc -l
# 5 vectors: diag-negative-tokens, diag-deduplicated, diag-pinned-override, diag-scored-inclusion, diagnostics-budget-exceeded
```
✅

**Success criterion 5 — `cargo clippy --all-targets -- -D warnings` passes with zero warnings:**
```
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
# Finished with 0 warnings, exit 0
cargo clippy --features serde --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
# Finished with 0 warnings, exit 0
```
✅

**Success criterion 6 — `KnapsackSlice` returns error (not OOM) when capacity × items > 50M in both languages:**
```
# Rust
cargo test --manifest-path crates/cupel/Cargo.toml --lib -- knapsack_table_too_large --nocapture
# test slicer::knapsack::tests::knapsack_table_too_large ... ok (fires at capacity=50001, n=1001, cells=50051001)

# .NET
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --treenode-filter "/*/*/KnapsackSliceTests/*"
# 17 tests passed including at-limit (50M → ok) and one-above (50.005M → throws)
```
✅

**Success criterion 7 — High-signal issues from backlog resolved in batches:**
- S06: 20 .NET issues (naming, epsilon, error messages, enum anchors, XML docs, interface contracts, 6 new tests)
- S07: CompositeScorer DFS removed, `Scorer::as_any` eliminated (8 impls), `UShapedPlacer` panic-free, 15 new unit tests
✅

**Milestone Definition of Done — full check:**

| Item | Status | Evidence |
|---|---|---|
| All 7 slices `[x]` | ✅ | M001-ROADMAP.md all checkboxes set |
| Slice summaries exist (S01–S07) | ✅ | All 7 .../slices/SNN/SNN-SUMMARY.md present |
| `cargo test` passes | ✅ | 35 passed, 0 failed |
| `cargo clippy --all-targets -- -D warnings` | ✅ | 0 warnings, exit 0 |
| `cargo deny check` | ✅ | advisories/bans/licenses/sources ok |
| Diagnostics vectors in spec/ and crates/ | ✅ | 5 vectors, zero drift |
| .NET test suite passes (641+) | ✅ | 663 total tests, 0 failures |
| KnapsackSlice DP guard both languages | ✅ | Rust (T01) + .NET (S06/T01) |
| v1.2.0 tag ready | ⏳ | All code ready; manual publish step pending |

## Requirement Changes

- R001: active → validated — All diagnostic types + `run_traced` + `dry_run` implemented; 5 conformance vectors pass; serde round-trips verified
- R002: active → validated — KnapsackSlice guard in both Rust and .NET; unit tests at/above/clearly-over 50M boundary
- R003: active → validated — `--all-targets` on all 4 clippy CI invocations; `deny.toml` unmaintained advisory configured; local checks exit 0
- R004: active → validated — 20 .NET issues resolved; 663 tests pass, 0 regressions; `dotnet build` clean
- R005: active → validated — CompositeScorer DFS removed; `as_any` eliminated; `UShapedPlacer` panic-free; 15 new unit tests; clippy clean
- R006: active → validated — All diagnostic types serde-complete with internally-tagged wire format; round-trips, validation, and forward-compat all tested

## Forward Intelligence

### What the next milestone should know
- `Slicer::slice` now returns `Result<Vec<ContextItem>, CupelError>` — this is a v1.2.0 public API break. Any downstream crate implementing `Slicer` must update. This was the only intentional semver break in v1.2.0.
- `_Unknown` variant exists on `ExclusionReason` — it must be preserved in any `match` refactoring; it handles forward-compat deserialization of future spec variants.
- `RawSelectionReport` is a private serde-only struct that mirrors `SelectionReport` — if a field is added to `SelectionReport`, `RawSelectionReport` must be updated simultaneously (the `deny_unknown_fields` annotation will error on mismatch, which is a good forcing function).
- `DiagnosticTraceCollector.callback` is always serde-skipped — callers that deserialize a collector will get `callback: None`. Documented in code.
- The conformance vector drift guard (`diff -rq spec/conformance/ crates/cupel/conformance/`) is the authoritative check for vector sync; run it after any vector change.

### What's fragile
- `CupelError::CycleDetected` is constructible but never emitted — intentionally kept for semver safety (D036); the doc comment is the only guard against confusion.
- `KnapsackSlice` guard uses discretized `capacity` (not raw `target_tokens`) — the threshold is not raw-budget × items > 50M but rather the DP table dimensions. Callers who reason about raw token budgets may be surprised.
- `diag-pinned-override.toml` PinnedOverride detection rule (D023) is tied to the exact form of `effective_target` in the Slice stage — if `effective_target` computation changes, the rule boundary must be re-verified.
- `TagScorer` case-insensitivity depends on `FrozenDictionary` preserving `StringComparer.OrdinalIgnoreCase` from the source Dictionary — .NET fragile point.
- `TraceEventCallback` is `Box<dyn Fn(&TraceEvent)>` and is NOT `Send` — if parallel pipeline stages are ever added, the callback must be rethought.

### Authoritative diagnostics
- `cargo test --manifest-path crates/cupel/Cargo.toml` — primary Rust signal; all 35 tests must pass
- `cargo test --features serde --manifest-path crates/cupel/Cargo.toml` — serde regression signal; must equal the non-serde count
- `cargo test --manifest-path crates/cupel/Cargo.toml --test conformance -- pipeline::diag --nocapture` — diagnostics conformance signal; per-field assertion messages on failure
- `diff -rq spec/conformance/ crates/cupel/conformance/` — conformance drift signal; any output = failure
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` — .NET core test signal (583 tests)

### What assumptions changed
- R001 validation status: the preloaded context had R001 as "active/unmapped" even though S03 completed `run_traced`/`dry_run` and all conformance tests pass. The evidence was always there — the status transition just hadn't been formally recorded. M001-SUMMARY now records R001 as validated with full proof.
- .NET test count: S06 summaries reported 658 tests; actual aggregate across all test projects is 663 (583 in Wollax.Cupel.Tests + 15 DI + 47 Json + 13 Tiktoken + 5 Consumption). The 658 figure was only the main test project count. All 663 pass.
- cargo-deny `unmaintained` field: original plan specified `"warn"` but cargo-deny 0.19.0 uses scope values — `"workspace"` was the correct choice (D030). True warn-without-fail requires CLI flags.

## Files Created/Modified

**S01 — Diagnostics Data Types:**
- `crates/cupel/src/diagnostics/mod.rs` — All 8 diagnostic types
- `crates/cupel/src/lib.rs` — pub mod diagnostics + re-exports
- `spec/conformance/required/pipeline/diag-*.toml` — 4 new conformance vectors
- `crates/cupel/conformance/required/pipeline/diag-*.toml` — vendored copies

**S02 — TraceCollector Trait & Implementations:**
- `crates/cupel/src/diagnostics/trace_collector.rs` — TraceCollector, NullTraceCollector, DiagnosticTraceCollector, TraceDetailLevel

**S03 — Pipeline run_traced & DryRun:**
- `crates/cupel/src/pipeline/mod.rs` — run_traced, dry_run, run() updated
- `crates/cupel/tests/conformance/pipeline.rs` — run_pipeline_diagnostics_test + 5 new tests
- `crates/cupel/src/pipeline/classify.rs` — ClassifyResult type alias
- `crates/cupel/src/pipeline/place.rs` — PlaceResult type alias

**S04 — Diagnostics Serde Integration:**
- `crates/cupel/src/diagnostics/mod.rs` — serde(tag), _Unknown, SelectionReport Deserialize
- `crates/cupel/src/diagnostics/trace_collector.rs` — ser/de_excluded_items free functions
- `crates/cupel/tests/serde.rs` — 16 new integration tests
- `crates/cupel/examples/serde_roundtrip.rs` — Part 4

**S05 — CI Quality Hardening:**
- `.github/workflows/ci-rust.yml` — --all-targets on 2 clippy steps
- `.github/workflows/release-rust.yml` — --all-targets on 2 clippy steps
- `crates/cupel/deny.toml` — unmaintained = "workspace"

**S06 — .NET Quality Hardening (20 files across src/tests):**
- `src/Wollax.Cupel/KnapsackSlice.cs`, `CupelPipeline.cs`, `QuotaBuilder.cs`, `CupelPolicy.cs`, `ScorerEntry.cs`, `ScorerType.cs`, `SlicerType.cs`, `ContextItem.cs`
- `src/Wollax.Cupel/Diagnostics/PipelineStage.cs`, `ITraceCollector.cs`, `SelectionReport.cs`
- `src/Wollax.Cupel/ISlicer.cs`, `ContextResult.cs`
- `src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs`
- `tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs`, `QuotaSliceTests.cs`
- `tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs`
- `tests/Wollax.Cupel.Tests/Scoring/PriorityScorerTests.cs`, `TagScorerTests.cs`
- `tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs`

**S07 — Rust Quality Hardening:**
- `crates/cupel/src/error.rs` — TableTooLarge variant; CycleDetected doc
- `crates/cupel/src/slicer/mod.rs`, `greedy.rs`, `knapsack.rs`, `quota.rs` — Result return type; guard; flat keep table
- `crates/cupel/src/pipeline/slice.rs`, `mod.rs` — Result propagation; pipeline unit tests
- `crates/cupel/src/scorer/mod.rs`, `composite.rs`, `scaled.rs`, `frequency.rs`, `kind.rs`, `priority.rs`, `recency.rs`, `reflexive.rs`, `tag.rs` — as_any removed; unit tests
- `crates/cupel/src/placer/u_shaped.rs` — left/right vec refactor; unit tests
- `crates/cupel/tests/conformance/slicing.rs` — .expect() on slice calls
- `.github/workflows/release-rust.yml` — job-level permissions
