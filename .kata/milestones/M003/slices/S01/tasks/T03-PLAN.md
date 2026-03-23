---
estimated_steps: 6
estimated_files: 4
---

# T03: Implement DecayScorer .NET

**Slice:** S01 — DecayScorer — Rust + .NET Implementation
**Milestone:** M003

## Description

Implement the .NET side of `DecayScorer`: a `DecayCurve` sealed abstract class hierarchy and `DecayScorer` class implementing `IScorer`. Uses `System.TimeProvider` (BCL, .NET 8+) — no NuGet dependency. Update `PublicAPI.Unshipped.txt` for every new public type and member (required by PublicApiAnalyzers + TreatWarningsAsErrors). Write unit tests covering the 5 conformance scenarios.

Note on `DecayCurve` modelling: Use a `public abstract class DecayCurve` with three nested `public sealed class` subtypes (`Exponential`, `Window`, `Step`) — each validating preconditions in the constructor and throwing `ArgumentException` (not `ArgumentOutOfRangeException`) naming the parameter, matching the spec requirement. Record syntax is acceptable for the subtypes if the project targets .NET 10.

## Steps

1. Create `src/Wollax.Cupel/Scoring/DecayCurve.cs`:
   - `public abstract class DecayCurve` (no public constructor needed; use `protected DecayCurve() {}`).
   - Nested `public sealed class Exponential : DecayCurve` with constructor `(TimeSpan HalfLife)`: throw `ArgumentException("halfLife must be > TimeSpan.Zero", nameof(halfLife))` if `HalfLife <= TimeSpan.Zero`.
   - Nested `public sealed class Window : DecayCurve` with constructor `(TimeSpan MaxAge)`: throw `ArgumentException("maxAge must be > TimeSpan.Zero", nameof(maxAge))` if `MaxAge <= TimeSpan.Zero`.
   - Nested `public sealed class Step : DecayCurve` with constructor `(IReadOnlyList<(TimeSpan MaxAge, double Score)> Windows)`: throw `ArgumentException("windows must not be empty", nameof(windows))` if empty; throw `ArgumentException("windows must not contain zero-width entries", nameof(windows))` if any `MaxAge <= TimeSpan.Zero`.
   - Store validated values in read-only properties.

2. Create `src/Wollax.Cupel/Scoring/DecayScorer.cs`:
   - `public sealed class DecayScorer : IScorer` with constructor `(System.TimeProvider timeProvider, DecayCurve curve, double nullTimestampScore = 0.5)`.
   - Validate `nullTimestampScore` in [0.0, 1.0]; throw `ArgumentOutOfRangeException(nameof(nullTimestampScore), "must be in [0.0, 1.0]")` if outside range.
   - Store all three fields.
   - Implement `Score(ContextItem item, IReadOnlyList<ContextItem> allItems)`:
     - If `item.Timestamp` is null: return `nullTimestampScore`.
     - Compute `age = (timeProvider.GetUtcNow() - item.Timestamp.Value).Duration()` then clamp: `if (age < TimeSpan.Zero) age = TimeSpan.Zero`.
     - Dispatch to curve:
       - `Exponential(h)`: `Math.Pow(2.0, -(age.TotalSeconds / h.HalfLife.TotalSeconds))`
       - `Window(m)`: `age < m.MaxAge ? 1.0 : 0.0`
       - `Step(windows)`: walk from youngest, return first where `w.MaxAge > age`; fall through to last entry's score.
     - `allItems` parameter accepted but not iterated.

3. Update `src/Wollax.Cupel/PublicAPI.Unshipped.txt`. Check `PublicAPI.Shipped.txt` for RecencyScorer format. Add entries for:
   - `Wollax.Cupel.Scoring.DecayCurve` (abstract class)
   - `Wollax.Cupel.Scoring.DecayCurve.Exponential` (nested sealed class + constructor + `HalfLife` property)
   - `Wollax.Cupel.Scoring.DecayCurve.Window` (nested sealed class + constructor + `MaxAge` property)
   - `Wollax.Cupel.Scoring.DecayCurve.Step` (nested sealed class + constructor + `Windows` property)
   - `Wollax.Cupel.Scoring.DecayScorer` (sealed class + constructor + `Score` method)

4. Create `src/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs`. Use a simple `FakeTimeProvider` subclass that overrides `GetUtcNow()` to return a fixed `DateTimeOffset`. Write tests covering the 5 conformance scenarios:
   - Exponential half-life: age = 24h, halfLife = 24h → score ≈ 0.5 (within 1e-9)
   - Future-dated item: timestamp > referenceTime → score = 1.0
   - Null timestamp: no timestamp → score = `nullTimestampScore` (0.5)
   - Step second window: age = 6h, windows = [(1h,0.9),(24h,0.5),(72h,0.1)] → score = 0.5
   - Window at boundary: age = 6h, maxAge = 6h → score = 0.0

5. Run `dotnet build` to verify 0 errors and 0 warnings (PublicAPI analyzer enforces this).
6. Run `dotnet test` to verify all tests pass.

## Must-Haves

- [ ] `src/Wollax.Cupel/Scoring/DecayCurve.cs` exists with `DecayCurve` abstract base and 3 sealed subtypes; each subtype validates preconditions and throws `ArgumentException` naming the parameter
- [ ] `src/Wollax.Cupel/Scoring/DecayScorer.cs` exists implementing `IScorer`; uses `System.TimeProvider` (no NuGet); validates `nullTimestampScore`; clamps age before curve dispatch; ignores `allItems`
- [ ] `PublicAPI.Unshipped.txt` updated for all new public types, constructors, properties, and methods
- [ ] `src/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs` exists with 5 tests covering all conformance scenarios
- [ ] `dotnet build 2>&1 | grep " error " | wc -l` → 0
- [ ] `dotnet test 2>&1 | grep "Failed"` → empty (all tests pass)

## Verification

- `dotnet build 2>&1 | grep -E " error | warning "` → no output
- `dotnet test 2>&1 | tail -10` — all tests pass
- `grep -c "DecayScorer\|DecayCurve" src/Wollax.Cupel/PublicAPI.Unshipped.txt` → ≥ 8 lines (covers class, constructor, method, nested types)
- `grep -c "void Score\|Score(" src/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs` → tests present
- `grep "System.TimeProvider" src/Wollax.Cupel/Scoring/DecayScorer.cs` → present (BCL, no NuGet)

## Observability Impact

- Signals added/changed: `ArgumentException` / `ArgumentOutOfRangeException` on construction failure — names the parameter; surfaces immediately to callers; no silent misconfiguration
- How a future agent inspects this: `dotnet test --filter DecayScorerTests --verbosity normal` to run only decay tests; `dotnet build` diagnostic output pinpoints PublicAPI mismatches with file/line
- Failure state exposed: PublicAPI analyzer fails the build with an actionable message listing the missing or unexpected API entry

## Inputs

- `crates/cupel/src/scorer/decay.rs` — T01 output; reference for algorithm parity (curve formulas, null handling, age clamping, allItems ignored)
- `src/Wollax.Cupel/Scoring/RecencyScorer.cs` — canonical .NET scorer pattern (`public sealed class`, `IScorer`, single `Score` method)
- `src/Wollax.Cupel/PublicAPI.Shipped.txt` — exact format for API entries (line `Wollax.Cupel.Scoring.RecencyScorer.RecencyScorer() -> void` shows the pattern)
- `src/Wollax.Cupel/Scoring/TagScorer.cs` — constructor validation (`ArgumentException`) pattern
- S01-RESEARCH.md constraints: `System.TimeProvider` is BCL (.NET 8+); no NuGet addition to core; `FakeTimeProvider` is test-only subclass; `.Duration()` for age; `DateTimeOffset?` for `Timestamp`

## Expected Output

- `src/Wollax.Cupel/Scoring/DecayCurve.cs` — ~50-70 lines; abstract base + 3 sealed subtypes with precondition validation
- `src/Wollax.Cupel/Scoring/DecayScorer.cs` — ~60-80 lines; implements `IScorer`; uses `System.TimeProvider`; clamps age; dispatches to curve
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 8-12 new lines covering all new public API surface
- `src/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs` — ~80-120 lines; 5 tests; no external assertion libs (xUnit conventions only)
- `dotnet test` exits 0 with all tests including new ones passing
