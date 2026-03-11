using BenchmarkDotNet.Attributes;
using Wollax.Cupel;

[MemoryDiagnoser]
public class EmptyPipelineBenchmark
{
    private ContextItem[] _items = null!;

    [Params(100, 250, 500)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _items = Enumerable.Range(0, ItemCount)
            .Select(i => new ContextItem
            {
                Content = $"Item {i}",
                Tokens = 10
            })
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    public int BaselineIteration()
    {
        var sum = 0;
        foreach (var item in _items)
            sum += item.Tokens;
        return sum;
    }
}
