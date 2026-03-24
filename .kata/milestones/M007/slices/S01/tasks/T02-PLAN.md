---
estimated_steps: 5
estimated_files: 4
---

# T02: Implement DryRunWithPolicy, policy-based PolicySensitivity overload, and update PublicAPI.Unshipped.txt

**Slice:** S01 — .NET DryRunWithPolicy and policy-accepting PolicySensitivity
**Milestone:** M007

## Description

Implement the two new public APIs and update `PublicAPI.Unshipped.txt`. The goal is to make all T01 tests pass without regressions. Implementation is deliberately thin: both new APIs delegate entirely to existing machinery (`PipelineBuilder.WithPolicy()`, `PipelineBuilder.Build()`, `DryRunWithBudget()`).

Key constraints from research and decisions:
- `DryRunWithPolicy` builds a temp pipeline and calls `DryRunWithBudget` — never duplicates policy→concrete mapping logic (D151, research "Don't Hand-Roll" table)
- Budget is explicit (D148) — `.WithBudget(budget)` on the temp pipeline builder before `.WithPolicy(policy).Build()`
- XML doc must document the `CountQuotaSlice` gap and `SlicerType.Stream` sync fallback (D151)
- Policy-based `PolicySensitivity` overload builds a temp pipeline per variant, calls `DryRunWithBudget`, reuses the same content-keyed diff algorithm
- Both new public members must appear in `PublicAPI.Unshipped.txt`

## Steps

1. **Add `DryRunWithPolicy` to `CupelPipeline.cs`** — insert after the existing `DryRunWithBudget` internal method (around line ~115):
   ```csharp
   /// <summary>
   /// Executes the pipeline in dry-run mode using the given <paramref name="policy"/> instead
   /// of this pipeline's own scorer, slicer, and placer. Useful for comparing configurations
   /// without building separate pipelines.
   /// </summary>
   /// <param name="items">The context items to process.</param>
   /// <param name="budget">The token budget to use. Must be provided explicitly — policies do not carry a budget.</param>
   /// <param name="policy">The policy that drives scorer, slicer, placer, deduplication, and overflow strategy.</param>
   /// <returns>The pipeline result with a fully populated <see cref="SelectionReport"/>.</returns>
   /// <remarks>
   /// <para>
   /// <b>CountQuota limitation:</b> <see cref="CupelPolicy"/> does not support count-based quota
   /// configurations (<c>CountQuotaSlice</c>). Callers needing count-quota fork diagnostics
   /// must use the pipeline-based <see cref="PolicySensitivityExtensions.PolicySensitivity"/>
   /// overload with pre-constructed pipelines.
   /// </para>
   /// <para>
   /// <b>Stream slicer fallback:</b> When <paramref name="policy"/> specifies
   /// <see cref="SlicerType.Stream"/>, the synchronous <see cref="GreedySlice"/> is used
   /// as the slicer (the same fallback applied by <see cref="PipelineBuilder.WithPolicy"/>).
   /// </para>
   /// </remarks>
   /// <exception cref="ArgumentNullException">
   /// <paramref name="items"/>, <paramref name="budget"/>, or <paramref name="policy"/> is <see langword="null"/>.
   /// </exception>
   public ContextResult DryRunWithPolicy(
       IReadOnlyList<ContextItem> items,
       ContextBudget budget,
       CupelPolicy policy)
   {
       ArgumentNullException.ThrowIfNull(items);
       ArgumentNullException.ThrowIfNull(budget);
       ArgumentNullException.ThrowIfNull(policy);
       var tempPipeline = CreateBuilder()
           .WithBudget(budget)
           .WithPolicy(policy)
           .Build();
       return tempPipeline.DryRunWithBudget(items, budget);
   }
   ```

2. **Add the policy-based `PolicySensitivity` overload to `PolicySensitivityExtensions.cs`** — add as a second `public static` method in the existing `PolicySensitivityExtensions` class. Follow the exact structure of the existing pipeline-based overload:
   ```csharp
   /// <summary>
   /// Runs each policy variant over the same items (with the given budget) and produces
   /// a structured diff of items whose inclusion status changed across variants.
   /// </summary>
   /// <param name="items">The candidate context items.</param>
   /// <param name="budget">The token budget to use for every variant.</param>
   /// <param name="variants">Labeled policy configurations to compare.</param>
   /// <returns>A report containing per-variant selection reports and a diff of items that swung.</returns>
   /// <remarks>
   /// Policies are converted to temporary pipelines internally using <see cref="PipelineBuilder.WithPolicy"/>.
   /// <see cref="CupelPolicy"/> does not support count-based quota configurations —
   /// callers needing count-quota fork diagnostics must use the pipeline-based overload.
   /// </remarks>
   /// <exception cref="ArgumentNullException"><paramref name="items"/>, <paramref name="budget"/>, or <paramref name="variants"/> is <see langword="null"/>.</exception>
   /// <exception cref="ArgumentException">Fewer than two variants were provided.</exception>
   public static PolicySensitivityReport PolicySensitivity(
       IReadOnlyList<ContextItem> items,
       ContextBudget budget,
       params (string Label, CupelPolicy Policy)[] variants)
   {
       ArgumentNullException.ThrowIfNull(items);
       ArgumentNullException.ThrowIfNull(budget);
       ArgumentNullException.ThrowIfNull(variants);

       if (variants.Length < 2)
           throw new ArgumentException("At least two variants are required for a sensitivity comparison.", nameof(variants));

       // Run each policy variant by building a temporary pipeline, then DryRunWithBudget.
       var labeledReports = new (string Label, SelectionReport Report)[variants.Length];
       for (var i = 0; i < variants.Length; i++)
       {
           var tempPipeline = CupelPipeline.CreateBuilder()
               .WithBudget(budget)
               .WithPolicy(variants[i].Policy)
               .Build();
           var result = tempPipeline.DryRunWithBudget(items, budget);
           labeledReports[i] = (variants[i].Label, result.Report!);
       }

       // Content-keyed diff: identical algorithm to the pipeline-based overload.
       var statusMap = new Dictionary<string, List<(string Label, ItemStatus Status)>>(StringComparer.Ordinal);
       for (var v = 0; v < labeledReports.Length; v++)
       {
           var label = labeledReports[v].Label;
           var report = labeledReports[v].Report;
           for (var i = 0; i < report.Included.Count; i++)
           {
               var content = report.Included[i].Item.Content;
               if (!statusMap.TryGetValue(content, out var list))
               { list = new List<(string, ItemStatus)>(variants.Length); statusMap[content] = list; }
               list.Add((label, ItemStatus.Included));
           }
           for (var i = 0; i < report.Excluded.Count; i++)
           {
               var content = report.Excluded[i].Item.Content;
               if (!statusMap.TryGetValue(content, out var list))
               { list = new List<(string, ItemStatus)>(variants.Length); statusMap[content] = list; }
               list.Add((label, ItemStatus.Excluded));
           }
       }

       var diffs = new List<PolicySensitivityDiffEntry>();
       foreach (var kvp in statusMap)
       {
           var statuses = kvp.Value;
           var hasIncluded = false;
           var hasExcluded = false;
           for (var i = 0; i < statuses.Count; i++)
           {
               if (statuses[i].Status == ItemStatus.Included) hasIncluded = true;
               else hasExcluded = true;
               if (hasIncluded && hasExcluded) break;
           }
           if (hasIncluded && hasExcluded)
               diffs.Add(new PolicySensitivityDiffEntry { Content = kvp.Key, Statuses = statuses });
       }

       return new PolicySensitivityReport { Variants = labeledReports, Diffs = diffs };
   }
   ```

3. **Update `PublicAPI.Unshipped.txt`** — append two entries:
   ```
   Wollax.Cupel.CupelPipeline.DryRunWithPolicy(System.Collections.Generic.IReadOnlyList<Wollax.Cupel.ContextItem!>! items, Wollax.Cupel.ContextBudget! budget, Wollax.Cupel.CupelPolicy! policy) -> Wollax.Cupel.ContextResult!
   static Wollax.Cupel.Diagnostics.PolicySensitivityExtensions.PolicySensitivity(System.Collections.Generic.IReadOnlyList<Wollax.Cupel.ContextItem!>! items, Wollax.Cupel.ContextBudget! budget, params (string! Label, Wollax.Cupel.CupelPolicy! Policy)[]! variants) -> Wollax.Cupel.Diagnostics.PolicySensitivityReport!
   ```

4. **Build and verify zero warnings** — `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj --no-incremental 2>&1 | grep -E "error|warning"`. If the PublicAPI analyzer complains about missing entries, adjust `PublicAPI.Unshipped.txt` to match the exact analyzer-generated format (use `dotnet build` error message as the canonical format hint).

5. **Run full test suite** — `dotnet test tests/Wollax.Cupel.Tests/ --filter "DryRunWithPolicy|PolicySensitivity"` must show all 9 new tests passing; then `dotnet test` must show no regressions.

## Must-Haves

- [ ] `DryRunWithPolicy` is added to `CupelPipeline` as a public method with complete XML doc including CountQuota gap and Stream slicer fallback notes (D151)
- [ ] `DryRunWithPolicy` null-guards all three parameters using `ArgumentNullException.ThrowIfNull`
- [ ] `DryRunWithPolicy` delegates to `tempPipeline.DryRunWithBudget(items, budget)` — does NOT duplicate policy→concrete mapping logic
- [ ] `DryRunWithPolicy` calls `.WithBudget(budget)` before `.WithPolicy(policy).Build()` on the temp pipeline (D148 — explicit budget required)
- [ ] Policy-based `PolicySensitivity` overload uses the same content-keyed diff algorithm as the pipeline-based overload (no code duplication in the algorithm, but the loop is necessarily re-stated)
- [ ] `PublicAPI.Unshipped.txt` contains exactly the two new entries; `dotnet build` passes the PublicAPI analyzer with 0 errors/warnings
- [ ] `dotnet test tests/Wollax.Cupel.Tests/ --filter "DryRunWithPolicy|PolicySensitivity"` → all 9 new tests pass
- [ ] `dotnet test` (full suite) → no regressions

## Verification

```bash
# 0 warnings from the library build
dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj --no-incremental 2>&1 | grep -cE "warning" || true

# All new tests pass
dotnet test tests/Wollax.Cupel.Tests/ --filter "DryRunWithPolicy|PolicySensitivity" --verbosity normal

# Full suite — no regressions
dotnet test --verbosity quiet 2>&1 | tail -5
```

## Observability Impact

- Signals added/changed: `DryRunWithPolicy` returns `ContextResult` with a fully-populated `SelectionReport` (same as `DryRunWithBudget`) — callers can inspect `result.Report!.Included` / `result.Report!.Excluded` to understand which items the policy selected and why.
- How a future agent inspects this: `result.Report!.Included.Count`, `result.Report!.Excluded[i].Reason` — all existing diagnostics surface is available through the temp pipeline's `DryRunWithBudget` path.
- Failure state exposed: if a test fails, `Assert.That(result.Report!.Included.Count)` message shows actual vs expected count; `result.Report!.Excluded` shows which items were excluded and with what `ExclusionReason`.

## Inputs

- T01 test files — `DryRunWithPolicyTests.cs` and additions to `PolicySensitivityTests.cs` — define the exact method signatures that must compile
- `src/Wollax.Cupel/CupelPipeline.cs` line ~115 — insert `DryRunWithPolicy` after `DryRunWithBudget`
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — add second overload alongside the existing one
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — append two new entries; run `dotnet build` to confirm PublicAPI analyzer accepts them
- `src/Wollax.Cupel/PipelineBuilder.cs` — `WithPolicy(CupelPolicy)` and `Build()` are the key internal entry points used by both new methods

## Expected Output

- `src/Wollax.Cupel/CupelPipeline.cs` — `DryRunWithPolicy` public method added
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — second `PolicySensitivity` overload added
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — two new entries appended
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` exits 0 with 0 warnings
- `dotnet test tests/Wollax.Cupel.Tests/ --filter "DryRunWithPolicy|PolicySensitivity"` → 9 tests pass
- `dotnet test` → full suite passes with no regressions
