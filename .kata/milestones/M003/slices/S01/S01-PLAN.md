# S01: DecayScorer — Rust + .NET Implementation

**Goal:** Implement `DecayScorer` with Exponential, Window, and Step curves in both Rust and .NET; establish the conformance vector format for the decay scorer; verify all conformance vectors pass in both languages.
**Demo:** A `DecayScorer` with any of three curve types can be constructed in both Rust and .NET with an injected time provider, `cargo test` and `dotnet test` both pass with new decay scorer tests and conformance vectors, and the drift guard is satisfied.

## Must-Haves

- `DecayScorer` struct in `crates/cupel/src/scorer/decay.rs` implementing `Scorer` trait, not `Clone`, with `Box<dyn TimeProvider + Send + Sync>` stored at construction
- `TimeProvider` trait (`fn now(&self) -> DateTime<Utc>`) + `SystemTimeProvider` ZST in `decay.rs`
- `DecayCurve` enum with `Exponential(Duration)`, `Window(Duration)`, `Step(Vec<(Duration, f64)>)` variants; all preconditions enforced at construction returning `Err(CupelError::ScorerConfig(...))`
- Future-dated items clamp to `Duration::ZERO` before curve dispatch
- `nullTimestampScore` defaults to 0.5; construction rejected if outside [0.0, 1.0]
- `DecayScorer`, `DecayCurve`, `TimeProvider`, `SystemTimeProvider` re-exported from `scorer/mod.rs` and `lib.rs`
- 5 conformance TOML vectors written in both `spec/conformance/required/scoring/` and `crates/cupel/conformance/required/scoring/`; drift guard passes
- `#[test]` functions for all 5 decay vectors in `crates/cupel/tests/conformance/scoring.rs`
- `build_scorer_by_type("decay", ...)` arm in `crates/cupel/tests/conformance.rs` reading `config.reference_time`, `config.curve`, and `config.null_timestamp_score`
- `DecayScorer` class in `src/Wollax.Cupel/Scoring/DecayScorer.cs` implementing `IScorer`; takes `System.TimeProvider` (BCL, no NuGet)
- `DecayCurve` sealed abstract class hierarchy in `src/Wollax.Cupel/Scoring/DecayCurve.cs` with `Exponential`, `Window`, `Step` sealed subclasses; preconditions throw `ArgumentException`
- `PublicAPI.Unshipped.txt` updated for every new public type and member
- `cargo test --all-targets` and `dotnet test` both pass with all new tests green

## Proof Level

- This slice proves: contract (unit tests + conformance vectors in both languages)
- Real runtime required: no
- Human/UAT required: no

## Verification

- `cargo test --all-targets 2>&1 | tail -5` — all tests pass including new `decay_*` test functions
- `dotnet test 2>&1 | tail -10` — all tests pass including new DecayScorer tests
- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` — no diff (drift guard satisfied)
- `grep -c "decay" crates/cupel/tests/conformance/scoring.rs` — 5 or more test functions
- `grep -c "DecayScorer\|DecayCurve\|TimeProvider\|SystemTimeProvider" src/Wollax.Cupel/PublicAPI.Unshipped.txt` — entries present for all new public types/members
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj 2>&1 | grep -c "error"` returns 0

## Observability / Diagnostics

- Runtime signals: Construction-time errors surface as `CupelError::ScorerConfig(String)` in Rust; `ArgumentException` in .NET — both name the offending parameter in the message
- Inspection surfaces: `cargo test -- --nocapture` shows test output; `dotnet test --verbosity normal` shows test names
- Failure visibility: `cargo test` failures include the vector path and expected vs actual score; conformance drift surfaces via `diff` output naming the divergent file
- Redaction constraints: none (no secrets or PII in this slice)

## Integration Closure

- Upstream surfaces consumed: `Scorer` trait (`crates/cupel/src/scorer/mod.rs`), `CupelError::ScorerConfig` (`crates/cupel/src/error.rs`), `IScorer` interface (`src/Wollax.Cupel/`), `build_scorer_by_type` function (`crates/cupel/tests/conformance.rs`), `parse_toml_datetime` helper (`crates/cupel/tests/conformance.rs`)
- New wiring introduced in this slice: `mod decay;` + re-exports in `scorer/mod.rs` and `lib.rs`; `"decay"` arm in `build_scorer_by_type`; new public types in `PublicAPI.Unshipped.txt`
- What remains before the milestone is truly usable end-to-end: S02 (MetadataTrustScorer), S03 (CountQuotaSlice), S04 (analytics + Cupel.Testing), S05 (OTel bridge), S06 (budget simulation + tiebreaker)

## Tasks

- [x] **T01: Implement DecayScorer Rust core** `est:2h`
  - Why: Establishes the Rust implementation of `DecayScorer` — the primary risk in this slice (chrono dep, TimeProvider trait, curve preconditions)
  - Files: `crates/cupel/src/scorer/decay.rs`, `crates/cupel/src/scorer/mod.rs`, `crates/cupel/src/lib.rs`
  - Do: Create `decay.rs` with `TimeProvider` trait (`fn now(&self) -> DateTime<Utc>`), `SystemTimeProvider` ZST, `DecayCurve` enum (`Exponential(Duration)`, `Window(Duration)`, `Step(Vec<(Duration, f64)>)`), and `DecayScorer` struct with `Box<dyn TimeProvider + Send + Sync>` + `DecayCurve` + `null_timestamp_score: f64`. Implement construction via `new()` returning `Result<Self, CupelError>`: reject `null_timestamp_score` outside [0.0, 1.0] with `ScorerConfig("nullTimestampScore must be in [0.0, 1.0]")`; validate each curve's preconditions (`halfLife > Duration::ZERO`, `maxAge > Duration::ZERO`, Step windows non-empty and no zero-width entries). Implement `Scorer` for `DecayScorer`: if `item.timestamp()` is None return `null_timestamp_score`; compute `age = (now - ts).max(Duration::ZERO)` (clamp before dispatch); dispatch to the curve formula. Register `mod decay;` in `scorer/mod.rs` and `pub use decay::{DecayScorer, DecayCurve, TimeProvider, SystemTimeProvider};`; add to `pub use scorer::{...}` in `lib.rs`. Do not derive `Clone` or `Copy` on `DecayScorer`.
  - Verify: `cargo test --all-targets` passes; `cargo clippy --all-targets -- -D warnings` clean; `cargo doc --no-deps` 0 warnings
  - Done when: `cargo test --all-targets` exits 0 with all existing tests still passing and the new module compiles without warnings

- [x] **T02: Write decay conformance vectors and wire conformance harness** `est:1.5h`
  - Why: Conformance vectors are the mechanically checkable proof that the Rust implementation matches the spec; drift guard closure is a must-have for this slice
  - Files: `spec/conformance/required/scoring/decay-exponential-half-life.toml`, `spec/conformance/required/scoring/decay-future-dated.toml`, `spec/conformance/required/scoring/decay-null-timestamp.toml`, `spec/conformance/required/scoring/decay-step-second-window.toml`, `spec/conformance/required/scoring/decay-window-at-boundary.toml`, `crates/cupel/conformance/required/scoring/` (copies of all 5), `crates/cupel/tests/conformance.rs`, `crates/cupel/tests/conformance/scoring.rs`
  - Do: Author 5 TOML vectors in `spec/conformance/required/scoring/` matching the 5 spec outlines — referenceTime = 2025-01-01T12:00:00Z. Copy verbatim to `crates/cupel/conformance/required/scoring/`. Add `"decay"` arm to `build_scorer_by_type` in `conformance.rs`: read `config.reference_time` via `parse_toml_datetime`, `config.curve.type` to select curve variant, `config.curve.half_life`/`max_age`/`windows` for parameters, `config.null_timestamp_score` (optional, default 0.5); construct a test-local `FixedTimeProvider(DateTime<Utc>)` that returns the fixed time; build and return `Box<new DecayScorer(Box::new(FixedTimeProvider(ref_time)), curve, null_ts_score)>`. Add 5 `#[test]` functions in `conformance/scoring.rs` using `run_scoring_test("scoring/decay-*.toml")`.
  - Verify: `cargo test --all-targets` passes with 5 new decay_* tests green; `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` exits 0 (no diff); `grep -c "fn decay_" crates/cupel/tests/conformance/scoring.rs` ≥ 5
  - Done when: All 5 decay conformance tests pass, drift guard diff is empty, no existing tests regressed

- [x] **T03: Implement DecayScorer .NET** `est:2h`
  - Why: Closes the .NET side of the slice; both languages must pass for the slice to be complete (R020)
  - Files: `src/Wollax.Cupel/Scoring/DecayScorer.cs`, `src/Wollax.Cupel/Scoring/DecayCurve.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`, `src/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs`
  - Do: Create `DecayCurve.cs` as `public abstract class DecayCurve` with three nested `public sealed class` types — `Exponential(TimeSpan HalfLife)`, `Window(TimeSpan MaxAge)`, `Step(IReadOnlyList<(TimeSpan MaxAge, double Score)> Windows)` — each validating preconditions in constructor and throwing `ArgumentException` naming the parameter. Create `DecayScorer.cs` as `public sealed class DecayScorer : IScorer` taking `System.TimeProvider timeProvider` and `DecayCurve curve` and optional `double nullTimestampScore = 0.5`; validate `nullTimestampScore` in [0.0, 1.0] throwing `ArgumentOutOfRangeException`; implement `Score`: if `item.Timestamp` is null return `nullTimestampScore`; compute age = `(timeProvider.GetUtcNow() - item.Timestamp.Value).Duration()` clamped to `TimeSpan.Zero`; dispatch to curve. Add all new public types and every constructor/method/property to `PublicAPI.Unshipped.txt` — check `PublicAPI.Shipped.txt` for RecencyScorer format. Write unit tests in `DecayScorerTests.cs` covering: Exponential half-life (expect 0.5), future-dated item (expect 1.0), null timestamp (expect configured score), Step second window, Window at-boundary (expect 0.0). Use `FakeTimeProvider` or a simple `TimeProvider` subclass in tests.
  - Verify: `dotnet build 2>&1 | grep -c "error"` → 0; `dotnet test 2>&1 | tail -10` all tests pass including new DecayScorer tests; `grep -c "DecayScorer\|DecayCurve" src/Wollax.Cupel/PublicAPI.Unshipped.txt` shows entries for all new public members
  - Done when: `dotnet test` exits 0 with new tests green and build is warning-free

## Files Likely Touched

- `crates/cupel/src/scorer/decay.rs` (new)
- `crates/cupel/src/scorer/mod.rs`
- `crates/cupel/src/lib.rs`
- `crates/cupel/tests/conformance.rs`
- `crates/cupel/tests/conformance/scoring.rs`
- `spec/conformance/required/scoring/decay-*.toml` (5 new)
- `crates/cupel/conformance/required/scoring/decay-*.toml` (5 new, copies)
- `src/Wollax.Cupel/Scoring/DecayScorer.cs` (new)
- `src/Wollax.Cupel/Scoring/DecayCurve.cs` (new)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
- `src/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs` (new)
