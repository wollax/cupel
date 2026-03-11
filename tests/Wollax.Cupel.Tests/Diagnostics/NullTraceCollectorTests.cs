using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Diagnostics;

public class NullTraceCollectorTests
{
    [Test]
    public async Task IsEnabled_ReturnsFalse()
    {
        await Assert.That(NullTraceCollector.Instance.IsEnabled).IsFalse();
    }

    [Test]
    public async Task Instance_IsSingleton()
    {
        var a = NullTraceCollector.Instance;
        var b = NullTraceCollector.Instance;

        await Assert.That(ReferenceEquals(a, b)).IsTrue();
    }

    [Test]
    public async Task RecordStageEvent_DoesNotThrow()
    {
        var traceEvent = new TraceEvent
        {
            Stage = PipelineStage.Score,
            Duration = TimeSpan.FromMilliseconds(10),
            ItemCount = 5
        };

        NullTraceCollector.Instance.RecordStageEvent(traceEvent);

        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task RecordItemEvent_DoesNotThrow()
    {
        var traceEvent = new TraceEvent
        {
            Stage = PipelineStage.Score,
            Duration = TimeSpan.FromMilliseconds(10),
            ItemCount = 1
        };

        NullTraceCollector.Instance.RecordItemEvent(traceEvent);

        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task Constructor_IsPrivate()
    {
        var constructors = typeof(NullTraceCollector)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        await Assert.That(constructors.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ImplementsITraceCollector()
    {
        await Assert.That(NullTraceCollector.Instance is ITraceCollector).IsTrue();
    }
}
