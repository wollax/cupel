---
estimated_steps: 5
estimated_files: 3
---

# T02: .NET MetadataTrustScorer + 5 TUnit Tests + PublicAPI Update

**Slice:** S02 — MetadataTrustScorer — Rust + .NET Implementation
**Milestone:** M003

## Description

Implement `MetadataTrustScorer` in .NET, handling both `double` and `string` values in `IReadOnlyDictionary<string, object?>` per D059; update `PublicAPI.Unshipped.txt` with 3 new entries; write 5 TUnit tests covering all conformance scenarios plus one D059 dual-type test. This task completes the .NET side of S02.

The primary pattern comes from `ReflexiveScorer.cs`: `sealed class` implementing `IScorer`, `double.IsFinite()` check, `Math.Clamp(value, 0.0, 1.0)`. Added complexity: constructor with `defaultScore` parameter and validation; D059 type dispatch in `Score()`.

**D059 dispatch order (mandatory):** check `double` before `string`. If the order is reversed, callers who pass `0.85` as a native `double` get `defaultScore` instead of `0.85`.

## Steps

1. Create `src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs` as `public sealed class MetadataTrustScorer : IScorer`. Constructor: `public MetadataTrustScorer(double defaultScore = 0.5)` storing `_defaultScore`; throw `new ArgumentOutOfRangeException(nameof(defaultScore), defaultScore, "defaultScore must be in [0.0, 1.0]")` if `defaultScore < 0.0 || defaultScore > 1.0`. `Score()`: call `item.Metadata.TryGetValue("cupel:trust", out var raw)` → if not found return `_defaultScore`. Type dispatch on `raw`:
   - `if (raw is double d) { value = d; }`
   - `else if (raw is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) { value = parsed; }`
   - `else return _defaultScore;`
   After dispatch: `if (!double.IsFinite(value)) return _defaultScore;` then `return Math.Clamp(value, 0.0, 1.0)`.

2. Add 3 entries to `src/Wollax.Cupel/PublicAPI.Unshipped.txt`:
   - `Wollax.Cupel.Scoring.MetadataTrustScorer`
   - `Wollax.Cupel.Scoring.MetadataTrustScorer.MetadataTrustScorer(double defaultScore = 0.5) -> void`
   - `Wollax.Cupel.Scoring.MetadataTrustScorer.Score(Wollax.Cupel.ContextItem! item, System.Collections.Generic.IReadOnlyList<Wollax.Cupel.ContextItem!>! allItems) -> double`

3. Run `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` immediately — verify 0 errors (catches RS0016 PublicAPI analyzer violations before test authoring).

4. Create `tests/Wollax.Cupel.Tests/Scoring/MetadataTrustScorerTests.cs` with 6 TUnit `[Test]` methods:
   - `PresentAndValid_ReturnsClampedValue`: metadata `{"cupel:trust": "0.85"}` → score ≈ 0.85
   - `KeyAbsent_ReturnsDefaultScore`: item has no `cupel:trust` key → score = 0.5
   - `UnparseableValue_ReturnsDefaultScore`: `{"cupel:trust": "high"}` → score = 0.5
   - `OutOfRangeHigh_ReturnsClamped`: `{"cupel:trust": "1.5"}` → score = 1.0
   - `NonFiniteNaN_ReturnsDefaultScore`: `{"cupel:trust": "NaN"}` → score = 0.5
   - `NativeDoubleValue_AcceptedDirectly`: metadata value is native `double` 0.75 (not string) → score = 0.75 (D059)

5. Run `dotnet test` — verify 0 failures (expected: 668+ total, 5 new MetadataTrust tests).

## Must-Haves

- [ ] `MetadataTrustScorer` sealed class implementing `IScorer` exists in `src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs`
- [ ] Constructor throws `ArgumentOutOfRangeException` naming `defaultScore` if outside [0.0, 1.0]
- [ ] `Score()` handles `double` natively, `string` via `TryParse(InvariantCulture)`, any other type → `_defaultScore` (D059 order: double first)
- [ ] `IsFinite()` check follows type dispatch; non-finite values return `_defaultScore`, not clamped boundary
- [ ] 3 entries in `PublicAPI.Unshipped.txt` for the new class
- [ ] `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` exits 0 with 0 errors
- [ ] 6 TUnit tests in `MetadataTrustScorerTests.cs` including D059 native-double test
- [ ] `dotnet test` exits 0

## Verification

- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` — 0 errors, 0 warnings (PublicAPI compliance)
- `grep "MetadataTrustScorer" src/Wollax.Cupel/PublicAPI.Unshipped.txt | wc -l` — prints `3`
- `dotnet test --filter "MetadataTrust"` — 6 tests, all pass
- `dotnet test` — all tests pass (≥668 total)

## Observability Impact

- Signals added/changed: `ArgumentOutOfRangeException(nameof(defaultScore), ...)` on construction failure with out-of-range default
- How a future agent inspects this: `dotnet test --filter "MetadataTrust" --logger "console;verbosity=detailed"` runs only the 6 new tests with full output
- Failure state exposed: build failure from RS0016 is deterministic and caught by `dotnet build` immediately after class creation; type-dispatch bugs surface as unexpected `defaultScore` returns in the D059 native-double test

## Inputs

- `src/Wollax.Cupel/Scoring/ReflexiveScorer.cs` — algorithmic pattern: sealed class, `IsFinite`, `Math.Clamp`
- `src/Wollax.Cupel/Scoring/DecayScorer.cs` — construction validation pattern: `ArgumentOutOfRangeException(nameof(param))`
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — current entries, format reference for new additions
- `tests/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs` — test structure: TUnit `[Test]`, `FakeTimeProvider` pattern (not needed here but shows the assertion style)
- `src/Wollax.Cupel/ContextItem.cs` — `Metadata` property: `IReadOnlyDictionary<string, object?>`
- D059 decision record — mandatory dual-type handling in .NET; `double` before `string` in dispatch order

## Expected Output

- `src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs` — new file: `MetadataTrustScorer` sealed class + D059 type dispatch + `IsFinite` + `Math.Clamp` (~50 lines)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 3 new entries appended
- `tests/Wollax.Cupel.Tests/Scoring/MetadataTrustScorerTests.cs` — new file: 6 TUnit test methods (~80 lines)
