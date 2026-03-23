---
estimated_steps: 6
estimated_files: 13
---

# T02: Write decay conformance vectors and wire conformance harness

**Slice:** S01 — DecayScorer — Rust + .NET Implementation
**Milestone:** M003

## Description

Author the 5 TOML conformance vectors for `DecayScorer` (matching the 5 spec outlines in `spec/src/scorers/decay.md`), copy them to the Rust crate conformance directory, extend the conformance harness `build_scorer_by_type` with a `"decay"` arm, and add 5 `#[test]` functions to `conformance/scoring.rs`. The drift guard (diff between spec and crate conformance dirs) must be empty after this task.

## Steps

1. Author 5 TOML vectors in `spec/conformance/required/scoring/` (all using `referenceTime = 2025-01-01T12:00:00Z`):
   - `decay-exponential-half-life.toml` — 1 item, age = 24h, halfLife = 24h → score ≈ 0.5
   - `decay-future-dated.toml` — 1 item with timestamp after referenceTime, Exponential halfLife=24h → score = 1.0
   - `decay-null-timestamp.toml` — 1 item with no timestamp, nullTimestampScore = 0.5 → score = 0.5
   - `decay-step-second-window.toml` — 1 item age = 6h, Step([(1h,0.9),(24h,0.5),(72h,0.1)]) → score = 0.5
   - `decay-window-at-boundary.toml` — 1 item age = 6h exactly, Window(maxAge=6h) → score = 0.0
2. Copy all 5 files verbatim to `crates/cupel/conformance/required/scoring/` (same names).
3. Add a `"decay"` match arm to `build_scorer_by_type` in `crates/cupel/tests/conformance.rs`:
   - Define a test-local `struct FixedTimeProvider(DateTime<Utc>); impl TimeProvider for FixedTimeProvider { fn now(&self) -> DateTime<Utc> { self.0 } }` — this is only needed in the test module.
   - Read `config.reference_time` via `parse_toml_datetime`.
   - Read `config.curve.type` to select the curve variant; read `config.curve.half_life_secs`, `config.curve.max_age_secs`, or `config.curve.windows` accordingly; construct the `DecayCurve`.
   - Read `config.null_timestamp_score` (optional float, default 0.5).
   - Return `Box::new(DecayScorer::new(Box::new(FixedTimeProvider(ref_time)), curve, null_ts_score).unwrap())`.
   - Add `DecayScorer, DecayCurve, TimeProvider` to the `use cupel::{...}` import at the top of the conformance module.
4. Add 5 `#[test]` functions to `crates/cupel/tests/conformance/scoring.rs` following the `recency_basic()` pattern: `decay_exponential_half_life()`, `decay_future_dated()`, `decay_null_timestamp()`, `decay_step_second_window()`, `decay_window_at_boundary()`.
5. Run `cargo test --all-targets` to confirm all 5 new tests pass.
6. Run `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` to confirm zero drift.

## Must-Haves

- [ ] 5 TOML files in `spec/conformance/required/scoring/` named `decay-*.toml`, each with `[test]`, `[[items]]`, `[[expected]]`, `[tolerance]`, and `[config]` sections including `reference_time`
- [ ] 5 identical TOML files in `crates/cupel/conformance/required/scoring/` (verbatim copies)
- [ ] `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` exits 0
- [ ] `"decay"` arm in `build_scorer_by_type` constructs a `FixedTimeProvider` from `config.reference_time`
- [ ] 5 new `#[test]` functions in `conformance/scoring.rs`, each calling `run_scoring_test("scoring/decay-*.toml")`
- [ ] `cargo test --all-targets` exits 0 with all 5 new decay tests green

## Verification

- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` → no output (zero drift)
- `cargo test --all-targets 2>&1 | grep "decay_"` — shows 5 lines, all `ok`
- `grep -c "fn decay_" crates/cupel/tests/conformance/scoring.rs` → 5
- `ls spec/conformance/required/scoring/decay-*.toml | wc -l` → 5

## Observability Impact

- Signals added/changed: Conformance test failures surface with the vector file path and expected vs actual score value — unambiguous localization
- How a future agent inspects this: `cargo test -- decay_ --nocapture` to run only decay tests; `diff` command shows exactly which files diverge if drift guard fails
- Failure state exposed: TOML parse errors and score mismatches name the vector file and field — no guessing needed

## Inputs

- `crates/cupel/src/scorer/decay.rs` — T01 output; `DecayScorer::new()`, `DecayCurve`, `TimeProvider` trait all available
- `crates/cupel/tests/conformance.rs` — `build_scorer_by_type` function to extend; `parse_toml_datetime` helper available
- `crates/cupel/tests/conformance/scoring.rs` — existing `run_scoring_test` and `#[test]` pattern
- `spec/conformance/required/scoring/recency-basic.toml` — TOML format reference
- `spec/src/scorers/decay.md` — 5 conformance vector outlines (section "Conformance Vector Outlines")

## Expected Output

- `spec/conformance/required/scoring/decay-exponential-half-life.toml` (new)
- `spec/conformance/required/scoring/decay-future-dated.toml` (new)
- `spec/conformance/required/scoring/decay-null-timestamp.toml` (new)
- `spec/conformance/required/scoring/decay-step-second-window.toml` (new)
- `spec/conformance/required/scoring/decay-window-at-boundary.toml` (new)
- `crates/cupel/conformance/required/scoring/decay-*.toml` (5 new, copies)
- `crates/cupel/tests/conformance.rs` — `"decay"` arm added; `FixedTimeProvider` test-local struct
- `crates/cupel/tests/conformance/scoring.rs` — 5 new `#[test]` functions
- `cargo test --all-targets` exits 0; drift guard diff empty
