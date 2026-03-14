using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Diagnostics;

public class SelectionReportTests
{
    [Test]
    public async Task Construction_StoresAllProperties()
    {
        var events = new List<TraceEvent>();
        var included = new List<IncludedItem>();
        var excluded = new List<ExcludedItem>();

        var report = new SelectionReport
        {
            Events = events,
            Included = included,
            Excluded = excluded,
            TotalCandidates = 10,
            TotalTokensConsidered = 5000
        };

        await Assert.That(report.Events).IsEqualTo(events);
        await Assert.That(report.Included).IsEqualTo(included);
        await Assert.That(report.Excluded).IsEqualTo(excluded);
        await Assert.That(report.TotalCandidates).IsEqualTo(10);
        await Assert.That(report.TotalTokensConsidered).IsEqualTo(5000);
    }

    [Test]
    public async Task RetainsEventsProperty()
    {
        var traceEvent = new TraceEvent
        {
            Stage = PipelineStage.Score,
            Duration = TimeSpan.FromMilliseconds(42),
            ItemCount = 5
        };
        var events = new List<TraceEvent> { traceEvent };

        var report = new SelectionReport
        {
            Events = events,
            Included = [],
            Excluded = [],
            TotalCandidates = 0,
            TotalTokensConsidered = 0
        };

        await Assert.That(report.Events.Count).IsEqualTo(1);
        await Assert.That(report.Events[0]).IsEqualTo(traceEvent);
    }

    [Test]
    public async Task IsSealed()
    {
        await Assert.That(typeof(SelectionReport).IsSealed).IsTrue();
    }
}
