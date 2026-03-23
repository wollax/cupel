# S02: MetadataTrustScorer — Rust + .NET Implementation

**Goal:** Implement `MetadataTrustScorer` in both Rust and .NET; extend the conformance harness to support `metadata` in TOML test vectors; author 5 conformance vectors covering all spec-defined edge cases; ship passing tests in both languages with drift guard satisfied.

**Demo:** A `MetadataTrustScorer` can be constructed in both Rust and .NET, returns `cupel:trust` float values clamped to [0.0, 1.0], falls back to `defaultScore` on key absence / parse failure / non-finite, and all 5 TOML conformance vectors pass in Rust; 5 TUnit tests pass in .NET; `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` exits clean.

## Must-Haves

- `MetadataTrustScorer` struct exists in `crates/cupel/src/scorer/metadata_trust.rs` implementing `Scorer` trait; re-exported from `scorer/mod.rs` and `lib.rs`
- `build_items` in `crates/cupel/tests/conformance.rs` parses `metadata` inline tables from `[[items]]` TOML blocks and passes them to `ContextItemBuilder::metadata()`
- `"metadata_trust"` arm added to `build_scorer_by_type` reading `config.default_score`
- 5 TOML conformance vectors exist in `spec/conformance/required/scoring/` (present+valid, key-absent, unparseable, out-of-range-high, non-finite)
- Vectors copied verbatim to `crates/cupel/conformance/required/scoring/`; drift guard diff exits 0
- 5 `#[test]` functions in `crates/cupel/tests/conformance/scoring.rs` — all pass
- `cargo test --all-targets` exits 0
- `MetadataTrustScorer` class exists in `src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs` implementing `IScorer`; handles `double`, `string`, and any-other-type cases (D059); exports from Wollax.Cupel package
- `PublicAPI.Unshipped.txt` updated with 3 entries (class, constructor, Score method)
- `dotnet test` exits 0 (includes 5 new MetadataTrustScorerTests)

## Proof Level

- This slice proves: contract
- Real runtime required: no
- Human/UAT required: no

## Verification

- `cargo test --all-targets` — all tests pass including the 5 new metadata_trust conformance tests
- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` — no output (zero drift)
- `grep -c "fn metadata_trust_" crates/cupel/tests/conformance/scoring.rs` — prints `5`
- `dotnet test` — all tests pass (expected: 668+ passing)
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` — 0 errors, 0 warnings (PublicAPI compliance)
- `grep "MetadataTrustScorer" src/Wollax.Cupel/PublicAPI.Unshipped.txt | wc -l` — prints `3`

## Observability / Diagnostics

- Runtime signals: `CupelError::ScorerConfig(String)` on Rust construction failure naming `"defaultScore"`; `ArgumentOutOfRangeException` on .NET construction failure naming `nameof(defaultScore)`
- Inspection surfaces: `cargo test -- metadata_trust --nocapture` runs only the new conformance tests; `dotnet test --filter "MetadataTrust"` runs .NET tests in isolation
- Failure visibility: construction errors name the offending parameter; scoring fallback to `defaultScore` is silent by design (algorithm spec — not a failure mode)
- Redaction constraints: none (no secrets or PII in scorer logic)

## Integration Closure

- Upstream surfaces consumed: `Scorer` trait (`crates/cupel/src/scorer/mod.rs`), `CupelError::ScorerConfig` (`crates/cupel/src/error.rs`), `ContextItemBuilder::metadata()` (`crates/cupel/src/model/context_item.rs`), `IScorer` interface (`src/Wollax.Cupel/Scoring/`), `build_items` / `build_scorer_by_type` patterns from S01 (`crates/cupel/tests/conformance.rs`)
- New wiring introduced in this slice: `metadata` field parsing in `build_items` (enables metadata-bearing conformance vectors for all future scorers); `MetadataTrustScorer` registered in both language scorer tables
- What remains before the milestone is truly usable end-to-end: S03 (CountQuotaSlice), S04 (analytics + Cupel.Testing), S05 (OTel bridge), S06 (budget sim + tiebreaker)

## Tasks

- [x] **T01: Rust MetadataTrustScorer + conformance harness metadata extension + 5 TOML vectors** `est:25m`
  - Why: Delivers the complete Rust side: scorer implementation, harness extension enabling metadata in vectors, 5 conformance vectors with passing tests, and drift guard satisfied.
  - Files: `crates/cupel/src/scorer/metadata_trust.rs`, `crates/cupel/src/scorer/mod.rs`, `crates/cupel/src/lib.rs`, `crates/cupel/tests/conformance.rs`, `crates/cupel/tests/conformance/scoring.rs`, `spec/conformance/required/scoring/metadata-trust-*.toml` (×5), `crates/cupel/conformance/required/scoring/metadata-trust-*.toml` (×5)
  - Do: Create `metadata_trust.rs` with `MetadataTrustScorer { default_score: f64 }` implementing `Scorer`; validate `default_score ∈ [0.0, 1.0]` at construction returning `CupelError::ScorerConfig("defaultScore must be in [0.0, 1.0]")`; in `score()`: get `item.metadata().get("cupel:trust")` → if absent return `default_score`; parse with `str::parse::<f64>()` → if `Err` return `default_score`; if `!value.is_finite()` return `default_score`; return `value.clamp(0.0, 1.0)`. **Critical:** `is_finite()` check MUST follow parse (not replace it) because `"NaN".parse::<f64>()` returns `Ok(f64::NAN)`. Add `mod metadata_trust; pub use metadata_trust::MetadataTrustScorer;` to `scorer/mod.rs`; add to scorer table doc comment row. Add `MetadataTrustScorer` to lib.rs pub use. In `conformance.rs` `build_items`: add block reading `item.get("metadata")` as TOML table, extracting `HashMap<String, String>` and calling `builder.metadata(...)`. Add `"metadata_trust"` arm to `build_scorer_by_type` reading `config.get("default_score").and_then(|v| v.as_float()).unwrap_or(0.5)`. Write 5 TOML vectors (inline table format: `metadata = { "cupel:trust" = "0.85" }`). Copy vectors verbatim to both `spec/conformance/` and `crates/cupel/conformance/`. Add 5 `#[test]` functions in `conformance/scoring.rs` following the `run_scoring_test` pattern.
  - Verify: `cargo test --all-targets` exits 0; `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` produces no output; `grep -c "fn metadata_trust_" crates/cupel/tests/conformance/scoring.rs` prints `5`
  - Done when: `cargo test --all-targets` exits 0, drift guard is clean, 5 metadata_trust test functions exist

- [x] **T02: .NET MetadataTrustScorer + 5 TUnit tests + PublicAPI update** `est:20m`
  - Why: Delivers the complete .NET side: scorer class with D059 dual-type handling, updated public API manifest, and 5 passing tests covering all conformance scenarios.
  - Files: `src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`, `tests/Wollax.Cupel.Tests/Scoring/MetadataTrustScorerTests.cs`
  - Do: Create `MetadataTrustScorer.cs` as a `public sealed class` implementing `IScorer`. Constructor takes `double defaultScore = 0.5`; throw `ArgumentOutOfRangeException(nameof(defaultScore))` if outside [0.0, 1.0]. `Score()`: call `item.Metadata.TryGetValue("cupel:trust", out var raw)` → if not found return `_defaultScore`; if `raw is double d` use `d` directly; else if `raw is string s` use `double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)` returning `_defaultScore` on failure and assigning `parsed` to `value`; else return `_defaultScore`. After type dispatch: if `!double.IsFinite(value)` return `_defaultScore`; return `Math.Clamp(value, 0.0, 1.0)`. Add 3 entries to `PublicAPI.Unshipped.txt`: class declaration, constructor, Score method. Run `dotnet build` immediately after adding the class to catch RS0016 before test authoring. Write `MetadataTrustScorerTests.cs` with 5 TUnit `[Test]` methods covering: present+valid (0.85→0.85), key-absent (→defaultScore=0.5), unparseable ("high"→defaultScore), out-of-range-high (1.5→1.0), non-finite ("NaN"→defaultScore). Use a `ContextItem` with `Metadata = new Dictionary<string, object?> { ["cupel:trust"] = value }` pattern. Also add one test verifying that a native `double` value in metadata is accepted directly (D059).
  - Verify: `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` exits 0 with 0 errors; `dotnet test` exits 0; `grep "MetadataTrustScorer" src/Wollax.Cupel/PublicAPI.Unshipped.txt | wc -l` prints `3`
  - Done when: `dotnet build` exits 0, `dotnet test` exits 0, 3 PublicAPI entries present for MetadataTrustScorer

## Files Likely Touched

- `crates/cupel/src/scorer/metadata_trust.rs` — new
- `crates/cupel/src/scorer/mod.rs` — add mod + pub use + doc table row
- `crates/cupel/src/lib.rs` — add MetadataTrustScorer to pub use
- `crates/cupel/tests/conformance.rs` — build_items metadata extension + metadata_trust arm
- `crates/cupel/tests/conformance/scoring.rs` — 5 new test functions
- `spec/conformance/required/scoring/metadata-trust-present-valid.toml` — new
- `spec/conformance/required/scoring/metadata-trust-key-absent.toml` — new
- `spec/conformance/required/scoring/metadata-trust-unparseable.toml` — new
- `spec/conformance/required/scoring/metadata-trust-out-of-range-high.toml` — new
- `spec/conformance/required/scoring/metadata-trust-non-finite.toml` — new
- `crates/cupel/conformance/required/scoring/metadata-trust-*.toml` — 5 verbatim copies
- `src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs` — new
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 3 new entries
- `tests/Wollax.Cupel.Tests/Scoring/MetadataTrustScorerTests.cs` — new
