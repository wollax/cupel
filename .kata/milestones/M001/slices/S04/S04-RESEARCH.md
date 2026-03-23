# S04: Diagnostics Serde Integration — Research

**Date:** 2026-03-21

## Summary

S04 adds `Serialize`/`Deserialize` support to all diagnostic types under `--features serde`. The primary deliverable is a correct `SelectionReport` serde round-trip that matches the spec wire format. The wire format is **internally-tagged** (`{ "reason": "BudgetExceeded", "item_tokens": ..., ... }`) for `ExclusionReason` and `InclusionReason`, and a plain struct derive for `SelectionReport` and its container types.

The good news: the core technical problem is fully solvable with `#[serde(tag = "reason")]` on both `ExclusionReason` and `InclusionReason`. The D017 comment ("adjacent-tagged wire format cannot be derived") was a scoping note, not a technical truth — the spec wants serde's _internally-tagged_ format (not adjacently-tagged), and all variants are struct/unit variants which is exactly what `#[serde(tag = "...")] ` requires to work. No custom `Serialize`/`Deserialize` impl is needed for these types.

Two additional tasks require attention: (1) `SelectionReport` currently has NO serde derive and must have one added, and (2) `DiagnosticTraceCollector.excluded: Vec<(ExcludedItem, usize)>` exposes the internal insertion-order index in serialized form — the field must be handled with a serde helper that strips the `usize` before serializing.

## Recommendation

1. **`ExclusionReason`**: Replace the bare `cfg_attr` derive stub with `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]` + `#[cfg_attr(feature = "serde", serde(tag = "reason"))]`. All variants are struct variants → internally-tagged format compiles and round-trips correctly. Also add a `#[serde(other)]` unit variant `_Unknown` (with `#[doc(hidden)]`) to satisfy the spec requirement for graceful unknown-variant handling on deserialization.

2. **`InclusionReason`**: Same treatment — add `#[serde(tag = "reason")]`. Unit variants with internal tagging produce `{ "reason": "Scored" }`, which matches the spec exactly.

3. **`SelectionReport`**: Add `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]`. No invariants justify a custom impl for _serialization_. For _deserialization_, consider a minimal validation layer that checks `total_candidates == included.len() + excluded.len()` per the spec's conformance note — this follows the validation-on-deserialize pattern (R006 note). Use the same `Raw` + constructor pattern from `ContextBudget`.

4. **`DiagnosticTraceCollector.excluded` field**: The field type `Vec<(ExcludedItem, usize)>` must not expose the `usize` in serialized form. Two options ranked by preference:
   - **(Preferred)** Add `#[cfg_attr(feature = "serde", serde(serialize_with = "excluded_items_ser", deserialize_with = "excluded_items_de"))]` with helpers that strip/reconstruct the index. Reconstruction on deserialization uses 0, 1, 2, ... (insertion order).
   - Alternative: Skip the field entirely with `#[cfg_attr(feature = "serde", serde(skip))]` — simpler, but round-trip loses excluded item data. Only acceptable if `DiagnosticTraceCollector` serde is treated as write-only / not intended for full round-trip.

5. **Tests**: Add to `crates/cupel/tests/serde.rs` following the existing pattern. Target: round-trips for all 8 `ExclusionReason` variants, all 3 `InclusionReason` variants, a full `SelectionReport` with mixed inclusion/exclusion, and wire-format assertions for the `reason` discriminator field.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Internally-tagged enum serde | `#[serde(tag = "reason")]` on enum | Serde natively produces `{ "reason": "...", ...fields }` for struct variants; zero custom code |
| Unknown variant graceful handling | `#[serde(other)]` on a unit variant | Serde's supported escape hatch for internal-tag enums; avoids full custom Deserializer |
| Struct field stripping for serde | `#[serde(serialize_with = "...")]` + helper | Less code than a full custom `Serialize`; field-level granularity |
| Validation-on-deserialize | `Raw` helper struct + constructor pattern | Established pattern in `ContextBudget` and `ContextItem`; `serde::de::Error::custom` for validation failures |

## Existing Code and Patterns

- `crates/cupel/src/model/context_budget.rs` — canonical validation-on-deserialize pattern: `Raw` struct with `#[serde(deny_unknown_fields)]`, deserialized first, then passed to constructor that returns `Result`. Use the same pattern for `SelectionReport` if validation is required.
- `crates/cupel/src/model/context_item.rs` — same pattern for a type with many optional fields. Note: `ContextItem` uses `#[serde(deny_unknown_fields)]` — consider whether `SelectionReport` should too.
- `crates/cupel/tests/serde.rs` — existing serde integration test file; the correct home for all new diagnostics serde tests. Already imports `serde_json`, `chrono`, and cupel types. Add diagnostics tests in a new section (e.g. `// 6. Diagnostics serde tests`).
- `crates/cupel/examples/serde_roundtrip.rs` — a good place to add a `SelectionReport` round-trip demo for human verification; extend after tests pass.
- `crates/cupel/src/diagnostics/mod.rs` — all 8 diagnostic types; `ExclusionReason` and `InclusionReason` need `#[serde(tag = "reason")]`; `SelectionReport` needs the full derive added.
- `crates/cupel/src/diagnostics/trace_collector.rs` — `DiagnosticTraceCollector.excluded` needs serde helper; `NullTraceCollector` and `TraceDetailLevel` stubs are already correct.

## Constraints

- `serde` must remain behind the feature flag. No `use serde::...` imports at the module level — only inside `#[cfg(feature = "serde")]` blocks or as `serde::Serialize` in `cfg_attr`.
- MSRV 1.85.0 / Edition 2024 — no unstable features; serde 1.x is already in `[dependencies]`.
- `#[non_exhaustive]` is on all public diagnostic types. Adding a `_Unknown` serde variant to `ExclusionReason` is a public API addition (even with `#[doc(hidden)]`) — document in the commit.
- `serde_json` is already in `[dev-dependencies]` — use it for test assertions (not `toml`).
- `DiagnosticTraceCollector` is documented as "incorrect until S04" in its serde note — this is the slice that fulfills that promise.
- Do NOT change the `ExclusionReason` variant names or field names — they must match the spec wire format exactly (variant names become `reason` values; field names become sibling JSON keys).
- `InclusionReason` fieldless variants must also use the envelope format `{ "reason": "Scored" }` — NOT the serde default of `"Scored"` (bare string). This is specified in `spec/src/diagnostics/exclusion-reasons.md`.

## Common Pitfalls

- **Externally-tagged vs internally-tagged confusion**: Serde defaults to externally-tagged for enums (`{ "BudgetExceeded": { ... } }`). The spec wire format is internally-tagged (`{ "reason": "BudgetExceeded", ... }`). Forgetting `#[serde(tag = "reason")]` will compile silently but produce wrong JSON. Wire-format assertions in tests are essential.
- **`InclusionReason` bare string**: Without `#[serde(tag = "reason")]`, fieldless unit variants serialize as bare strings (`"Scored"`). The spec requires `{ "reason": "Scored" }`. Add the tag attribute to `InclusionReason` too.
- **Serde internally-tagged + `#[non_exhaustive]`**: The combination works for _serialization_ and _known-variant deserialization_. For unknown variants, serde will error unless a `#[serde(other)]` unit variant is present. Omitting this will cause `serde_json::from_str::<ExclusionReason>(r#"{"reason":"FutureVariant"}"#)` to panic in tests from future spec versions. Add `_Unknown` with `#[serde(other)]` to future-proof.
- **`DiagnosticTraceCollector.excluded usize leak**: The current derive stub will serialize each excluded item as a tuple `[item, index]` in JSON. The usize insertion index is an internal sort key and must not appear in the wire format. Missing this will produce invalid JSON output and break any consumer parsing `SelectionReport.excluded` items.
- **`SelectionReport` no-op derive pitfall**: `SelectionReport` contains `Vec<ExcludedItem>` which contains `ExclusionReason`. If `ExclusionReason`'s serde is wrong, `SelectionReport`'s derive will compile but produce wrong JSON. Always verify the full chain with a top-level round-trip test, not just individual-type tests.
- **Unknown field rejection for `SelectionReport`**: If using `#[serde(deny_unknown_fields)]` on the `Raw` helper for `SelectionReport` (recommended), consumers sending extra fields will get errors. This is the established pattern for cupel types but restricts forward extensibility. Acceptable for `SelectionReport` (diagnostic output, not configuration input) — but note the tradeoff.

## Open Risks

- **`_Unknown` variant visibility**: Adding `_Unknown` with `#[doc(hidden)]` to `ExclusionReason` changes the public API (no matter how hidden). External `match` users already need a `_` arm due to `#[non_exhaustive]`, so the change is non-breaking in practice, but it's a new variant. Document in the PR.
- **`DiagnosticTraceCollector` round-trip fidelity**: The `callback` field is skipped during serde. A round-trip of `DiagnosticTraceCollector` silently drops any registered callback. This is documented in the code; ensure the doc comment is updated to reflect that S04 is the slice where the serde impl becomes "correct" (as promised in the existing doc comment).
- **`SelectionReport` invariant validation scope**: R006 says "Must follow validation-on-deserialize pattern". The two checkable invariants are `total_candidates == included.len() + excluded.len()` and `total_tokens_considered == sum(item.tokens)`. Implementing both is correct and cheap. Skipping them is simpler but violates the stated requirement. Recommend implementing at least `total_candidates` validation.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Serde (Rust) | n/a | No dedicated skill; Context7 docs available |

## Sources

- `spec/src/diagnostics/exclusion-reasons.md` — authoritative wire format spec; JSON examples for all variants; `{ "reason": "...", ...fields }` format confirmed (internally-tagged, not adjacently-tagged)
- `spec/src/diagnostics/selection-report.md` — full JSON example for `SelectionReport`; conformance notes on `total_candidates` invariant
- `crates/cupel/src/diagnostics/mod.rs` — current state: 7 types with `cfg_attr` stubs, `SelectionReport` with NO serde derive; `ExclusionReason`/`InclusionReason` need `#[serde(tag = "reason")]`
- `crates/cupel/src/diagnostics/trace_collector.rs` — `DiagnosticTraceCollector.excluded: Vec<(ExcludedItem, usize)>` with existing `#[cfg_attr(feature = "serde", derive(...))]` stub; broken without fix
- `crates/cupel/src/model/context_budget.rs` — validation-on-deserialize pattern to follow for `SelectionReport`
- `crates/cupel/tests/serde.rs` — existing serde tests; add diagnostics section here
