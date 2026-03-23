# M003/S06 — Research

**Date:** 2026-03-23

## Summary

S06 does not own any currently **Active** requirements in `.kata/REQUIREMENTS.md`; instead it closes the last milestone-level acceptance gaps for M003: the remaining .NET budget-simulation API, the GreedySlice deterministic-tiebreak rule, and spec/index/changelog alignment for the features already shipped in S01–S05. The main implementation risk is not algorithmic complexity — the budget-simulation behavior is already specified — but internal wiring: `CupelPipeline.DryRun()` is currently hard-wired to the pipeline's stored `_budget`, while `GetMarginalItems` and `FindMinBudgetFor` need repeated dry runs at alternate budgets.

The safest execution path is to keep budget simulation inside `Wollax.Cupel` and add an internal budget-override path on `CupelPipeline` rather than trying to reconstruct a second pipeline from extension methods. Current code already has the right determinism primitives: the Sort stage uses `(score desc, originalIndex asc)` and both GreedySlice implementations use `(density desc, index asc)`. The roadmap's “score ties → id ascending” language is currently not implementable literally because `ContextItem` has no `Id` field in either language, so planning must first resolve whether S06 should (a) formalize the existing stable-index rule as the snapshot-safe contract or (b) introduce a new identifier surface.

Spec alignment work is partly straightforward and partly blocked by missing documentation structure. `scorers.md` already lists DecayScorer and MetadataTrustScorer, but `spec/src/SUMMARY.md` and `spec/src/slicers.md` still omit CountQuotaSlice, and there is currently no `spec/src/slicers/count-quota.md` page to link to. `spec/src/changelog.md` is still effectively a 1.0.0-only file. S06 therefore needs both traceability edits and one new slicer page if the mdBook navigation is going to be made internally consistent.

## Recommendation

Implement S06 around three concrete seams:

1. **Budget override seam in `CupelPipeline`** — add an internal helper so budget-simulation methods can run deterministic `DryRun` calls with alternate `ContextBudget` values without duplicating the pipeline algorithm.
2. **Determinism-first tiebreak clarification** — resolve the roadmap/spec mismatch before any code edits. The codebase currently has stable index-based tie handling; do not invent a fake “id” rule in implementation text unless the data model actually grows an id field.
3. **Spec traceability completion** — update `SUMMARY.md`, `slicers.md`, and `changelog.md`, and add a CountQuotaSlice slicer page so navigation matches shipped functionality.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Re-running the pipeline at alternate budgets | Extend `CupelPipeline`'s internal `DryRun`/`ExecuteCore` path | Keeps one source of truth for classify/score/deduplicate/slice/place behavior and preserves DryRun determinism guarantees. |
| Stable tie-breaking for simulation-safe output | Reuse the existing composite-key sort patterns in Sort and GreedySlice | The repo already enforces deterministic ordering via index tiebreaks; replacing that with ad hoc comparison logic is unnecessary risk. |
| Spec navigation for new slicers | Follow the existing mdBook page + index + SUMMARY pattern used by Greedy/Knapsack/Quota | Keeps the spec discoverable and prevents broken navigation or undocumented shipped features. |

## Existing Code and Patterns

- `src/Wollax.Cupel/CupelPipeline.cs` — `DryRun(IReadOnlyList<ContextItem>)` always uses the pipeline's private `_budget`; this is the key seam S06 must extend.
- `tests/Wollax.Cupel.Tests/Pipeline/DryRunTests.cs` — already proves DryRun report-population and idempotence; these are the right regression surfaces for any budget-override change.
- `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` — established extension-method home for analytics in .NET; S06 should mirror this pattern for pipeline-focused helpers, but not at the cost of losing access to private pipeline state.
- `src/Wollax.Cupel/GreedySlice.cs` — current .NET GreedySlice sorts by `(density desc, index asc)`.
- `crates/cupel/src/slicer/greedy.rs` — current Rust GreedySlice matches the same `(density desc, index asc)` rule.
- `spec/src/pipeline/sort.md` — already defines stable `(score desc, originalIndex asc)` ordering as the deterministic rule feeding slicers.
- `spec/src/slicers/greedy.md` — currently documents index-based tiebreaking, not `id`-based tiebreaking.
- `spec/src/analytics/budget-simulation.md` — authoritative budget-simulation contract, guards, and determinism invariant.
- `spec/src/SUMMARY.md` — mdBook navigation; currently missing CountQuotaSlice.
- `spec/src/slicers.md` — slicer summary table; currently missing CountQuotaSlice.
- `spec/src/changelog.md` — still only documents `1.0.0`; no M003 feature coverage yet.

## Constraints

- `GetMarginalItems` and `FindMinBudgetFor` rely on repeated `DryRun` calls; non-deterministic output would make the spec-defined comparisons invalid.
- `CupelPipeline` exposes internal getters for scorer/slicer/placer/async-slicer/dedup/overflow strategy, but **not** for `_budget`; external extension methods cannot clone the pipeline with a new budget unless the core class adds a seam.
- `ContextItem` has no `Id` field in either .NET or Rust today; any literal “id ascending” rule is currently underspecified.
- `GetMarginalItems` must guard against `QuotaSlice`; `FindMinBudgetFor` must guard against both `QuotaSlice` and `CountQuotaSlice` because inclusion becomes non-monotonic as allocations shift with budget.
- Public .NET extension methods require `src/Wollax.Cupel/PublicAPI.Unshipped.txt` updates to keep RS0016 clean.
- Rust budget simulation is explicitly deferred in the spec for v1.3; S06 should document the parity decision, not widen scope by implementing it.
- `spec/src/SUMMARY.md` cannot honestly reference CountQuotaSlice without an actual `spec/src/slicers/count-quota.md` page.

## Common Pitfalls

- **Treating “id ascending” as already-defined** — it is not. The code and spec currently use stable index ordering; planning must resolve the wording mismatch before implementation starts.
- **Building budget simulation purely as external extension methods** — they cannot access `CupelPipeline._budget`, so they'll either duplicate core logic or hit a wall. Add an internal core seam instead.
- **Diffing marginal items by value equality** — the spec requires object reference equality when comparing full-budget and reduced-budget inclusions.
- **Updating only the nav/index files** — CountQuotaSlice is missing not just from `SUMMARY.md` and `slicers.md`, but from the actual slicer-page set.
- **Forgetting the roadmap/spec signature mismatch for `FindMinBudgetFor`** — the S06 boundary map mentions a `ContextBudget` parameter, while `spec/src/analytics/budget-simulation.md` currently does not.

## Open Risks

- The biggest planning blocker is the tiebreak rule ambiguity: formalize existing stable-index semantics or change the data model/API to support a true id-based rule.
- `FindMinBudgetFor` has a contract mismatch between roadmap and spec; execution should reconcile this before writing code or tests.
- Adding an internal budget-override seam to `CupelPipeline` risks accidental divergence between normal `DryRun` and simulation `DryRun` unless both routes share the same execution core.
- Spec traceability may expand into one additional doc artifact (`spec/src/slicers/count-quota.md`) even though the roadmap only names nav/index files.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| .NET / C# | `github/awesome-copilot@dotnet-best-practices` | Available (not installed) |
| .NET / C# | `mhagrelius/dotfiles@dotnet-10-csharp-14` | Available (not installed) |
| Rust | `apollographql/skills@rust-best-practices` | Available (not installed) |
| Rust | `jeffallan/claude-skills@rust-engineer` | Available (not installed) |
| mdBook / spec docs | None found in current skill scan | None found |

## Sources

- Budget-simulation contracts, monotonicity guards, and DryRun determinism invariant (source: `spec/src/analytics/budget-simulation.md`)
- Current .NET DryRun implementation and internal pipeline seams (source: `src/Wollax.Cupel/CupelPipeline.cs`)
- Existing .NET GreedySlice density/index ordering (source: `src/Wollax.Cupel/GreedySlice.cs`)
- Existing Rust GreedySlice density/index ordering (source: `crates/cupel/src/slicer/greedy.rs`)
- Current GreedySlice spec text still using index-based tiebreaking (source: `spec/src/slicers/greedy.md`)
- Sort-stage determinism rule already defined as `(score desc, originalIndex asc)` (source: `spec/src/pipeline/sort.md`)
- Missing CountQuotaSlice entries in spec navigation and slicer index (sources: `spec/src/SUMMARY.md`, `spec/src/slicers.md`, `spec/src/slicers/`)
- Changelog still missing any M003 feature entries (source: `spec/src/changelog.md`)
- Existing DryRun idempotence tests to reuse during execution (source: `tests/Wollax.Cupel.Tests/Pipeline/DryRunTests.cs`)
- Skill discovery suggestions (source: `npx skills find "dotnet"`, `npx skills find "rust"`)
