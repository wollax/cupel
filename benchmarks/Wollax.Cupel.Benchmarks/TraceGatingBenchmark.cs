using BenchmarkDotNet.Attributes;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;

/// <summary>
/// Verifies that NullTraceCollector path produces zero allocations
/// from trace code when gated behind IsEnabled checks.
/// </summary>
[MemoryDiagnoser]
public class TraceGatingBenchmark
{
    private ContextItem[] _items = null!;

    [Params(100, 500)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _items = Enumerable.Range(0, ItemCount)
            .Select(i => new ContextItem
            {
                Content = $"Item {i}",
                Tokens = 10 + i,
            })
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    public int BaselineNoTracing()
    {
        ITraceCollector trace = NullTraceCollector.Instance;
        var sum = 0;

        for (var i = 0; i < _items.Length; i++)
        {
            sum += _items[i].Tokens;

            if (trace.IsEnabled)
            {
                trace.RecordItemEvent(new TraceEvent
                {
                    Stage = PipelineStage.Score,
                    Duration = TimeSpan.Zero,
                    ItemCount = 1,
                });
            }
        }

        if (trace.IsEnabled)
        {
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Score,
                Duration = TimeSpan.FromMilliseconds(1),
                ItemCount = _items.Length,
            });
        }

        return sum;
    }

    [Benchmark]
    public int WithDiagnosticTracing()
    {
        var trace = new DiagnosticTraceCollector(TraceDetailLevel.Item);
        var sum = 0;

        for (var i = 0; i < _items.Length; i++)
        {
            sum += _items[i].Tokens;

            if (trace.IsEnabled)
            {
                trace.RecordItemEvent(new TraceEvent
                {
                    Stage = PipelineStage.Score,
                    Duration = TimeSpan.Zero,
                    ItemCount = 1,
                });
            }
        }

        if (trace.IsEnabled)
        {
            trace.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Score,
                Duration = TimeSpan.FromMilliseconds(1),
                ItemCount = _items.Length,
            });
        }

        return sum;
    }
}
