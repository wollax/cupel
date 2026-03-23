---
id: T02
parent: S02
milestone: M003
provides:
  - MetadataTrustScorer sealed class implementing IScorer in src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs
  - Constructor with defaultScore parameter, ArgumentOutOfRangeException on out-of-range value
  - Score() with D059 dual-type dispatch (double first, then string via TryParse InvariantCulture)
  - IsFinite() guard after dispatch; Math.Clamp(value, 0.0, 1.0) on valid finite values
  - 3 PublicAPI.Unshipped.txt entries for the new class
  - 6 TUnit tests in MetadataTrustScorerTests.cs including D059 native-double test
key_files:
  - src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Scoring/MetadataTrustScorerTests.cs
key_decisions:
  - D059 enforced: double branch checked before string branch in Score() — ensures native double callers receive their value directly rather than falling through to defaultScore
patterns_established:
  - MetadataTrustScorer follows ReflexiveScorer pattern (sealed class, IsFinite, Math.Clamp) extended with constructor-validated defaultScore and D059 dual-type metadata dispatch
observability_surfaces:
  - Construction failure throws ArgumentOutOfRangeException(nameof(defaultScore), defaultScore, "defaultScore must be in [0.0, 1.0]") — parameter name and bad value included
  - dotnet test --project tests/Wollax.Cupel.Tests -- --treenode-filter "*MetadataTrust*" (note: filter runs 0 tests via dotnet test CLI due to TUnit filter behaviour; use full dotnet test instead)
duration: 10min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T02: .NET MetadataTrustScorer + 5 TUnit Tests + PublicAPI Update

**MetadataTrustScorer implemented in .NET with D059 dual-type dispatch, IsFinite guard, clamp, and 6 passing TUnit tests; all 669 suite tests green.**

## What Happened

Created `MetadataTrustScorer` as a `sealed class` implementing `IScorer`. The constructor accepts `defaultScore` (default 0.5) and throws `ArgumentOutOfRangeException` naming `defaultScore` if outside [0.0, 1.0]. `Score()` looks up `cupel:trust` in `item.Metadata`; if absent returns `_defaultScore`. Type dispatch follows D059: `double` branch first, then `string` via `double.TryParse(InvariantCulture)`, else `_defaultScore`. After dispatch, `double.IsFinite()` guards against NaN/Infinity before `Math.Clamp(value, 0.0, 1.0)`.

Added 3 entries to `PublicAPI.Unshipped.txt`. Build passed immediately with 0 errors/warnings (RS0016 PublicAPI compliance confirmed).

Wrote 6 TUnit tests: 5 conformance scenarios (present+valid string, key absent, unparseable, out-of-range-high, NaN) plus D059 native-double test. All 669 total tests pass.

## Verification

- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` → Build succeeded, 0 Warning(s), 0 Error(s)
- `grep "MetadataTrustScorer" src/Wollax.Cupel/PublicAPI.Unshipped.txt | wc -l` → 3
- `dotnet test` → total: 669, failed: 0 (up from 663 prior to this task — 6 new tests)

## Diagnostics

- Construction failure: `ArgumentOutOfRangeException(nameof(defaultScore), defaultScore, "defaultScore must be in [0.0, 1.0]")` — includes parameter name and offending value
- Filter for MetadataTrust tests: TUnit's `--treenode-filter` did not match tests with `dotnet test` (zero tests ran). Run full `dotnet test` and inspect output manually, or use `dotnet test 2>&1 | grep MetadataTrust`

## Deviations

The `--filter "MetadataTrust"` and `--treenode-filter "*MetadataTrust*"` TUnit filter approaches both returned zero tests. This is a known TUnit CLI quirk. All 6 new tests are verified to pass via `dotnet test` (669 total, 0 failed). No functional deviation from the plan.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs` — new file: MetadataTrustScorer sealed class with D059 dual-type dispatch, IsFinite guard, Math.Clamp (~55 lines)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 3 new entries appended for MetadataTrustScorer
- `tests/Wollax.Cupel.Tests/Scoring/MetadataTrustScorerTests.cs` — new file: 6 TUnit test methods (~95 lines)
