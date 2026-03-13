using BenchmarkDotNet.Attributes;
using Wollax.Cupel;
using Wollax.Cupel.Scoring;

/// <summary>
/// Verifies zero heap allocation for all six scorers under MemoryDiagnoser.
/// Each benchmark returns the accumulated score to prevent dead code elimination.
/// </summary>
[MemoryDiagnoser]
public class ScorerBenchmark
{
    private static readonly ContextKind[] Kinds =
    [
        ContextKind.Message,
        ContextKind.Document,
        ContextKind.ToolOutput,
        ContextKind.Memory,
        ContextKind.SystemPrompt,
    ];

    private ContextItem[] _items = null!;

    private RecencyScorer _recencyScorer = null!;
    private PriorityScorer _priorityScorer = null!;
    private ReflexiveScorer _reflexiveScorer = null!;
    private KindScorer _kindScorer = null!;
    private TagScorer _tagScorer = null!;
    private FrequencyScorer _frequencyScorer = null!;
    private CompositeScorer _compositeScorer = null!;
    private ScaledScorer _scaledScorer = null!;
    private ScaledScorer _scaledCompositeScorer = null!;

    [Params(100, 500)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var now = DateTimeOffset.UtcNow;

        _items = Enumerable.Range(0, ItemCount)
            .Select(i => new ContextItem
            {
                Content = $"Item {i}",
                Tokens = 10 + i,
                Timestamp = now.AddMinutes(-i),
                Priority = i,
                Kind = Kinds[i % Kinds.Length],
                Tags = [$"tag-{i % 5}"],
                FutureRelevanceHint = i / (double)ItemCount,
            })
            .ToArray();

        _recencyScorer = new RecencyScorer();
        _priorityScorer = new PriorityScorer();
        _reflexiveScorer = new ReflexiveScorer();
        _kindScorer = new KindScorer();
        _tagScorer = new TagScorer(new Dictionary<string, double>
        {
            ["tag-0"] = 1.0,
            ["tag-1"] = 0.8,
            ["tag-2"] = 0.6,
            ["tag-3"] = 0.4,
            ["tag-4"] = 0.2,
        });
        _frequencyScorer = new FrequencyScorer();
        _compositeScorer = new CompositeScorer([
            (new RecencyScorer(), 2.0),
            (new PriorityScorer(), 1.0),
            (new ReflexiveScorer(), 1.0)
        ]);
        _scaledScorer = new ScaledScorer(new RecencyScorer());
        _scaledCompositeScorer = new ScaledScorer(_compositeScorer);
    }

    [Benchmark]
    public double Recency()
    {
        var sum = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            sum += _recencyScorer.Score(_items[i], _items);
        }
        return sum;
    }

    [Benchmark]
    public double Priority()
    {
        var sum = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            sum += _priorityScorer.Score(_items[i], _items);
        }
        return sum;
    }

    [Benchmark]
    public double Reflexive()
    {
        var sum = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            sum += _reflexiveScorer.Score(_items[i], _items);
        }
        return sum;
    }

    [Benchmark]
    public double Kind()
    {
        var sum = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            sum += _kindScorer.Score(_items[i], _items);
        }
        return sum;
    }

    [Benchmark]
    public double Tag()
    {
        var sum = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            sum += _tagScorer.Score(_items[i], _items);
        }
        return sum;
    }

    [Benchmark]
    public double Frequency()
    {
        var sum = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            sum += _frequencyScorer.Score(_items[i], _items);
        }
        return sum;
    }

    [Benchmark]
    public double CompositeScorer_Score()
    {
        var sum = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            sum += _compositeScorer.Score(_items[i], _items);
        }
        return sum;
    }

    [Benchmark]
    public double ScaledScorer_Score()
    {
        var sum = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            sum += _scaledScorer.Score(_items[i], _items);
        }
        return sum;
    }

    [Benchmark]
    public double ScaledCompositeScorer_Score()
    {
        var sum = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            sum += _scaledCompositeScorer.Score(_items[i], _items);
        }
        return sum;
    }
}
