# S02: CountConstrainedKnapsackSlice — .NET implementation — Research

**Date:** 2026-03-25

## Summary

S02 is a faithful port of the Rust `CountConstrainedKnapsackSlice` to C#. The Rust implementation is the normative spec-by-example; the .NET port must produce identical outputs on the 5 conformance vectors. The algorithm is already proven in Rust (S01 summary) and the 3-phase structure maps cleanly to C# — there are no architectural unknowns.

The primary implementation concern is the **pipeline wiring**: `CupelPipeline.cs` currently hard-codes three `is CountQuotaSlice` checks to wire shortfalls and cap-classification into the report. `CountConstrainedKnapsackSlice` needs identical wiring, which requires extending each of those three pipeline checks. This is the only non-trivial change outside the new slicer class itself.

The second concern is the **Phase 2 sort**: just like Rust, `KnapsackSlice` in .NET reconstructs its DP solution by backtracking items in reverse-index order (not score order). Phase 2 output must be sorted by score descending before Phase 3 cap enforcement — or the cap-exclusion conformance vector will fail (D180).

## Recommendation

Port `CountConstrainedKnapsackSlice` from `CountQuotaSlice.cs` as the structural template, replacing the inner-slicer delegation with a stored `KnapsackSlice` field, adding the Phase 2 sort, and seeding `selectedCount` from Phase 1 counts before Phase 3. Then extend the 3 pipeline `is CountQuotaSlice` checks to also handle `is CountConstrainedKnapsackSlice`. Write 5 integration tests mirroring the TOML conformance vectors.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Phase 1 (count-satisfy) logic | `CountQuotaSlice.Slice()` Phase 1 block | Identical semantics — copy verbatim, adapting field names |
| Phase 3 (cap enforcement) logic | `CountQuotaSlice.Slice()` Phase 3 block | Same cap logic; seed from Phase 1 counts (D181) |
| Test structure | `CountQuotaIntegrationTests.cs` | Same helper pattern (`Item()`, `Run()` helpers, `DryRun()`) |
| `LastShortfalls` inspection surface | `CountQuotaSlice.LastShortfalls` | Same pattern (D087) — populated after `Slice()` returns |

## Existing Code and Patterns

- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — Primary reference; 262 lines; copy Phase 1 and Phase 3 blocks verbatim adapting field names. Replace `_innerSlicer.Slice(residual, ...)` with `_knapsack.Slice(residualSorted, ...)` + score-descending sort before Phase 3.
- `src/Wollax.Cupel/KnapsackSlice.cs` — Not in `Slicing/` subdirectory; lives at root of `src/Wollax.Cupel/`. Take as constructor parameter typed as `KnapsackSlice` (not `ISlicer`).
- `src/Wollax.Cupel/CupelPipeline.cs` lines 347–420 — Three `is CountQuotaSlice` checks; extend each to `is CountConstrainedKnapsackSlice` with parallel logic using the new slicer's `Entries` (internal) and `LastShortfalls` properties.
- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — Template for 5 integration tests; use same `Item()` + `DryRun()` helper pattern with `CupelPipeline.CreateBuilder()`.
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Add `CountConstrainedKnapsackSlice` class, constructor, `LastShortfalls`, `GetConstraints()`, and `Slice()` entries.
- `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml` — 5 TOML files; use as the source of truth for integration test data.

## Constraints

- `CountConstrainedKnapsackSlice` must implement `ISlicer` and `IQuotaPolicy` (same as `CountQuotaSlice`).
- Constructor takes `KnapsackSlice` directly (not `ISlicer`) — prevents accidental wrapping by callers.
- No guard rejecting `KnapsackSlice` as constructor parameter (unlike `CountQuotaSlice` which blocks it).
- `LastShortfalls` is `public` (same as `CountQuotaSlice`) for test inspection surface.
- `Entries` is `internal` (same as `CountQuotaSlice`) for pipeline cap-classification.
- `GetConstraints()` must return `QuotaConstraintMode.Count` entries (same as `CountQuotaSlice.GetConstraints()`).
- `ScarcityBehavior.Throw` throws `InvalidOperationException` (same message pattern as `CountQuotaSlice`).
- Phase 2 output MUST be sorted by score descending before Phase 3 cap enforcement (D180).
- Phase 3 `selectedCount` MUST be seeded from Phase 1 committed counts, not zero (D181).
- `PublicAPI.Unshipped.txt` must be updated — build will fail with RS0016 if entries are missing.

## Pipeline Wiring — Three Changes Required

`CupelPipeline.cs` has three `is CountQuotaSlice` checks that gate shortfall wiring and cap-classification. Each must be extended for `CountConstrainedKnapsackSlice`:

**Change 1 — shortfall wiring (line ~349):**
```csharp
// BEFORE:
if (_slicer is CountQuotaSlice countQuotaSlicer && ...)
    reportBuilder.SetCountRequirementShortfalls(countQuotaSlicer.LastShortfalls);

// AFTER: also handle CountConstrainedKnapsackSlice
```
`CountConstrainedKnapsackSlice` exposes the same `LastShortfalls` property.

**Change 2 — selectedKindCounts construction (line ~378):**
```csharp
// BEFORE: if (reportBuilder is not null && _slicer is CountQuotaSlice)
// AFTER: if (reportBuilder is not null && (_slicer is CountQuotaSlice || _slicer is CountConstrainedKnapsackSlice))
```

**Change 3 — cap-classification (line ~408):**
```csharp
// BEFORE: if (selectedKindCounts is not null && _slicer is CountQuotaSlice cqs)
// AFTER: needs to handle CountConstrainedKnapsackSlice ccks similarly
```
Use `_slicer is CountConstrainedKnapsackSlice ccks` with `ccks.Entries.FirstOrDefault(...)` to find cap.

## Common Pitfalls

- **Missing Phase 2 sort** — `KnapsackSlice` returns items in DP backtracking order (reverse-index), not score order. Without sorting Phase 2 output score-descending before Phase 3, a lower-scoring item survives the cap while a higher-scoring one is dropped. This caused the Rust `cap-exclusion` test to fail before D180 was added. Always sort before Phase 3.
- **Phase 3 starting from zero** — `selectedCount` must start from Phase 1 committed counts (D181). Starting from zero allows `Phase1_committed + cap` items total instead of `cap` total.
- **Missing `Entries` internal accessor** — Pipeline cap-classification accesses `cqs.Entries` internally. `CountConstrainedKnapsackSlice` needs the same `internal IReadOnlyList<CountQuotaEntry> Entries => _entries` accessor.
- **Forgetting `PublicAPI.Unshipped.txt`** — The RS0016 analyzer fails the build silently if new public/internal-accessible members are missing from this file. Add all public members of `CountConstrainedKnapsackSlice`.
- **Wrong namespace** — `CountQuotaSlice` lives in `Wollax.Cupel.Slicing`; `KnapsackSlice` lives in `Wollax.Cupel`. The new class should live in `Wollax.Cupel.Slicing` to match the count-quota family.
- **Residual budget sub-budget construction** — `CountQuotaSlice` creates the residual budget as `new ContextBudget(maxTokens: budget.MaxTokens, targetTokens: Math.Min(residualTarget, budget.MaxTokens))`. Use the same pattern.

## Open Risks

- The pipeline `is CountQuotaSlice` checks are duplicated (3 locations). If the pattern grows further, future slicers with count constraints will require the same triple-update. This is a known v1 limitation; no refactor needed now.
- `is CountConstrainedKnapsackSlice ccks` at the cap-classification site uses `FirstOrDefault` which is O(n) per excluded item. This matches the existing `CountQuotaSlice` pattern and is acceptable for v1.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| C# / .NET | none needed | n/a — standard library port |

## Sources

- Rust `count_constrained_knapsack.rs` — normative implementation reference (codebase read)
- `CountQuotaSlice.cs` — .NET structural template (codebase read)
- `KnapsackSlice.cs` — Phase 2 delegate (codebase read)
- `CupelPipeline.cs` lines 347–420 — pipeline wiring to extend (codebase read)
- `CountQuotaIntegrationTests.cs` — test pattern reference (codebase read)
- `PublicAPI.Unshipped.txt` — existing entries for CountQuotaSlice (codebase read)
- 5 TOML conformance vectors — data source for integration tests (codebase read)
- S01 Summary `Forward Intelligence` section — D180, D181, D182 pitfalls confirmed (preloaded context)
