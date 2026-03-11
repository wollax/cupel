using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Models;

public class ContextResultTests
{
    private static ContextItem CreateItem(string content = "test", int tokens = 10) =>
        new() { Content = content, Tokens = tokens };

    #region ContextResult Construction

    [Test]
    public async Task Construction_WithItems_Compiles()
    {
        var items = new List<ContextItem> { CreateItem() };

        var result = new ContextResult { Items = items };

        await Assert.That(result.Items).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Construction_ReportDefaultsToNull()
    {
        var result = new ContextResult { Items = new List<ContextItem>() };

        await Assert.That(result.Report).IsNull();
    }

    [Test]
    public async Task Construction_ReportCanBeSet()
    {
        var report = new SelectionReport
        {
            Events = new List<TraceEvent>
            {
                new() { Stage = PipelineStage.Score, Duration = TimeSpan.FromMilliseconds(5), ItemCount = 3 }
            }
        };

        var result = new ContextResult
        {
            Items = new List<ContextItem> { CreateItem() },
            Report = report,
        };

        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report!.Events).Count().IsEqualTo(1);
    }

    #endregion

    #region TotalTokens Computation

    [Test]
    public async Task TotalTokens_EmptyItems_ReturnsZero()
    {
        var result = new ContextResult { Items = new List<ContextItem>() };

        await Assert.That(result.TotalTokens).IsEqualTo(0);
    }

    [Test]
    public async Task TotalTokens_SingleItem_ReturnsThatItemsTokens()
    {
        var result = new ContextResult { Items = new List<ContextItem> { CreateItem(tokens: 42) } };

        await Assert.That(result.TotalTokens).IsEqualTo(42);
    }

    [Test]
    public async Task TotalTokens_MultipleItems_ReturnsCorrectSum()
    {
        var items = new List<ContextItem>
        {
            CreateItem(tokens: 10),
            CreateItem(tokens: 20),
            CreateItem(tokens: 30),
        };

        var result = new ContextResult { Items = items };

        await Assert.That(result.TotalTokens).IsEqualTo(60);
    }

    #endregion

    #region Immutability

    [Test]
    public async Task WithExpression_CreatesNewInstanceWithUpdatedReport()
    {
        var original = new ContextResult { Items = new List<ContextItem> { CreateItem() } };
        var report = new SelectionReport
        {
            Events = new List<TraceEvent>
            {
                new() { Stage = PipelineStage.Slice, Duration = TimeSpan.FromMilliseconds(2), ItemCount = 1 }
            }
        };

        var updated = original with { Report = report };

        await Assert.That(original.Report).IsNull();
        await Assert.That(updated.Report).IsNotNull();
        await Assert.That(updated.Items).Count().IsEqualTo(1);
    }

    #endregion

    #region SelectionReport

    [Test]
    public async Task SelectionReport_Construction_WithEvents()
    {
        var events = new List<TraceEvent>
        {
            new() { Stage = PipelineStage.Score, Duration = TimeSpan.FromMilliseconds(10), ItemCount = 5 },
            new() { Stage = PipelineStage.Slice, Duration = TimeSpan.FromMilliseconds(3), ItemCount = 3 },
        };

        var report = new SelectionReport { Events = events };

        await Assert.That(report.Events).Count().IsEqualTo(2);
        await Assert.That(report.Events[0].Stage).IsEqualTo(PipelineStage.Score);
        await Assert.That(report.Events[1].Stage).IsEqualTo(PipelineStage.Slice);
    }

    #endregion
}
