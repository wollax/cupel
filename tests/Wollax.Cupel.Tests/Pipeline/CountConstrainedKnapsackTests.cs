using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Pipeline;

/// <summary>
/// Integration tests verifying CountConstrainedKnapsackSlice wiring through CupelPipeline.DryRun().
/// Each test mirrors one Rust conformance vector from
/// crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml.
/// </summary>
public class CountConstrainedKnapsackTests
{
    private static ContextItem Item(string content, int tokens, double score, ContextKind kind) =>
        new()
        {
            Content = content,
            Tokens = tokens,
            FutureRelevanceHint = score,
            Kind = kind
        };

    private static ContextResult DryRun(
        IReadOnlyList<CountQuotaEntry> entries,
        IReadOnlyList<ContextItem> items,
        int budgetTokens,
        int bucketSize = 100,
        ScarcityBehavior scarcity = ScarcityBehavior.Degrade)
    {
        var knapsack = new KnapsackSlice(bucketSize);
        var slicer = new CountConstrainedKnapsackSlice(entries, knapsack, scarcity);
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(budgetTokens, budgetTokens))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(slicer)
            .Build();

        return pipeline.DryRun(items);
    }

    // count-constrained-knapsack-baseline.toml
    // 3 items (2 tool, 1 msg), budget=1000, require=2 cap=4 bucket=100 — all 3 selected, no shortfalls, no cap exclusions.
    [Test]
    public async Task Baseline_AllItemsIncluded_NoShortfalls_NoCap()
    {
        var tool = ContextKind.ToolOutput;
        var msg = new ContextKind("msg");
        var items = new[]
        {
            Item("tool-a", 100, 0.9, tool),
            Item("tool-b", 100, 0.7, tool),
            Item("msg-x",  100, 0.5, msg),
        };
        var entries = new[] { new CountQuotaEntry(tool, requireCount: 2, capCount: 4) };

        var result = DryRun(entries, items, budgetTokens: 1000, bucketSize: 100);

        await Assert.That(result.Report!.Included.Count).IsEqualTo(3);

        var includedContents = result.Report.Included.Select(i => i.Item.Content).ToList();
        await Assert.That(includedContents).Contains("tool-a");
        await Assert.That(includedContents).Contains("tool-b");
        await Assert.That(includedContents).Contains("msg-x");

        await Assert.That(result.Report.CountRequirementShortfalls.Count).IsEqualTo(0);
        await Assert.That(result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded)).IsEqualTo(0);
    }

    // count-constrained-knapsack-cap-exclusion.toml
    // 4 tool items, budget=600, require=1 cap=2 bucket=100 — top 2 included, 2 cap-excluded, no shortfalls.
    [Test]
    public async Task CapExclusion_TwoCapExcluded()
    {
        var tool = ContextKind.ToolOutput;
        var items = new[]
        {
            Item("tool-a", 100, 0.9, tool),
            Item("tool-b", 100, 0.8, tool),
            Item("tool-c", 100, 0.7, tool),
            Item("tool-d", 100, 0.6, tool),
        };
        var entries = new[] { new CountQuotaEntry(tool, requireCount: 1, capCount: 2) };

        var result = DryRun(entries, items, budgetTokens: 600, bucketSize: 100);

        await Assert.That(result.Report!.Included.Count).IsEqualTo(2);

        var includedContents = result.Report.Included.Select(i => i.Item.Content).ToList();
        await Assert.That(includedContents).Contains("tool-a");
        await Assert.That(includedContents).Contains("tool-b");

        await Assert.That(result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded)).IsEqualTo(2);

        await Assert.That(result.Report.CountRequirementShortfalls.Count).IsEqualTo(0);
    }

    // count-constrained-knapsack-scarcity-degrade.toml
    // 1 tool item, budget=500, require=3 cap=5 bucket=100, scarcity=Degrade — 1 included, 1 shortfall recorded.
    [Test]
    public async Task ScarcityDegrade_ShortfallRecorded()
    {
        var tool = ContextKind.ToolOutput;
        var items = new[]
        {
            Item("tool-a", 100, 0.9, tool),
        };
        var entries = new[] { new CountQuotaEntry(tool, requireCount: 3, capCount: 5) };

        var result = DryRun(entries, items, budgetTokens: 500, bucketSize: 100, scarcity: ScarcityBehavior.Degrade);

        await Assert.That(result.Report!.Included.Count).IsEqualTo(1);

        var includedContents = result.Report.Included.Select(i => i.Item.Content).ToList();
        await Assert.That(includedContents).Contains("tool-a");

        await Assert.That(result.Report.CountRequirementShortfalls.Count).IsEqualTo(1);
        await Assert.That(result.Report.CountRequirementShortfalls[0].Kind).IsEqualTo(tool);
        await Assert.That(result.Report.CountRequirementShortfalls[0].RequiredCount).IsEqualTo(3);
        await Assert.That(result.Report.CountRequirementShortfalls[0].SatisfiedCount).IsEqualTo(1);

        await Assert.That(result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded)).IsEqualTo(0);
    }

    // count-constrained-knapsack-tag-nonexclusive.toml
    // 3 items (2 tool, 1 memory), budget=1000, require=1 cap=4 for each kind, bucket=100 — all 3 included, no shortfalls, no cap exclusions.
    [Test]
    public async Task TagNonExclusive_MultipleKindsRequiredIndependently()
    {
        var tool = ContextKind.ToolOutput;
        var memory = ContextKind.Memory;
        var items = new[]
        {
            Item("item-tool",   100, 0.9, tool),
            Item("item-memory", 100, 0.8, memory),
            Item("item-extra",  100, 0.5, tool),
        };
        var entries = new[]
        {
            new CountQuotaEntry(tool,   requireCount: 1, capCount: 4),
            new CountQuotaEntry(memory, requireCount: 1, capCount: 4),
        };

        var result = DryRun(entries, items, budgetTokens: 1000, bucketSize: 100);

        await Assert.That(result.Report!.Included.Count).IsEqualTo(3);

        await Assert.That(result.Report.CountRequirementShortfalls.Count).IsEqualTo(0);
        await Assert.That(result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded)).IsEqualTo(0);
    }

    // count-constrained-knapsack-require-and-cap.toml
    // 5 items (2 tool, 3 msg), budget=1000, require=2 cap=2 for tool, bucket=1 — all 5 included, no shortfalls, no cap exclusions.
    [Test]
    public async Task RequireAndCap_NoResidualExcluded()
    {
        var tool = ContextKind.ToolOutput;
        var msg = new ContextKind("msg");
        var items = new[]
        {
            Item("tool-a", 100, 0.9, tool),
            Item("tool-b", 100, 0.7, tool),
            Item("msg-s",   50, 0.8, msg),
            Item("msg-m",  150, 0.6, msg),
            Item("msg-l",  200, 0.4, msg),
        };
        var entries = new[] { new CountQuotaEntry(tool, requireCount: 2, capCount: 2) };

        var result = DryRun(entries, items, budgetTokens: 1000, bucketSize: 1);

        await Assert.That(result.Report!.Included.Count).IsEqualTo(5);

        await Assert.That(result.Report.CountRequirementShortfalls.Count).IsEqualTo(0);
        await Assert.That(result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded)).IsEqualTo(0);
    }
}
