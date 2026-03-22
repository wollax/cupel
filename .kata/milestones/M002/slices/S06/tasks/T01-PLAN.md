---
estimated_steps: 5
estimated_files: 2
---

# T01: Write DecayScorer Spec Chapter

**Slice:** S06 — Future Features Spec Chapters
**Milestone:** M002

## Description

Write `spec/src/scorers/decay.md` — the DecayScorer spec chapter — and add it to `spec/src/SUMMARY.md`. DecayScorer is self-contained: it references only `ContextItem.timestamp` and the TimeProvider abstraction. All design decisions are locked (D042, D047); the task is spec authoring, not design work.

The chapter must cover: algorithm with age-clamping pseudocode (DECAY-SCORE), mandatory TimeProvider injection (.NET `System.TimeProvider` and Rust `TimeProvider` trait), three curve factories (Exponential/Step/Window) with precise semantics and preconditions, `nullTimestampScore` configuration, edge cases table, and 5 conformance vector outlines.

## Steps

1. **Read reference files** — Read `spec/src/scorers/metadata-trust.md` fully (canonical chapter template: Overview → Fields Used → Conventions/Configuration → Algorithm → Edge Cases → Conformance Notes). Read `spec/src/scorers/recency.md` to understand the contrast framing (RecencyScorer is rank-based; DecayScorer is absolute-decay — state this in the Overview). Note pseudocode style: `text` fenced blocks, CAPS-WITH-HYPHENS procedure names, numbered steps.

2. **Write `spec/src/scorers/decay.md`** — Follow the metadata-trust.md structure:
   - **Overview**: DecayScorer is an **absolute scorer** using `timestamp` against an injected reference time. Contrast: RecencyScorer ranks items relative to each other; DecayScorer computes absolute decay from a configurable time reference. Fields Used table: `timestamp` from ContextItem.
   - **TimeProvider**: Mandatory injection (D042 — no silent default). .NET: `System.TimeProvider` (BCL since .NET 8, available in `net10.0` without NuGet). Rust: `pub trait TimeProvider: Send + Sync { fn now(&self) -> DateTime<Utc>; }` with `SystemTimeProvider` unit struct implementing it via `Utc::now()`; stored as `Box<dyn TimeProvider + Send + Sync>`.
   - **Algorithm (DECAY-SCORE pseudocode)**:
     ```text
     DECAY-SCORE(item, allItems, config):
         if item.timestamp = null:
             return config.nullTimestampScore
         age <- max(duration_zero, config.timeProvider.now() - item.timestamp)
         return APPLY-CURVE(age, config.curve)
     ```
     Note: `allItems` is ignored (absolute scorer). Negative age (future-dated items) clamps to `duration_zero` — item is treated as maximally fresh.
   - **Curve Factories** (three subsections):
     - `Exponential(halfLife)`: `score = 2^(−age / halfLife)`. Precondition: `halfLife > Duration::ZERO` / `halfLife > TimeSpan.Zero`; throw `ArgumentException`/`Err` at construction with message naming the parameter. Pseudocode: `EXPONENTIAL-CURVE(age, halfLife): return pow(2.0, −duration_to_seconds(age) / duration_to_seconds(halfLife))`.
     - `Step(windows)`: `windows` is an ordered list of `(maxAge: Duration, score: double)` pairs from youngest to oldest boundary. Scorer walks the list and returns the `score` for the first entry where `window.maxAge > age` (strict greater-than). If `age` exceeds all window boundaries, the last entry's score applies (or 0.0 if the list is empty — throw at construction if empty). Precondition: at least one window; no window with `maxAge = Duration::ZERO` (zero-width window forbidden — throw at construction). Pseudocode provided.
     - `Window(maxAge)`: Binary score. Returns 1.0 when `age < maxAge` (half-open interval `[0, maxAge)`); returns 0.0 when `age >= maxAge`. Precondition: `maxAge > Duration::ZERO`.
   - **Configuration table**: `timeProvider` (required), `curve` (required), `nullTimestampScore` (float64, default 0.5, range [0.0, 1.0]) — described as "neutral: neither rewards nor penalizes missing timestamps."
   - **Edge Cases table**: future-dated item (age clamps to zero → curve(0)); zero age at window boundary for Window curve (0.0 — half-open); Step curve exhausted (last entry's score); Exponential with very large age (approaches 0.0, never negative); null timestamp (`nullTimestampScore`).
   - **Conformance Vector Outlines**: 5 scenarios, all with fixed `referenceTime = 2025-01-01T12:00:00Z`: (1) item with exact 1-half-life age scores 0.5 with Exponential; (2) future-dated item (timestamp > referenceTime) scores same as age-zero; (3) null-timestamp item returns `nullTimestampScore = 0.5`; (4) Step curve: item age in second window returns second window's score; (5) Window curve: item age exactly at `maxAge` returns 0.0.

3. **Update `spec/src/SUMMARY.md`** — Add `  - [DecayScorer](scorers/decay.md)` immediately after the `MetadataTrustScorer` entry under `# Scorers`.

4. **Run TBD check** — `grep -ci "\bTBD\b" spec/src/scorers/decay.md` → must return 0.

5. **Run test suites** — `cargo test --manifest-path crates/cupel/Cargo.toml` (35+ passed, 0 failed); `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` (583+ passed, 0 failed). Spec changes only — both suites should pass unchanged.

## Must-Haves

- [ ] `spec/src/scorers/decay.md` exists
- [ ] DECAY-SCORE pseudocode in `text` fenced block; `allItems` parameter noted as ignored
- [ ] Negative-age clamping to `duration_zero` explicitly stated in pseudocode and prose
- [ ] TimeProvider section names both .NET `System.TimeProvider` and Rust trait declaration verbatim
- [ ] Exponential: zero half-life throw-at-construction documented
- [ ] Step: `windows` type defined precisely (ordered list, youngest-to-oldest, strict `>` comparison); zero-maxAge precondition documented
- [ ] Window: half-open `[0, maxAge)` interval stated; `age == maxAge` returns 0.0 explicitly called out
- [ ] `nullTimestampScore` documented with default 0.5 and neutral-semantics rationale
- [ ] 5 conformance vector outlines present
- [ ] `grep -ci "\bTBD\b" spec/src/scorers/decay.md` returns 0
- [ ] `grep -q "decay" spec/src/SUMMARY.md` passes
- [ ] Both test suites pass (no regressions from spec-only changes)

## Verification

- `test -f spec/src/scorers/decay.md` — file exists
- `grep -ci "\bTBD\b" spec/src/scorers/decay.md` → 0
- `grep -q "DECAY-SCORE" spec/src/scorers/decay.md` — pseudocode present
- `grep -q "Exponential\|Step\|Window" spec/src/scorers/decay.md` — all three curves present
- `grep -q "TimeProvider" spec/src/scorers/decay.md` — TimeProvider section present
- `grep -q "nullTimestampScore" spec/src/scorers/decay.md` — null-timestamp policy present
- `grep -q "Conformance" spec/src/scorers/decay.md` — conformance section present
- `grep -q "decay" spec/src/SUMMARY.md` — SUMMARY.md entry present
- `cargo test --manifest-path crates/cupel/Cargo.toml` exits 0
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` exits 0

## Observability Impact

- Signals added/changed: None (spec authoring; no code changes)
- How a future agent inspects this: `grep -ci "\bTBD\b" spec/src/scorers/decay.md` → 0; `grep -q "DECAY-SCORE" spec/src/scorers/decay.md`; `grep -q "decay" spec/src/SUMMARY.md`
- Failure state exposed: TBD count > 0 means section is incomplete; missing SUMMARY entry means chapter is unreachable from mdBook index; test failure means an incidental regression was introduced (should not happen since this is spec-only)

## Inputs

- `spec/src/scorers/metadata-trust.md` — canonical chapter template; use its section ordering and prose style verbatim
- `spec/src/scorers/recency.md` — RecencyScorer chapter; provides the contrast for "rank-based vs absolute-decay" framing in the Overview
- `.kata/DECISIONS.md` D042, D047 — locked TimeProvider decisions; must be honored exactly
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` "S06 must specify — DecayScorer" list — authoritative 5-item mandate list for this chapter
- `src/Wollax.Cupel/ContextItem.cs:69` — `DateTimeOffset? Timestamp` in .NET; confirms nullable timestamp field name

## Expected Output

- `spec/src/scorers/decay.md` — new file; fully-specified DecayScorer chapter with DECAY-SCORE pseudocode, three curve factories with preconditions, TimeProvider section, edge cases table, 5 conformance vector outlines, zero TBD fields
- `spec/src/SUMMARY.md` — DecayScorer entry added under Scorers section
