using BenchmarkDotNet.Attributes;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Slicing;

[MemoryDiagnoser]
public class SlicerBenchmark
{
    private ScoredItem[] _scoredItems = null!;
    private ContextBudget _budget = null!;

    private GreedySlice _greedy = null!;
    private KnapsackSlice _knapsack50 = null!;
    private KnapsackSlice _knapsack100 = null!;
    private KnapsackSlice _knapsack200 = null!;
    private QuotaSlice _quotaGreedy = null!;
    private QuotaSlice _quotaKnapsack100 = null!;

    [Params(100, 250, 500)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        var kinds = new[] { ContextKind.Message, ContextKind.Document, ContextKind.ToolOutput, ContextKind.Memory };
        var baseTime = DateTimeOffset.UtcNow;

        var items = new ContextItem[ItemCount];
        var totalTokens = 0;

        for (var i = 0; i < ItemCount; i++)
        {
            var tokens = rng.Next(10, 201);
            totalTokens += tokens;

            items[i] = new ContextItem
            {
                Content = $"Benchmark context item {i} with representative content payload",
                Tokens = tokens,
                Kind = kinds[rng.Next(kinds.Length)],
                Timestamp = baseTime.AddMinutes(-ItemCount + i)
            };
        }

        _scoredItems = new ScoredItem[ItemCount];
        for (var i = 0; i < ItemCount; i++)
        {
            var score = rng.NextDouble() * 0.9 + 0.1; // 0.1–1.0
            _scoredItems[i] = new ScoredItem(items[i], score);
        }

        var targetTokens = (int)(totalTokens * 0.4);
        _budget = new ContextBudget(
            maxTokens: totalTokens,
            targetTokens: targetTokens);

        _greedy = new GreedySlice();
        _knapsack50 = new KnapsackSlice(bucketSize: 50);
        _knapsack100 = new KnapsackSlice(bucketSize: 100);
        _knapsack200 = new KnapsackSlice(bucketSize: 200);

        var quotas = new QuotaBuilder()
            .Require(ContextKind.Message, 30)
            .Cap(ContextKind.Document, 40)
            .Build();

        _quotaGreedy = new QuotaSlice(_greedy, quotas);
        _quotaKnapsack100 = new QuotaSlice(_knapsack100, quotas);
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyList<ContextItem> Greedy()
    {
        return _greedy.Slice(_scoredItems, _budget, NullTraceCollector.Instance);
    }

    [Benchmark]
    public IReadOnlyList<ContextItem> Knapsack_Bucket50()
    {
        return _knapsack50.Slice(_scoredItems, _budget, NullTraceCollector.Instance);
    }

    [Benchmark]
    public IReadOnlyList<ContextItem> Knapsack_Bucket100()
    {
        return _knapsack100.Slice(_scoredItems, _budget, NullTraceCollector.Instance);
    }

    [Benchmark]
    public IReadOnlyList<ContextItem> Knapsack_Bucket200()
    {
        return _knapsack200.Slice(_scoredItems, _budget, NullTraceCollector.Instance);
    }

    [Benchmark]
    public IReadOnlyList<ContextItem> QuotaGreedy()
    {
        return _quotaGreedy.Slice(_scoredItems, _budget, NullTraceCollector.Instance);
    }

    [Benchmark]
    public IReadOnlyList<ContextItem> QuotaKnapsack100()
    {
        return _quotaKnapsack100.Slice(_scoredItems, _budget, NullTraceCollector.Instance);
    }
}

[MemoryDiagnoser]
public class StreamSliceBenchmark
{
    private ScoredItem[] _materializedItems = null!;
    private ContextBudget _budget = null!;
    private StreamSlice _streamSlice = null!;

    [Params(100, 250, 500)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        var kinds = new[] { ContextKind.Message, ContextKind.Document, ContextKind.ToolOutput, ContextKind.Memory };
        var baseTime = DateTimeOffset.UtcNow;

        _materializedItems = new ScoredItem[ItemCount];
        var totalTokens = 0;

        for (var i = 0; i < ItemCount; i++)
        {
            var tokens = rng.Next(10, 201);
            totalTokens += tokens;

            var item = new ContextItem
            {
                Content = $"Benchmark context item {i} with representative content payload",
                Tokens = tokens,
                Kind = kinds[rng.Next(kinds.Length)],
                Timestamp = baseTime.AddMinutes(-ItemCount + i)
            };

            var score = rng.NextDouble() * 0.9 + 0.1;
            _materializedItems[i] = new ScoredItem(item, score);
        }

        var targetTokens = (int)(totalTokens * 0.4);
        _budget = new ContextBudget(
            maxTokens: totalTokens,
            targetTokens: targetTokens);

        _streamSlice = new StreamSlice(batchSize: 32);
    }

    [Benchmark]
    public async Task<IReadOnlyList<ContextItem>> StreamSlice_Batch32()
    {
        return await _streamSlice.SliceAsync(
            ToAsyncEnumerable(_materializedItems),
            _budget,
            NullTraceCollector.Instance);
    }

    private static async IAsyncEnumerable<ScoredItem> ToAsyncEnumerable(ScoredItem[] items)
    {
        for (var i = 0; i < items.Length; i++)
        {
            yield return items[i];
        }

        await Task.CompletedTask;
    }
}
