---
id: T01
parent: S04
milestone: M001
provides:
  - ExclusionReason with #[serde(tag = "reason")] producing internally-tagged wire format
  - ExclusionReason._Unknown variant with #[serde(other)] for forward-compat deserialization
  - InclusionReason with #[serde(tag = "reason")] producing internally-tagged wire format
  - Updated doc comments reflecting correct "internally-tagged" terminology
key_files:
  - crates/cupel/src/diagnostics/mod.rs
key_decisions:
  - "Used #[serde(tag = \"reason\")] (internally-tagged) not adjacent-tagged; all ExclusionReason and InclusionReason variants are struct or unit type — compatible with serde internal tagging without any custom impl"
  - "_Unknown variant added with #[serde(other)] and #[doc(hidden)] on ExclusionReason; InclusionReason omitted _Unknown per spec (inclusion is always known-set)"
patterns_established:
  - "Internally-tagged serde on non_exhaustive enums: add cfg_attr serde(tag) line immediately after the derive cfg_attr line, before the enum declaration"
observability_surfaces:
  - "cargo test --features serde confirms wire format correctness in T03; serde_json::to_string on ExclusionReason/InclusionReason variants produces {\"reason\":\"VariantName\",...} envelopes"
duration: 5min
verification_result: passed
completed_at: 2026-03-21T00:00:00Z
blocker_discovered: false
---

# T01: Fix ExclusionReason and InclusionReason wire format

**Added `#[serde(tag = "reason")]` to `ExclusionReason` and `InclusionReason`, plus `_Unknown` forward-compat variant — zero custom serde code needed.**

## What Happened

Added `#[cfg_attr(feature = "serde", serde(tag = "reason"))]` to both `ExclusionReason` and `InclusionReason` in `crates/cupel/src/diagnostics/mod.rs`. Both enums consist entirely of struct and unit variants, which is exactly what serde's internally-tagged format requires — no custom Serialize/Deserialize impls needed.

Added `_Unknown` as the final variant of `ExclusionReason` with `#[doc(hidden)]` and `#[cfg_attr(feature = "serde", serde(other))]`. This provides a stable deserialization target for unknown `reason` field values from future spec versions, preventing panics when callers parse diagnostic output from newer protocol versions.

Updated the `ExclusionReason` doc comment and inline comment from "adjacent-tagged" to "internally-tagged" to correct the imprecise terminology from D017.

## Verification

- `cargo build --features serde` — exit 0, no warnings ✓
- `cargo clippy --all-targets --features serde -- -D warnings` — exit 0, no warnings ✓
- `cargo test --features serde` — 29 unit + 33 conformance + 33 serde + 35 doc tests = 130 total, 0 failures ✓
- Existing `match` arms on `ExclusionReason` compile cleanly because `#[non_exhaustive]` already required `_` wildcard arms — `_Unknown` slots into the wildcard with no source changes needed elsewhere

## Diagnostics

Future wire format verification: `serde_json::to_string(&ExclusionReason::NegativeTokens { tokens: -1 }).unwrap()` should produce `{"reason":"NegativeTokens","tokens":-1}`. Unknown-variant deserialization: `serde_json::from_str::<ExclusionReason>(r#"{"reason":"FutureVariant"}"#).unwrap()` should return `ExclusionReason::_Unknown`. Both are asserted in T03.

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/diagnostics/mod.rs` — added `#[serde(tag = "reason")]` to `ExclusionReason` and `InclusionReason`; added `_Unknown` variant to `ExclusionReason`; updated doc comments
