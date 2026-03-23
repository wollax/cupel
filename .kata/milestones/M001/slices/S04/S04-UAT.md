# S04: Diagnostics Serde Integration — UAT

**Milestone:** M001
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All contracts are machine-verifiable via `cargo test --features serde` and `cargo run --example serde_roundtrip --features serde`. There is no UI, no service, and no human-experience surface to exercise. The test suite covers wire-format assertions, round-trips for every variant, validation rejection, and graceful unknown handling. Running the commands below fully proves the slice.

## Preconditions

- Rust toolchain present (`rustup` / `cargo`)
- Working directory: `crates/cupel/`
- No preconditions beyond a normal `cargo build`

## Smoke Test

```bash
cd crates/cupel
cargo test --features serde
```

Expected: `test result: ok. N passed; 0 failed` across all test suites (unit, conformance, serde integration, doctests).

## Test Cases

### 1. Wire format — ExclusionReason is internally-tagged

```bash
cargo test --features serde exclusion_reason_budget_exceeded_wire_format
```

**Expected:** test passes; JSON produced is `{"reason":"BudgetExceeded","item_tokens":...,"available_tokens":...}` (internally-tagged, not `{"BudgetExceeded":{...}}`).

### 2. Wire format — InclusionReason is internally-tagged

```bash
cargo test --features serde inclusion_reason_scored_wire_format
```

**Expected:** test passes; JSON produced is `{"reason":"Scored"}` (not bare `"Scored"` string).

### 3. All 8 ExclusionReason variants round-trip

```bash
cargo test --features serde roundtrip_exclusion
```

**Expected:** 8 tests pass (`roundtrip_exclusion_budget_exceeded`, `roundtrip_exclusion_scored_too_low`, `roundtrip_exclusion_deduplicated`, `roundtrip_exclusion_quota_cap_exceeded`, `roundtrip_exclusion_quota_require_displaced`, `roundtrip_exclusion_negative_tokens`, `roundtrip_exclusion_pinned_override`, `roundtrip_exclusion_filtered`).

### 4. All 3 InclusionReason variants round-trip

```bash
cargo test --features serde roundtrip_inclusion
```

**Expected:** 3 tests pass with field equality preserved.

### 5. SelectionReport full round-trip

```bash
cargo test --features serde roundtrip_selection_report_full
```

**Expected:** test passes; `total_candidates`, `included`, and `excluded` all survive the round-trip with correct `reason` fields.

### 6. SelectionReport validation rejection

```bash
cargo test --features serde selection_report_validation_rejects_mismatched_total
```

**Expected:** test passes; `serde_json::from_str` returns `Err` when `total_candidates` doesn't match `included.len() + excluded.len()`.

### 7. Graceful unknown variant

```bash
cargo test --features serde exclusion_reason_unknown_variant_graceful
```

**Expected:** test passes; `{"reason":"FutureVariantFromSpec3"}` deserializes to `ExclusionReason::_Unknown` without panic or error.

### 8. Live pipeline → SelectionReport → JSON demo

```bash
cargo run --example serde_roundtrip --features serde
```

**Expected:** exits 0; output includes a block like:
```
"reason": {
  "reason": "BudgetExceeded",
  "item_tokens": ...,
  "available_tokens": ...
}
```
and a "Round-trip verified" summary line.

## Edge Cases

### SelectionReport with mismatched total_candidates is rejected

```bash
# Construct JSON manually: total_candidates: 99 but only 2 items
cargo test --features serde selection_report_validation_rejects_mismatched_total
```

**Expected:** `Err` returned; error message contains `"total_candidates"`.

### Unknown ExclusionReason variant does not panic

```bash
cargo test --features serde exclusion_reason_unknown_variant_graceful
```

**Expected:** `Ok(ExclusionReason::_Unknown)` — no panic.

### Build without serde feature remains clean

```bash
cargo build
cargo clippy --all-targets -- -D warnings
```

**Expected:** exit 0, no warnings — serde code is fully gated behind `cfg(feature = "serde")`.

## Failure Signals

- Any `FAILED` line in `cargo test --features serde` output
- JSON containing `{"BudgetExceeded":{...}}` (externally-tagged) instead of `{"reason":"BudgetExceeded",...}`
- JSON containing bare `"Scored"` string instead of `{"reason":"Scored"}`
- `cargo build --features serde` emitting warnings
- `cargo clippy --all-targets -- -D warnings` emitting any diagnostic

## Requirements Proved By This UAT

- R006 — Diagnostics serde coverage: all diagnostic types (`ExclusionReason`, `InclusionReason`, `SelectionReport`, and their component types) serialize/deserialize correctly behind the `serde` feature flag, following the validation-on-deserialize pattern; wire format matches spec (internally-tagged reason envelopes); forward-compat deserialization works for unknown variants

## Not Proven By This UAT

- Serialization of `DiagnosticTraceCollector` itself (only `SelectionReport` output is typically serialized; collector serde is a secondary concern)
- OTel / tracing integration (R022 — deferred to v1.3)
- KnapsackSlice DP guard (R002 — owned by S07)
- CI enforcement of clippy --all-targets (R003 — owned by S05)
- .NET serde equivalents (not applicable; .NET uses System.Text.Json)

## Notes for Tester

- All test cases are fully automated — no manual input required.
- The `_Unknown` variant is intentionally undocumented (marked `#[doc(hidden)]`) and should never appear in output from the built-in pipeline stages. If you see it in a real diagnostic report, it means the sender is using a newer spec version than the current crate.
- `DiagnosticTraceCollector.callback` is always skipped during serialization — this is expected. A deserialized collector will have `callback: None`.
