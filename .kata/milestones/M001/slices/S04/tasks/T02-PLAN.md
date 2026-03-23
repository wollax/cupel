---
estimated_steps: 6
estimated_files: 2
---

# T02: Add SelectionReport serde and fix DiagnosticTraceCollector.excluded

**Slice:** S04 — Diagnostics Serde Integration
**Milestone:** M001

## Description

Two types still need serde work after T01:

**`SelectionReport`**: Has no serde derive at all. Must add `Serialize` (derive is fine — no transformation needed) and a custom `Deserialize` using the `Raw` pattern from `context_budget.rs`. The `Raw` struct uses `#[serde(deny_unknown_fields)]`; the `Deserialize` impl validates that `total_candidates == included.len() + excluded.len()` (R006 note; the one checkable invariant using available data).

**`DiagnosticTraceCollector.excluded`**: Currently `Vec<(ExcludedItem, usize)>`. The bare derive produces JSON like `[[item, 0], [item, 1]]` — the `usize` insertion index leaks into the wire format. The fix is two small free functions: `ser_excluded_items` strips the usize on serialize; `de_excluded_items` reconstructs it (sequential 0, 1, 2, ...) on deserialize. The field gets `serialize_with`/`deserialize_with` attributes.

`DiagnosticTraceCollector`'s doc comment currently says "output will be **incorrect until S04**" — update it now that S04 is landed.

## Steps

1. In `crates/cupel/src/diagnostics/mod.rs`, add `#[cfg_attr(feature = "serde", derive(serde::Serialize))]` to `SelectionReport`. This is safe now because `ExclusionReason` and `InclusionReason` have correct serde from T01.

2. Add a `#[cfg(feature = "serde")]` block in `mod.rs` with the custom `Deserialize<'de>` impl for `SelectionReport`. Follow the `ContextBudget` pattern exactly: define `RawSelectionReport` with `#[serde(deny_unknown_fields)]` containing the same fields as `SelectionReport`, then deserialize the raw struct, validate `total_candidates == raw.included.len() + raw.excluded.len()` using `serde::de::Error::custom` for the failure case, and construct `SelectionReport` from the raw fields. Add `use serde::{Deserialize, Deserializer};` inside the `#[cfg(feature = "serde")]` block only.

3. In `crates/cupel/src/diagnostics/trace_collector.rs`, inside a `#[cfg(feature = "serde")]` block, add two free functions:
   ```rust
   fn ser_excluded_items<S: serde::Serializer>(
       items: &Vec<(ExcludedItem, usize)>,
       serializer: S,
   ) -> Result<S::Ok, S::Error> {
       use serde::ser::SerializeSeq;
       let mut seq = serializer.serialize_seq(Some(items.len()))?;
       for (item, _) in items {
           seq.serialize_element(item)?;
       }
       seq.end()
   }

   fn de_excluded_items<'de, D: serde::Deserializer<'de>>(
       deserializer: D,
   ) -> Result<Vec<(ExcludedItem, usize)>, D::Error> {
       use serde::Deserialize;
       let items = Vec::<ExcludedItem>::deserialize(deserializer)?;
       Ok(items.into_iter().enumerate().map(|(i, item)| (item, i)).collect())
   }
   ```

4. On the `excluded` field in `DiagnosticTraceCollector`, replace the bare `#[cfg_attr(feature = "serde", derive(...))]` (from the struct-level derive) with explicit field-level attributes. Since the struct uses `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]`, add a separate attribute on the `excluded` field:
   ```rust
   #[cfg_attr(feature = "serde", serde(serialize_with = "ser_excluded_items", deserialize_with = "de_excluded_items"))]
   excluded: Vec<(ExcludedItem, usize)>,
   ```

5. Update `DiagnosticTraceCollector`'s serde doc comment: remove the "output will be **incorrect until S04**" note and replace with "Serde support is complete as of S04. The `callback` field is always skipped — callbacks cannot be serialized."

6. Run `cargo build --features serde` and `cargo test --features serde` to confirm no regressions. If there are any `#[cfg(feature = "serde")]` import issues in `mod.rs`, add the minimal use statements needed inside the cfg block.

## Must-Haves

- [ ] `SelectionReport` has `#[cfg_attr(feature = "serde", derive(serde::Serialize))]`
- [ ] `SelectionReport` has a custom `Deserialize<'de>` impl using the `Raw` pattern
- [ ] `SelectionReport` deserialization rejects JSON where `total_candidates != included.len() + excluded.len()`
- [ ] `DiagnosticTraceCollector.excluded` serializes as `Vec<ExcludedItem>` (no usize)
- [ ] `DiagnosticTraceCollector.excluded` deserializes from `Vec<ExcludedItem>` (indices reconstructed as 0, 1, 2, ...)
- [ ] `cargo test --features serde` passes all existing tests (no regressions)
- [ ] `cargo build --features serde` exits 0 with no warnings
- [ ] `cargo clippy --all-targets -- -D warnings` passes with no new warnings

## Verification

- `cargo test --features serde` — all 29 existing tests pass; zero failures
- `cargo build --features serde` — exits 0, no warnings
- `cargo clippy --all-targets -- -D warnings` — exits 0, no warnings
- Manual sanity: `SelectionReport` is now referenced in `serde.rs` imports without compilation failure

## Observability Impact

- Signals added/changed: `SelectionReport` can now be serialized/deserialized; `DiagnosticTraceCollector` produces valid JSON without leaking internal indices; validation error on `total_candidates` mismatch uses `serde::de::Error::custom` with a descriptive message
- How a future agent inspects this: `cargo test --features serde` confirms round-trips work; `serde_json::from_str::<SelectionReport>` with mismatched `total_candidates` returns a descriptive error message
- Failure state exposed: `serde::de::Error::custom("total_candidates N does not equal included.len() M + excluded.len() K")`

## Inputs

- `crates/cupel/src/diagnostics/mod.rs` — `SelectionReport` struct (no serde derive); `ExclusionReason` + `InclusionReason` with correct wire format from T01
- `crates/cupel/src/diagnostics/trace_collector.rs` — `DiagnosticTraceCollector` with `excluded: Vec<(ExcludedItem, usize)>` and existing derive stub
- `crates/cupel/src/model/context_budget.rs` — canonical `Raw` pattern for validation-on-deserialize
- T01-SUMMARY.md — confirms ExclusionReason/InclusionReason serde is correct (prerequisite)

## Expected Output

- `crates/cupel/src/diagnostics/mod.rs` — `SelectionReport` with `Serialize` derive + custom `Deserialize` impl with `Raw` validation
- `crates/cupel/src/diagnostics/trace_collector.rs` — `ser_excluded_items` + `de_excluded_items` helpers; `excluded` field with `serialize_with`/`deserialize_with`; updated doc comment
