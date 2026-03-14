using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Diagnostics;

public class ReportBuilderTests
{
    [Test]
    public async Task Build_EmptyBuilder_ReturnsEmptyReport()
    {
        var builder = new ReportBuilder();
        var events = new List<TraceEvent>();

        var report = builder.Build(events);

        await Assert.That(report.Included.Count).IsEqualTo(0);
        await Assert.That(report.Excluded.Count).IsEqualTo(0);
        await Assert.That(report.TotalCandidates).IsEqualTo(0);
        await Assert.That(report.TotalTokensConsidered).IsEqualTo(0);
        await Assert.That(report.Events).IsEqualTo(events);
    }

    [Test]
    public async Task AddIncluded_AccumulatesItems()
    {
        var builder = new ReportBuilder();
        var item1 = new ContextItem { Content = "a", Tokens = 100 };
        var item2 = new ContextItem { Content = "b", Tokens = 200 };

        builder.AddIncluded(item1, 0.9, InclusionReason.Scored);
        builder.AddIncluded(item2, 1.0, InclusionReason.Pinned);

        var report = builder.Build([]);

        await Assert.That(report.Included.Count).IsEqualTo(2);
        await Assert.That(report.Included[0].Item).IsEqualTo(item1);
        await Assert.That(report.Included[0].Score).IsEqualTo(0.9);
        await Assert.That(report.Included[0].Reason).IsEqualTo(InclusionReason.Scored);
        await Assert.That(report.Included[1].Item).IsEqualTo(item2);
        await Assert.That(report.Included[1].Reason).IsEqualTo(InclusionReason.Pinned);
    }

    [Test]
    public async Task AddExcluded_AccumulatesItems()
    {
        var builder = new ReportBuilder();
        var item = new ContextItem { Content = "excluded", Tokens = 300 };

        builder.AddExcluded(item, 0.2, ExclusionReason.BudgetExceeded);

        var report = builder.Build([]);

        await Assert.That(report.Excluded.Count).IsEqualTo(1);
        await Assert.That(report.Excluded[0].Item).IsEqualTo(item);
        await Assert.That(report.Excluded[0].Score).IsEqualTo(0.2);
        await Assert.That(report.Excluded[0].Reason).IsEqualTo(ExclusionReason.BudgetExceeded);
        await Assert.That(report.Excluded[0].DeduplicatedAgainst).IsNull();
    }

    [Test]
    public async Task AddExcluded_WithDeduplicatedAgainst()
    {
        var builder = new ReportBuilder();
        var item = new ContextItem { Content = "dup", Tokens = 100 };
        var winner = new ContextItem { Content = "dup", Tokens = 100 };

        builder.AddExcluded(item, 0.3, ExclusionReason.Deduplicated, winner);

        var report = builder.Build([]);

        await Assert.That(report.Excluded[0].DeduplicatedAgainst).IsEqualTo(winner);
    }

    [Test]
    public async Task SetTotalCandidates_RecordsValue()
    {
        var builder = new ReportBuilder();
        builder.SetTotalCandidates(42);

        var report = builder.Build([]);

        await Assert.That(report.TotalCandidates).IsEqualTo(42);
    }

    [Test]
    public async Task SetTotalTokensConsidered_RecordsValue()
    {
        var builder = new ReportBuilder();
        builder.SetTotalTokensConsidered(9999);

        var report = builder.Build([]);

        await Assert.That(report.TotalTokensConsidered).IsEqualTo(9999);
    }

    [Test]
    public async Task Build_ExcludedSortedByScoreDescending()
    {
        var builder = new ReportBuilder();
        var low = new ContextItem { Content = "low", Tokens = 50 };
        var mid = new ContextItem { Content = "mid", Tokens = 100 };
        var high = new ContextItem { Content = "high", Tokens = 200 };

        // Add in non-sorted order
        builder.AddExcluded(mid, 0.5, ExclusionReason.BudgetExceeded);
        builder.AddExcluded(low, 0.1, ExclusionReason.ScoredTooLow);
        builder.AddExcluded(high, 0.9, ExclusionReason.QuotaCapExceeded);

        var report = builder.Build([]);

        await Assert.That(report.Excluded[0].Score).IsEqualTo(0.9);
        await Assert.That(report.Excluded[1].Score).IsEqualTo(0.5);
        await Assert.That(report.Excluded[2].Score).IsEqualTo(0.1);
    }

    [Test]
    public async Task Build_ExcludedSortStableByInsertionOrder()
    {
        var builder = new ReportBuilder();
        var first = new ContextItem { Content = "first", Tokens = 50 };
        var second = new ContextItem { Content = "second", Tokens = 100 };

        // Same score, different insertion order
        builder.AddExcluded(first, 0.5, ExclusionReason.BudgetExceeded);
        builder.AddExcluded(second, 0.5, ExclusionReason.BudgetExceeded);

        var report = builder.Build([]);

        await Assert.That(report.Excluded[0].Item).IsEqualTo(first);
        await Assert.That(report.Excluded[1].Item).IsEqualTo(second);
    }

    [Test]
    public async Task Build_PassesThroughEvents()
    {
        var builder = new ReportBuilder();
        var traceEvent = new TraceEvent
        {
            Stage = PipelineStage.Slice,
            Duration = TimeSpan.FromMilliseconds(10),
            ItemCount = 3
        };

        var report = builder.Build([traceEvent]);

        await Assert.That(report.Events.Count).IsEqualTo(1);
        await Assert.That(report.Events[0]).IsEqualTo(traceEvent);
    }
}
