# S01: DecayScorer — Rust + .NET Implementation — Research

**Researched:** 2026-03-23
**Domain:** Time-decay scoring, chrono integration, PublicAPI analyzer, TOML conformance harness
**Confidence:** HIGH

## Summary

S01 is a clean implementation task against a fully-locked spec (`spec/src/scorers/decay.md`). All
design decisions are settled (D042, D047, D070, D071). The major declared risk — `chrono` dependency
for Rust — is already retired: `chrono = "0.4"` is in `Cargo.toml` and resolved to 0.4.44 in
`Cargo.lock`. `DateTime<Utc>` is already used by `ContextItem`, so no new dependency or MSRV concern
exists.

The implementation follows the established scorer pattern in both languages closely. Rust needs a new
`crates/cupel/src/scorer/decay.rs` module with a `TimeProvider` trait, `SystemTimeProvider` ZST,
`DecayCurve` enum, and `DecayScorer` struct. .NET needs a `src/Wollax.Cupel/Scoring/DecayScorer.cs`
that takes `System.TimeProvider` (BCL, .NET 8+, no NuGet dependency) as a constructor argument. The
conformance harness needs to grow a `"decay"` arm in `build_scorer_by_type` that constructs a
`FixedTimeProvider` from the vector's `config.reference_time` field.

The .NET `PublicAPI.Unshipped.txt` file must be updated for every new public member — this is a
hard requirement enforced by `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `TreatWarningsAsErrors`
on. Missing entries will fail the build.

## Recommendation

Implement in task order: T01 Rust core → T02 Rust conformance vectors → T03 .NET implementation.
Each task is self-contained and independently verifiable. The Rust `TimeProvider` trait and
`SystemTimeProvider` ZST must be defined in the `decay.rs` module (not a separate file) since they
are exclusively consumed by `DecayScorer`.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Rust time dependency | `chrono = "0.4"` already in `Cargo.toml` | Already resolved to 0.4.44; use `DateTime<Utc>`, `Duration` directly |
| .NET system time | `System.TimeProvider` (BCL .NET 8+) | `TimeProvider.System` for production; subclass for tests; no NuGet needed in core |
| .NET test time control | Subclass `TimeProvider`, override `GetUtcNow()` | `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing` is available in tests only |
| Rust construction errors | `CupelError::ScorerConfig(String)` | Existing variant for invalid scorer config; matches pattern in `ScaledScorer`, `KindScorer` |
| .NET construction errors | `ArgumentException` | Spec requires `ArgumentException` at construction for invalid halfLife/maxAge/windows |

## Existing Code and Patterns

- `crates/cupel/src/scorer/recency.rs` — Canonical Rust scorer; zero-sized struct implementing `Scorer` trait; `score(&self, item, all_items) -> f64`; no state; `Send + Sync` via derive
- `crates/cupel/src/scorer/mod.rs` — Module re-exports: each scorer is `pub use <module>::<Type>`; DecayScorer, DecayCurve, TimeProvider, SystemTimeProvider all need entries here
- `crates/cupel/src/lib.rs` — Crate root re-exports: `pub use scorer::{..., DecayScorer, DecayCurve, TimeProvider, SystemTimeProvider};` needed
- `crates/cupel/src/error.rs` — `CupelError::ScorerConfig(String)` is the right variant for construction-time failures; follows `ScorerConfig("halfLife must be > 0")`
- `src/Wollax.Cupel/Scoring/RecencyScorer.cs` — .NET scorer pattern; `public sealed class`, implements `IScorer`, single `Score` method; follow exactly
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Must be updated for every new public type and member; failure to update causes build errors (PublicApiAnalyzers + TreatWarningsAsErrors)
- `spec/conformance/required/scoring/recency-basic.toml` — TOML vector format for scoring: `[test]`, `[[items]]`, `[[expected]]`, `[tolerance]`; use this as template for DecayScorer vectors
- `crates/cupel/tests/conformance.rs` — `build_scorer_by_type(scorer_type, config)` function needs a `"decay"` arm; existing arms (tag, composite, scaled) show how to read config fields from TOML `Value`
- `crates/cupel/tests/conformance/scoring.rs` — Where new `#[test]` functions for `decay_*` vectors go; follow `recency_basic()` / `run_scoring_test(path)` pattern
- `src/Wollax.Cupel/Scoring/TagScorer.cs` — Shows constructor validation (`ArgumentException`) pattern for .NET scorer with configuration

## Constraints

- **Rust `DecayScorer` is NOT `Clone`** (D047): stores `Box<dyn TimeProvider + Send + Sync>`; do not derive `Clone` or `Copy`
- **`TimeProvider` trait must be `Send + Sync`** (D047): required for use across thread boundaries
- **Zero external deps in `Wollax.Cupel` core** (D045): `System.TimeProvider` is BCL — fine; do not add any NuGet reference to the core project for this feature
- **`timeProvider` injection is mandatory, no default** (D042): no overload that silently defaults to `TimeProvider.System`; both Rust and .NET constructors require explicit injection
- **Rust `DecayCurve::Step` windows preconditions** (D070): empty list → `Err(ScorerConfig)` at construction; any entry with `maxAge = Duration::ZERO` → `Err(ScorerConfig)` at construction
- **Rust/`Window`/`Exponential` preconditions**: `halfLife > Duration::ZERO`, `maxAge > Duration::ZERO`; violation → `Err(ScorerConfig)` naming the parameter
- **`allItems` is ignored** (conformance note): DecayScorer is an absolute scorer; must accept but not iterate `all_items`/`allItems`
- **Negative age (future-dated items) clamps to `Duration::ZERO`** before curve application; the clamp is in the main `score` function, not inside each curve variant
- **`nullTimestampScore` must be in [0.0, 1.0]**; default 0.5; SHOULD reject construction if outside range
- **Conformance vectors live in both `spec/conformance/required/scoring/` and `crates/cupel/conformance/required/`** — same content, drift guard syncs them; author in both
- **.NET `Timestamp` field is `DateTimeOffset?`** (not `DateTime?`); `TimeProvider.GetUtcNow()` returns `DateTimeOffset`; age calculation: `(timeProvider.GetUtcNow() - item.Timestamp.Value).Duration()` then clamp to `TimeSpan.Zero`
- **MSRV 1.85 / Rust Edition 2024** — no syntax or features beyond that

## Common Pitfalls

- **Missing PublicAPI entries** — Every `public` type, constructor, property, and method in the .NET project must be added to `PublicAPI.Unshipped.txt`. Forgetting a single entry fails the build. Check `RecencyScorer` entries in `PublicAPI.Shipped.txt` for the exact format: `Wollax.Cupel.Scoring.DecayScorer.DecayScorer(System.TimeProvider! timeProvider, Wollax.Cupel.Scoring.DecayCurve! curve) -> void`, etc.
- **Step curve index confusion** — The spec uses strict `>`: `window.maxAge > age` (not `>=`). An item at exactly the boundary falls into the older window. Implement as a forward scan, return on first match, fall through to last entry.
- **Age clamping must happen before curve dispatch** — Future-dated items must clamp to `Duration::ZERO` in `DECAY-SCORE`, not inside each curve variant. If each variant handles clamping independently, the behaviour diverges.
- **Rust conformance harness: mock time provider** — `build_scorer_by_type` for `"decay"` must read `config.reference_time` (a TOML datetime) and construct a `FixedTimeProvider` (local struct in the test module) returning that fixed time. Without this, conformance vectors cannot specify a deterministic `referenceTime`. The `FixedTimeProvider` struct is test-local; it does not need to be exported from the crate.
- **Rust module registration** — After adding `decay.rs`, add `mod decay;` and `pub use decay::{...}` in `scorer/mod.rs`, and add to `pub use scorer::{...}` in `lib.rs`. Missing either registration causes compile errors that look like the type doesn't exist.
- **.NET `DecayCurve` sealed hierarchy** — The spec calls for a sealed class hierarchy. Use `public abstract class DecayCurve` with three nested `public sealed class Exponential(TimeSpan HalfLife)`, `Sealed class Window(TimeSpan MaxAge)`, `public sealed class Step(IReadOnlyList<(TimeSpan MaxAge, double Score)> Windows)`. Or use a sealed record hierarchy (preferred for .NET 10). Validate preconditions in each subtype's constructor.
- **`Window(maxAge)` boundary is half-open `[0, maxAge)`** (D071): age == maxAge returns 0.0, not 1.0. The spec is explicit.

## Open Risks

- **Conformance harness `FixedTimeProvider`**: The Rust test harness needs a mock `TimeProvider` that accepts a `DateTime<Utc>` at construction. This is a test-local type — not exported. Its construction from a TOML datetime string requires the same `parse_toml_datetime` helper already in `conformance.rs`. Low risk — straightforward 5-line struct.
- **`nullTimestampScore` parameter in conformance vector config**: The TOML vector format for `"decay"` scorer needs to encode `referenceTime`, `curve`, and optionally `nullTimestampScore`. The `build_scorer_by_type` match arm must parse all three. Pattern is established by `"kind"` and `"tag"` arms — medium complexity.
- **.NET `DecayCurve` variance in PublicAPI.txt**: Depending on whether `DecayCurve` is modelled as abstract class, sealed records, or sealed nested classes, the PublicAPI entries differ slightly. Decide at T03 authoring time; document in T03-PLAN. Sealed records are idiomatic in .NET 10 and produce cleaner PublicAPI entries.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust | (no relevant skill for stdlib patterns) | none found |
| .NET | (no relevant skill for BCL TimeProvider) | none found |

## Sources

- `spec/src/scorers/decay.md` — Locked spec; DECAY-SCORE pseudocode, all 5 conformance vector outlines, edge-case table, curve factory preconditions
- `crates/cupel/Cargo.toml` + `Cargo.lock` — `chrono = "0.4"` confirmed present at 0.4.44; no MSRV concern
- `crates/cupel/src/scorer/recency.rs` — Canonical Rust scorer pattern to replicate
- `crates/cupel/tests/conformance.rs` — `build_scorer_by_type` function to extend; `parse_toml_datetime` helper available
- `src/Wollax.Cupel/Scoring/RecencyScorer.cs` — Canonical .NET scorer pattern
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — API surface tracking; must be updated per new type
- `spec/conformance/required/scoring/recency-basic.toml` — TOML vector format reference for scoring stage
- D042, D047, D070, D071 — Decisions register (locked; no re-discussion)
