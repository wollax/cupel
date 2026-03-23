---
id: T02
parent: S01
milestone: M003
provides:
  - 5 TOML conformance vectors for DecayScorer in spec/conformance/required/scoring/
  - 5 verbatim copies in crates/cupel/conformance/required/scoring/ (drift guard satisfied)
  - "decay" arm in build_scorer_by_type (conformance.rs) with FixedTimeProvider test struct
  - 5 #[test] functions in conformance/scoring.rs: decay_exponential_half_life, decay_future_dated, decay_null_timestamp, decay_step_second_window, decay_window_at_boundary
key_files:
  - spec/conformance/required/scoring/decay-exponential-half-life.toml
  - spec/conformance/required/scoring/decay-future-dated.toml
  - spec/conformance/required/scoring/decay-null-timestamp.toml
  - spec/conformance/required/scoring/decay-step-second-window.toml
  - spec/conformance/required/scoring/decay-window-at-boundary.toml
  - crates/cupel/conformance/required/scoring/decay-*.toml (5 files, copies)
  - crates/cupel/tests/conformance.rs
  - crates/cupel/tests/conformance/scoring.rs
key_decisions:
  - Step curve TOML uses [[config.curve.windows]] array-of-tables with max_age_secs and score fields; harness maps these to Vec<(Duration, f64)>
  - Duration conversion uses milliseconds precision (secs * 1000.0) to match T01's num_milliseconds() pattern and avoid truncation
  - FixedTimeProvider is defined as a test-local struct inside the conformance mod (not in the library), keeping test infrastructure isolated from public API
patterns_established:
  - TOML conformance vector layout for absolute scorers: [config] holds reference_time plus scorer-specific fields; [config.curve] holds type + curve-specific params; [[config.curve.windows]] is an array-of-tables for Step
observability_surfaces:
  - cargo test -- decay_ --nocapture runs only the 5 new decay tests with output
  - diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring shows drift if files diverge
  - Test failures name vector path and expected vs actual score via assert_scores_match
duration: 15min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T02: Write decay conformance vectors and wire conformance harness

**5 DecayScorer TOML conformance vectors authored and wired; all 5 tests green; drift guard satisfied (zero diff)**

## What Happened

Authored 5 TOML test vectors covering all three DecayScorer curve types and the key edge cases from the spec: Exponential half-life decay (0.5 at one half-life), future-dated items clamping to age zero (score 1.0), null timestamps returning nullTimestampScore (0.5), Step curve second-window selection (6h falls into 24h window, returns 0.5), and Window curve boundary exclusion (age == maxAge returns 0.0).

Extended `build_scorer_by_type` in `conformance.rs` with a `"decay"` arm. Added a test-local `FixedTimeProvider` struct implementing `TimeProvider` with a pinned `DateTime<Utc>`, which is constructed from `config.reference_time`. The arm parses `config.curve.type` and dispatches to exponential/window/step constructors with appropriate parameter names (`half_life_secs`, `max_age_secs`, `[[config.curve.windows]]`). Duration conversion uses milliseconds precision throughout to match the T01 implementation.

Added `DecayCurve`, `DecayScorer`, and `TimeProvider` to the `use cupel::{...}` import block.

Added 5 `#[test]` functions to `conformance/scoring.rs` following the existing `run_scoring_test` pattern.

## Verification

- `cargo test --all-targets` → 43 passed (38 pre-existing + 5 new decay tests), 0 failed
- `cargo test -- decay_` shows: decay_exponential_half_life, decay_future_dated, decay_null_timestamp, decay_step_second_window, decay_window_at_boundary — all `ok`
- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` → no output (zero drift)
- `grep -c "fn decay_" crates/cupel/tests/conformance/scoring.rs` → 5
- `ls spec/conformance/required/scoring/decay-*.toml | wc -l` → 5

## Diagnostics

- `cargo test -- decay_ --nocapture` runs only the 5 new tests with full output
- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` is the drift guard — run after any vector edit
- Test failures from `assert_scores_match` name the content key, expected score, actual score, and epsilon — no guessing

## Deviations

None. The TOML structure, harness wiring, and test function names all follow the plan exactly.

## Known Issues

None.

## Files Created/Modified

- `spec/conformance/required/scoring/decay-exponential-half-life.toml` — new vector: Exponential, age=24h, halfLife=24h → 0.5
- `spec/conformance/required/scoring/decay-future-dated.toml` — new vector: future-dated item, age clamps to 0 → 1.0
- `spec/conformance/required/scoring/decay-null-timestamp.toml` — new vector: no timestamp → nullTimestampScore 0.5
- `spec/conformance/required/scoring/decay-step-second-window.toml` — new vector: Step curve, age 6h falls in 24h window → 0.5
- `spec/conformance/required/scoring/decay-window-at-boundary.toml` — new vector: Window maxAge=6h, age=6h → 0.0
- `crates/cupel/conformance/required/scoring/decay-*.toml` — 5 verbatim copies of the above
- `crates/cupel/tests/conformance.rs` — added FixedTimeProvider struct + "decay" arm in build_scorer_by_type; added DecayCurve, DecayScorer, TimeProvider to imports
- `crates/cupel/tests/conformance/scoring.rs` — added 5 #[test] functions for decay scenarios
