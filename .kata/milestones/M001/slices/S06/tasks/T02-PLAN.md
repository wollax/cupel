---
estimated_steps: 7
estimated_files: 9
---

# T02: Harden API surface (naming, error messages, enum anchoring, epsilon fix)

**Slice:** S06 — .NET Quality Hardening
**Milestone:** M001

## Description

Seven discrete API surface fixes: rename an internal property to eliminate the shadowing smell, fix a floating-point accumulation bug that spuriously rejects three equal-share quotas, replace two internal-leaking error messages with caller-facing language, and anchor three enums with explicit integer values for forward-compatibility. Also add comprehensive XML doc comments to `ContextItem` properties. These are all internal or doc changes — no new public API surface is introduced. The rename is `internal` so no `PublicAPI.Shipped.txt` change is needed.

## Steps

1. **Rename OverflowStrategyValue → OverflowStrategy** (CupelPipeline.cs). First run `rg "OverflowStrategyValue" /Users/wollax/Git/personal/cupel/src /Users/wollax/Git/personal/cupel/tests` to locate all consumers. Rename the property in `CupelPipeline.cs` and update all internal call sites found by rg (likely `CupelServiceCollectionExtensions.cs` in the DI extension).

2. **QuotaBuilder epsilon fix** (`src/Wollax.Cupel/Slicing/QuotaBuilder.cs`). Change `if (totalRequired > 100)` to `if (totalRequired > 100.0 + 1e-9)`. This is the sum-accumulation check only — the per-kind `Require > Cap` check does not need an epsilon.

3. **CupelPolicy Quotas+Stream error message** (`src/Wollax.Cupel/CupelPolicy.cs`). Find the error near line 134 that mentions `QuotaSlice`, `ISlicer`, `IAsyncSlicer`. Replace with: `"Quotas cannot be combined with SlicerType.Stream. Stream slicing is asynchronous and does not support synchronous quota wrapping."`.

4. **ScorerEntry InnerScorer error message** (`src/Wollax.Cupel/ScorerEntry.cs`). Find the validation near lines 86-90: `"InnerScorer must be null when Type is not Scaled."`. Replace with: `"InnerScorer is only valid for ScorerType.Scaled. Remove it or change the type to Scaled."`.

5. **Explicit enum integer anchoring** — three files:
   - `src/Wollax.Cupel/ScorerType.cs`: add `= 6` to the `Scaled` variant. Verify against `src/Wollax.Cupel/PublicAPI.Shipped.txt` first — confirm `Scaled = 6` appears there.
   - `src/Wollax.Cupel/SlicerType.cs`: add `= 2` to the `Stream` variant. Verify against PublicAPI.Shipped.txt.
   - `src/Wollax.Cupel/Diagnostics/PipelineStage.cs`: add `Classify = 0, Score = 1, Deduplicate = 2, Slice = 3, Place = 4` explicit values.

6. **ContextItem XML docs** (`src/Wollax.Cupel/ContextItem.cs`). Add `<summary>` XML comments to every property. Use `ContextBudget.cs` as the model for doc style and comprehensiveness. Each property needs at minimum: what it represents, its unit where applicable, and any invariants (e.g. `Tokens >= 0`).

7. Run `dotnet build` and `dotnet test`. Fix any compilation errors from the rename before continuing. Ensure all 649+ tests pass.

## Must-Haves

- [ ] `OverflowStrategyValue` renamed to `OverflowStrategy` in `CupelPipeline.cs` and all internal consumers found by `rg`
- [ ] `QuotaBuilder` total-sum check uses `> 100.0 + 1e-9` (not `> 100`)
- [ ] `CupelPolicy` error message at Quotas+Stream validation does not reference `QuotaSlice`, `ISlicer`, or `IAsyncSlicer`
- [ ] `ScorerEntry` InnerScorer error message includes corrective guidance ("Remove it or change the type to Scaled")
- [ ] `ScorerType.Scaled = 6` matches value in `PublicAPI.Shipped.txt`
- [ ] `SlicerType.Stream = 2` matches value in `PublicAPI.Shipped.txt`
- [ ] `PipelineStage` has explicit values `Classify = 0` through `Place = 4`
- [ ] All `ContextItem` properties have XML `<summary>` comments
- [ ] `dotnet build` — zero errors, zero warnings
- [ ] `dotnet test` — zero regressions (all 649+ tests pass)

## Verification

- `rg "OverflowStrategyValue" src/ tests/` — returns zero results after rename
- `dotnet build` — zero errors, zero warnings (renamed property compiles cleanly)
- `dotnet test` — all tests pass; in particular the QuotaBuilder tests exercise the three-equal-shares case
- Check `ScorerType.cs` and `SlicerType.cs` to confirm explicit values match PublicAPI.Shipped.txt before writing

## Observability Impact

- Signals added/changed: error messages for Quotas+Stream and InnerScorer now name the problem precisely — easier to locate in test output and production exceptions
- How a future agent inspects this: `dotnet build` output; failing test names the exception message if the error message check fails
- Failure state exposed: the QuotaBuilder epsilon fix makes three equal-share quotas pass validation — previously silent correctness bug becomes testable via the existing QuotaBuilderTests

## Inputs

- `src/Wollax.Cupel/CupelPipeline.cs` — find `OverflowStrategyValue` internal property definition
- `src/Wollax.Cupel/PublicAPI.Shipped.txt` — verify `ScorerType.Scaled = 6` and `SlicerType.Stream = 2` values before hardcoding
- `src/Wollax.Cupel/ContextBudget.cs` — use as XML doc style model for `ContextItem` properties
- Research note (pitfall): epsilon applies to `totalRequired > 100` check only in QuotaBuilder; per-kind `Require > Cap` does not need epsilon

## Expected Output

- `src/Wollax.Cupel/CupelPipeline.cs` — `OverflowStrategy` (renamed from `OverflowStrategyValue`)
- `src/Wollax.Cupel/Slicing/QuotaBuilder.cs` — epsilon fix applied
- `src/Wollax.Cupel/CupelPolicy.cs` — caller-facing error message at Quotas+Stream
- `src/Wollax.Cupel/ScorerEntry.cs` — improved InnerScorer error message
- `src/Wollax.Cupel/ScorerType.cs` — `Scaled = 6`
- `src/Wollax.Cupel/SlicerType.cs` — `Stream = 2`
- `src/Wollax.Cupel/Diagnostics/PipelineStage.cs` — explicit 0–4 values
- `src/Wollax.Cupel/ContextItem.cs` — XML `<summary>` on all properties
- Any consumer file found by `rg OverflowStrategyValue` — updated to new property name
