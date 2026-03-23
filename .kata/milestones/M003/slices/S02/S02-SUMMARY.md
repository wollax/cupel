---
id: S02
parent: M003
milestone: M003
provides:
  - MetadataTrustScorer struct in Rust (crates/cupel/src/scorer/metadata_trust.rs) implementing Scorer trait
  - MetadataTrustScorer sealed class in .NET (src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs) implementing IScorer
  - NaN-safe scoring: is_finite() placed after parse() because "NaN".parse::<f64>() returns Ok(NaN)
  - D059 dual-type dispatch in .NET: double branch checked before string branch
  - Constructor validation: CupelError::ScorerConfig (Rust) / ArgumentOutOfRangeException (NET) on out-of-range defaultScore
  - Conformance harness extended: build_items() parses metadata inline tables; "metadata_trust" arm added to build_scorer_by_type
  - 5 TOML conformance vectors in all three canonical locations (spec/, conformance/, crates/cupel/conformance/)
  - 5 Rust conformance tests in crates/cupel/tests/conformance/scoring.rs; 6 .NET TUnit tests
  - 3 PublicAPI.Unshipped.txt entries for MetadataTrustScorer
  - Drift guard satisfied: diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring exits 0
requires:
  - slice: S01
    provides: Scorer trait impl structure (Rust) and IScorer impl structure (.NET); conformance harness build_scorer_by_type pattern; CupelError::ScorerConfig variant
affects:
  - S03
  - S04
  - S05
  - S06
key_files:
  - crates/cupel/src/scorer/metadata_trust.rs
  - crates/cupel/src/scorer/mod.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/conformance.rs
  - crates/cupel/tests/conformance/scoring.rs
  - src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Scoring/MetadataTrustScorerTests.cs
  - spec/conformance/required/scoring/metadata-trust-present-valid.toml
  - spec/conformance/required/scoring/metadata-trust-key-absent.toml
  - spec/conformance/required/scoring/metadata-trust-unparseable.toml
  - spec/conformance/required/scoring/metadata-trust-out-of-range-high.toml
  - spec/conformance/required/scoring/metadata-trust-non-finite.toml
  - conformance/required/scoring/metadata-trust-*.toml (5 files — root canonical dir)
  - crates/cupel/conformance/required/scoring/metadata-trust-*.toml (5 files)
key_decisions:
  - D078: S02 verification strategy — contract-level only (cargo test, dotnet test, drift guard)
  - D079: TOML vector metadata format — inline table `metadata = { "cupel:trust" = "0.85" }` on [[items]] entries
  - D080: .NET ArgumentOutOfRangeException for defaultScore (not ArgumentException — numeric range violation)
  - D081: is_finite() MUST follow parse() — NaN successfully parses so the two guards are orthogonal
  - D082: Vectors must be in THREE locations — pre-commit hook checks root conformance/ vs crates/cupel/conformance/
  - D083: D059 double branch before string branch — native double callers must not fall through to TryParse
patterns_established:
  - MetadataTrustScorer scorer pattern: key lookup → parse (if string) → is_finite guard → clamp; follows ReflexiveScorer shape
  - D059 dual-type metadata dispatch: double first, string second, else fallback — reusable for future metadata-reading scorers
  - build_items metadata extension: filter_map over table entries keeping only string values; skip builder.metadata() when map is empty
  - Three-location vector sync: spec/, root conformance/, crates/cupel/conformance/ — all must stay in sync for pre-commit hook
observability_surfaces:
  - cargo test -- metadata_trust --nocapture — isolates 5 Rust conformance tests
  - diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring — drift guard (zero output = pass)
  - diff -r conformance/required/scoring crates/cupel/conformance/required/scoring — pre-commit hook canonical check
  - CupelError::ScorerConfig("defaultScore must be in [0.0, 1.0]") — Rust construction failure
  - ArgumentOutOfRangeException(nameof(defaultScore)) — .NET construction failure with parameter name and bad value
drill_down_paths:
  - .kata/milestones/M003/slices/S02/tasks/T01-SUMMARY.md
  - .kata/milestones/M003/slices/S02/tasks/T02-SUMMARY.md
duration: 30min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
---

# S02: MetadataTrustScorer — Rust + .NET Implementation

**`MetadataTrustScorer` implemented in both Rust and .NET with NaN-safe scoring, D059 dual-type dispatch, 5 conformance vectors in all three canonical locations, and 669 total tests passing.**

## What Happened

**T01 — Rust:** Created `MetadataTrustScorer { default_score: f64 }` implementing the `Scorer` trait. The `new()` constructor validates `[0.0, 1.0]` and returns `CupelError::ScorerConfig` on violation. `score()` performs the three-step fallback: key lookup in `item.metadata()` → `str::parse::<f64>()` → `is_finite()` check → `clamp(0.0, 1.0)`. The critical design choice (D081): `is_finite()` is placed *after* parse because `"NaN".parse::<f64>()` returns `Ok(f64::NAN)` — a NaN successfully parses.

Wired into `scorer/mod.rs` (new `mod metadata_trust` + `pub use` + doc table row) and `lib.rs`. Extended `build_items` in `conformance.rs` to parse `metadata` inline tables via filter_map over key-value pairs, skipping non-string values. Added `"metadata_trust"` arm to `build_scorer_by_type`.

Authored 5 TOML vectors covering all spec-defined edge cases: present-valid (0.85→0.85), key-absent (→0.5), unparseable ("high"→0.5), out-of-range-high (1.5→1.0), and non-finite ("NaN"→0.5). **Deviation discovered:** The pre-commit hook checks root `conformance/` vs `crates/cupel/conformance/`, not `spec/conformance/`. Vectors were copied to all three locations.

**T02 — .NET:** Created `MetadataTrustScorer` as a `sealed class` implementing `IScorer`. Constructor accepts `defaultScore` (default 0.5), throws `ArgumentOutOfRangeException` naming the parameter and including the offending value. `Score()` implements D059 dual-type dispatch: `double` branch checked before `string` branch to ensure native double callers receive their value directly without falling through to string parsing. `double.IsFinite()` guards after dispatch; `Math.Clamp(value, 0.0, 1.0)` on valid finite values.

Added 3 PublicAPI.Unshipped.txt entries; build passed immediately (0 RS0016 errors). Wrote 6 TUnit tests (5 conformance scenarios + D059 native-double test). Total suite: 669 tests, 0 failed.

## Verification

- `cargo test --all-targets` — 43 passed, 0 failed
- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` — no output (zero drift)
- `grep -c "fn metadata_trust_" crates/cupel/tests/conformance/scoring.rs` — 5
- `dotnet test` — 669 passed, 0 failed (up from 663 prior to S02)
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` — 0 errors, 0 warnings
- `grep "MetadataTrustScorer" src/Wollax.Cupel/PublicAPI.Unshipped.txt | wc -l` — 3

## Requirements Advanced

- R042 (Metadata convention system spec) — implementation complete for `cupel:trust` convention in both languages; all 5 conformance vector outlines from the spec chapter now have passing implementations

## Requirements Validated

- R042 — `MetadataTrustScorer` implemented in both Rust and .NET; `cupel:trust` float convention with string storage, configurable `defaultScore`, explicit parse-failure/non-finite handling; 5 conformance vectors passing; drift guard clean; `cargo test --all-targets` → 43 passed; `dotnet test` → 669 passed

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

**T01 — Three-location vector sync:** The task plan stated vectors should go in `spec/conformance/` and `crates/cupel/conformance/` only. The pre-commit hook enforces sync between root `conformance/required/` and `crates/cupel/conformance/required/`. Vectors were copied to all three locations; the commit succeeded. D082 captures this as a project-wide convention.

**T02 — TUnit filter:** The `--treenode-filter "*MetadataTrust*"` TUnit filter returned zero tests via `dotnet test` CLI. All 6 new tests were verified via full `dotnet test` (669 total, 0 failed). Not a functional deviation — TUnit CLI filter behaviour is a known quirk.

## Known Limitations

- `cupel:source-type` convention (also part of R042 metadata spec) is not implemented as a scorer — the spec defines it as a string enum for callers to filter on, not a built-in scorer. This is by design.
- Metadata key lookup is case-sensitive (`cupel:trust` exact match only). The spec does not mention case insensitivity; this matches the spec.

## Follow-ups

- None blocking downstream slices. S03 (CountQuotaSlice) depends on S01, not S02.

## Files Created/Modified

- `crates/cupel/src/scorer/metadata_trust.rs` — new: MetadataTrustScorer struct + Scorer impl + unit tests (~120 lines)
- `crates/cupel/src/scorer/mod.rs` — added mod metadata_trust; pub use MetadataTrustScorer; doc table row
- `crates/cupel/src/lib.rs` — added MetadataTrustScorer to pub use scorer export list
- `crates/cupel/tests/conformance.rs` — build_items metadata block; "metadata_trust" arm; MetadataTrustScorer import
- `crates/cupel/tests/conformance/scoring.rs` — 5 new #[test] functions for metadata_trust
- `spec/conformance/required/scoring/metadata-trust-*.toml` — 5 new TOML vectors
- `conformance/required/scoring/metadata-trust-*.toml` — 5 copies (root canonical dir, required by pre-commit hook)
- `crates/cupel/conformance/required/scoring/metadata-trust-*.toml` — 5 verbatim copies
- `src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs` — new: sealed class with D059 dual-type dispatch, IsFinite guard, Math.Clamp (~55 lines)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 3 new entries for MetadataTrustScorer
- `tests/Wollax.Cupel.Tests/Scoring/MetadataTrustScorerTests.cs` — new: 6 TUnit test methods (~95 lines)

## Forward Intelligence

### What the next slice should know
- The three-location conformance vector convention (D082) applies to all future scorer slices — vectors must land in `spec/conformance/`, root `conformance/`, and `crates/cupel/conformance/` to pass the pre-commit hook
- The build_items metadata extension is now in place — future scorers that use metadata fields can reference `metadata` in their TOML conformance vectors without any additional harness changes
- `MetadataTrustScorer` is re-exported from `lib.rs` and `scorer/mod.rs` — the export pattern is established; follow the same pattern for future scorers

### What's fragile
- The `metadata` field in TOML conformance vectors only supports string values (non-string entries are silently skipped in `build_items`). If a future scorer requires non-string metadata values, `build_items` will need extension.
- TUnit's `--treenode-filter` CLI flag does not reliably filter tests via `dotnet test`. Use full `dotnet test` or `dotnet test 2>&1 | grep TestName` for isolation.

### Authoritative diagnostics
- `diff -r conformance/required/scoring crates/cupel/conformance/required/scoring` — this is what the pre-commit hook actually checks (root vs crates/cupel, NOT spec vs crates/cupel)
- `cargo test -- metadata_trust --nocapture` — isolates exactly the 5 new conformance tests plus 1 doctest
- `grep "MetadataTrustScorer" src/Wollax.Cupel/PublicAPI.Unshipped.txt | wc -l` — must print 3 for RS0016 compliance

### What assumptions changed
- Task plan assumed two vector locations (spec/ + crates/cupel/conformance/); the pre-commit hook requires a third root location. D082 now documents this as the project-wide convention.
