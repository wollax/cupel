---
estimated_steps: 7
estimated_files: 14
---

# T01: Rust MetadataTrustScorer + Conformance Harness Metadata Extension + 5 TOML Vectors

**Slice:** S02 — MetadataTrustScorer — Rust + .NET Implementation
**Milestone:** M003

## Description

Implement `MetadataTrustScorer` in Rust, extend the conformance harness's `build_items` function to parse `metadata` from TOML test vectors, add a `"metadata_trust"` arm to `build_scorer_by_type`, author 5 TOML conformance vectors covering all spec-defined scenarios, copy them to both the spec and crate directories, and add 5 passing test functions. This task completes the entire Rust side of S02.

The key algorithmic insight from the research: `str::parse::<f64>()` on `"NaN"` returns `Ok(f64::NAN)`, not `Err`. The `is_finite()` check MUST follow the parse call — not replace it — or NaN will pass through. Follow the `ReflexiveScorer` pattern (absolute scorer, single-field read, `is_finite` + `clamp`) with the addition of metadata key lookup and string parse.

## Steps

1. Create `crates/cupel/src/scorer/metadata_trust.rs`: `MetadataTrustScorer { default_score: f64 }` with `new(default_score: f64) -> Result<Self, CupelError>` validating `[0.0, 1.0]` (returns `CupelError::ScorerConfig("defaultScore must be in [0.0, 1.0]")` if out of range); implement `Scorer` trait with `score()`: get `item.metadata().get("cupel:trust")` → if `None` return `self.default_score`; parse with `str::parse::<f64>()` → if `Err` return `self.default_score`; if `!parsed.is_finite()` return `self.default_score`; return `parsed.clamp(0.0, 1.0)`.

2. Wire into `scorer/mod.rs`: add `mod metadata_trust;` and `pub use metadata_trust::MetadataTrustScorer;`; add a `MetadataTrustScorer` row to the doc table in the module-level comment.

3. Wire into `lib.rs`: add `MetadataTrustScorer` to the `pub use scorer::{...}` export list.

4. Extend `build_items` in `crates/cupel/tests/conformance.rs`: after the existing `pinned` parsing block, add a block that reads `item.get("metadata").and_then(|v| v.as_table())`, iterates the table's key-value pairs, collects them into `HashMap<String, String>` (skipping non-string values), and calls `builder.metadata(map)` only when the map is non-empty.

5. Add `"metadata_trust"` arm to `build_scorer_by_type` in `conformance.rs`: read `config.get("default_score").and_then(|v| v.as_float()).unwrap_or(0.5)`; call `MetadataTrustScorer::new(default_score).unwrap()` and box it; add `MetadataTrustScorer` to the imports.

6. Write 5 TOML conformance vectors under `spec/conformance/required/scoring/` using inline table format (`metadata = { "cupel:trust" = "0.85" }`):
   - `metadata-trust-present-valid.toml` — trust = "0.85" → expected score 0.85
   - `metadata-trust-key-absent.toml` — item has no cupel:trust key → expected score = default_score (0.5)
   - `metadata-trust-unparseable.toml` — trust = "high" → expected score = default_score (0.5)
   - `metadata-trust-out-of-range-high.toml` — trust = "1.5" → expected score 1.0 (clamped)
   - `metadata-trust-non-finite.toml` — trust = "NaN" → expected score = default_score (0.5)
   
   Use `[config]` block with `default_score = 0.5`. Copy all 5 vectors verbatim to `crates/cupel/conformance/required/scoring/`.

7. Add 5 `#[test]` functions to `crates/cupel/tests/conformance/scoring.rs` following the `run_scoring_test("scoring/metadata-trust-*.toml")` pattern; name them `metadata_trust_present_valid`, `metadata_trust_key_absent`, `metadata_trust_unparseable`, `metadata_trust_out_of_range_high`, `metadata_trust_non_finite`.

## Must-Haves

- [ ] `MetadataTrustScorer` struct in `metadata_trust.rs` with `new()` validation: `CupelError::ScorerConfig` on out-of-range `default_score`
- [ ] `score()` returns `default_score` on key-absent, parse-error, and non-finite; returns `value.clamp(0.0, 1.0)` otherwise
- [ ] `is_finite()` check follows the parse call (not skipped because parse succeeded)
- [ ] `MetadataTrustScorer` re-exported from `scorer/mod.rs` and `lib.rs`
- [ ] `build_items` in `conformance.rs` parses `metadata` inline tables and passes them to `ContextItemBuilder::metadata()`
- [ ] `"metadata_trust"` arm in `build_scorer_by_type` correctly constructs `MetadataTrustScorer` from `config.default_score`
- [ ] 5 TOML vectors exist in `spec/conformance/required/scoring/` with correct `[test]`, `[[items]]`, `[[expected]]`, and `[config]` sections
- [ ] 5 vectors copied verbatim to `crates/cupel/conformance/required/scoring/`
- [ ] 5 `#[test]` functions in `conformance/scoring.rs` named `metadata_trust_*`
- [ ] `cargo test --all-targets` exits 0
- [ ] `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` produces no output

## Verification

- `cargo test --all-targets` — all tests pass (should be ≥50 total including the 5 new ones)
- `cargo test -- metadata_trust --nocapture` — runs only the 5 new tests with full output; all pass
- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` — no output (zero drift)
- `grep -c "fn metadata_trust_" crates/cupel/tests/conformance/scoring.rs` — prints `5`
- `grep "MetadataTrustScorer" crates/cupel/src/lib.rs` — prints a non-empty line (export present)

## Observability Impact

- Signals added/changed: `CupelError::ScorerConfig("defaultScore must be in [0.0, 1.0]")` on construction failure; consistent with all other construction-time scorer errors
- How a future agent inspects this: `cargo test -- metadata_trust --nocapture` isolates the 5 vectors; `diff -r spec/conformance/... crates/cupel/conformance/...` is the canonical drift check
- Failure state exposed: parse errors and non-finite values silently return `default_score` per spec; construction errors are explicit `CupelError::ScorerConfig`

## Inputs

- `crates/cupel/src/scorer/decay.rs` — construction validation pattern (`CupelError::ScorerConfig`, `f64` range check)
- `crates/cupel/src/scorer/reflexive.rs` — algorithmic pattern (`is_finite()` + `clamp()` without extra state)
- `crates/cupel/tests/conformance.rs` — `build_items` to extend; `build_scorer_by_type` `"decay"` arm as model for new arm
- `spec/conformance/required/scoring/decay-exponential-half-life.toml` — TOML vector format reference
- `crates/cupel/tests/conformance/scoring.rs` — `run_scoring_test` pattern and test function naming convention
- S01 summary — `ContextItemBuilder::metadata()` method exists; `build_items` currently parses content/tokens/kind/timestamp/priority/tags/futureRelevanceHint/pinned (no metadata)

## Expected Output

- `crates/cupel/src/scorer/metadata_trust.rs` — new file: `MetadataTrustScorer` struct + `Scorer` impl (~40 lines)
- `crates/cupel/src/scorer/mod.rs` — `mod metadata_trust` added; `MetadataTrustScorer` in pub use and doc table
- `crates/cupel/src/lib.rs` — `MetadataTrustScorer` added to pub use
- `crates/cupel/tests/conformance.rs` — `build_items` extended with metadata block; `"metadata_trust"` arm added to `build_scorer_by_type`; `MetadataTrustScorer` added to imports
- `crates/cupel/tests/conformance/scoring.rs` — 5 new `#[test]` functions
- `spec/conformance/required/scoring/metadata-trust-*.toml` — 5 new files
- `crates/cupel/conformance/required/scoring/metadata-trust-*.toml` — 5 verbatim copies
