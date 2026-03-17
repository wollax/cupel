# S01: Diagnostics Data Types — Research

**Date:** 2026-03-17

## Summary

S01 is purely additive: create a `src/diagnostics/` module in the Rust crate with the full set of diagnostic data types defined in `spec/src/diagnostics/`, then author the 4 missing conformance vectors (one existing `diagnostics-budget-exceeded.toml` counts toward the minimum-5 target). No pipeline wiring, no trait definitions — those belong to S02 and S03.

The spec is complete and unambiguous. All field names, variant counts, wire format, and sort ordering constraints are specified. The .NET reference implementation in `src/Wollax.Cupel/Diagnostics/` provides a second source of truth for the type shapes. The primary risk is the `ExclusionReason` wire format: the spec uses adjacent tagging (`{ "reason": "BudgetExceeded", "item_tokens": 400, "available_tokens": 50 }`), which cannot be modelled with `#[derive(Serialize, Deserialize)]` alone — but serde implementation is deferred to S04. S01 only needs to define the types and write doc comments.

The conformance vector harness in `tests/conformance/pipeline.rs` currently verifies `expected_output` only; the `expected.diagnostics.*` sections in vectors are not checked until S03 wires `run_traced`. S01 can author all 4 new vectors without touching the test harness.

## Recommendation

Create `crates/cupel/src/diagnostics/mod.rs` with all 8 types. Export them from `lib.rs`. Apply `#[non_exhaustive]` to the two enums and the structs that will evolve. Follow the `OverflowStrategy` pattern for enum definition style and the `ContextItem`/`ContextBudget` pattern for struct definition style. Author 4 new conformance vectors and vendor them to `crates/cupel/conformance/required/pipeline/`.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Enum with `#[non_exhaustive]` | `OverflowStrategy` in `src/model/overflow_strategy.rs` | Exact pattern to follow: `#[non_exhaustive]`, `#[derive(Debug, Clone, ...)]`, optional serde behind feature flag |
| Struct with doc comments + derive | `ContextItem`, `ContextBudget` in `src/model/` | Established doc comment style and derive stack for public structs |
| Conditional serde derives | All `#[cfg_attr(feature = "serde", derive(...))]` uses in model types | S01 should stub the `cfg_attr` annotations even though S04 fills them in |

## Existing Code and Patterns

- `crates/cupel/src/model/overflow_strategy.rs` — enum definition pattern: `#[non_exhaustive]`, `#[derive(Debug, Clone, Copy, PartialEq, Eq)]`, `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]`. ExclusionReason and InclusionReason follow this exactly (minus Copy, since data-carrying variants prohibit Copy on ExclusionReason).
- `crates/cupel/src/model/context_item.rs` — struct with private fields, accessor methods, and serde guard. `IncludedItem`/`ExcludedItem`/`SelectionReport` should be simpler (public fields or `pub` structs with `#[non_exhaustive]`).
- `crates/cupel/src/lib.rs` — re-export surface: all new public types must be added here.
- `crates/cupel/src/error.rs` — `CupelError` is `#[non_exhaustive]`; reference for error type style.
- `spec/conformance/required/pipeline/diagnostics-budget-exceeded.toml` — the 1 existing diagnostics vector; covers `BudgetExceeded`. Its `expected.diagnostics.*` schema is the canonical TOML shape for the 4 new vectors.
- `crates/cupel/tests/conformance/pipeline.rs` — test harness that loads vectors; currently only checks `expected_output`. No changes needed in S01.
- `src/Wollax.Cupel/Diagnostics/` — 13-file .NET reference implementation. `ExcludedItem.cs` shows that `score` is `double` (not nullable), confirming spec's "items excluded before Score get 0.0" rule.

## Type Inventory (from spec)

### Types to create in `src/diagnostics/mod.rs`

| Type | Kind | Non-exhaustive? | Notes |
|------|------|-----------------|-------|
| `PipelineStage` | enum | Yes | 5 variants: Classify, Score, Deduplicate, Slice, Place |
| `TraceEvent` | struct | Yes | stage, duration_ms: f64, item_count: usize, message: Option<String> |
| `OverflowEvent` | struct | Yes | tokens_over_budget: i64, overflowing_items: Vec<ContextItem>, budget: ContextBudget |
| `ExclusionReason` | enum | Yes | 8 variants (4 active, 4 reserved), data-carrying |
| `InclusionReason` | enum | Yes | 3 fieldless variants: Scored, Pinned, ZeroToken |
| `IncludedItem` | struct | Yes | item: ContextItem, score: f64, reason: InclusionReason |
| `ExcludedItem` | struct | Yes | item: ContextItem, score: f64 (0.0 for pre-score), reason: ExclusionReason |
| `SelectionReport` | struct | Yes | events: Vec<TraceEvent>, included: Vec<IncludedItem>, excluded: Vec<ExcludedItem>, total_candidates: usize, total_tokens_considered: i64 |

### ExclusionReason variants (data-carrying)

| Variant | Status | Fields |
|---------|--------|--------|
| `BudgetExceeded { item_tokens: i64, available_tokens: i64 }` | Active | |
| `ScoredTooLow { score: f64, threshold: f64 }` | Reserved | |
| `Deduplicated { deduplicated_against: String }` | Active | content identifier |
| `QuotaCapExceeded { kind: String, cap: i64, actual: i64 }` | Reserved | |
| `QuotaRequireDisplaced { displaced_by_kind: String }` | Reserved | |
| `NegativeTokens { tokens: i64 }` | Active | |
| `PinnedOverride { displaced_by: String }` | Active | content identifier |
| `Filtered { filter_name: String }` | Reserved | |

### InclusionReason variants (fieldless)

`Scored`, `Pinned`, `ZeroToken`

## Conformance Vectors

Minimum 5 required. 1 already exists:

| Vector file | Exclusion/Inclusion reason | Status |
|-------------|--------------------------|--------|
| `diagnostics-budget-exceeded.toml` | BudgetExceeded | **exists** |
| `diag-negative-tokens.toml` | NegativeTokens | **to author** |
| `diag-deduplicated.toml` | Deduplicated | **to author** |
| `diag-pinned-override.toml` | PinnedOverride | **to author** |
| `diag-scored-inclusion.toml` | Scored (inclusion) | **to author** |

Vectors live in `spec/conformance/required/pipeline/` and are vendored to `crates/cupel/conformance/required/pipeline/`. Drift guard in CI checks they match.

**PinnedOverride scenario requires** `overflow_strategy = "truncate"` and a non-pinned item that gets displaced when a pinned item doesn't fit. Look at how `tests/conformance/pipeline.rs` reads `overflow_strategy` from config (it does — see the `overflow_strategy` parsing block in `build_pipeline_from_config`).

## Constraints

- All new public types must be `#[non_exhaustive]` — forward-compat requirement (M001-CONTEXT.md technical constraints)
- MSRV 1.85.0 / Edition 2024 — no 1.86+ APIs
- Zero new external dependencies — `ContextItem` and `ContextBudget` are already in the crate; diagnostic types reference them directly
- Serde derives go behind `#[cfg_attr(feature = "serde", ...)]` even in S01 — S04 fills in the actual implementations (custom serialize for `ExclusionReason` wire format with `reason` discriminator field)
- `ExclusionReason` cannot derive `Copy` because it carries `String` fields
- `score` on `ExcludedItem` is `f64` not `Option<f64>` — items excluded before Score stage get `0.0`
- `excluded` list in `SelectionReport` must be sorted by score descending (stable on ties by insertion order) — not a concern for S01 type definitions, but must be documented in struct doc comment

## Common Pitfalls

- **`ExclusionReason` serde wire format** — The spec uses adjacent tagging: `{ "reason": "BudgetExceeded", "item_tokens": ..., "available_tokens": ... }`. Standard `#[serde(tag = "type")]` or `#[serde(tag = "reason")]` with externally-tagged content won't match this. S01 should stub `// serde impl in S04` comments on the enum. Do not attempt to implement serde in S01.
- **`ExclusionReason` as `#[non_exhaustive]`** — The `match` exhaustiveness guard on reserved variants still applies; downstream code must handle unknown variants. Document this in the enum's doc comment.
- **`OverflowEvent` is a data type only** — The spec explicitly says "delivery mechanism is left to implementations." Do not define any callback/observer mechanism in S01. Just the struct.
- **Naming: `duration_ms` not `duration`** — Field must match spec exactly for serde round-trip in S04.
- **`item_count: usize` vs `i64`** — The spec says "integer"; use `usize` for count fields (consistent with `Vec::len()`) but `i64` for token counts (consistent with `ContextItem::tokens()` which is `i64`).

## Open Risks

- **Conformance vector test harness coverage**: The `pipeline.rs` test harness skips `expected.diagnostics.*` verification. S01 authors the vectors but they won't be exercised until S03. This means a malformed vector could pass CI in S01 and only fail in S03. Mitigation: manually verify the TOML structure matches the `diagnostics-budget-exceeded.toml` schema exactly.
- **`PinnedOverride` scenario complexity**: Triggering `PinnedOverride` requires `OverflowStrategy::Truncate` plus a placement collision. The existing `pinned-items.toml` vector exercises the happy path; a truncate collision scenario needs careful item sizing. Verify with a local `cargo test` run after authoring the vector.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust | (core language — no specialized skill needed) | none required |

## Sources

- `ExclusionReason` variant table, wire format, and conformance notes (source: `spec/src/diagnostics/exclusion-reasons.md`)
- `TraceEvent` fields, `PipelineStage` enumeration, `OverflowEvent` structure (source: `spec/src/diagnostics/events.md`)
- `SelectionReport` fields, sort ordering, `IncludedItem`/`ExcludedItem` shapes (source: `spec/src/diagnostics/selection-report.md`)
- `TraceCollector` contract, `NullTraceCollector`, `DiagnosticTraceCollector`, `TraceDetailLevel` (source: `spec/src/diagnostics/trace-collector.md` — consumed by S02, documented here for context)
- .NET reference types confirming `score: double` (not nullable) on `ExcludedItem` (source: `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs`)
- Existing conformance vector schema for `expected.diagnostics.*` (source: `spec/conformance/required/pipeline/diagnostics-budget-exceeded.toml`)
- `#[non_exhaustive]` enum pattern (source: `crates/cupel/src/model/overflow_strategy.rs`)
