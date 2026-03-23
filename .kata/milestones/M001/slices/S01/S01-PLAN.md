# S01: Diagnostics Data Types

**Goal:** `TraceEvent`, `ExclusionReason`, `InclusionReason`, and `SelectionReport` types (plus `PipelineStage`, `OverflowEvent`, `IncludedItem`, `ExcludedItem`) exist in the Rust crate with full doc comments; 5 diagnostics conformance vectors exist in both `spec/conformance/` and `crates/cupel/conformance/` with no drift.
**Demo:** `cargo test` passes; `cargo doc --no-deps` produces no warnings; drift guard reports all vectors in sync; the 5 TOML files exist with correct `[expected.diagnostics.*]` sections.

## Must-Haves

- `crates/cupel/src/diagnostics/mod.rs` with all 8 types: `PipelineStage`, `TraceEvent`, `OverflowEvent`, `ExclusionReason`, `InclusionReason`, `IncludedItem`, `ExcludedItem`, `SelectionReport`
- All new public types have `#[non_exhaustive]` and complete doc comments
- `ExclusionReason` has all 8 variants with the exact field shapes from spec (4 active, 4 reserved)
- `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]` stubs on all types; `ExclusionReason` has a `// custom serde impl in S04` comment
- All new types re-exported from `crates/cupel/src/lib.rs`
- 5 conformance vectors total (1 existing + 4 new) in `spec/conformance/required/pipeline/` and mirrored in `crates/cupel/conformance/required/pipeline/`
- Drift guard check passes (spec and crates copies identical)

## Proof Level

- This slice proves: contract
- Real runtime required: no (no pipeline wiring; types defined and documented only)
- Human/UAT required: no

## Verification

```bash
# All tests pass (including existing conformance harness)
cargo test

# No doc warnings
cargo doc --no-deps 2>&1 | grep -E "warning|error" && echo "DOC ISSUES" || echo "DOC OK"

# No clippy warnings on new module
cargo clippy --all-targets -- -D warnings

# Drift guard: spec and crates copies must be identical
diff -rq spec/conformance/required/pipeline/ crates/cupel/conformance/required/pipeline/

# 5 diagnostics vectors exist
ls spec/conformance/required/pipeline/diag*.toml spec/conformance/required/pipeline/diagnostics-budget-exceeded.toml | wc -l
# Expected: 5
```

## Observability / Diagnostics

- Runtime signals: none (pure type definitions)
- Inspection surfaces: `cargo doc --no-deps --open` for doc surface; TOML vectors readable directly
- Failure visibility: `diff -r spec/conformance/ crates/cupel/conformance/` exposes drift; `cargo doc` warnings surface missing doc comments
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `ContextItem`, `ContextBudget` (already in crate) — referenced as field types in new structs
- New wiring introduced in this slice: `pub mod diagnostics;` in `lib.rs`, re-exports of all 8 public types
- What remains before the milestone is truly usable end-to-end: S02 (TraceCollector trait + implementations), S03 (run_traced pipeline wiring), S04 (serde custom impls), conformance vector test harness coverage (S03)

## Tasks

- [x] **T01: Define diagnostic types in `src/diagnostics/mod.rs`** `est:45m`
  - Why: Core deliverable of S01 — without the types, nothing else in M001 can proceed
  - Files: `crates/cupel/src/diagnostics/mod.rs` (new), `crates/cupel/src/lib.rs`
  - Do: Create `src/diagnostics/mod.rs` with all 8 types following the `OverflowStrategy` enum pattern and `ContextItem`/`ContextBudget` struct pattern. Apply `#[non_exhaustive]` to all. Add `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]` stubs. On `ExclusionReason` add `// custom serde impl in S04 — adjacent-tagged wire format`. Add `score: f64` (not `Option<f64>`) on `ExcludedItem`. Document `excluded` sort ordering in `SelectionReport` doc comment. Add `pub mod diagnostics;` and re-export all 8 types in `lib.rs`.
  - Verify: `cargo test` passes; `cargo doc --no-deps 2>&1 | grep -E "warning|error"` is empty; `cargo clippy --all-targets -- -D warnings` exits 0
  - Done when: All 8 types compile, `cargo test` green, zero doc warnings, zero clippy warnings

- [x] **T02: Author 4 conformance vectors and vendor to crates/** `est:45m`
  - Why: Minimum 5 diagnostics conformance vectors required (1 exists); vectors authored now avoid a gap that would silently pass CI until S03 wires the harness
  - Files: `spec/conformance/required/pipeline/diag-negative-tokens.toml` (new), `spec/conformance/required/pipeline/diag-deduplicated.toml` (new), `spec/conformance/required/pipeline/diag-pinned-override.toml` (new), `spec/conformance/required/pipeline/diag-scored-inclusion.toml` (new), and their mirrors in `crates/cupel/conformance/required/pipeline/`
  - Do: Author each vector following the `diagnostics-budget-exceeded.toml` schema exactly (same section headers: `[expected.diagnostics.summary]`, `[[expected.diagnostics.included]]`, `[[expected.diagnostics.excluded]]`). For `diag-pinned-override.toml`: use `overflow_strategy = "truncate"`, one pinned item that fits, one non-pinned item that gets displaced when the pinned item consumes available space (so the non-pinned item triggers `PinnedOverride`). Verify each vector's `expected_output` is consistent with its scenario description. Copy all 4 new vectors verbatim to `crates/cupel/conformance/required/pipeline/`.
  - Verify: `diff -rq spec/conformance/required/pipeline/ crates/cupel/conformance/required/pipeline/` exits 0; `ls spec/conformance/required/pipeline/diag*.toml | wc -l` = 4; `cargo test` still passes
  - Done when: 5 total diagnostics vectors present in both directories, drift guard exits 0, `cargo test` green

## Files Likely Touched

- `crates/cupel/src/diagnostics/mod.rs` (new)
- `crates/cupel/src/lib.rs`
- `spec/conformance/required/pipeline/diag-negative-tokens.toml` (new)
- `spec/conformance/required/pipeline/diag-deduplicated.toml` (new)
- `spec/conformance/required/pipeline/diag-pinned-override.toml` (new)
- `spec/conformance/required/pipeline/diag-scored-inclusion.toml` (new)
- `crates/cupel/conformance/required/pipeline/diag-negative-tokens.toml` (new)
- `crates/cupel/conformance/required/pipeline/diag-deduplicated.toml` (new)
- `crates/cupel/conformance/required/pipeline/diag-pinned-override.toml` (new)
- `crates/cupel/conformance/required/pipeline/diag-scored-inclusion.toml` (new)
