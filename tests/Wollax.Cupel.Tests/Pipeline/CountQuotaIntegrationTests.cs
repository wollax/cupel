using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Pipeline;

/// <summary>
/// Integration tests verifying CountQuotaSlice wiring through CupelPipeline.DryRun().
/// Each test mirrors one Rust conformance vector from
/// crates/cupel/conformance/required/slicing/count-quota-*.toml.
/// </summary>
public class CountQuotaIntegrationTests
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
        IReadOnlyList<CountQuotaEntry> entries,
        IReadOnlyList<ContextItem> items,
        int budgetTokens,
        ScarcityBehavior scarcity = ScarcityBehavior.Degrade)
    {
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(budgetTokens, budgetTokens))
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(new CountQuotaSlice(new GreedySlice(), entries, scarcity))
            .Build();

        return pipeline.DryRun(items);
    }

    // count-quota-baseline.toml
    // 3 tool items, budget 1000, require=2 cap=4 — all 3 fit, no shortfalls, no cap exclusions.
    [Test]
    public async Task Baseline_AllItemsIncluded_NoShortfalls_NoCap()
    {
        var tool = ContextKind.ToolOutput;
        var items = new[]
        {
            Item("tool-a", 100, 0.9, tool),
            Item("tool-b", 100, 0.7, tool),
            Item("tool-c", 100, 0.5, tool),
        };
        var entries = new[] { new CountQuotaEntry(tool, requireCount: 2, capCount: 4) };

        var result = Run(entries, items, budgetTokens: 1000);

        await Assert.That(result.Report!.Included.Count).IsEqualTo(3);

        var includedContents = result.Report.Included.Select(i => i.Item.Content).ToList();
        await Assert.That(includedContents).Contains("tool-a");
        await Assert.That(includedContents).Contains("tool-b");
        await Assert.That(includedContents).Contains("tool-c");

        await Assert.That(result.Report.CountRequirementShortfalls.Count).IsEqualTo(0);
        await Assert.That(result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded)).IsEqualTo(0);
    }

    // count-quota-cap-exclusion.toml
    // 3 tool items, budget 1000, require=0 cap=1 — only highest-scoring item passes; 2 cap-excluded.
    [Test]
    public async Task CapExclusion_OnlyOneIncluded_TwoCapExcluded()
    {
        var tool = ContextKind.ToolOutput;
        var items = new[]
        {
            Item("tool-a", 100, 0.9, tool),
            Item("tool-b", 100, 0.7, tool),
            Item("tool-c", 100, 0.5, tool),
        };
        var entries = new[] { new CountQuotaEntry(tool, requireCount: 0, capCount: 1) };

        var result = Run(entries, items, budgetTokens: 1000);

        await Assert.That(result.Report!.Included.Count).IsEqualTo(1);

        var includedContents = result.Report.Included.Select(i => i.Item.Content).ToList();
        await Assert.That(includedContents).Contains("tool-a");

        var capExcluded = result.Report.Excluded.Where(e => e.Reason == ExclusionReason.CountCapExceeded).ToList();
        await Assert.That(capExcluded.Count).IsEqualTo(2);

        var capExcludedContents = capExcluded.Select(e => e.Item.Content).ToList();
        await Assert.That(capExcludedContents).Contains("tool-b");
        await Assert.That(capExcludedContents).Contains("tool-c");

        await Assert.That(result.Report.CountRequirementShortfalls.Count).IsEqualTo(0);
    }

    // count-quota-require-and-cap.toml
    // 4 tool items, budget 1000, require=2 cap=2 — top 2 included, bottom 2 cap-excluded, no shortfalls.
    [Test]
    public async Task RequireAndCap_TopTwoIncluded_BottomTwoCapExcluded()
    {
        var tool = ContextKind.ToolOutput;
        var items = new[]
        {
            Item("tool-a", 100, 0.9, tool),
            Item("tool-b", 100, 0.7, tool),
            Item("tool-c", 100, 0.6, tool),
            Item("tool-d", 100, 0.4, tool),
        };
        var entries = new[] { new CountQuotaEntry(tool, requireCount: 2, capCount: 2) };

        var result = Run(entries, items, budgetTokens: 1000);

        await Assert.That(result.Report!.Included.Count).IsEqualTo(2);

        var includedContents = result.Report.Included.Select(i => i.Item.Content).ToList();
        await Assert.That(includedContents).Contains("tool-a");
        await Assert.That(includedContents).Contains("tool-b");

        var capExcluded = result.Report.Excluded.Where(e => e.Reason == ExclusionReason.CountCapExceeded).ToList();
        await Assert.That(capExcluded.Count).IsEqualTo(2);

        var capExcludedContents = capExcluded.Select(e => e.Item.Content).ToList();
        await Assert.That(capExcludedContents).Contains("tool-c");
        await Assert.That(capExcludedContents).Contains("tool-d");

        await Assert.That(result.Report.CountRequirementShortfalls.Count).IsEqualTo(0);
    }

    // count-quota-scarcity-degrade.toml
    // 1 tool item, budget 1000, require=3 cap=5 — item included, shortfall recorded (required 3, satisfied 1).
    [Test]
    public async Task ScarcityDegrade_OneIncluded_ShortfallRecorded()
    {
        var tool = ContextKind.ToolOutput;
        var items = new[]
        {
            Item("tool-a", 100, 0.9, tool),
        };
        var entries = new[] { new CountQuotaEntry(tool, requireCount: 3, capCount: 5) };

        var result = Run(entries, items, budgetTokens: 1000, scarcity: ScarcityBehavior.Degrade);

        await Assert.That(result.Report!.Included.Count).IsEqualTo(1);

        var includedContents = result.Report.Included.Select(i => i.Item.Content).ToList();
        await Assert.That(includedContents).Contains("tool-a");

        await Assert.That(result.Report.CountRequirementShortfalls.Count).IsEqualTo(1);
        await Assert.That(result.Report.CountRequirementShortfalls[0].RequiredCount).IsEqualTo(3);
        await Assert.That(result.Report.CountRequirementShortfalls[0].SatisfiedCount).IsEqualTo(1);

        await Assert.That(result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded)).IsEqualTo(0);
    }

    // count-quota-tag-nonexclusive.toml
    // 3 items across 2 kinds (critical + urgent), require=1 cap=4 for each — all 3 included, no shortfalls, no cap exclusions.
    [Test]
    public async Task TagNonExclusive_AllThreeIncluded_NoShortfalls_NoCap()
    {
        var critical = new ContextKind("critical");
        var urgent = new ContextKind("urgent");

        var items = new[]
        {
            Item("item-critical", 100, 0.9, critical),
            Item("item-urgent",   100, 0.8, urgent),
            Item("item-extra",    100, 0.5, critical),
        };
        var entries = new[]
        {
            new CountQuotaEntry(critical, requireCount: 1, capCount: 4),
            new CountQuotaEntry(urgent,   requireCount: 1, capCount: 4),
        };

        var result = Run(entries, items, budgetTokens: 1000);

        await Assert.That(result.Report!.Included.Count).IsEqualTo(3);

        await Assert.That(result.Report.CountRequirementShortfalls.Count).IsEqualTo(0);
        await Assert.That(result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded)).IsEqualTo(0);
    }
}
