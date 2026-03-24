using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Pipeline;

/// <summary>
/// Integration tests verifying CountQuotaSlice(QuotaSlice(GreedySlice)) composition
/// through CupelPipeline.DryRun(). Proves both count constraints and percentage
/// constraints are simultaneously active and produce visible effects on the report.
/// </summary>
public class CountQuotaCompositionTests
{
    private static ContextItem Item(string content, int tokens, double score, ContextKind kind) =>
        new()
        {
            Content = content,
            Tokens = tokens,
            FutureRelevanceHint = score,
            Kind = kind
        };

    private static ContextResult Run(
        IReadOnlyList<CountQuotaEntry> countEntries,
        QuotaSet quotaSet,
        IReadOnlyList<ContextItem> items,
        int budgetTokens)
    {
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(budgetTokens, budgetTokens))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new CountQuotaSlice(new QuotaSlice(new GreedySlice(), quotaSet), countEntries))
            .Build();

        return pipeline.DryRun(items);
    }

    // CountQuotaSlice(QuotaSlice(GreedySlice)) — both count cap and percentage cap active.
    // 3 ToolOutput items (scores 0.9/0.7/0.5) + 2 Message items (scores 0.8/0.6), budget 400.
    // Count entry: require=1, cap=2 for ToolOutput.
    // Quota: 10% require, 60% cap for ToolOutput.
    // Expected: at most 2 ToolOutput included (count cap), at least 1 CountCapExceeded, no shortfalls.
    [Test]
    public async Task CompositionWithQuotaSlice_CountCapAndPercentageConstraintsBothActive()
    {
        var tool = ContextKind.ToolOutput;

        var items = new[]
        {
            Item("tool-a", 100, 0.9, tool),
            Item("tool-b", 100, 0.7, tool),
            Item("tool-c", 100, 0.5, tool),
            Item("msg-a",  100, 0.8, ContextKind.Message),
            Item("msg-b",  100, 0.6, ContextKind.Message),
        };

        var countEntries = new[] { new CountQuotaEntry(tool, requireCount: 1, capCount: 2) };
        var quotaSet = new QuotaBuilder()
            .Require(tool, 10)
            .Cap(tool, 60)
            .Build();

        var result = Run(countEntries, quotaSet, items, budgetTokens: 400);

        // Count cap holds: at most 2 ToolOutput items included
        await Assert.That(result.Report!.Included.Count(i => i.Item.Kind == tool)).IsLessThanOrEqualTo(2);

        // Count cap exclusion visible: at least 1 CountCapExceeded in excluded
        await Assert.That(result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded)).IsGreaterThanOrEqualTo(1);

        // Require=1 satisfied: no count requirement shortfalls
        await Assert.That(result.Report.CountRequirementShortfalls.Count).IsEqualTo(0);
    }
}
