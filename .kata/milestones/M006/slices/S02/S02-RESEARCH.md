# S02: .NET CountQuotaSlice — audit, complete, and test — Research

**Researched:** 2026-03-24
**Domain:** .NET slicer pipeline wiring
**Confidence:** HIGH

## Summary

The .NET `CountQuotaSlice` implementation in `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` (256 lines) is **functionally complete** — both phases of the COUNT-DISTRIBUTE-BUDGET algorithm are correctly implemented, `LastShortfalls` is populated per-call, `IQuotaPolicy.GetConstraints()` is implemented, the KnapsackSlice guard throws at construction, and 13 unit tests covering the main scenarios pass. The `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall`, and `ExclusionReason.CountCapExceeded` types all exist and are in `PublicAPI.Unshipped.txt`.

**Two structural wiring gaps** exist that prevent the M006 milestone criteria from being met:

1. **`SelectionReport.CountRequirementShortfalls` is never populated via the pipeline.** `CupelPipeline.Execute()` calls `_slicer.Slice()` but never reads `CountQuotaSlice.LastShortfalls` afterward. `ReportBuilder.Build()` always produces `CountRequirementShortfalls = []`. There is no path for shortfall data to reach the report.

2. **`ExclusionReason.CountCapExceeded` never appears on `ExclusionReason` in `SelectionReport.Excluded`.** All items not returned by the slicer are stamped `ExclusionReason.BudgetExceeded` in the pipeline's re-association loop. Cap-excluded items are dropped silently inside `CountQuotaSlice.Slice()` with only a structured `RecordItemEvent` message (not an `ExcludedItem`). There is no `ITraceCollector.RecordExcluded` method to call from the slicer.

The fix is surgical: after calling `_slicer.Slice()`, the pipeline casts to `CountQuotaSlice`, reads `LastShortfalls`, passes them to `ReportBuilder`, and uses the slicer's cap configuration to distinguish BudgetExceeded from CountCapExceeded when re-associating excluded items. Then integration tests prove the wiring end-to-end.

## Recommendation

**Implement pipeline wiring first, then add integration tests mirroring the 5 Rust conformance vectors.** The existing unit tests are correct; the implementation itself is sound. The work is entirely in `CupelPipeline.cs`, `ReportBuilder.cs`, and a new integration test file.

Recommended task decomposition:
- **T01: Audit + shortfall wiring** — Confirm the audit findings; wire `LastShortfalls` → `SelectionReport.CountRequirementShortfalls`; wire cap-excluded items → `ExclusionReason.CountCapExceeded` in the pipeline re-association loop
- **T02: Conformance tests** — 5 integration tests mirroring the Rust conformance vectors, using real `Pipeline.DryRun()`, asserting `Excluded` contains `CountCapExceeded` and `CountRequirementShortfalls` is populated
- **T03: quota_utilization + final verification** — Verify `quota_utilization` tests pass with `CountQuotaSlice`; run `dotnet build` (0 warnings) + full `dotnet test` (777+)

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Detecting CountQuotaSlice at pipeline level | `_slicer is QuotaSlice quotaSlicer` pattern (line 361 in CupelPipeline.cs) | Same pattern already used for pinned+quota conflict detection — cast and read |
| Per-kind count in sliced output | `selectedCount.TryGetValue(kind, out var count)` loop inside CountQuotaSlice.Slice() | Cannot reuse directly; the pipeline needs its own count reconstruction from slicer output to classify exclusion reasons |
| Structured shortfall type | `CountRequirementShortfall(Kind, RequiredCount, SatisfiedCount)` sealed record | Exists; only needs to flow from LastShortfalls → ReportBuilder |
| BuildReport with shortfalls | `ReportBuilder.Build(events)` → add overload or setter | Add `SetCountRequirementShortfalls(IReadOnlyList<CountRequirementShortfall>)` method on `ReportBuilder` (already has `SetTotalCandidates` / `SetTotalTokensConsidered` pattern) |
| Cap-excluded vs budget-excluded classification | Post-hoc: after slicer returns, reconstruct per-kind selected counts from slicedItems; any budget-fitting item whose kind is capped and count >= cap → CountCapExceeded | Avoids API changes to ISlicer or ITraceCollector |

## Existing Code and Patterns

- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — **Complete 2-phase implementation.** Phase 1: commit top-N by score, track `committedSet` (ReferenceEqualityComparer), accumulate `preAllocatedTokens`. Phase 2: build residual, call `_innerSlicer.Slice()`. Phase 3: cap-filter inner output, maintain `selectedCount` per kind. `LastShortfalls` populated each call. `GetConstraints()` returns count-mode `QuotaConstraint` entries.
- `src/Wollax.Cupel/Slicing/CountQuotaEntry.cs` — Validates `require > cap` and `cap <= 0` at construction. Has `Kind`, `RequireCount`, `CapCount` properties.
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — `CountRequirementShortfalls` is non-required, defaults to `[]`. `Equals()` includes it in equality check. `GetHashCode()` uses `.Count`.
- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — Internal builder; `AddExcluded(item, score, reason)` adds items; `Build(events)` produces report. Pattern: `SetTotalCandidates()` / `SetTotalTokensConsidered()` setter methods. **Need: `SetCountRequirementShortfalls()` setter.**
- `src/Wollax.Cupel/CupelPipeline.cs:305` — `var slicedItems = _slicer.Slice(sorted, adjustedBudget, trace);` followed by re-association loop at ~line 327. Items NOT in slicedItems receive `ExclusionReason.BudgetExceeded`. **This is where CountCapExceeded classification must be inserted.**
- `src/Wollax.Cupel/CupelPipeline.cs:361` — `if (pinned.Count > 0 && _slicer is QuotaSlice quotaSlicer)` — reference pattern for casting to specific slicer after Slice() call.
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs:35` — `CountCapExceeded = 8` is defined (integer anchor). `CountRequireCandidatesExhausted = 9` also defined. Neither is emitted anywhere yet.
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — All `CountQuotaSlice`, `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall`, `CountCapExceeded` entries already declared. **No new public API additions required for the wiring fix.**
- `tests/Wollax.Cupel.Tests/Slicing/CountQuotaSliceTests.cs` — 13 unit tests; all pass. Tests call `slicer.Slice()` directly (not via pipeline). No pipeline-level integration tests exist yet for CountQuotaSlice.
- `tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs` — `CountQuotaSlice_ImplementsIQuotaPolicy_GetConstraints_ReturnsCorrectEntries` and `QuotaUtilization_WithCountQuotaSlice` tests exist and pass. **No new quota_utilization work needed.**
- `crates/cupel/conformance/required/slicing/count-quota-*.toml` — 5 conformance vectors are the test shape to mirror in .NET integration tests.

## Constraints

- **`ISlicer.Slice` has no pinned parameter** — Design scenario 3 (pinned-count decrement) cannot be tested through the current `ISlicer` contract. The pipeline excludes pinned items before calling the slicer. If 2 items are pinned and `require = 2`, the slicer sees 0 candidates for that kind → shortfall. This is a known architectural limitation (D086/D087-adjacent). **Skip scenario 3 integration test; document as out of scope for v1.**
- **`ITraceCollector.RecordExcluded` does not exist in .NET** — `ITraceCollector` only exposes `RecordStageEvent` and `RecordItemEvent`. Cap exclusion reason must be conveyed through pipeline post-processing, not through the slicer calling into the collector.
- **`D087` is locked** — `LastShortfalls` is the approved inspection surface. Do not add `RecordShortfall` to `ITraceCollector` for v1.
- **`D057` is locked** — `SelectionReport.CountRequirementShortfalls` is non-required (default `[]`) and must remain so. Positional deconstruction is explicitly unsupported.
- **`ReportBuilder` is `internal sealed`** — Safe to add a setter method; no public API impact.
- **`dotnet build` must stay at 0 warnings** — All new code must have full XML doc coverage.
- **`PublicAPI.Unshipped.txt` already declares all needed public surface** — If any new public member is added, it must also appear there.

## Common Pitfalls

- **Classifying CountCapExceeded items using `traceCollector`** — `ITraceCollector` has no `RecordExcluded` method. The only path is pipeline-level post-processing: after `_slicer.Slice()`, rebuild per-kind selected counts from `slicedItems`, then for each sorted item not in `slicedItems`: check if it fits in budget AND its kind is capped AND the count from `slicedItems` for that kind is >= cap → `CountCapExceeded`; otherwise `BudgetExceeded`.
- **Reading `LastShortfalls` before `Slice()` is called** — `LastShortfalls` is populated during `Slice()`. Always read it after the `_slicer.Slice()` call, not before.
- **Assuming `ReferenceEqualityComparer` works for sliced-set reconstruction in the pipeline** — The pipeline already uses `new HashSet<ContextItem>(ReferenceEqualityComparer.Instance)` via `slicedSet`; verify same-reference items come back from `CountQuotaSlice.Slice()`. The slicer clones items in Rust but in .NET it returns the same `ContextItem` references from `committed` and from the inner slicer output.
- **Ordering of cap-excluded items in `SelectionReport.Excluded`** — `ReportBuilder.AddExcluded()` records insertion index for score-desc sort. Cap-excluded items must be added with their score (retrieved from the `sorted` array) at the right position in the re-association loop.
- **`selectedCount` reconstruction for cap classification** — The pipeline must count per-kind items in `slicedItems` (including Phase 1 committed items) before classifying excluded items. Phase 1 items come first in the result; Phase 2 items follow. The per-kind count in `slicedItems` is the exact cap state at the point when cap exclusions happened.
- **Running DryRun vs Execute** — `CupelPipeline.DryRun()` internally calls `Execute()` with a `DiagnosticTraceCollector`. The wiring must be in `Execute()` (the `reportBuilder is not null` branch), not in a `dry_run`-specific path.

## Open Risks

- **Cap item classification edge case**: If an item's budget would be exceeded AND it's also cap-excluded, the pipeline must classify it correctly. The safest rule: build per-kind count from `slicedItems` first; if `item.Tokens > adjustedBudget.TargetTokens` AND it's not cap-related → `BudgetExceeded`; if it fits in budget AND cap count exceeded → `CountCapExceeded`. Items that both exceed budget and cap should use `BudgetExceeded` (budget constraint is the primary reason; cap is moot when budget is the binding constraint).
- **S01 Rust shortfall wiring not done**: The Rust `SelectionReport.count_requirement_shortfalls` is always `Vec::new()` from `DiagnosticTraceCollector.into_report()`. The Rust conformance tests recompute shortfall count arithmetically rather than asserting it from the report. If S03 requires both languages to have the same observability story, the Rust wiring gap will need to be fixed there. **Do not attempt to fix Rust in S02.**
- **Pinned scenario in conformance**: The 5 Rust conformance vectors do not include scenario 3 (pinned decrement). The .NET integration tests should mirror all 5 Rust vectors but NOT add a pinned scenario — it's not implementable without `ISlicer` API changes.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| .NET / TUnit | none needed | n/a — codebase patterns established |

## Sources

- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — direct code audit (256 lines, complete implementation)
- `src/Wollax.Cupel/CupelPipeline.cs` — pipeline re-association loop at line 327–344, QuotaSlice cast pattern at line 361
- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — builder internals, setter pattern
- `.kata/milestones/M003/slices/S03/S03-SUMMARY.md` — Forward Intelligence section: "pipeline wiring deferred", "LastShortfalls sidecar is v1 test surface", "cap exclusions deferred to pipeline wiring"
- `crates/cupel/conformance/required/slicing/count-quota-*.toml` — 5 Rust conformance vector shapes to mirror
- `crates/cupel/tests/conformance/slicing.rs` — `run_count_quota_full_test` harness (shortfall/cap count verification pattern)
- `.kata/DECISIONS.md` — D084–D087 (S03 implementation decisions), D046 (shortfalls at report level), D053 (cap scope: slicer-selected only), D057 (non-required property)
- `.planning/design/count-quota-design.md` — DI-3 to DI-6, Section 3 (pinned interaction), Section 6 (backward compat audit)
