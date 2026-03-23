---
id: T03
parent: S01
milestone: M003
provides:
  - DecayCurve abstract class with Exponential, Window, Step sealed nested subtypes; each validates constructor args with ArgumentException naming the parameter
  - DecayScorer implementing IScorer; uses System.TimeProvider (BCL, no NuGet); clamps future-dated ages to zero before curve dispatch; ignores allItems
  - PublicAPI.Unshipped.txt updated with 14 entries covering all new public types, constructors, properties, and the Score method
  - 5 DecayScorerTests conformance tests (TUnit); all passing; covers exponential half-life, future-dated clamping, null timestamp, step second window, window boundary
key_files:
  - src/Wollax.Cupel/Scoring/DecayCurve.cs
  - src/Wollax.Cupel/Scoring/DecayScorer.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs
key_decisions:
  - "Used rawAge < TimeSpan.Zero ? TimeSpan.Zero : rawAge instead of .Duration() for age clamping — .Duration() returns absolute value so a +1h future timestamp would produce a positive age instead of being clamped to zero; matches Rust's raw_age.max(Duration::zero()) behaviour"
  - "Protected constructor DecayCurve() listed in PublicAPI.Unshipped.txt — PublicApiAnalyzers RS0016 requires it even though it serves only to prevent external subclassing"
  - "Step curve uses > not >= for window boundary check (age < MaxAge for Exponential/Window, MaxAge > age for Step) — window boundary at boundary scores 0.0 for Window and falls to next window for Step; consistent with Rust impl and conformance vector"
patterns_established:
  - "FakeTimeProvider sealed subclass of System.TimeProvider overriding GetUtcNow() — test-only, defined inline in the test file; same injection pattern as Rust TimeProvider trait mock"
  - "Abstract base + nested sealed subtypes for closed type hierarchies — switch expression exhaustiveness checked by compiler; unknown curve type is an InvalidOperationException branch"
observability_surfaces:
  - "Construction failures: ArgumentException (DecayCurve subtypes) and ArgumentOutOfRangeException (DecayScorer.nullTimestampScore) name the offending parameter; surfaces immediately to callers"
  - "dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj to run all decay tests alongside suite"
  - "dotnet build — PublicAPI analyzer fails build with RS0016 and actionable message if any new public API is not declared"
duration: 20min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T03: Implement DecayScorer .NET

**`DecayCurve` abstract hierarchy and `DecayScorer` using `System.TimeProvider` added to .NET crate; all 5 conformance scenarios passing; 0 build errors/warnings.**

## What Happened

Created `DecayCurve.cs` with an abstract base class and three nested sealed subtypes (`Exponential`, `Window`, `Step`). Each subtype validates its constructor arguments and throws `ArgumentException` naming the parameter, matching the spec requirement. The abstract base uses `protected DecayCurve() {}` to prevent external subclassing outside the nested types.

Created `DecayScorer.cs` implementing `IScorer`. Uses `System.TimeProvider` (BCL, .NET 8+, no NuGet). The key implementation detail: age clamping uses `rawAge < TimeSpan.Zero ? TimeSpan.Zero : rawAge` rather than `.Duration()`. The `.Duration()` method returns the absolute value, which would incorrectly give a positive age to a future-dated item (e.g., +1h future timestamp → 1h age instead of 0h age). The fix matches the Rust implementation's `raw_age.max(Duration::zero())`.

The Step curve walk returns the score of the first window where `windows[i].MaxAge > age` (strictly greater than), with fallthrough to the last window's score for items older than every window.

Updated `PublicAPI.Unshipped.txt` with 14 entries. The protected constructor on the abstract base class must also be listed — RS0016 requires it.

Created `DecayScorerTests.cs` with 5 tests covering all conformance scenarios using a `FakeTimeProvider` that returns a fixed `DateTimeOffset`.

## Verification

- `dotnet build 2>&1 | grep -E " error | warning "` → no output (0 errors, 0 warnings)
- `dotnet test` → 663 passed, 0 failed (includes all 5 new DecayScorer tests)
- `grep -c "DecayScorer\|DecayCurve" src/Wollax.Cupel/PublicAPI.Unshipped.txt` → 14
- `grep "System.TimeProvider" src/Wollax.Cupel/Scoring/DecayScorer.cs` → present (BCL, no NuGet)
- Rust `cargo test` → 37 passed, 0 failed (prior work unaffected)
- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` → no output (drift guard satisfied)

## Diagnostics

- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` — runs all decay tests
- `dotnet build` — PublicAPI analyzer RS0016 surfaces missing API entries with file:line precision
- Construction failures: `ArgumentException` from curve subtypes and `ArgumentOutOfRangeException` from `DecayScorer` name the offending parameter in the message; surfaces immediately to callers

## Deviations

- Age clamping uses `rawAge < TimeSpan.Zero ? TimeSpan.Zero : rawAge` instead of the plan's `.Duration()` — `.Duration()` is the wrong API (returns absolute value); the plan text was imprecise. The Rust reference implementation (`raw_age.max(Duration::zero())`) confirms the correct semantics.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/Scoring/DecayCurve.cs` — abstract base + 3 sealed subtypes with precondition validation (~100 lines)
- `src/Wollax.Cupel/Scoring/DecayScorer.cs` — IScorer implementation using System.TimeProvider; clamps age; dispatches via switch expression (~90 lines)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 14 new entries for all new public types, constructors, properties, and Score method
- `tests/Wollax.Cupel.Tests/Scoring/DecayScorerTests.cs` — 5 conformance tests (TUnit); FakeTimeProvider inline subclass (~120 lines)
