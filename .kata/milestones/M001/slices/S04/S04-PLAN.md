# S04: Diagnostics Serde Integration

**Goal:** All diagnostic types (`ExclusionReason`, `InclusionReason`, `SelectionReport`, `TraceEvent`, `OverflowEvent`, `IncludedItem`, `ExcludedItem`, `DiagnosticTraceCollector`, `NullTraceCollector`, `TraceDetailLevel`, `PipelineStage`) serialize/deserialize correctly under `--features serde`, with wire format matching the spec.
**Demo:** `cargo test --features serde` passes including new diagnostics serde tests; `serde_json::to_string(&report)` on a `SelectionReport` produces `{ "reason": "BudgetExceeded", ... }` internally-tagged envelope format matching the spec wire format.

## Must-Haves

- `ExclusionReason` serializes with `{ "reason": "<VariantName>", ...fields }` internally-tagged envelope (not serde default externally-tagged)
- `InclusionReason` serializes as `{ "reason": "<VariantName>" }` (not bare string)
- Unknown `ExclusionReason` variants deserialize gracefully into `_Unknown` — no panic
- `SelectionReport` round-trips with validation-on-deserialize: `total_candidates == included.len() + excluded.len()`
- `DiagnosticTraceCollector` serializes without exposing the internal `usize` insertion index from the `excluded` field
- All 8 `ExclusionReason` variants and all 3 `InclusionReason` variants pass round-trip tests
- `cargo test --features serde` passes with zero failures
- `cargo clippy --all-targets -- -D warnings` passes with zero new warnings
- `cargo build --features serde` compiles cleanly

## Proof Level

- This slice proves: integration
- Real runtime required: yes — serde serialize/deserialize calls with `serde_json`
- Human/UAT required: no — all contracts are machine-verifiable

## Verification

- `cargo test --features serde -- diag` — all new diagnostics serde tests pass
- `cargo test --features serde` — all 29+ existing tests plus new diagnostics serde tests pass, zero failures
- `cargo clippy --all-targets -- -D warnings` — zero warnings/errors
- `cargo build --features serde` — clean compilation, no warnings
- Wire-format assertion: `serde_json::to_string(&ExclusionReason::BudgetExceeded { item_tokens: 100, available_tokens: 50 }).unwrap()` → JSON contains `"reason":"BudgetExceeded"`
- Wire-format assertion: `serde_json::to_string(&InclusionReason::Scored).unwrap()` → JSON is `{"reason":"Scored"}`
- Graceful unknown: `serde_json::from_str::<ExclusionReason>(r#"{"reason":"FutureVariant"}"#).unwrap()` → `ExclusionReason::_Unknown`

## Observability / Diagnostics

- Runtime signals: serde error messages from `serde_json::from_str` on invalid input; `serde::de::Error::custom` from validation-on-deserialize for `SelectionReport`
- Inspection surfaces: `cargo test --features serde -- diag --nocapture`; `cargo run --example serde_roundtrip --features serde`
- Failure visibility: serde errors carry field path + reason; validation rejects `total_candidates` mismatch with a descriptive message
- Redaction constraints: none — diagnostic types contain only item content/scores, no secrets

## Integration Closure

- Upstream surfaces consumed: `ExclusionReason`, `InclusionReason`, `SelectionReport`, `DiagnosticTraceCollector` from S01/S02; existing `Raw` + validation-on-deserialize pattern from `context_budget.rs`
- New wiring introduced in this slice: `#[serde(tag = "reason")]` on both reason enums; custom `Deserialize<'de>` for `SelectionReport`; `serialize_with`/`deserialize_with` helpers on `DiagnosticTraceCollector.excluded`
- What remains before the milestone is truly usable end-to-end: S05 (CI clippy + deny hardening); S06/S07 (quality hardening)

## Tasks

- [x] **T01: Fix ExclusionReason and InclusionReason wire format** `est:30m`
  - Why: Current `cfg_attr` stubs produce serde's default externally-tagged format (`{ "BudgetExceeded": { ... } }`); the spec requires internally-tagged (`{ "reason": "BudgetExceeded", ... }`). This is the foundational fix — `SelectionReport` serde and all tests depend on it being correct.
  - Files: `crates/cupel/src/diagnostics/mod.rs`
  - Do: (1) Add `#[cfg_attr(feature = "serde", serde(tag = "reason"))]` to `ExclusionReason` immediately after its existing serde derive `cfg_attr`. (2) Add `_Unknown` unit variant with `#[doc(hidden)]` and `#[cfg_attr(feature = "serde", serde(other))]` to `ExclusionReason` for graceful unknown-variant deserialization — note in a doc comment that this variant exists for serde forward-compat and will never be emitted by built-in stages. (3) Add `#[cfg_attr(feature = "serde", serde(tag = "reason"))]` to `InclusionReason`. (4) Update the `ExclusionReason` doc comment to replace "adjacent-tagged" with "internally-tagged" (D017's comment was imprecise — the spec uses `#[serde(tag = "reason")]` which is Serde's _internal_ tagging, not adjacent).
  - Verify: `cargo build --features serde` compiles cleanly; `cargo clippy --all-targets -- -D warnings` passes.
  - Done when: `cargo build --features serde` exits 0 with no warnings; the `_Unknown` variant is present in `ExclusionReason`.

- [x] **T02: Add SelectionReport serde and fix DiagnosticTraceCollector.excluded** `est:45m`
  - Why: `SelectionReport` has no serde derive at all; `DiagnosticTraceCollector.excluded` leaks the internal `usize` insertion index under the current derive stub. Both must be fixed before tests can pass.
  - Files: `crates/cupel/src/diagnostics/mod.rs`, `crates/cupel/src/diagnostics/trace_collector.rs`
  - Do for SelectionReport: (1) Add `#[cfg_attr(feature = "serde", derive(serde::Serialize))]` to `SelectionReport` — derive handles plain-struct serialization correctly. (2) Add a `#[cfg(feature = "serde")] impl<'de> serde::Deserialize<'de> for SelectionReport` using the `Raw` pattern from `context_budget.rs`: define a `RawSelectionReport` with `#[serde(deny_unknown_fields)]`, deserialize into it, then validate `total_candidates == raw.included.len() + raw.excluded.len()`, returning `serde::de::Error::custom` on mismatch. (3) Add `use serde::{Deserialize, Deserializer, Serialize};` inside the `#[cfg(feature = "serde")]` block only — no module-level serde import. Do for DiagnosticTraceCollector: (4) Add two free functions (inside `#[cfg(feature = "serde")]`) in `trace_collector.rs`: `ser_excluded_items` that serializes `Vec<(ExcludedItem, usize)>` as `Vec<&ExcludedItem>` (strips the usize), and `de_excluded_items` that deserializes `Vec<ExcludedItem>` and re-attaches sequential insertion indices (0, 1, 2, ...). (5) Replace the bare derive stub on `DiagnosticTraceCollector.excluded` field with `#[cfg_attr(feature = "serde", serde(serialize_with = "ser_excluded_items", deserialize_with = "de_excluded_items"))]`. (6) Update `DiagnosticTraceCollector`'s serde doc comment to remove "incorrect until S04" — S04 is now landed.
  - Verify: `cargo build --features serde` compiles; `cargo test --features serde` passes all 29 existing tests.
  - Done when: `cargo test --features serde` exits 0 with all existing tests passing; `SelectionReport` has both Serialize and Deserialize; `DiagnosticTraceCollector` no longer leaks `usize`.

- [x] **T03: Write diagnostics serde integration tests and extend the roundtrip example** `est:30m`
  - Why: Tests are the proof that the wire format is correct end-to-end. Without them, a subtle serde tag misconfiguration would compile silently and produce wrong JSON.
  - Files: `crates/cupel/tests/serde.rs`, `crates/cupel/examples/serde_roundtrip.rs`
  - Do for tests/serde.rs: Add section `// 6. Diagnostics serde tests` after the existing section 5. Include: (a) Wire-format assertions: `ExclusionReason::BudgetExceeded` JSON contains `"reason":"BudgetExceeded"` with sibling fields; `InclusionReason::Scored` JSON is `{"reason":"Scored"}`; these must NOT be externally-tagged. (b) Round-trips for all 8 `ExclusionReason` variants (verify field values survive): `BudgetExceeded`, `ScoredTooLow`, `Deduplicated`, `QuotaCapExceeded`, `QuotaRequireDisplaced`, `NegativeTokens`, `PinnedOverride`, `Filtered`. (c) Round-trips for all 3 `InclusionReason` variants: `Scored`, `Pinned`, `ZeroToken`. (d) Full `SelectionReport` round-trip with one included and one excluded item — verify all fields survive. (e) `SelectionReport` validation rejection: construct JSON with `total_candidates` mismatching `included.len() + excluded.len()`, assert `serde_json::from_str::<SelectionReport>` returns `Err`. (f) Graceful unknown: `serde_json::from_str::<ExclusionReason>(r#"{"reason":"FutureVariant"}"#)` → `Ok(ExclusionReason::_Unknown)`. (g) `DiagnosticTraceCollector` round-trip: construct a collector, record one included + one excluded item, call `into_report()`, serialize the report, deserialize it back, verify `excluded[0].reason` is the right variant (not a tuple artifact). Do for serde_roundtrip.rs: Add a Part 4 block demonstrating `pipeline.dry_run()` → `SelectionReport` → serialize to pretty JSON → print.
  - Verify: `cargo test --features serde -- diag` all pass; `cargo run --example serde_roundtrip --features serde` exits 0; `cargo clippy --all-targets -- -D warnings` passes.
  - Done when: all new test functions pass; the example runs without error; zero warnings from clippy.

## Files Likely Touched

- `crates/cupel/src/diagnostics/mod.rs`
- `crates/cupel/src/diagnostics/trace_collector.rs`
- `crates/cupel/tests/serde.rs`
- `crates/cupel/examples/serde_roundtrip.rs`
- `.kata/DECISIONS.md`
