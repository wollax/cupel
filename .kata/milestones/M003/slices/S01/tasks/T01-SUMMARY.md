---
id: T01
parent: S01
milestone: M003
provides:
  - TimeProvider trait (Send + Sync) with now() -> DateTime<Utc>
  - SystemTimeProvider ZST implementing TimeProvider via Utc::now()
  - DecayCurve enum with Exponential(Duration), Window(Duration), Step(Vec<(Duration,f64)>) variants
  - DecayCurve::exponential / window / step constructors with validated preconditions
  - DecayScorer struct (no Clone/Copy) implementing Scorer with injected Box<dyn TimeProvider>
  - DecayScorer::new() enforcing null_timestamp_score in [0.0, 1.0]
  - Age clamped to Duration::zero() before curve dispatch
key_files:
  - crates/cupel/src/scorer/decay.rs
  - crates/cupel/src/scorer/mod.rs
  - crates/cupel/src/lib.rs
key_decisions:
  - Used millisecond precision (num_milliseconds() / 1000.0) for age_secs to avoid integer truncation from num_seconds() on sub-second ages
  - Step curve falls through to last window's score for items older than every window; documented in module doc
  - DecayCurve::compute() is a private method on the enum rather than a free function to keep curve logic encapsulated
patterns_established:
  - Injected TimeProvider trait pattern for deterministic time-dependent scorers (mirrors recency scorer pattern but injectable)
  - Curve validation at construction time via typed constructors — errors name the offending parameter
observability_surfaces:
  - CupelError::ScorerConfig(String) names offending parameter on construction failure
  - cargo test -- --nocapture shows construction error messages
duration: 15min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T01: Implement DecayScorer Rust core

**`DecayScorer` with Exponential, Window, and Step curves added to Rust crate; `TimeProvider` trait enables deterministic testing via clock injection.**

## What Happened

Created `crates/cupel/src/scorer/decay.rs` (~185 lines) with the full `DecayScorer` implementation:

- `TimeProvider` trait (`Send + Sync`, `fn now() -> DateTime<Utc>`) and `SystemTimeProvider` ZST
- `DecayCurve` enum with three variants; each validated via a typed constructor (`exponential`, `window`, `step`) returning `Err(CupelError::ScorerConfig(...))` naming the offending parameter on violation
- `DecayScorer` struct (no `Clone`/`Copy`, stores `Box<dyn TimeProvider + Send + Sync>`) with `new()` enforcing `null_timestamp_score ∈ [0.0, 1.0]`
- `Scorer` impl: items without timestamp → `null_timestamp_score`; age clamped to zero before curve dispatch; Exponential uses millisecond precision to avoid integer truncation; Step falls through to last window's score

Registered all four public types in `scorer/mod.rs` (`mod decay` + `pub use`) and extended the `pub use scorer::{...}` line in `lib.rs`.

## Verification

```
cargo test --all-targets  → all tests pass (exit 0)
cargo clippy --all-targets -- -D warnings  → 0 error[...] lines
cargo doc --no-deps  → 0 warnings
grep -n "Clone\|Copy" decay.rs | grep "DecayScorer"  → empty
grep "DecayScorer\|DecayCurve\|TimeProvider\|SystemTimeProvider" lib.rs  → all 4 present
```

## Diagnostics

- Construction failures: `CupelError::ScorerConfig(String)` with parameter name in message — surfaced via `Result` propagation to caller
- Scorers are pure functions; runtime behavior inspectable via `cargo test -- --nocapture`
- `cargo clippy` catches any future type errors

## Deviations

None — implementation matches the task plan exactly. Used `num_milliseconds()` for age precision (not explicitly specified in plan, but more correct than `num_seconds()` which truncates sub-second ages).

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/scorer/decay.rs` — new file; `TimeProvider`, `SystemTimeProvider`, `DecayCurve`, `DecayScorer`, `Scorer` impl
- `crates/cupel/src/scorer/mod.rs` — added `mod decay;` + `pub use decay::{...}` line; updated scorer table in module doc
- `crates/cupel/src/lib.rs` — extended `pub use scorer::{...}` with 4 new identifiers
