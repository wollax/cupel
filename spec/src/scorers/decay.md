# DecayScorer

The DecayScorer assigns a score based on how much time has elapsed since an item's `timestamp`, measured against a caller-supplied reference time. Older items score lower; fresher items score higher.

## Overview

DecayScorer is an **absolute scorer** — it computes a score for each item independently using only that item's `timestamp` and the injected reference time. The `allItems` parameter is ignored.

**Contrast with RecencyScorer:** RecencyScorer is a *relative* scorer — it ranks items against each other (most recent scores 1.0, oldest scores 0.0, with linear interpolation). DecayScorer is an *absolute-decay* scorer — each item's score is derived solely from the elapsed time between its timestamp and a caller-supplied reference time, with no dependence on the other items in the set.

### Fields Used

| Field | Source | Purpose |
|---|---|---|
| `timestamp` | [ContextItem](../data-model/context-item.md) | Temporal reference point for computing item age |

## TimeProvider

DecayScorer requires the caller to supply a time provider at construction. **There is no default** — the caller must inject one explicitly (D042).

This makes the time dependency visible, prevents stale time values in long-lived pipelines, and enables deterministic testing without special test modes.

### .NET

Use `System.TimeProvider` (BCL since .NET 8; available in `net10.0` without NuGet). Pass the system clock as `TimeProvider.System`. For testing, supply a `FakeTimeProvider` or any subclass of `TimeProvider`.

```csharp
new DecayScorer(TimeProvider.System, curve: Curve.Exponential(halfLife: TimeSpan.FromHours(24)));
```

### Rust

```rust
pub trait TimeProvider: Send + Sync {
    fn now(&self) -> DateTime<Utc>;
}
```

A zero-sized unit struct `SystemTimeProvider` implements `TimeProvider` via `Utc::now()`. The scorer stores the provider as `Box<dyn TimeProvider + Send + Sync>`, which is sufficient because `DecayScorer` is not `Clone` (D047).

```rust
new DecayScorer(Box::new(SystemTimeProvider), curve: Curve::exponential(half_life));
```

## Algorithm

```text
DECAY-SCORE(item, allItems, config):
    // allItems is ignored — DecayScorer is an absolute scorer
    if item.timestamp = null:
        return config.nullTimestampScore

    age <- max(duration_zero, config.timeProvider.now() - item.timestamp)
    // Negative age (future-dated item: timestamp > now) clamps to duration_zero.
    // The item is treated as maximally fresh — age zero feeds directly into the curve.

    return APPLY-CURVE(age, config.curve)
```

Where `duration_zero` is the zero-duration constant (i.e., `Duration::ZERO` in Rust / `TimeSpan.Zero` in .NET).

## Curve Factories

A curve is a function from a non-negative duration (the item's age) to a score in [0.0, 1.0]. DecayScorer ships three built-in curve factories.

### Exponential(halfLife)

Returns `2^(−age / halfLife)`. At age zero the score is 1.0; at age equal to `halfLife` the score is 0.5; as age approaches infinity the score approaches 0.0 (but is never negative).

**Precondition:** `halfLife > Duration::ZERO` / `halfLife > TimeSpan.Zero`. Throw `ArgumentException` (.NET) / return `Err(...)` (Rust) at construction if the precondition is violated; the error message MUST name the `halfLife` parameter.

```text
EXPONENTIAL-CURVE(age, halfLife):
    return pow(2.0, −duration_to_seconds(age) / duration_to_seconds(halfLife))
```

Where `duration_to_seconds` converts a duration to a non-negative float64 count of seconds.

### Step(windows)

`windows` is an ordered list of `(maxAge: Duration, score: double)` pairs, sorted from youngest (smallest `maxAge`) to oldest (largest `maxAge`). The scorer walks the list and returns the `score` of the first entry where `window.maxAge > age` (strict greater-than). If `age` is greater than or equal to every boundary, the last entry's score applies.

```text
STEP-CURVE(age, windows):
    for each window in windows (youngest to oldest):
        if window.maxAge > age:
            return window.score
    // age exceeded all boundaries — fall through to last entry
    return windows[last].score
```

**Preconditions:**
- `windows` MUST contain at least one entry. Throw at construction if the list is empty.
- No entry may have `maxAge = duration_zero` (zero-width windows are forbidden). Throw at construction if any `window.maxAge` is zero.

The strict `>` comparison means that an item whose age equals a boundary falls into the *next* window (older side). For example, with `windows = [(1h, 0.9), (24h, 0.5), (72h, 0.1)]`:

| age | first window where maxAge > age | returned score |
|-----|--------------------------------|----------------|
| 0h | 1h window | 0.9 |
| 1h | 24h window (1h is not > 1h) | 0.5 |
| 24h | 72h window | 0.1 |
| 72h | none — fall through | 0.1 (last entry) |

### Window(maxAge)

Binary score. Returns 1.0 when `age < maxAge` (half-open interval `[0, maxAge)`); returns 0.0 when `age >= maxAge`.

```text
WINDOW-CURVE(age, maxAge):
    if age < maxAge:
        return 1.0
    return 0.0
```

**Precondition:** `maxAge > Duration::ZERO` / `maxAge > TimeSpan.Zero`. Throw at construction if violated.

The boundary is exclusive on the right: an item whose age is **exactly** `maxAge` returns 0.0.

## Configuration

| Parameter | Type | Required | Default | Notes |
|---|---|---|---|---|
| `timeProvider` | `System.TimeProvider` (.NET) / `Box<dyn TimeProvider>` (Rust) | Yes | — | Caller must inject; no default (D042) |
| `curve` | `Curve` (one of Exponential, Step, Window) | Yes | — | Decay shape |
| `nullTimestampScore` | float64, range [0.0, 1.0] | No | 0.5 | Score returned for items with no timestamp. Default 0.5 is neutral: it neither rewards nor penalizes missing timestamps. |

`nullTimestampScore` MUST be in [0.0, 1.0]. Implementations SHOULD reject construction if the value is outside this range.

## Edge Cases

| Condition | Result |
|---|---|
| `item.timestamp` is null | `config.nullTimestampScore` |
| Item is future-dated (`timestamp > now`) | Age clamps to `duration_zero`; score = `APPLY-CURVE(0, config.curve)` |
| Age is exactly zero (freshest possible) | Exponential: 1.0; Step: first window's score (if `window.maxAge > 0`); Window: 1.0 (since 0 < maxAge) |
| `age == maxAge` for Window curve | 0.0 — boundary is half-open `[0, maxAge)` |
| Step curve: age exceeds all boundaries | Last entry's score |
| Exponential with very large age | Approaches 0.0; never negative |

## Conformance Vector Outlines

All scenarios use a fixed `referenceTime = 2025-01-01T12:00:00Z` (set via the injected `TimeProvider`).

1. **Exponential, one-half-life age:** An item with `timestamp = 2024-12-31T12:00:00Z` (age = 24 h) and `curve = Exponential(halfLife: 24h)` — the scorer returns 0.5 (`2^(−1) = 0.5`).

2. **Future-dated item:** An item with `timestamp = 2025-01-02T00:00:00Z` (timestamp is after referenceTime; age clamps to zero) and `curve = Exponential(halfLife: 24h)` — the scorer returns the same score as an age-zero item, i.e., 1.0.

3. **Null timestamp:** An item with no timestamp and `nullTimestampScore = 0.5` — the scorer returns 0.5 regardless of the configured curve.

4. **Step curve, second window:** An item with `timestamp = 2025-01-01T06:00:00Z` (age = 6 h) and `curve = Step(windows: [(1h, 0.9), (24h, 0.5), (72h, 0.1)])` — age 6h is not less than 1h (`1h > 6h` is false), but is less than 24h (`24h > 6h` is true) — the scorer returns 0.5 (second window's score).

5. **Window curve, age exactly at maxAge:** An item with `timestamp = 2025-01-01T06:00:00Z` (age = 6 h) and `curve = Window(maxAge: 6h)` — since `age >= maxAge` (`6h >= 6h`), the scorer returns 0.0.

## Complexity

- **Time:** O(1) per item for Exponential and Window; O(W) per item for Step, where W is the number of windows.
- **Space:** O(1) auxiliary per invocation.

## Conformance Notes

- The `allItems` parameter MUST be accepted by the scorer interface but MUST be ignored. Implementations MUST NOT iterate or inspect `allItems`.
- Negative age (future-dated items) MUST be clamped to `duration_zero` before being passed to the curve. The clamp MUST occur before the curve computation, not inside each curve.
- `nullTimestampScore` defaults to 0.5. The neutral default is intentional: it avoids penalizing items that legitimately have no timestamp (e.g., synthetic items injected at pipeline construction time) while also not rewarding them above fresher real items.
- Curve constructors MUST validate preconditions at construction time, not at scoring time. A scorer with an invalid curve configuration MUST NOT be created.
- In .NET, `halfLife` and `maxAge` are `TimeSpan`; comparisons use `TimeSpan.Zero`. In Rust, they are `Duration`; comparisons use `Duration::ZERO`.
- The Rust `TimeProvider` trait MUST be `Send + Sync` to allow `DecayScorer` to be used across thread boundaries. The stored provider is `Box<dyn TimeProvider + Send + Sync>`.
