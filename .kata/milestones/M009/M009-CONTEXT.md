# M009: CountConstrainedKnapsackSlice + MetadataKeyScorer — Context

**Gathered:** 2026-03-25
**Status:** Ready for planning

## Project Description

Cupel is a dual-language (.NET + Rust) context management library. Given candidate items and a token budget, it selects the optimal context window via a fixed pipeline: Classify → Score → Deduplicate → Slice → Place. This milestone adds two features: a constrained knapsack slicer that combines count guarantees with token-optimal selection, and a multiplicative metadata-keyed scorer.

## Why This Milestone

`CountQuotaSlice` (M006) deliberately rejected `KnapsackSlice` as its inner slicer (D052) — the count+knapsack combination was deferred pending demand. The guard message explicitly promises a `CountConstrainedKnapsackSlice` in a future release. With `CountQuotaSlice` now battle-tested, the pre-processing upgrade path (5A from the March 21 brainstorm) is the correct approach: commit count-required items first by score, then run standard knapsack on the residual. This avoids the state-explosion risk of full constrained-DP while delivering the primary caller need.

`MetadataKeyScorer` was accepted in the March 21 brainstorm (B7) and pairs naturally with the `cupel:priority` convention. It's small, well-understood, and rounds the milestone to a comfortable size.

## User-Visible Outcome

### When this milestone is complete, the user can:

- Construct `CountConstrainedKnapsackSlice(entries, bucket_size)` and use it anywhere `KnapsackSlice` is valid — gets both count guarantees (require/cap per kind) and near-optimal token packing in a single slicer.
- Construct `MetadataKeyScorer("cupel:priority", "high", 1.5)` and compose it in a `CompositeScorer` — items tagged `cupel:priority=high` get a 1.5× score boost; others are unaffected.
- Read `spec/src/slicers/count-constrained-knapsack.md` and `spec/src/scorers/metadata-key.md` for language-agnostic contracts.

### Entry point / environment

- Entry point: Library API — Rust `use cupel::{CountConstrainedKnapsackSlice, MetadataKeyScorer}` / .NET `new CountConstrainedKnapsackSlice(...)` / `new MetadataKeyScorer(...)`
- Environment: Tests + `cargo test --all-targets` + `dotnet test` — no running service needed
- Live dependencies involved: none

## Completion Class

- Contract complete means: `cargo test --all-targets` green across all crates; `dotnet test` green; spec chapters exist with no TBD fields; `CountConstrainedKnapsackSlice` and `MetadataKeyScorer` exported from public API in both languages.
- Integration complete means: none (library, not a service)
- Operational complete means: none

## Final Integrated Acceptance

To call this milestone complete, we must prove:

- `CountConstrainedKnapsackSlice` with `require_count=2` for kind "tool" selects exactly 2 tool items AND the remaining capacity is filled near-optimally by knapsack on non-tool items — demonstrated by a conformance integration test.
- `MetadataKeyScorer("cupel:priority", "high", 1.5)` produces a 1.5× boost for matching items and 1.0× for non-matching — demonstrated by unit tests; result flows through `Pipeline::run()` end-to-end.
- Both slicers/scorers appear in `PublicAPI.Approved.txt` (.NET) and `lib.rs` re-exports (Rust).

## Risks and Unknowns

- **Pre-processing sub-optimality at tight budgets** — When committed items consume a large fraction of the budget, the residual knapsack may have very little room and produce a worse result than unconstrained knapsack. This is inherent to the pre-processing approach (D052 accepted this trade-off). The spec must document this behavior clearly.
- **Cap enforcement placement** — In `CountConstrainedKnapsackSlice`, cap enforcement happens after the knapsack phase (Phase 3). Items from the knapsack output that would exceed a kind's `cap_count` must be dropped. This is a post-processing step, not a constraint the knapsack solver itself enforces — which means the knapsack may "waste" budget on items that get dropped. Acceptable for v1 but must be documented.
- **`ScarcityBehavior` reuse** — `CountConstrainedKnapsackSlice` re-uses `ScarcityBehavior` and `CountRequirementShortfall` from M006. Verify these are exported at the right visibility level.

## Existing Codebase / Prior Art

- `crates/cupel/src/slicer/knapsack.rs` — The existing `KnapsackSlice` implementation; `CountConstrainedKnapsackSlice` uses the same DP core via the `Slicer` trait (passes the residual candidate set and residual budget to a `KnapsackSlice` instance).
- `crates/cupel/src/slicer/count_quota.rs` — 783 lines; reference implementation for two-phase count-satisfy + Phase 2 delegation; `CountConstrainedKnapsackSlice` follows the same Phase 1 logic.
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — .NET reference; 262 lines.
- `crates/cupel/src/scorer/metadata_trust.rs` — 138 lines; absolute passthrough scorer; `MetadataKeyScorer` follows similar structure but implements multiplicative conditional logic.
- `src/Wollax.Cupel/Scoring/MetadataTrustScorer.cs` — .NET reference scorer; 60 lines.
- `spec/src/slicers/count-quota.md` — Existing count-quota spec; `CountConstrainedKnapsackSlice` spec will reference and contrast this.
- `spec/src/scorers/metadata-trust.md` — `cupel:trust` convention; `MetadataKeyScorer` spec adds `cupel:priority` convention alongside.
- `.planning/design/count-quota-design.md` — D052 documents the KnapsackSlice guard and the 5A/5D upgrade path. Pre-processing (5A) is what we're building.
- `.kata/DECISIONS.md` — Read before planning; D040–D057 and D084–D087 are locked for `CountQuotaSlice`; relevant ones carry over.

> See `.kata/DECISIONS.md` for all architectural and pattern decisions — it is an append-only register; read it during planning, append to it during execution.

## Relevant Requirements

- R062 — CountConstrainedKnapsackSlice; this milestone is its primary owner
- R063 — MetadataKeyScorer; this milestone is its primary owner
- R061 — CountQuotaSlice (validated M006); provides diagnostics types and `ScarcityBehavior` re-used by R062

## Scope

### In Scope

- `CountConstrainedKnapsackSlice` in Rust (`crates/cupel/src/slicer/`) with unit + integration tests and 5 conformance vectors
- `CountConstrainedKnapsackSlice` in .NET (`src/Wollax.Cupel/Slicing/`) with unit + integration tests; `PublicAPI.Approved.txt` updated
- `MetadataKeyScorer` in Rust (`crates/cupel/src/scorer/`) with unit tests and 5 conformance vectors
- `MetadataKeyScorer` in .NET (`src/Wollax.Cupel/Scoring/`) with unit tests; `PublicAPI.Approved.txt` updated
- Spec chapter: `spec/src/slicers/count-constrained-knapsack.md` — algorithm (pre-processing path), construction validation, scarcity, cap enforcement, OOM guard, examples
- Spec chapter: `spec/src/scorers/metadata-key.md` — algorithm, `cupel:priority` convention, composability, edge cases
- `crates/cupel/src/lib.rs` and `.NET` public surface updated with new exports
- `CHANGELOG.md` unreleased section updated for both new types

### Out of Scope / Non-Goals

- Full constrained-DP approach (state-dimension extension) — D052 deferred this; pre-processing is the v1 path
- Removing the `KnapsackSlice` guard from `CountQuotaSlice` — that guard stays; `CountConstrainedKnapsackSlice` is a separate slicer
- `cupel-testing` or `cupel-otel` crate changes
- Publishing (crates.io / NuGet) — manual publish step by user

## Technical Constraints

- Re-use `ScarcityBehavior`, `CountRequirementShortfall`, and `CountCapExceeded` from M006 — do not define new parallel types
- `CountConstrainedKnapsackSlice` must **not** implement `is_knapsack() → true` (it's not a raw knapsack slicer; callers using `CountQuotaSlice` should not be able to wrap it by mistake — wait, actually reconsider: does `CountConstrainedKnapsackSlice` need a guard too? The `is_knapsack()` trait method is checked by `CountQuotaSlice` and `find_min_budget_for`. `CountConstrainedKnapsackSlice` is NOT a raw knapsack — it should return `false` for `is_knapsack()`. The `find_min_budget_for` monotonicity guard should not fire on it.)
- `MetadataKeyScorer` boost must be validated `> 0.0` at construction time; non-finite boost is a construction error
- Both languages must produce identical conformance vector outputs

## Integration Points

- `KnapsackSlice` — `CountConstrainedKnapsackSlice` wraps a `KnapsackSlice` instance internally (Phase 2 delegate); does not inherit from it
- `CountQuotaEntry` / `CountQuotaSlice` diagnostics — `CountConstrainedKnapsackSlice` re-uses `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall`, `CountCapExceeded`
- `CompositeScorer` — `MetadataKeyScorer` composes cleanly; test at least one composition scenario
- Conformance test infrastructure — new vectors follow the existing pattern in `crates/cupel/tests/conformance.rs` and `.NET` conformance tests

## Open Questions

- **`CountQuotaEntry` re-use** — `CountConstrainedKnapsackSlice` uses the same per-kind require/cap structure as `CountQuotaSlice`. Should it accept `Vec<CountQuotaEntry>` directly (re-using the existing type) or define its own entry type? Re-using is strongly preferred — same struct, same validation semantics. Confirm no naming confusion arises.
- **`is_count_quota()` trait method** — `Slicer` trait has `is_count_quota() → false` default, overridden by `CountQuotaSlice`. Should `CountConstrainedKnapsackSlice` also return `true` for `is_count_quota()`? This affects `find_min_budget_for` monotonicity guard. Answer: yes — it enforces count constraints and the monotonicity concern applies.
