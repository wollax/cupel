---
estimated_steps: 7
estimated_files: 3
---

# T01: Implement DecayScorer Rust core

**Slice:** S01 — DecayScorer — Rust + .NET Implementation
**Milestone:** M003

## Description

Create the Rust `DecayScorer` implementation in a new `crates/cupel/src/scorer/decay.rs` module. This covers the `TimeProvider` trait, `SystemTimeProvider` ZST, `DecayCurve` enum with three variants, and the `DecayScorer` struct implementing `Scorer`. All preconditions must be enforced at construction returning `CupelError::ScorerConfig`. The module is registered in `scorer/mod.rs` and re-exported from `lib.rs`.

## Steps

1. Create `crates/cupel/src/scorer/decay.rs`. Define `pub trait TimeProvider: Send + Sync { fn now(&self) -> chrono::DateTime<chrono::Utc>; }` and `pub struct SystemTimeProvider;` implementing it via `Utc::now()`.
2. Define `pub enum DecayCurve` with variants `Exponential(chrono::Duration)`, `Window(chrono::Duration)`, `Step(Vec<(chrono::Duration, f64)>)`. Add a `pub fn new_exponential`, `pub fn new_window`, `pub fn new_step` constructor set (or implement `DecayCurve::exponential(half_life) -> Result<Self, CupelError>` etc.) that validate: `half_life > Duration::zero()`, `max_age > Duration::zero()`, `windows` non-empty, no zero-width window entries; return `Err(CupelError::ScorerConfig("..."))` naming the parameter on violation.
3. Define `pub struct DecayScorer { provider: Box<dyn TimeProvider + Send + Sync>, curve: DecayCurve, null_timestamp_score: f64 }`. Do NOT derive `Clone` or `Copy`.
4. Implement a `pub fn new(provider: Box<dyn TimeProvider + Send + Sync>, curve: DecayCurve, null_timestamp_score: f64) -> Result<Self, CupelError>` constructor: reject `null_timestamp_score` outside [0.0, 1.0] with `ScorerConfig("nullTimestampScore must be in [0.0, 1.0]")`.
5. Implement `Scorer for DecayScorer`: if `item.timestamp()` is None return `self.null_timestamp_score`; compute `age = (self.provider.now() - ts).max(chrono::Duration::zero())` (clamp before dispatch); dispatch to curve formula — `Exponential(h)`: `2_f64.powf(-(age_secs / h_secs))`; `Window(m)`: if `age < m { 1.0 } else { 0.0 }`; `Step(windows)`: walk from youngest, return first `w.maxAge > age`'s score, fall through to last. Ignore `all_items` parameter.
6. Register in `scorer/mod.rs`: add `mod decay;` and `pub use decay::{DecayScorer, DecayCurve, TimeProvider, SystemTimeProvider};`.
7. Add to `lib.rs`: extend the `pub use scorer::{...}` line to include `DecayScorer, DecayCurve, TimeProvider, SystemTimeProvider`.

## Must-Haves

- [ ] `crates/cupel/src/scorer/decay.rs` exists with `TimeProvider` trait, `SystemTimeProvider` ZST, `DecayCurve` enum with 3 variants, `DecayScorer` struct
- [ ] `DecayScorer` is NOT `Clone` (no derive Clone/Copy); stores `Box<dyn TimeProvider + Send + Sync>`
- [ ] All curve preconditions return `Err(CupelError::ScorerConfig(...))` naming the parameter
- [ ] `null_timestamp_score` outside [0.0, 1.0] returns `Err(CupelError::ScorerConfig(...))`
- [ ] Age clamped to `Duration::zero()` before curve dispatch (not inside each variant)
- [ ] `all_items` accepted but ignored in `score()` implementation
- [ ] Types re-exported from `scorer/mod.rs` and `lib.rs`
- [ ] `cargo test --all-targets` exits 0; `cargo clippy --all-targets -- -D warnings` clean; `cargo doc --no-deps` 0 warnings

## Verification

- `cargo test --all-targets 2>&1 | tail -5` — all tests pass
- `cargo clippy --all-targets -- -D warnings 2>&1 | grep "error\[" | wc -l` → 0
- `cargo doc --no-deps 2>&1 | grep "warning" | wc -l` → 0
- `grep -n "Clone\|Copy" crates/cupel/src/scorer/decay.rs | grep "DecayScorer"` → empty (no derive)
- `grep "DecayScorer\|DecayCurve\|TimeProvider\|SystemTimeProvider" crates/cupel/src/lib.rs` shows entries

## Observability Impact

- Signals added/changed: `CupelError::ScorerConfig(String)` returned on construction failure — names the offending parameter; surfaced to callers via `Result` propagation
- How a future agent inspects this: `cargo test -- --nocapture` shows construction error messages; `cargo clippy` catches type errors
- Failure state exposed: Invalid curve config surfaces immediately at construction, not at scoring time — prevents silent misconfiguration

## Inputs

- `crates/cupel/src/scorer/mod.rs` — existing module structure; add `mod decay;` and re-exports
- `crates/cupel/src/lib.rs` — existing `pub use scorer::{...}` line to extend
- `crates/cupel/src/error.rs` — `CupelError::ScorerConfig(String)` variant (confirmed present)
- `crates/cupel/Cargo.toml` — `chrono = "0.4"` confirmed present at 0.4.44
- `crates/cupel/src/scorer/recency.rs` — canonical Rust scorer pattern to replicate

## Expected Output

- `crates/cupel/src/scorer/decay.rs` — ~120-160 lines; exports `TimeProvider`, `SystemTimeProvider`, `DecayCurve`, `DecayScorer`; implements `Scorer`; no `Clone`/`Copy` on `DecayScorer`
- `crates/cupel/src/scorer/mod.rs` — 2 lines added: `mod decay;` + `pub use` line
- `crates/cupel/src/lib.rs` — `pub use scorer` line extended with 4 new identifiers
- All prior tests still pass; no new warnings
