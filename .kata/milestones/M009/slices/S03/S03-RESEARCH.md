# S03: Spec chapters — count-constrained-knapsack + metadata-key — Research

**Researched:** 2026-03-25
**Domain:** Spec authoring (mdBook markdown, pseudocode, conformance vector outlines)
**Confidence:** HIGH — both implementations exist in full; spec is purely a documentation task against working code

## Summary

S03 is a pure spec-authoring slice. Both `CountConstrainedKnapsackSlice` (Rust + .NET) and `MetadataKeyScorer` (unimplemented, authored for S04 as contract) need spec chapters. The implementations are complete and tested for the slicer; the scorer chapter is authored from the design decisions and will guide S04's implementation.

The primary inputs are:
- The 5 TOML conformance vectors in `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml` — the ground truth for the slicer spec
- `crates/cupel/src/slicer/count_constrained_knapsack.rs` and `src/Wollax.Cupel/Slicing/CountConstrainedKnapsackSlice.cs` — working implementations to validate spec accuracy
- `spec/src/slicers/count-quota.md` — the primary structural template to follow (same 3-phase algorithm family)
- `spec/src/scorers/metadata-trust.md` — the scorer chapter template to follow for `MetadataKeyScorer`

The two files to produce are:
1. `spec/src/slicers/count-constrained-knapsack.md`
2. `spec/src/scorers/metadata-key.md`

Both must be linked from `spec/src/SUMMARY.md`. Neither must have any TBD fields. The CHANGELOG.md .NET entry for `CountConstrainedKnapsackSlice` should also be added in this slice (the Rust entry was added in S01).

## Recommendation

Author `count-constrained-knapsack.md` by adapting `count-quota.md` — the structure is nearly identical (same 3-phase algorithm, same entry types, same scarcity model). The key differences to document are:
1. **Phase 2 delegates to a stored `KnapsackSlice`** (not a configurable inner slicer)
2. **Phase 2 output is re-sorted by score descending before Phase 3** (D180) — critical for correct cap enforcement
3. **No KnapsackSlice guard** (unlike `CountQuotaSlice`, which guards against it)
4. **`is_count_quota() → true`** triggering `find_min_budget_for` monotonicity guard
5. **Pre-processing sub-optimality trade-off** must be documented (D174)
6. **Cap waste** (Phase 3 drops knapsack selections) must be documented

Author `metadata-key.md` by adapting `metadata-trust.md` — the structure maps directly (same metadata lookup pattern, same parse/finite guard logic). Key differences:
1. **Multiplicative semantics**: `score_out = score_in × boost` vs passthrough absolute value
2. **`cupel:priority` convention** (analogous to `cupel:trust` convention section)
3. **Boost validation**: `> 0.0` at construction (not a range check like `[0.0, 1.0]`)
4. **No clamping**: output is not clamped — the scorer returns the product directly
5. **Neutral multiplier** for non-matching items: `score_in × 1.0 = score_in`

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Spec chapter structure | `spec/src/slicers/count-quota.md` | Same 3-phase algorithm family; copy sections verbatim and adapt CCKS differences |
| Scorer chapter structure | `spec/src/scorers/metadata-trust.md` | Same metadata lookup + parse + fallback pattern; copy skeleton |
| Conformance vector facts | TOML files in `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml` | These are the verified ground truth; derive all vector outlines directly from the comments |
| SUMMARY.md link format | `spec/src/SUMMARY.md` existing entries | Follow `  - [Name](path/to/file.md)` indented format |

## Existing Code and Patterns

- `spec/src/slicers/count-quota.md` — Primary structural model for `count-constrained-knapsack.md`; same sections in same order: Overview, Configuration, Algorithm, Scarcity Reporting, Monotonicity, Edge Cases, Complexity, Conformance Notes
- `spec/src/scorers/metadata-trust.md` — Primary structural model for `metadata-key.md`; same sections: Overview, Metadata Namespace Reservation, Conventions, Algorithm, Score Interpretation, Edge Cases, Conformance Vector Outlines, Complexity, Conformance Notes
- `crates/cupel/src/slicer/count_constrained_knapsack.rs` — Working Rust implementation; module doc strings describe the 3-phase algorithm verbatim; `is_count_quota()=true`, `count_cap_map()` from `self.entries`, `QuotaPolicy::quota_constraints()` returning `QuotaConstraintMode::Count`
- `src/Wollax.Cupel/Slicing/CountConstrainedKnapsackSlice.cs` — Working .NET implementation (~230 lines); XML docs describe Phase 1/2/3; ScarcityBehavior.Throw message: `"CountConstrainedKnapsackSlice: candidate pool for kind '{entry.Kind}' has {satisfied} items but RequireCount is {entry.RequireCount}."` — document this exact message in the spec
- `crates/cupel/src/scorer/metadata_trust.rs` — Reference scorer implementation; `MetadataKeyScorer` will follow the same structural pattern but with multiplicative semantics
- `spec/src/analytics/budget-simulation.md` — Contains the `FindMinBudgetFor QuotaSlice + CountQuotaSlice Guard` section; `CountConstrainedKnapsackSlice` spec must cross-reference this guard (since `is_count_quota()=true` triggers it)

## Constraints

- **Zero TBD fields** — both files must be complete with no open questions; S04 depends on `metadata-key.md` as its implementation contract
- **SUMMARY.md must be updated** — both new spec files need entries; `count-constrained-knapsack.md` under Slicers, `metadata-key.md` under Scorers; currently neither appears in SUMMARY.md
- **`spec/src/slicers/` and `spec/src/scorers/`** exist and only need new `.md` files dropped in — no directory creation needed
- **`cupel:priority` is a NEW convention** — `metadata-key.md` introduces it alongside the existing `cupel:trust` and `cupel:source-type` conventions; the `metadata-trust.md` already reserves the `cupel:` namespace; `metadata-key.md` just adds `cupel:priority` as a usage example
- **CHANGELOG.md .NET entry** — S01 added the Rust CHANGELOG entry; S02's summary noted this was deferred to S03; add the `.NET CountConstrainedKnapsackSlice` note and also `MetadataKeyScorer` is S04's concern, so just handle the CCKS .NET entry here

## Key Facts for the Count-Constrained-Knapsack Spec Chapter

**Algorithm pseudocode differences from CountQuotaSlice:**
- Phase 2 uses: `innerSelected <- KnapsackSlice(residual, residualBudget)` (not `innerSlicer.Slice(...)`)
- After Phase 2, before Phase 3: `innerSelected <- SORT(innerSelected, by score descending)` — this is the D180 re-sort
- Phase 3 `selectedCount` is seeded from Phase 1 committed counts (D181) — same as CountQuotaSlice

**No KnapsackSlice guard:** Unlike CountQuotaSlice, there is no construction-time check on inner slicer type — the KnapsackSlice is hardwired by the type itself.

**`is_count_quota() → true`:** This means `FindMinBudgetFor` MUST guard against `CountConstrainedKnapsackSlice` (same guard as `CountQuotaSlice`). The spec must document this in a Monotonicity section cross-referencing `budget-simulation.md`.

**Pre-processing sub-optimality (D174):** Must be explicitly documented — Phase 1 commits required items before knapsack sees them, consuming budget. If required items are token-heavy, the residual is small and knapsack operates on a poor pool.

**Cap waste (D174 corollary):** Must be explicitly documented — Phase 3 may drop items that the KnapsackSlice selected (the knapsack optimized for them, then they're discarded by cap). Budget was "spent" on items that don't appear in the result.

**Construction parameters:**
- Rust: `CountConstrainedKnapsackSlice::new(entries: Vec<CountQuotaEntry>, knapsack: KnapsackSlice, scarcity: ScarcityBehavior)`
- .NET: `CountConstrainedKnapsackSlice(IReadOnlyList<CountQuotaEntry> entries, KnapsackSlice knapsack, ScarcityBehavior scarcity = ScarcityBehavior.Degrade)`
- Validation: same as CountQuotaSlice — `requireCount ≤ capCount` per entry; no cross-entry validation

**Scarcity Throw message format** (verbatim from .NET, mirrored in Rust):
`"CountConstrainedKnapsackSlice: candidate pool for kind '<kind>' has <satisfied> items but RequireCount is <requireCount>."`

## Key Facts for the MetadataKeyScorer Spec Chapter

**Algorithm (multiplicative):**
```
METADATA-KEY-SCORE(item, allItems, config):
    if item.metadata does not contain config.key:
        return item.score × config.defaultMultiplier  // neutral, default 1.0

    raw <- item.metadata[config.key]
    if raw != config.value:
        return item.score × config.defaultMultiplier  // no match → neutral

    return item.score × config.boost
```

Wait — important clarification: `MetadataKeyScorer` is a `Scorer` implementing `score(item, all_items) -> f64`. It does NOT receive `item.score` as input — scorers produce the score from scratch. The "multiplicative" semantics mean:
- If item matches: return `boost`
- If item doesn't match: return `1.0` (neutral multiplier)

When composed with `CompositeScorer`, the composite multiplies scores. So: `MetadataKeyScorer(key, value, boost=1.5)` returns `1.5` for matching items and `1.0` for non-matching. The `CompositeScorer` then multiplies this with other scorer outputs.

**Correct algorithm:**
```
METADATA-KEY-SCORE(item, allItems, config):
    if item.metadata does not contain config.key:
        return config.defaultMultiplier  // default 1.0

    raw <- item.metadata[config.key]
    if raw != config.value:
        return config.defaultMultiplier

    return config.boost
```

**Construction parameters:**
- `MetadataKeyScorer(key: string, value: string, boost: f64)`
- `boost` MUST be `> 0.0`; zero, negative, and non-finite → construction error
- `defaultMultiplier` defaults to `1.0` (neutral)

**`cupel:priority` convention:**
- Key: `"cupel:priority"`, RECOMMENDED values: `"high"`, `"normal"` (or `"low"`)
- Typical usage: `MetadataKeyScorer("cupel:priority", "high", 1.5)` boosts high-priority items 1.5×
- Convention is open-string: implementations MUST NOT validate or reject unknown values

**Error type:**
- Rust: `CupelError::ScorerConfig(String)` — same as MetadataTrustScorer's out-of-range error
- .NET: `ArgumentException` (D178) — boost is a behavioral invalidity, not numeric range violation like `ArgumentOutOfRangeException`

**5 conformance vectors (narrative, for S04 implementation):**
1. Match → boost applied: item with `metadata["cupel:priority"] = "high"`, scorer returns `1.5`
2. No-match → neutral: item with `metadata["cupel:priority"] = "normal"`, scorer returns `1.0`
3. Missing key → neutral: item with no `cupel:priority` key, scorer returns `1.0`
4. Zero boost → construction error
5. Negative boost → construction error

## Common Pitfalls

- **Pseudocode Phase 3 init from Phase 1** — When writing the algorithm, `selectedCount` must be explicitly initialized from Phase 1 committed counts, not from zero. This is the D181 insight. CountQuotaSlice has the same pattern; copy it exactly.
- **Missing Phase 2 re-sort** — The re-sort of KnapsackSlice output (D180) is a CCKS-specific requirement not present in CountQuotaSlice. If the pseudocode doesn't include it, cap enforcement is underspecified and implementors will build incorrect Phase 3 behavior.
- **MetadataKeyScorer is NOT an absolute scorer** — Unlike MetadataTrustScorer, it returns a multiplier (1.0 or boost), not a clamped trust value. It should NOT have a `defaultScore` parameter in the spec — instead `defaultMultiplier` (default 1.0). Don't copy the defaultScore semantics from MetadataTrustScorer.
- **SUMMARY.md forget** — Easy to write the .md files but forget to add SUMMARY.md entries. The spec's mdBook won't link them without SUMMARY.md updates.
- **No TBD fields** — S04 needs `metadata-key.md` as its implementation contract. Any TBD would block S04. Write the chapter completely.

## Open Risks

- **`MetadataKeyScorer` exact .NET type for comparison** — The value comparison is string-to-string in Rust (`metadata` is `HashMap<String, String>`). In .NET, `metadata` is `IReadOnlyDictionary<string, object?>`. The spec should probably require string comparison for the value field and handle both `string` and stringified cases — but this is a S04 implementation detail. The spec can state: "the `value` parameter is compared as a string; implementations that store metadata with non-string types SHOULD convert to string for comparison."
- **`defaultMultiplier` parameter** — Decide whether to expose this as a construction parameter or hardcode to 1.0. D178 doesn't mention it. For simplicity, default to 1.0 and make it a fixed constant in the spec (not a constructor parameter). Callers needing a different neutral value can use `CompositeScorer` composition.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| mdBook / Rust spec | none | none found — pure text authoring task |

## Sources

- Conformance vectors `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml` — ground truth for algorithm behavior; all 5 scenarios with inline comments describing phases
- `spec/src/slicers/count-quota.md` — structural template for CCKS spec chapter; same section organization
- `spec/src/scorers/metadata-trust.md` — structural template for MetadataKeyScorer spec chapter
- `crates/cupel/src/slicer/count_constrained_knapsack.rs` — Rust implementation; module doc, trait impls, error messages
- `src/Wollax.Cupel/Slicing/CountConstrainedKnapsackSlice.cs` — .NET implementation; XML docs, exact ScarcityBehavior.Throw message, Phase 2 scoreByContent pattern
- DECISIONS.md D174–D184 — locked decisions governing algorithm design, type reuse, and construction validation
