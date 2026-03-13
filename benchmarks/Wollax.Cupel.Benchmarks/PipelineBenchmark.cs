using BenchmarkDotNet.Attributes;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;

[MemoryDiagnoser]
public class PipelineBenchmark
{
    private CupelPipeline _pipeline = null!;
    private ContextItem[] _items = null!;

    [Params(100, 250, 500)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var budget = new ContextBudget(
            maxTokens: ItemCount * 10,
            targetTokens: ItemCount * 5);

        _pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(budget)
            .AddScorer(new RecencyScorer(), 2.0)
            .AddScorer(new PriorityScorer(), 1.0)
            .WithPlacer(new UShapedPlacer())
            .Build();

        var baseTime = DateTimeOffset.UtcNow;
        _items = new ContextItem[ItemCount];
        for (var i = 0; i < ItemCount; i++)
        {
            _items[i] = new ContextItem
            {
                Content = $"Context item number {i} with some realistic content length",
                Tokens = 8 + (i % 5),
                Kind = i % 3 == 0 ? ContextKind.ToolOutput : ContextKind.Message,
                Priority = i % 7 == 0 ? i : null,
                Timestamp = baseTime.AddMinutes(-ItemCount + i),
                Pinned = i == 0
            };
        }
    }

    [Benchmark]
    public ContextResult FullPipeline()
    {
        return _pipeline.Execute(_items);
    }

    [Benchmark]
    public ContextResult FullPipelineWithTracing()
    {
        var trace = new DiagnosticTraceCollector();
        return _pipeline.Execute(_items, trace);
    }
}
