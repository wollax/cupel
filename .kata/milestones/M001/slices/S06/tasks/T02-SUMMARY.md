---
id: T02
parent: S06
milestone: M001
provides:
  - OverflowStrategyValue internal property renamed to OverflowStrategy in CupelPipeline.cs and its one consumer (CupelServiceCollectionExtensions.cs)
  - QuotaBuilder total-sum check uses epsilon (> 100.0 + 1e-9) to accept three equal-share 33.333...% quotas
  - CupelPolicy Quotas+Stream error message replaced with caller-facing language (no internal type names)
  - ScorerEntry InnerScorer error message includes corrective guidance ("Remove it or change the type to Scaled")
  - ScorerType.Scaled = 6, SlicerType.Stream = 2, PipelineStage explicit 0–4 values anchored
  - ContextItem all properties enriched with comprehensive XML <summary> docs including units and invariants
key_files:
  - src/Wollax.Cupel/CupelPipeline.cs
  - src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs
  - src/Wollax.Cupel/Slicing/QuotaBuilder.cs
  - src/Wollax.Cupel/CupelPolicy.cs
  - src/Wollax.Cupel/ScorerEntry.cs
  - src/Wollax.Cupel/ScorerType.cs
  - src/Wollax.Cupel/SlicerType.cs
  - src/Wollax.Cupel/Diagnostics/PipelineStage.cs
  - src/Wollax.Cupel/ContextItem.cs
key_decisions:
  - Epsilon applied only to the total-sum check (totalRequired > 100.0 + 1e-9), not to per-kind Require > Cap checks, matching the task plan constraint
  - CupelPipeline.OverflowStrategy property name matches the type name (OverflowStrategy OverflowStrategy) — valid in C# and already used by CupelPolicy and PolicyComponents in the same codebase
  - PublicAPI.Shipped.txt values verified before hardcoding: ScorerType.Scaled = 6, SlicerType.Stream = 2, PipelineStage 0–4
patterns_established:
  - Error messages name only the public API surface (param name, enum value) — no internal types like ISlicer, QuotaSlice, IAsyncSlicer in user-visible exceptions
observability_surfaces:
  - CupelPolicy Quotas+Stream exception message now names the public constraint precisely — easier to locate in test output and logs
  - ScorerEntry InnerScorer exception message now includes corrective action — callers know what to change
duration: 12min
verification_result: passed
completed_at: 2026-03-21T00:00:00Z
blocker_discovered: false
---

# T02: Harden API surface (naming, error messages, enum anchoring, epsilon fix)

**Seven API surface fixes: property rename, epsilon for equal-share quotas, two caller-facing error messages, three enum integer anchors, and comprehensive ContextItem XML docs.**

## What Happened

All seven fixes applied cleanly with no build errors or test regressions.

**Step 1 — Rename:** `CupelPipeline.OverflowStrategyValue` renamed to `OverflowStrategy`. Only one internal consumer (`CupelServiceCollectionExtensions.cs`) needed updating. The property type is also `OverflowStrategy`, but C# allows property and type to share a name — the pattern already existed in `CupelPolicy` and `PolicyComponents` in the same codebase.

**Step 2 — Epsilon fix:** `QuotaBuilder.Build()` total-sum check changed from `> 100` to `> 100.0 + 1e-9`. This allows three equal-share quotas (3 × 33.333...%) to pass validation without rejecting due to floating-point accumulation drift. Per-kind `Require > Cap` checks left unchanged as specified.

**Step 3 — CupelPolicy error message:** Removed internal type names (`QuotaSlice`, `ISlicer`, `IAsyncSlicer`) from the Quotas+Stream validation error. Replaced with: `"Quotas cannot be combined with SlicerType.Stream. Stream slicing is asynchronous and does not support synchronous quota wrapping."` — names only public API concepts.

**Step 4 — ScorerEntry error message:** Replaced `"InnerScorer must be null when Type is not Scaled."` with `"InnerScorer is only valid for ScorerType.Scaled. Remove it or change the type to Scaled."` — adds corrective guidance.

**Step 5 — Enum anchoring:** All values verified against `PublicAPI.Shipped.txt` before writing. `ScorerType.Scaled = 6`, `SlicerType.Stream = 2`, `PipelineStage.Classify = 0` through `Place = 4` — all match the shipped API file.

**Step 6 — ContextItem XML docs:** All 11 properties enriched with `<summary>` comments covering what each represents, units (e.g. token counts in model tokenization units), default values, invariants (`Tokens >= 0`, `OriginalTokens >= 0` when set), and behavioral notes (how scorers use each field).

## Verification

- `rg "OverflowStrategyValue" src/ tests/` — zero results
- `dotnet build` — Build succeeded, 0 warnings, 0 errors
- `dotnet test` — 653/653 passed, 0 failed, 0 skipped

## Diagnostics

- Error messages in `CupelPolicy` and `ScorerEntry` are the primary observability surface — test failures display the exception message directly in output, making the root cause immediately visible
- `rg "OverflowStrategyValue"` is the authoritative post-rename check

## Deviations

None — all steps executed as planned.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/CupelPipeline.cs` — renamed `OverflowStrategyValue` → `OverflowStrategy`
- `src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs` — updated call site to use `OverflowStrategy`
- `src/Wollax.Cupel/Slicing/QuotaBuilder.cs` — epsilon fix on total-sum check
- `src/Wollax.Cupel/CupelPolicy.cs` — caller-facing Quotas+Stream error message
- `src/Wollax.Cupel/ScorerEntry.cs` — improved InnerScorer error message with corrective guidance
- `src/Wollax.Cupel/ScorerType.cs` — `Scaled = 6` explicit anchor
- `src/Wollax.Cupel/SlicerType.cs` — `Stream = 2` explicit anchor
- `src/Wollax.Cupel/Diagnostics/PipelineStage.cs` — `Classify = 0` through `Place = 4` explicit anchors
- `src/Wollax.Cupel/ContextItem.cs` — comprehensive XML `<summary>` docs on all 11 properties
