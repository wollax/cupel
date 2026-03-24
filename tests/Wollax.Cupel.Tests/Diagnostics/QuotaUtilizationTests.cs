using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Diagnostics;

public class QuotaUtilizationTests
{
    [Test]
    public async Task QuotaSlice_ImplementsIQuotaPolicy_GetConstraints_ReturnsCorrectEntries()
    {
        var quotas = new QuotaBuilder()
            .Require(ContextKind.Message, 20)
            .Cap(ContextKind.Message, 60)
            .Require(ContextKind.Document, 10)
            .Cap(ContextKind.Document, 40)
            .Build();

        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        IQuotaPolicy policy = slicer;

        var constraints = policy.GetConstraints();

        await Assert.That(constraints.Count).IsEqualTo(2);

        // Sort for determinism
        var sorted = constraints.OrderBy(c => c.Kind.Value, StringComparer.Ordinal).ToList();

        // Document comes before Message alphabetically
        await Assert.That(sorted[0].Kind).IsEqualTo(ContextKind.Document);
        await Assert.That(sorted[0].Mode).IsEqualTo(QuotaConstraintMode.Percentage);
        await Assert.That(sorted[0].Require).IsEqualTo(10.0);
        await Assert.That(sorted[0].Cap).IsEqualTo(40.0);

        await Assert.That(sorted[1].Kind).IsEqualTo(ContextKind.Message);
        await Assert.That(sorted[1].Mode).IsEqualTo(QuotaConstraintMode.Percentage);
        await Assert.That(sorted[1].Require).IsEqualTo(20.0);
        await Assert.That(sorted[1].Cap).IsEqualTo(60.0);
    }

    [Test]
    public async Task CountQuotaSlice_ImplementsIQuotaPolicy_GetConstraints_ReturnsCorrectEntries()
    {
        var entries = new List<CountQuotaEntry>
        {
            new(ContextKind.Message, requireCount: 2, capCount: 5),
            new(ContextKind.Document, requireCount: 1, capCount: 3),
        };

        var slicer = new CountQuotaSlice(new GreedySlice(), entries);
        IQuotaPolicy policy = slicer;

        var constraints = policy.GetConstraints();

        await Assert.That(constraints.Count).IsEqualTo(2);

        // Order matches entry order
        await Assert.That(constraints[0].Kind).IsEqualTo(ContextKind.Message);
        await Assert.That(constraints[0].Mode).IsEqualTo(QuotaConstraintMode.Count);
        await Assert.That(constraints[0].Require).IsEqualTo(2.0);
        await Assert.That(constraints[0].Cap).IsEqualTo(5.0);

        await Assert.That(constraints[1].Kind).IsEqualTo(ContextKind.Document);
        await Assert.That(constraints[1].Mode).IsEqualTo(QuotaConstraintMode.Count);
        await Assert.That(constraints[1].Require).IsEqualTo(1.0);
        await Assert.That(constraints[1].Cap).IsEqualTo(3.0);
    }

    [Test]
    public async Task QuotaUtilization_PercentageMode_ReturnsCorrectPerKindUtilization()
    {
        // Budget: 1000 target tokens
        var budget = new ContextBudget(maxTokens: 2000, targetTokens: 1000);

        // Policy: Message require 20%, cap 60%; Document require 10%, cap 40%
        var quotas = new QuotaBuilder()
            .Require(ContextKind.Message, 20)
            .Cap(ContextKind.Message, 60)
            .Require(ContextKind.Document, 10)
            .Cap(ContextKind.Document, 40)
            .Build();
        IQuotaPolicy policy = new QuotaSlice(new GreedySlice(), quotas);

        // Report: 300 tokens of Message, 200 tokens of Document
        var report = new SelectionReport
        {
            Events = [],
            Included = new List<IncludedItem>
            {
                new() { Item = new ContextItem { Content = "msg1", Tokens = 200, Kind = ContextKind.Message }, Score = 1.0, Reason = InclusionReason.Scored },
                new() { Item = new ContextItem { Content = "msg2", Tokens = 100, Kind = ContextKind.Message }, Score = 0.9, Reason = InclusionReason.Scored },
                new() { Item = new ContextItem { Content = "doc1", Tokens = 200, Kind = ContextKind.Document }, Score = 0.8, Reason = InclusionReason.Scored },
            },
            Excluded = [],
            TotalCandidates = 5,
            TotalTokensConsidered = 1000
        };

        var utilizations = report.QuotaUtilization(policy, budget);

        // Results sorted by kind: Document, Message
        await Assert.That(utilizations.Count).IsEqualTo(2);

        // Document: actual = 200/1000 * 100 = 20%, utilization = 20/40 = 0.5
        await Assert.That(utilizations[0].Kind).IsEqualTo(ContextKind.Document);
        await Assert.That(utilizations[0].Mode).IsEqualTo(QuotaConstraintMode.Percentage);
        await Assert.That(utilizations[0].Require).IsEqualTo(10.0);
        await Assert.That(utilizations[0].Cap).IsEqualTo(40.0);
        await Assert.That(utilizations[0].Actual).IsEqualTo(20.0);
        await Assert.That(utilizations[0].Utilization).IsEqualTo(0.5);

        // Message: actual = 300/1000 * 100 = 30%, utilization = 30/60 = 0.5
        await Assert.That(utilizations[1].Kind).IsEqualTo(ContextKind.Message);
        await Assert.That(utilizations[1].Mode).IsEqualTo(QuotaConstraintMode.Percentage);
        await Assert.That(utilizations[1].Require).IsEqualTo(20.0);
        await Assert.That(utilizations[1].Cap).IsEqualTo(60.0);
        await Assert.That(utilizations[1].Actual).IsEqualTo(30.0);
        await Assert.That(utilizations[1].Utilization).IsEqualTo(0.5);
    }

    [Test]
    public async Task QuotaUtilization_CountMode_ReturnsCorrectPerKindUtilization()
    {
        var budget = new ContextBudget(maxTokens: 2000, targetTokens: 1000);

        var entries = new List<CountQuotaEntry>
        {
            new(ContextKind.Message, requireCount: 2, capCount: 5),
            new(ContextKind.Document, requireCount: 1, capCount: 3),
        };
        IQuotaPolicy policy = new CountQuotaSlice(new GreedySlice(), entries);

        // Report: 3 Messages, 1 Document
        var report = new SelectionReport
        {
            Events = [],
            Included = new List<IncludedItem>
            {
                new() { Item = new ContextItem { Content = "msg1", Tokens = 100, Kind = ContextKind.Message }, Score = 1.0, Reason = InclusionReason.Scored },
                new() { Item = new ContextItem { Content = "msg2", Tokens = 100, Kind = ContextKind.Message }, Score = 0.9, Reason = InclusionReason.Scored },
                new() { Item = new ContextItem { Content = "msg3", Tokens = 100, Kind = ContextKind.Message }, Score = 0.8, Reason = InclusionReason.Scored },
                new() { Item = new ContextItem { Content = "doc1", Tokens = 100, Kind = ContextKind.Document }, Score = 0.7, Reason = InclusionReason.Scored },
            },
            Excluded = [],
            TotalCandidates = 6,
            TotalTokensConsidered = 1200
        };

        var utilizations = report.QuotaUtilization(policy, budget);

        // Sorted by kind: Document, Message
        await Assert.That(utilizations.Count).IsEqualTo(2);

        // Document: actual = 1, utilization = 1/3 ≈ 0.333
        await Assert.That(utilizations[0].Kind).IsEqualTo(ContextKind.Document);
        await Assert.That(utilizations[0].Mode).IsEqualTo(QuotaConstraintMode.Count);
        await Assert.That(utilizations[0].Require).IsEqualTo(1.0);
        await Assert.That(utilizations[0].Cap).IsEqualTo(3.0);
        await Assert.That(utilizations[0].Actual).IsEqualTo(1.0);
        await Assert.That(Math.Abs(utilizations[0].Utilization - (1.0 / 3.0))).IsLessThan(0.001);

        // Message: actual = 3, utilization = 3/5 = 0.6
        await Assert.That(utilizations[1].Kind).IsEqualTo(ContextKind.Message);
        await Assert.That(utilizations[1].Mode).IsEqualTo(QuotaConstraintMode.Count);
        await Assert.That(utilizations[1].Require).IsEqualTo(2.0);
        await Assert.That(utilizations[1].Cap).IsEqualTo(5.0);
        await Assert.That(utilizations[1].Actual).IsEqualTo(3.0);
        await Assert.That(utilizations[1].Utilization).IsEqualTo(0.6);
    }

    [Test]
    public async Task QuotaUtilization_EmptyReport_ReturnsZeroUtilization()
    {
        var budget = new ContextBudget(maxTokens: 2000, targetTokens: 1000);

        var quotas = new QuotaBuilder()
            .Require(ContextKind.Message, 20)
            .Cap(ContextKind.Message, 60)
            .Build();
        IQuotaPolicy policy = new QuotaSlice(new GreedySlice(), quotas);

        var report = new SelectionReport
        {
            Events = [],
            Included = [],
            Excluded = [],
            TotalCandidates = 0,
            TotalTokensConsidered = 0
        };

        var utilizations = report.QuotaUtilization(policy, budget);

        await Assert.That(utilizations.Count).IsEqualTo(1);
        await Assert.That(utilizations[0].Kind).IsEqualTo(ContextKind.Message);
        await Assert.That(utilizations[0].Actual).IsEqualTo(0.0);
        await Assert.That(utilizations[0].Utilization).IsEqualTo(0.0);
    }
}
