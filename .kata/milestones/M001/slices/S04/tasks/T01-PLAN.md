---
estimated_steps: 4
estimated_files: 1
---

# T01: Fix ExclusionReason and InclusionReason wire format

**Slice:** S04 — Diagnostics Serde Integration
**Milestone:** M001

## Description

The current `cfg_attr` stubs on `ExclusionReason` and `InclusionReason` use serde's default externally-tagged format, which produces `{ "BudgetExceeded": { "item_tokens": ... } }`. The spec wire format is internally-tagged: `{ "reason": "BudgetExceeded", "item_tokens": ... }`. Adding `#[serde(tag = "reason")]` to both enums corrects this with zero custom code — all variants are struct or unit variants, which is exactly what internally-tagged serde supports.

An `_Unknown` unit variant with `#[serde(other)]` must also be added to `ExclusionReason` to satisfy the spec's requirement for graceful handling of unknown variant names. This is forward-compat infrastructure: callers parsing diagnostic output from a newer spec version won't panic.

D017's doc comment on `ExclusionReason` states "adjacent-tagged wire format" which is imprecise — the correct term is "internally-tagged". The comment should be updated.

This task is the prerequisite for everything else in S04: T02 adds `SelectionReport` serde (which contains `ExclusionReason`), and T03 writes tests that assert wire format correctness. Both require this to be right first.

## Steps

1. Open `crates/cupel/src/diagnostics/mod.rs`. On `ExclusionReason`, add a second `cfg_attr` line immediately after the existing derive stub: `#[cfg_attr(feature = "serde", serde(tag = "reason"))]`. This line must appear between the derive attribute and the `pub enum ExclusionReason` declaration.

2. Add the `_Unknown` variant to `ExclusionReason` as the last variant in the enum:
   ```rust
   /// Unknown variant — present for forward-compatibility with future spec versions.
   ///
   /// Emitted during deserialization when the `reason` field does not match any
   /// known variant. Never emitted by built-in pipeline stages.
   #[doc(hidden)]
   #[cfg_attr(feature = "serde", serde(other))]
   _Unknown,
   ```
   This variant must be `#[doc(hidden)]` and carry `#[cfg_attr(feature = "serde", serde(other))]`.

3. On `InclusionReason`, add the same tag attribute: `#[cfg_attr(feature = "serde", serde(tag = "reason"))]` between the existing derive attribute and the enum declaration.

4. Update the `ExclusionReason` doc comment: change "adjacent-tagged wire format" to "internally-tagged wire format". Change the `// custom serde impl in S04 — adjacent-tagged wire format` comment to `// S04: internally-tagged via #[serde(tag = "reason")]` to record that the impl is now complete.

## Must-Haves

- [ ] `ExclusionReason` carries `#[cfg_attr(feature = "serde", serde(tag = "reason"))]`
- [ ] `ExclusionReason` has `_Unknown` variant with `#[doc(hidden)]` and `#[cfg_attr(feature = "serde", serde(other))]`
- [ ] `InclusionReason` carries `#[cfg_attr(feature = "serde", serde(tag = "reason"))]`
- [ ] `cargo build --features serde` exits 0 with no warnings
- [ ] `cargo clippy --all-targets -- -D warnings` passes with no new warnings

## Verification

- `cargo build --features serde` — must exit 0 with no warnings
- `cargo clippy --all-targets -- -D warnings` — must exit 0 with no warnings
- Quick manual check: `cargo test --features serde` passes all 29 existing tests (no regressions from the new `_Unknown` variant on `ExclusionReason` — existing `match` arms should still compile because `#[non_exhaustive]` already requires a `_` arm)

## Observability Impact

- Signals added/changed: `ExclusionReason` and `InclusionReason` now produce spec-compliant JSON under `--features serde`; `_Unknown` provides a stable deserialization target for unknown variants
- How a future agent inspects this: `cargo test --features serde` confirms no regressions; `serde_json::to_string(&ExclusionReason::NegativeTokens { tokens: -1 })` in a test should produce `{"reason":"NegativeTokens","tokens":-1}`
- Failure state exposed: compile error if `serde(tag = ...)` is placed incorrectly; clippy warns if `_Unknown` visibility is wrong

## Inputs

- `crates/cupel/src/diagnostics/mod.rs` — current `ExclusionReason` and `InclusionReason` with `cfg_attr` derive stubs; `ExclusionReason` has 8 active/reserved variants (all struct or unit type — compatible with internal tagging)
- S04-RESEARCH.md — confirms internal tagging is the correct approach; specifies `_Unknown` with `#[serde(other)]` for forward-compat; confirms no custom Serialize/Deserialize impl needed
- `spec/src/diagnostics/exclusion-reasons.md` — authoritative wire format: `{ "reason": "...", ...fields }`

## Expected Output

- `crates/cupel/src/diagnostics/mod.rs` — `ExclusionReason` with `#[serde(tag = "reason")]` + `_Unknown` variant; `InclusionReason` with `#[serde(tag = "reason")]`; updated doc comments
