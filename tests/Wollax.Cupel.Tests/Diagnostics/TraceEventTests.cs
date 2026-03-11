using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Diagnostics;

public class TraceEventTests
{
    [Test]
    public async Task PipelineStage_HasExpectedValues()
    {
        var values = Enum.GetValues<PipelineStage>();

        await Assert.That(values).Contains(PipelineStage.Classify);
        await Assert.That(values).Contains(PipelineStage.Score);
        await Assert.That(values).Contains(PipelineStage.Deduplicate);
        await Assert.That(values).Contains(PipelineStage.Slice);
        await Assert.That(values).Contains(PipelineStage.Place);
        await Assert.That(values.Length).IsEqualTo(5);
    }

    [Test]
    public async Task TraceDetailLevel_StageIsLessThanItem()
    {
        await Assert.That((int)TraceDetailLevel.Stage).IsLessThan((int)TraceDetailLevel.Item);
    }

    [Test]
    public async Task ExclusionReason_HasExpectedValues()
    {
        var values = Enum.GetValues<ExclusionReason>();

        await Assert.That(values).Contains(ExclusionReason.LowScore);
        await Assert.That(values).Contains(ExclusionReason.BudgetExceeded);
        await Assert.That(values).Contains(ExclusionReason.Duplicate);
        await Assert.That(values).Contains(ExclusionReason.QuotaExceeded);
        await Assert.That(values.Length).IsEqualTo(4);
    }

    [Test]
    public async Task TraceEvent_Construction_StoresAllProperties()
    {
        var duration = TimeSpan.FromMilliseconds(42);
        var traceEvent = new TraceEvent
        {
            Stage = PipelineStage.Score,
            Duration = duration,
            ItemCount = 10
        };

        await Assert.That(traceEvent.Stage).IsEqualTo(PipelineStage.Score);
        await Assert.That(traceEvent.Duration).IsEqualTo(duration);
        await Assert.That(traceEvent.ItemCount).IsEqualTo(10);
    }

    [Test]
    public async Task TraceEvent_ValueEquality_SameValues_AreEqual()
    {
        var a = new TraceEvent
        {
            Stage = PipelineStage.Slice,
            Duration = TimeSpan.FromMilliseconds(100),
            ItemCount = 5
        };
        var b = new TraceEvent
        {
            Stage = PipelineStage.Slice,
            Duration = TimeSpan.FromMilliseconds(100),
            ItemCount = 5
        };

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task TraceEvent_ValueInequality_DifferentValues_AreNotEqual()
    {
        var a = new TraceEvent
        {
            Stage = PipelineStage.Slice,
            Duration = TimeSpan.FromMilliseconds(100),
            ItemCount = 5
        };
        var b = new TraceEvent
        {
            Stage = PipelineStage.Place,
            Duration = TimeSpan.FromMilliseconds(100),
            ItemCount = 5
        };

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task TraceEvent_IsReadonlyRecordStruct()
    {
        await Assert.That(typeof(TraceEvent).IsValueType).IsTrue();
    }
}
