---
id: T01
parent: S02
milestone: M003
provides:
  - MetadataTrustScorer struct with new() validation (CupelError::ScorerConfig on out-of-range default_score)
  - score() returning default_score on key-absent / parse-error / non-finite; parsed.clamp(0.0, 1.0) otherwise
  - is_finite() guard applied after parse (NaN-safe by design)
  - MetadataTrustScorer re-exported from scorer/mod.rs and lib.rs
  - build_items() in conformance.rs extended to parse metadata inline tables
  - "metadata_trust" arm added to build_scorer_by_type in conformance.rs
  - 5 TOML vectors in spec/, conformance/ (root), and crates/cupel/conformance/required/scoring/
  - 5 #[test] functions in conformance/scoring.rs
key_files:
  - crates/cupel/src/scorer/metadata_trust.rs
  - crates/cupel/src/scorer/mod.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/conformance.rs
  - crates/cupel/tests/conformance/scoring.rs
  - spec/conformance/required/scoring/metadata-trust-*.toml (5 files)
  - crates/cupel/conformance/required/scoring/metadata-trust-*.toml (5 files)
  - conformance/required/scoring/metadata-trust-*.toml (5 files — root canonical dir)
key_decisions:
  - Vectors must exist in THREE locations: spec/, root conformance/, and crates/cupel/conformance/ — the pre-commit hook checks root conformance/ vs crates/cupel/conformance/, not spec/
  - is_finite() placed AFTER parse() because "NaN".parse::<f64>() returns Ok(NaN), not Err
patterns_established:
  - MetadataTrustScorer follows ReflexiveScorer pattern (absolute, single-field, is_finite + clamp) plus metadata key lookup and string parse
  - build_items metadata parsing: filter_map on table entries skipping non-string values, skip builder.metadata() call when map is empty
observability_surfaces:
  - "cargo test -- metadata_trust --nocapture" isolates the 5 new conformance tests
  - "diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring" is canonical drift check
  - CupelError::ScorerConfig("defaultScore must be in [0.0, 1.0]") on construction with out-of-range default_score
duration: 20min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T01: Rust MetadataTrustScorer + Conformance Harness Metadata Extension + 5 TOML Vectors

**`MetadataTrustScorer` implemented in Rust with NaN-safe scoring, conformance harness extended to parse metadata inline tables, and 5 TOML vectors passing across all edge cases.**

## What Happened

Created `crates/cupel/src/scorer/metadata_trust.rs` with `MetadataTrustScorer { default_score: f64 }`. `new()` validates `[0.0, 1.0]` and returns `CupelError::ScorerConfig` on failure. `score()` performs the three-step fallback: key lookup → parse → is_finite check, then `clamp(0.0, 1.0)`. The is_finite guard follows (not replaces) the parse call because `"NaN".parse::<f64>()` returns `Ok(f64::NAN)`.

Wired into `scorer/mod.rs` (new `mod metadata_trust` + `pub use` + doc table row) and `lib.rs` (added to `pub use scorer::{...}` export list).

Extended `build_items` in `conformance.rs` to parse `metadata` inline tables: filter_map over key-value pairs keeping only string values, skip the `builder.metadata()` call when the map is empty. Added `"metadata_trust"` arm to `build_scorer_by_type` reading `config.default_score` with 0.5 fallback.

Authored 5 TOML vectors covering all spec-defined scenarios: present-valid, key-absent, unparseable, out-of-range-high (clamped), and non-finite (NaN). Added 5 `#[test]` functions to `conformance/scoring.rs`.

**Deviation discovered during commit:** The pre-commit hook enforces sync between `conformance/required/` (repo root) and `crates/cupel/conformance/required/` — not `spec/conformance/required/` as stated in the task plan. Vectors were copied to all three locations; the hook passed.

## Verification

- `cargo test --all-targets` — 43 passed, 0 failed
- `cargo test -- metadata_trust --nocapture` — 5 passed (conformance tests) + 1 doctest
- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` — no output
- `grep -c "fn metadata_trust_" crates/cupel/tests/conformance/scoring.rs` — 5
- `grep "MetadataTrustScorer" crates/cupel/src/lib.rs` — non-empty line present

## Diagnostics

- Construction failure: `CupelError::ScorerConfig("defaultScore must be in [0.0, 1.0]")`
- Isolation: `cargo test -- metadata_trust --nocapture` runs only the 5 new conformance tests
- Drift guard: `diff -r conformance/required/scoring crates/cupel/conformance/required/scoring` (uses root canonical dir)

## Deviations

- Task plan said to copy vectors to `spec/` and `crates/cupel/conformance/` only. The pre-commit hook also requires vectors in the root `conformance/required/scoring/` directory. Copied to all three; all three are now in sync.

## Known Issues

None. The Rust side of S02 is complete. Remaining work is .NET implementation (T02).

## Files Created/Modified

- `crates/cupel/src/scorer/metadata_trust.rs` — new: MetadataTrustScorer struct + Scorer impl + unit tests (~120 lines)
- `crates/cupel/src/scorer/mod.rs` — added mod metadata_trust; pub use MetadataTrustScorer; doc table row
- `crates/cupel/src/lib.rs` — added MetadataTrustScorer to pub use scorer export list
- `crates/cupel/tests/conformance.rs` — build_items metadata block; "metadata_trust" arm; MetadataTrustScorer import
- `crates/cupel/tests/conformance/scoring.rs` — 5 new #[test] functions
- `spec/conformance/required/scoring/metadata-trust-*.toml` — 5 new TOML vectors
- `crates/cupel/conformance/required/scoring/metadata-trust-*.toml` — 5 verbatim copies
- `conformance/required/scoring/metadata-trust-*.toml` — 5 copies (required by pre-commit hook)
