using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Diagnostics;

public class SelectionReportEqualityTests
{
    private static ContextItem MakeItem(string content = "hello", int tokens = 10) =>
        new() { Content = content, Tokens = tokens };

    private static IncludedItem MakeIncluded(
        string content = "inc",
        double score = 0.9,
        InclusionReason reason = InclusionReason.Scored) =>
        new() { Item = MakeItem(content), Score = score, Reason = reason };

    private static ExcludedItem MakeExcluded(
        string content = "exc",
        double score = 0.1,
        ExclusionReason reason = ExclusionReason.BudgetExceeded,
        ContextItem? deduplicatedAgainst = null) =>
        new() { Item = MakeItem(content), Score = score, Reason = reason, DeduplicatedAgainst = deduplicatedAgainst };

    private static TraceEvent MakeEvent(
        PipelineStage stage = PipelineStage.Score,
        int itemCount = 5) =>
        new() { Stage = stage, Duration = TimeSpan.FromMilliseconds(42), ItemCount = itemCount };

    private static CountRequirementShortfall MakeShortfall(
        int required = 5,
        int satisfied = 2) =>
        new(ContextKind.Message, required, satisfied);

    private static SelectionReport MakeReport(
        IReadOnlyList<TraceEvent>? events = null,
        IReadOnlyList<IncludedItem>? included = null,
        IReadOnlyList<ExcludedItem>? excluded = null,
        int totalCandidates = 10,
        int totalTokensConsidered = 5000,
        IReadOnlyList<CountRequirementShortfall>? shortfalls = null) =>
        new()
        {
            Events = events ?? [MakeEvent()],
            Included = included ?? [MakeIncluded()],
            Excluded = excluded ?? [MakeExcluded()],
            TotalCandidates = totalCandidates,
            TotalTokensConsidered = totalTokensConsidered,
            CountRequirementShortfalls = shortfalls ?? [],
        };

    // --- SelectionReport equality ---

    [Test]
    public async Task IdenticalReports_AreEqual()
    {
        var a = MakeReport();
        var b = MakeReport();

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
        await Assert.That(a != b).IsFalse();
    }

    [Test]
    public async Task DifferentTotalCandidates_AreNotEqual()
    {
        var a = MakeReport(totalCandidates: 10);
        var b = MakeReport(totalCandidates: 20);

        await Assert.That(a).IsNotEqualTo(b);
        await Assert.That(a != b).IsTrue();
    }

    [Test]
    public async Task DifferentIncluded_AreNotEqual()
    {
        var a = MakeReport(included: [MakeIncluded("a")]);
        var b = MakeReport(included: [MakeIncluded("b")]);

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task DifferentExcluded_AreNotEqual()
    {
        var a = MakeReport(excluded: [MakeExcluded("x")]);
        var b = MakeReport(excluded: [MakeExcluded("y")]);

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task DifferentEvents_AreNotEqual()
    {
        var a = MakeReport(events: [MakeEvent(itemCount: 1)]);
        var b = MakeReport(events: [MakeEvent(itemCount: 99)]);

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task DifferentCountRequirementShortfalls_AreNotEqual()
    {
        var a = MakeReport(shortfalls: [MakeShortfall(required: 5)]);
        var b = MakeReport(shortfalls: [MakeShortfall(required: 10)]);

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task EmptyReports_AreEqual()
    {
        var a = MakeReport(events: [], included: [], excluded: [], totalCandidates: 0, totalTokensConsidered: 0);
        var b = MakeReport(events: [], included: [], excluded: [], totalCandidates: 0, totalTokensConsidered: 0);

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task GetHashCode_EqualReports_ProduceSameHash()
    {
        var a = MakeReport();
        var b = MakeReport();

        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    // --- IncludedItem equality ---

    [Test]
    public async Task IncludedItem_SameValues_AreEqual()
    {
        var a = MakeIncluded("test", 0.85, InclusionReason.Scored);
        var b = MakeIncluded("test", 0.85, InclusionReason.Scored);

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task IncludedItem_DifferentScore_AreNotEqual()
    {
        var a = MakeIncluded(score: 0.5);
        var b = MakeIncluded(score: 0.9);

        await Assert.That(a).IsNotEqualTo(b);
    }

    // --- ExcludedItem equality ---

    [Test]
    public async Task ExcludedItem_SameValuesNullDeduplicated_AreEqual()
    {
        var a = MakeExcluded("test", 0.1, ExclusionReason.BudgetExceeded);
        var b = MakeExcluded("test", 0.1, ExclusionReason.BudgetExceeded);

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task ExcludedItem_DifferentDeduplicatedAgainst_AreNotEqual()
    {
        var winner1 = MakeItem("winner1");
        var winner2 = MakeItem("winner2");
        var a = MakeExcluded(deduplicatedAgainst: winner1);
        var b = MakeExcluded(deduplicatedAgainst: winner2);

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task ExcludedItem_NullVsNonNullDeduplicatedAgainst_AreNotEqual()
    {
        var a = MakeExcluded(deduplicatedAgainst: null);
        var b = MakeExcluded(deduplicatedAgainst: MakeItem("winner"));

        await Assert.That(a).IsNotEqualTo(b);
        await Assert.That(a != b).IsTrue();
    }
}
