---
id: S01
parent: M003
milestone: M003
provides:
  - TimeProvider trait (Send + Sync) + SystemTimeProvider ZST in Rust crate
  - DecayCurve enum (Exponential, Window, Step) with validated constructors in Rust
  - DecayScorer struct implementing Scorer trait in Rust; injected Box<dyn TimeProvider>
  - 5 TOML conformance vectors for DecayScorer in spec/conformance/required/scoring/ and crates/cupel/conformance/required/scoring/
  - "decay" arm in build_scorer_by_type with FixedTimeProvider test struct; 5 #[test] functions in conformance/scoring.rs
  - DecayCurve abstract class + Exponential/Window/Step sealed nested subtypes in .NET (ArgumentException on bad args)
  - DecayScorer implementing IScorer using System.TimeProvider (BCL, no NuGet) in .NET
  - PublicAPI.Unshipped.txt updated with 14 new entries for all new public types and members
  - 5 DecayScorerTests in .NET (TUnit) covering all conformance scenarios
requires:
  - slice: none
    provides: Scorer trait (crates/cupel/src/scorer/mod.rs), CupelError::ScorerConfig, IScorer interface, build_scorer_by_type function
affects:
  - S02
  - S03
  - S04
key_files:
  - crates/cupel/src/scorer/decay.rs
  - crates/cupel/src/scorer/mod.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/conformance.rs
  - crates/cupel/tests/conformance/scoring.rs
  - spec/conformance/required/scoring/decay-exponential-half-life.toml
  - spec/conformance/required/scoring/decay-future-dated.toml
  - spec/conformance/required/scoring/decay-null-timestamp.toml
  - spec/conformance/required/scoring/decay-step-second-window.toml
  - spec/conformance/required/scoring/decay-window-at-boundary.toml
  - src/Wollax.Cupel/Scoring/DecayCurve.cs
  - src/Wollax.Cupel/Scoring/DecayScorer.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs
key_decisions:
  - Millisecond precision (num_milliseconds() / 1000.0) for age_secs to avoid integer truncation on sub-second ages (Rust)
  - Step curve falls through to last window score for items older than all windows; documented in module doc
  - Age clamping uses rawAge < TimeSpan.Zero ? TimeSpan.Zero : rawAge (not .Duration()) in .NET — matches Rust raw_age.max(Duration::zero()); .Duration() would incorrectly return absolute value
  - FixedTimeProvider defined as test-local struct in conformance.rs, not in library — keeps test infrastructure out of public API
  - Protected constructor DecayCurve() listed in PublicAPI.Unshipped.txt — RS0016 requires it even for external-subclassing prevention
  - Step curve uses strictly-greater-than for window boundary (age < MaxAge for Window, MaxAge > age for Step); consistent with Rust and conformance vectors
patterns_established:
  - Injected TimeProvider trait pattern for deterministic time-dependent scorers (Rust trait; BCL System.TimeProvider in .NET)
  - Curve validation at construction time via typed constructors — errors name the offending parameter in both languages
  - Abstract base + nested sealed subtypes for closed type hierarchies in .NET — switch expression exhaustiveness is compiler-enforced
  - TOML conformance vector layout for absolute scorers: [config] holds reference_time + scorer fields; [config.curve] holds type + curve params; [[config.curve.windows]] is array-of-tables for Step
observability_surfaces:
  - CupelError::ScorerConfig(String) names offending parameter on Rust construction failure
  - ArgumentException (DecayCurve) and ArgumentOutOfRangeException (DecayScorer.nullTimestampScore) name offending parameter in .NET
  - cargo test -- decay_ --nocapture runs only the 5 new decay tests with full output
  - diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring is the live drift guard
  - dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj runs all decay tests alongside suite
drill_down_paths:
  - .kata/milestones/M003/slices/S01/tasks/T01-SUMMARY.md
  - .kata/milestones/M003/slices/S01/tasks/T02-SUMMARY.md
  - .kata/milestones/M003/slices/S01/tasks/T03-SUMMARY.md
duration: ~50min (3 tasks × ~15-20min each)
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
---

# S01: DecayScorer — Rust + .NET Implementation

**`DecayScorer` with Exponential, Window, and Step curves ships in both Rust and .NET with injectable time providers; 5 conformance vectors pass in both languages; drift guard satisfied.**

## What Happened

T01 established the Rust implementation: `TimeProvider` trait + `SystemTimeProvider` ZST, `DecayCurve` enum with three validated variants, and `DecayScorer` (no Clone/Copy) storing `Box<dyn TimeProvider + Send + Sync>`. Construction validates `null_timestamp_score ∈ [0.0, 1.0]` and each curve's preconditions, returning `CupelError::ScorerConfig(...)` naming the offending parameter. Scoring clamps future-dated items to age zero before curve dispatch; Exponential uses millisecond precision to avoid truncation on sub-second ages; Step falls through to the last window's score. All four public types were re-exported from `scorer/mod.rs` and `lib.rs`.

T02 authored 5 TOML conformance vectors covering all curve types and key edge cases: Exponential half-life (0.5 at one half-life), future-dated clamping (score 1.0), null timestamp (nullTimestampScore), Step second-window selection, and Window boundary exclusion (score 0.0 at exactly maxAge). The conformance harness was extended with a `FixedTimeProvider` test struct and a `"decay"` arm in `build_scorer_by_type`, with 5 `#[test]` functions in `conformance/scoring.rs`. All 5 vectors were copied verbatim to both `spec/conformance/` and `crates/cupel/conformance/`; drift guard diff exits clean.

T03 implemented the .NET side: `DecayCurve` abstract class with three nested sealed subtypes (each throwing `ArgumentException` naming the parameter), and `DecayScorer` using `System.TimeProvider` (BCL, .NET 8+, no NuGet). The critical age-clamping insight: `.Duration()` returns absolute value and would give a positive age to a future timestamp — the correct pattern is `rawAge < TimeSpan.Zero ? TimeSpan.Zero : rawAge`, matching Rust's `raw_age.max(Duration::zero())`. `PublicAPI.Unshipped.txt` was extended with 14 entries; the protected base constructor must be listed for RS0016 compliance. Five TUnit tests covering all conformance scenarios were added and pass.

## Verification

- `cargo test --all-targets` → 45 unit tests + 38 conformance/integration tests pass; 0 failed
- `dotnet test` → 663 passed, 0 failed (includes all 5 new DecayScorerTests)
- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` → no output (zero drift)
- `grep -c "fn decay_" crates/cupel/tests/conformance/scoring.rs` → 5
- `grep -c "DecayScorer\|DecayCurve\|TimeProvider\|SystemTimeProvider" src/Wollax.Cupel/PublicAPI.Unshipped.txt` → 14
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` → 0 errors

## Requirements Advanced

- R020 (DecayScorer with TimeProvider injection) — fully implemented in both Rust and .NET with all three curve types, injected TimeProvider, conformance vectors, and passing tests in both languages

## Requirements Validated

- R020 — validated: DecayScorer exists in both languages; all 5 conformance vectors pass in Rust; all 5 conformance scenarios pass in .NET; drift guard satisfied; `cargo test --all-targets` and `dotnet test` both exit 0

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

- Age clamping in .NET uses `rawAge < TimeSpan.Zero ? TimeSpan.Zero : rawAge` instead of the plan's `.Duration()` — `.Duration()` is semantically wrong (returns absolute value); the Rust reference implementation confirms the correct behavior. This is a plan-text imprecision, not a scope change.
- Rust age computation uses millisecond precision (`num_milliseconds() / 1000.0`) rather than `num_seconds()` to avoid integer truncation on sub-second ages; not explicitly specified in the plan but more correct and consistent with spec intent.

## Known Limitations

- `SystemTimeProvider` and `FakeTimeProvider` are in .NET; the Rust `TimeProvider` trait has `SystemTimeProvider` but no library-shipped `FixedTimeProvider` — test consumers define their own (as `FixedTimeProvider` is in the conformance test module). This is intentional (keeps test infrastructure out of the public API) and mirrors the design of other time-dependent types.

## Follow-ups

- S02 (MetadataTrustScorer) can begin immediately — no S01 outputs are consumed by S02 beyond the scorer pattern itself, which is now established in both languages
- S03 (CountQuotaSlice) also depends on the established scorer pattern (indirectly) and can begin after S01

## Files Created/Modified

- `crates/cupel/src/scorer/decay.rs` — new; TimeProvider, SystemTimeProvider, DecayCurve, DecayScorer, Scorer impl (~185 lines)
- `crates/cupel/src/scorer/mod.rs` — added `mod decay;` + `pub use decay::{...}`
- `crates/cupel/src/lib.rs` — extended `pub use scorer::{...}` with 4 new identifiers
- `crates/cupel/tests/conformance.rs` — FixedTimeProvider struct + "decay" arm in build_scorer_by_type; updated imports
- `crates/cupel/tests/conformance/scoring.rs` — 5 new #[test] functions for decay scenarios
- `spec/conformance/required/scoring/decay-*.toml` — 5 new conformance vectors
- `crates/cupel/conformance/required/scoring/decay-*.toml` — 5 verbatim copies (drift guard)
- `src/Wollax.Cupel/Scoring/DecayCurve.cs` — new; abstract base + 3 sealed subtypes (~100 lines)
- `src/Wollax.Cupel/Scoring/DecayScorer.cs` — new; IScorer using System.TimeProvider (~90 lines)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 14 new entries
- `tests/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs` — new; 5 TUnit conformance tests (~120 lines)

## Forward Intelligence

### What the next slice should know
- The scorer pattern is now established in both languages: typed constructors validate preconditions, return Result/throw, name the offending parameter; `Scorer` trait impl in Rust and `IScorer` impl in .NET are the templates to follow for MetadataTrustScorer (S02)
- `build_scorer_by_type` in `conformance.rs` is the Rust conformance harness entry point — add a new arm for each new scorer type; the `FixedTimeProvider` pattern there shows how to inject test-time providers
- The TOML conformance vector layout for scorers is now documented: `[config]` holds scorer fields; curve-specific params go in `[config.curve]`; `[[config.curve.windows]]` is array-of-tables for multi-entry configs

### What's fragile
- `PublicAPI.Unshipped.txt` requires the protected constructor of abstract base classes — RS0016 will block the build silently if a new abstract class is added without listing its protected ctor; always check with `dotnet build` immediately after adding a new public abstract type
- Step curve fallthrough behavior (items older than all windows get the last window's score) is implicitly spec-verified by the conformance vector but not independently documented with a test name that says "fallthrough" — a future reader might not realize this is intentional behavior

### Authoritative diagnostics
- `cargo test -- decay_ --nocapture` — runs exactly the 5 decay tests with full output; fastest signal for Rust scorer correctness
- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` — canonical drift guard; run after any vector edit; non-empty output = drift = failure
- `dotnet build 2>&1 | grep " error "` — catches PublicAPI analyzer violations and build errors before running tests

### What assumptions changed
- Plan assumed `.Duration()` for age clamping in .NET — this is wrong; `.Duration()` returns the absolute value, which would give positive age to future timestamps. The correct pattern (confirmed by Rust reference impl) is an explicit zero-clamp check.
