using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Diagnostics;

public class DiagnosticTraceCollectorTests
{
    private static TraceEvent CreateStageEvent(
        PipelineStage stage = PipelineStage.Score,
        int itemCount = 5) =>
        new()
        {
            Stage = stage,
            Duration = TimeSpan.FromMilliseconds(42),
            ItemCount = itemCount
        };

    [Test]
    public async Task IsEnabled_ReturnsTrue()
    {
        var collector = new DiagnosticTraceCollector();

        await Assert.That(collector.IsEnabled).IsTrue();
    }

    [Test]
    public async Task DefaultConstruction_StageDetailLevel_NoCallback()
    {
        var collector = new DiagnosticTraceCollector();

        await Assert.That(collector.IsEnabled).IsTrue();
        await Assert.That(collector.Events).IsEmpty();
    }

    [Test]
    public async Task RecordStageEvent_AddsEventToList()
    {
        var collector = new DiagnosticTraceCollector();
        var traceEvent = CreateStageEvent();

        collector.RecordStageEvent(traceEvent);

        await Assert.That(collector.Events).Count().IsEqualTo(1);
        await Assert.That(collector.Events[0]).IsEqualTo(traceEvent);
    }

    [Test]
    public async Task RecordStageEvent_WithCallback_InvokesCallback()
    {
        TraceEvent? captured = null;
        var collector = new DiagnosticTraceCollector(
            callback: e => captured = e);
        var traceEvent = CreateStageEvent();

        collector.RecordStageEvent(traceEvent);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Value).IsEqualTo(traceEvent);
    }

    [Test]
    public async Task RecordItemEvent_AtStageDetailLevel_IsIgnored()
    {
        var collector = new DiagnosticTraceCollector(TraceDetailLevel.Stage);
        var traceEvent = CreateStageEvent();

        collector.RecordItemEvent(traceEvent);

        await Assert.That(collector.Events).IsEmpty();
    }

    [Test]
    public async Task RecordItemEvent_AtItemDetailLevel_IsAdded()
    {
        var collector = new DiagnosticTraceCollector(TraceDetailLevel.Item);
        var traceEvent = CreateStageEvent();

        collector.RecordItemEvent(traceEvent);

        await Assert.That(collector.Events).Count().IsEqualTo(1);
        await Assert.That(collector.Events[0]).IsEqualTo(traceEvent);
    }

    [Test]
    public async Task RecordItemEvent_AtItemDetailLevel_WithCallback_InvokesCallback()
    {
        TraceEvent? captured = null;
        var collector = new DiagnosticTraceCollector(
            TraceDetailLevel.Item,
            e => captured = e);
        var traceEvent = CreateStageEvent();

        collector.RecordItemEvent(traceEvent);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Value).IsEqualTo(traceEvent);
    }

    [Test]
    public async Task RecordItemEvent_AtStageDetailLevel_WithCallback_DoesNotInvokeCallback()
    {
        var callbackInvoked = false;
        var collector = new DiagnosticTraceCollector(
            TraceDetailLevel.Stage,
            _ => callbackInvoked = true);
        var traceEvent = CreateStageEvent();

        collector.RecordItemEvent(traceEvent);

        await Assert.That(callbackInvoked).IsFalse();
    }

    [Test]
    public async Task MultipleEvents_PreservesInsertionOrder()
    {
        var collector = new DiagnosticTraceCollector();
        var event1 = CreateStageEvent(PipelineStage.Score);
        var event2 = CreateStageEvent(PipelineStage.Slice);
        var event3 = CreateStageEvent(PipelineStage.Place);

        collector.RecordStageEvent(event1);
        collector.RecordStageEvent(event2);
        collector.RecordStageEvent(event3);

        await Assert.That(collector.Events).Count().IsEqualTo(3);
        await Assert.That(collector.Events[0].Stage).IsEqualTo(PipelineStage.Score);
        await Assert.That(collector.Events[1].Stage).IsEqualTo(PipelineStage.Slice);
        await Assert.That(collector.Events[2].Stage).IsEqualTo(PipelineStage.Place);
    }

    [Test]
    public async Task Events_IsInitiallyEmpty()
    {
        var collector = new DiagnosticTraceCollector();

        await Assert.That(collector.Events).IsEmpty();
    }

    [Test]
    public async Task RecordStageEvent_ThrowingCallback_DoesNotPropagate()
    {
        var collector = new DiagnosticTraceCollector(
            callback: _ => throw new InvalidOperationException("boom"));
        var traceEvent = CreateStageEvent();

        collector.RecordStageEvent(traceEvent);

        await Assert.That(collector.Events).Count().IsEqualTo(1);
    }

    [Test]
    public async Task RecordItemEvent_ThrowingCallback_DoesNotPropagate()
    {
        var collector = new DiagnosticTraceCollector(
            TraceDetailLevel.Item,
            _ => throw new InvalidOperationException("boom"));
        var traceEvent = CreateStageEvent();

        collector.RecordItemEvent(traceEvent);

        await Assert.That(collector.Events).Count().IsEqualTo(1);
    }
}
