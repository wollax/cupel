# S03: CountQuotaSlice — Rust + .NET Implementation — Research

**Researched:** 2026-03-23
**Domain:** Slicer implementation (Rust + .NET dual-language, decorator pattern, ExclusionReason extension)
**Confidence:** HIGH — design fully locked in `.planning/design/count-quota-design.md`; codebase patterns established by S01/S02

## Summary

S03 implements `CountQuotaSlice` in both Rust and .NET. The design is completely locked via 14 design
decisions (DI-1 through DI-6, D040, D046, D052–D057). No design work is required. The pseudocode is
in `.planning/design/count-quota-design.md` as `COUNT-DISTRIBUTE-BUDGET`.

The primary structural work is: (1) adding two new `ExclusionReason` variants in both languages
(`CountCapExceeded`, `CountRequireCandidatesExhausted`), (2) adding `count_requirement_shortfalls`
to `SelectionReport` in both languages, (3) implementing the two-phase algorithm as a slicer, and
(4) writing 5 conformance vectors covering the 5 scenario sketches from the design document.

Key gate: verify `#[non_exhaustive]` is set on `ExclusionReason` before adding variants (D052
precondition). **Status: CLEAR.** `ExclusionReason` in `crates/cupel/src/diagnostics/mod.rs` is
already `#[non_exhaustive]`. Same for the .NET `ExclusionReason` enum — it is a plain enum (not
an exhaustive pattern-match source), so adding variants is safe for property-access callers.

One significant difference from scorer slices: `CountQuotaSlice` involves no conformance vectors
that run through the full `run_traced` pipeline — only the `Slicer::slice` interface is exercised.
The existing `slicing.rs` conformance harness is the right test vehicle. However, exclusion-reason
verification requires either a custom test pattern or using `dry_run` to capture the `SelectionReport`.

## Recommendation

Implement in three tasks mirroring S01/S02:

- **T01 — Rust implementation** (`crates/cupel/src/slicer/count_quota.rs`): new `ScarcityBehavior`
  enum, `CountQuotaEntry` struct, `CountQuotaSlice` struct implementing `Slicer`, new
  `ExclusionReason` variants, new `SelectionReport.count_requirement_shortfalls` field.
- **T02 — Conformance vectors**: 5 TOML vectors in all three canonical locations; extend
  `build_slicer_by_type` with `"count_quota"` arm; extend harness to verify exclusion reasons
  and shortfalls (not just selected_contents).
- **T03 — .NET implementation** (`src/Wollax.Cupel/Slicing/CountQuotaSlice.cs`): matching
  `ScarcityBehavior` enum, `CountQuotaEntry` class, `CountQuotaSlice` class, new `ExclusionReason`
  variants, `CountRequirementShortfalls` property on `SelectionReport`, TUnit tests.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Budget distribution across kinds | `QuotaSlice` DISTRIBUTE-BUDGET logic in `quota.rs` | The COUNT-DISTRIBUTE-BUDGET pseudocode is an extension of this; the residual-budget Phase 2 is identical logic with a smaller candidate pool |
| KnapsackSlice OOM guard pattern | `KnapsackSlice::slice` `TableTooLarge` check | D052 KnapsackSlice guard follows the same construction-time throw pattern |
| Per-kind score-descending iteration | `GreedySlice.slice` density sort + greedy fill | Phase 1 (count-satisfy) iterates per-kind by score desc — same pattern as GreedySlice's density loop but without the density computation |
| ExclusionReason serde wire format | `#[serde(tag = "reason")]` already on ExclusionReason | New variants get auto-serialized with the same internally-tagged format; no custom serde needed |
| PublicAPI.Unshipped.txt tracking | S01 established the pattern | All new public types and members must be added to `PublicAPI.Unshipped.txt` before `dotnet build` will pass |

## Existing Code and Patterns

- `crates/cupel/src/slicer/quota.rs` — Full DISTRIBUTE-BUDGET implementation in Rust; Phase 2 of COUNT-DISTRIBUTE-BUDGET replicates this logic on residual candidates and residual budget; copy the partition/mass/distribution structure
- `crates/cupel/src/slicer/greedy.rs` — Score-descending greedy loop; Phase 1 needs the same per-kind sort-by-score and fill
- `crates/cupel/src/diagnostics/mod.rs` line 100+ — `ExclusionReason` enum with `#[non_exhaustive]` + `#[serde(tag = "reason")]`; add `CountCapExceeded` and `CountRequireCandidatesExhausted` variants here
- `crates/cupel/src/diagnostics/mod.rs` line 266+ — `SelectionReport` struct with `#[non_exhaustive]`; add `count_requirement_shortfalls: Vec<CountRequirementShortfall>` field here
- `crates/cupel/tests/conformance.rs` `build_slicer_by_type()` function — add `"count_quota"` arm to parse `CountQuotaSlice` config from TOML
- `crates/cupel/tests/conformance/slicing.rs` — existing slicing conformance test runner; need an extended version that also checks exclusion reasons and shortfalls (not just `selected_contents`)
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — flat enum; add `CountCapExceeded` and `CountRequireCandidatesExhausted` values here; no attributes needed (no discriminated union in .NET)
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — sealed record; add `CountRequirementShortfalls` property; `required IReadOnlyList<CountRequirementShortfall> CountRequirementShortfalls { get; init; }` — but see pitfall below on non-required with default
- `src/Wollax.Cupel/Slicing/QuotaSlice.cs` — .NET QuotaSlice; same structural model for CountQuotaSlice decorator
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — must be updated for every new public type and member

## Constraints

- `#[non_exhaustive]` already set on Rust `ExclusionReason` — safe to add variants without breaking downstream `match` exhaustiveness (existing `_Unknown` catch-all in serde is unrelated to exhaustive matching)
- .NET `ExclusionReason` is a plain flat enum — new variants are backward-compatible for property-access callers; positional deconstruction callers already documented as unsupported (D057)
- `SelectionReport` in Rust is `#[non_exhaustive]` — adding `count_requirement_shortfalls` is safe for struct-construction callers (they use `..` spread)
- `SelectionReport` in .NET is a `sealed record` with `required` properties — new property must NOT be `required` (would break all existing call sites that don't set it); use `IReadOnlyList<CountRequirementShortfall> CountRequirementShortfalls { get; init; } = [];` (default empty)
- D055: non-exclusive tag semantics are not configurable — no toggle, no per-entry exclusivity mode
- D056: `ScarcityBehavior::Degrade` is the default; `Throw` is opt-in — constructor parameter with default
- D052: KnapsackSlice guard must be enforced at construction time — check `typeof(inner) == KnapsackSlice` (Rust: `type_id` or trait downcast; simpler: use `std::any::Any`; even simpler: check by type name via a marker method on `Slicer`) — the design doc doesn't prescribe the detection mechanism; simplest approach is a `fn is_knapsack(&self) -> bool { false }` default method on `Slicer` trait overridden in `KnapsackSlice`
- D082 applies: conformance vectors must be copied to all THREE locations: `spec/conformance/required/slicing/`, `conformance/required/slicing/` (repo root), AND `crates/cupel/conformance/required/slicing/` — pre-commit hook checks root vs crates diff
- Serde: `SelectionReport` has a custom `Deserialize` impl using `RawSelectionReport` — adding `count_requirement_shortfalls` requires updating BOTH the `RawSelectionReport` inner struct AND the construction of `SelectionReport` from it; `#[serde(default)]` on the raw field handles old wire payloads that lack the field

## Common Pitfalls

- **`SelectionReport.CountRequirementShortfalls` must NOT be `required` in .NET** — `required` properties in a record break all existing call sites that construct `SelectionReport` via `new SelectionReport { ... }` without setting the new property. Use `= []` (empty init default) instead. This is the same pattern as any optional field addition to a `sealed record`.
- **Serde custom Deserialize needs updating for new `SelectionReport` field** — The existing `RawSelectionReport` struct inside the custom `Deserialize` impl (line 288 in `mod.rs`) must include `count_requirement_shortfalls` with `#[serde(default)]`. Forgetting this causes deserialization to reject old payloads with an "unknown field" or "missing field" error.
- **KnapsackSlice detection mechanism** — Using `std::any::Any + TypeId` requires adding `as_any()` to `Slicer` trait (which was specifically removed in S07 of M001, per D037 context). A cleaner approach is adding `fn is_knapsack(&self) -> bool { false }` as a default method on `Slicer` and overriding it in `KnapsackSlice`. This avoids Any/downcast entirely.
- **Phase 1 iterates candidates, not scored_items globally** — The count-satisfy phase operates on items grouped by kind. Items committed in Phase 1 must be removed from the candidate pool BEFORE Phase 2 runs. Forgetting to remove them causes items to be double-counted (selected in both Phase 1 and Phase 2).
- **Pinned-count decrement** — The design (DI-5) requires decrementing `require_count(K)` by `pinned_count(K)` before Phase 1. The slicer receives only `sorted: &[ScoredItem]` (non-pinned candidates); pinned items are committed by the pipeline before the slicer runs. The `ContextBudget` carries `pinned_tokens` but NOT pinned item counts. The caller must pass pinned counts separately — OR the slicer must accept a `pinned_count_by_kind: HashMap<ContextKind, usize>` in its constructor or `slice` call. The design doc says the slicer receives only non-pinned candidates; the pinned-count-decrement rule means the slicer must know how many pinned items there were for each kind. **Resolution**: the `ISlicer.Slice` / `Slicer::slice` interface signature does NOT include pinned item counts — the caller (pipeline) has already excluded pinned items from `sorted`. The slicer therefore cannot know the pinned count. For v1, the conformance vectors should NOT test pinned-count decrement (it requires pipeline-level integration). The design's pinned-count decrement requirement is a pipeline concern. The slicer only sees non-pinned candidates — if `require_count(K)` says "2" and there are already 2 pinned items of kind K, the pipeline should pass 0 candidates of kind K (or the slicer should still select 2, resulting in 4 total). **Clarification**: the slicer has no visibility into pinned items; pinned items bypass the slicer entirely. The `require_count` should be interpreted by the slicer against the candidates it receives. Shortfall detection only applies to non-pinned candidates. This is consistent with D053 ("count-quota caps apply to slicer-selected items only").
- **Conformance vector testing needs exclusion reason verification** — The existing `slicing.rs` harness only checks `selected_contents`. The CountCapExceeded vectors require verifying which items were excluded with which reason. The conformance vectors for count-quota need to go through `Pipeline::dry_run` (not just `Slicer::slice` directly) to capture the full `SelectionReport` with exclusion reasons. **Alternative**: add a separate test module that constructs a `CountQuotaSlice` directly and verifies exclusions by wrapping it with a `DiagnosticTraceCollector` directly.
- **Three-location vector copy** — vectors must be in `spec/conformance/required/slicing/`, `conformance/required/slicing/` (repo root), AND `crates/cupel/conformance/required/slicing/`. The pre-commit hook only checks root vs crates; spec/ is separate. All three must stay in sync per D082.

## Open Risks

- **Exclusion reason verification in Rust conformance harness**: The existing slicing conformance tests only check `selected_contents`. Adding `CountCapExceeded` and `CountRequireCandidatesExhausted` exclusion reasons requires a new test pattern. Options: (a) extend `run_slicing_test` to also check `expected.excluded_reasons`, (b) write unit tests directly against `CountQuotaSlice` using a manual score-collection loop. Option (a) is cleaner but requires extending the harness. Option (b) is simpler but doesn't go through TOML vectors.
- **`is_knapsack` guard on Slicer trait**: Adding `fn is_knapsack(&self) -> bool { false }` to `Slicer` is a non-breaking additive change (defaulted method). But it's a questionable API — it's a special-case predicate on a general trait. Document as internal use in the CountQuotaSlice guard. Alternatively: just check via `std::any::TypeId` if `Slicer: Any`, but that requires adding `Any` bound or a cast method back to the trait.
- **Rust `SelectionReport` serde compatibility**: The custom `Deserialize` impl uses `#[serde(deny_unknown_fields)]` on `RawSelectionReport`. Adding `count_requirement_shortfalls` to `RawSelectionReport` with `#[serde(default)]` handles old payloads (missing field = empty vec). Without `#[serde(default)]`, old payloads fail deserialization.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust (general) | none needed | n/a — patterns established in S01/S02 |
| .NET (general) | none needed | n/a — patterns established in S01/S02 |

## Sources

- `.planning/design/count-quota-design.md` — Complete locked design with pseudocode (HIGH confidence)
- `crates/cupel/src/slicer/quota.rs` — Existing QuotaSlice Rust implementation to follow/extend (HIGH confidence)
- `src/Wollax.Cupel/Slicing/QuotaSlice.cs` — Existing QuotaSlice .NET implementation to follow (HIGH confidence)
- `crates/cupel/src/diagnostics/mod.rs` — ExclusionReason and SelectionReport structures (HIGH confidence)
- `crates/cupel/tests/conformance.rs` — Conformance harness; `build_slicer_by_type` to extend (HIGH confidence)
- S01-SUMMARY.md forward intelligence — PublicAPI.Unshipped.txt RS0016 pattern; FixedTimeProvider pattern (HIGH confidence)
