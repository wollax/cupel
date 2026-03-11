# Phase 3 Verification Report — Individual Scorers

**Status:** `passed`
**Score:** 7/7 must-haves verified
**Date:** 2026-03-11

---

## Criterion 1: All six scorers exist, implement IScorer, produce 0.0–1.0

**Result: PASS**

All six files confirmed in `src/Wollax.Cupel/Scoring/`:
- `RecencyScorer.cs` — `public sealed class RecencyScorer : IScorer`
- `PriorityScorer.cs` — `public sealed class PriorityScorer : IScorer`
- `KindScorer.cs` — `public sealed class KindScorer : IScorer`
- `TagScorer.cs` — `public sealed class TagScorer : IScorer`
- `FrequencyScorer.cs` — `public sealed class FrequencyScorer : IScorer`
- `ReflexiveScorer.cs` — `public sealed class ReflexiveScorer : IScorer`

`IScorer` is defined in `src/Wollax.Cupel/IScorer.cs` at the namespace root. All scorers implement `double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)`.

Output range analysis:
- `RecencyScorer`: rank-based, returns `rank / (countWithTimestamp - 1)` → [0.0, 1.0]
- `PriorityScorer`: rank-based, same pattern → [0.0, 1.0]
- `KindScorer`: dictionary lookup with default 0.0, max value 1.0 in default map → [0.0, 1.0]
- `TagScorer`: `matchedSum / _totalWeight`, capped at 1.0 when all tags match → [0.0, 1.0]
- `FrequencyScorer`: `matchingItems / (allItems.Count - 1)` → [0.0, 1.0]
- `ReflexiveScorer`: `Math.Clamp(hint, 0.0, 1.0)` → [0.0, 1.0] enforced

---

## Criterion 2: RecencyScorer uses relative timestamp ranking (not DateTime.Now)

**Result: PASS**

`RecencyScorer.Score()` contains no reference to `DateTime.Now`, `DateTimeOffset.Now`, or `DateTimeOffset.UtcNow`. It computes rank entirely by comparing `other.Timestamp.Value < itemTimestamp` within the provided `allItems` list, then returns `rank / (double)(countWithTimestamp - 1)`. Pure relative ranking within the input set.

---

## Criterion 3: Tests assert ordinal relationships (IsGreaterThan/IsLessThan), not exact floats for ordinal tests

**Result: PASS**

All six test files use TUnit assertions. Ordinal relationship tests verified:

- `RecencyScorerTests`: `MostRecent_ScoresHigher_ThanOlder` uses `IsGreaterThan`; `ThreeItems_LinearInterpolation` uses `IsGreaterThan` and `IsLessThan` for the middle item
- `PriorityScorerTests`: `HigherPriority_ScoresHigher` uses `IsGreaterThan`; `ThreeItems_LinearInterpolation` uses `IsGreaterThan` and `IsLessThan`
- `KindScorerTests`: `DefaultWeights_Ordinal` uses a chain of `IsGreaterThan` across all 5 kinds
- `TagScorerTests`: `PartialMatch_ScoresLower_ThanFullMatch` uses `IsLessThan`; exact-value tests use `Within(0.0001)` tolerance for floating-point comparisons
- `FrequencyScorerTests`: `PartialOverlap_ScoresBetweenZeroAndOne` uses `IsGreaterThan(0.0)` and `IsLessThan(1.0)`; `MoreSharedItems_ScoresHigher` uses `IsGreaterThan`
- `ReflexiveScorerTests`: all tests check exact values by nature (pass-through/clamp), which is correct for this scorer's contract

No test asserts an exact floating-point for an ordinal relationship where an approximation or inequality would be more appropriate.

---

## Criterion 4: No LINQ, closure captures, or boxing in any scorer's Score() method

**Result: PASS**

Each `Score()` method was read in full. Summary:

| Scorer | Loop style | LINQ in Score()? | Closures? |
|---|---|---|---|
| `RecencyScorer` | `for (var i = 0; ...)` | None | None |
| `PriorityScorer` | `for (var i = 0; ...)` | None | None |
| `KindScorer` | No loop — single `_weights.TryGetValue(...)` | None | None |
| `TagScorer` | `for (var i = 0; i < item.Tags.Count; i++)` | None | None |
| `FrequencyScorer` | `for (var i = 0; ...)` + nested `for (var j = 0; ...)` in `SharesAnyTag` | None | None |
| `ReflexiveScorer` | No loop — single `Math.Clamp(...)` | None | None |

**Note:** `TagScorer`'s constructor uses `foreach (var kvp in tagWeights)` to pre-compute `_totalWeight`. This is explicitly in the constructor, not `Score()`, and is called once at construction time. The comment in the source (`// constructor only, not Score path`) confirms intentional placement. This does not violate the criterion.

**Note:** `ScorerBenchmark.GlobalSetup()` uses `Enumerable.Range(...).Select(...).ToArray()` to build test data. This is in `[GlobalSetup]`, not inside any `[Benchmark]` method, so it does not represent allocation during scored operations. The six `[Benchmark]` methods themselves use only `for` loops.

No boxing was identified: all loop variables are value types iterated by index, `TryGetValue` uses direct generic parameters, and `Math.Clamp` operates on `double`.

---

## Criterion 5: ScorerBenchmark.cs exists with [MemoryDiagnoser] covering all 6 scorers

**Result: PASS**

File: `benchmarks/Wollax.Cupel.Benchmarks/ScorerBenchmark.cs`

- `[MemoryDiagnoser]` attribute present on the class
- Six `[Benchmark]` methods: `Recency()`, `Priority()`, `Reflexive()`, `Kind()`, `Tag()`, `Frequency()`
- Each method loops over all items and accumulates `scorer.Score(item, items)`, returning the sum to prevent dead-code elimination
- `[Params(100, 500)]` on `ItemCount` provides two data-size configurations

---

## Criterion 6: PublicAPI.Unshipped.txt has entries for all 6 scorers

**Result: PASS**

`src/Wollax.Cupel/PublicAPI.Unshipped.txt` contains entries at lines 123–141:

```
Wollax.Cupel.Scoring.RecencyScorer
Wollax.Cupel.Scoring.RecencyScorer.RecencyScorer() -> void
Wollax.Cupel.Scoring.RecencyScorer.Score(...) -> double
Wollax.Cupel.Scoring.PriorityScorer
Wollax.Cupel.Scoring.PriorityScorer.PriorityScorer() -> void
Wollax.Cupel.Scoring.PriorityScorer.Score(...) -> double
Wollax.Cupel.Scoring.ReflexiveScorer
Wollax.Cupel.Scoring.ReflexiveScorer.ReflexiveScorer() -> void
Wollax.Cupel.Scoring.ReflexiveScorer.Score(...) -> double
Wollax.Cupel.Scoring.KindScorer
Wollax.Cupel.Scoring.KindScorer.KindScorer() -> void
Wollax.Cupel.Scoring.KindScorer.KindScorer(IReadOnlyDictionary<ContextKind!, double>!) -> void
Wollax.Cupel.Scoring.KindScorer.Score(...) -> double
Wollax.Cupel.Scoring.TagScorer
Wollax.Cupel.Scoring.TagScorer.TagScorer(IReadOnlyDictionary<string!, double>!) -> void
Wollax.Cupel.Scoring.TagScorer.Score(...) -> double
Wollax.Cupel.Scoring.FrequencyScorer
Wollax.Cupel.Scoring.FrequencyScorer.FrequencyScorer() -> void
Wollax.Cupel.Scoring.FrequencyScorer.Score(...) -> double
```

All 6 scorers with constructors and `Score` methods are present.

---

## Criterion 7: Zero-warning build and all tests pass

**Result: PASS**

```
rtk dotnet build Cupel.slnx  →  ok (build succeeded)
```

```
rtk dotnet test --project tests/Wollax.Cupel.Tests
→ passed: 202 / 202, failed: 0, duration: 494ms
```

---

## Gaps Found

None. All 7 criteria pass without qualification.

The one observation worth noting for the record: `ScorerBenchmark.GlobalSetup()` uses LINQ, but this is data setup code running outside of `[Benchmark]` methods. The criterion targets scorer `Score()` methods specifically, and those are allocation-free.
