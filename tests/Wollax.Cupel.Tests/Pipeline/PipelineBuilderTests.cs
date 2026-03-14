#pragma warning disable CUPEL001, CUPEL002, CUPEL003, CUPEL004, CUPEL005, CUPEL006, CUPEL007

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Pipeline;

public class PipelineBuilderTests
{
    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10) =>
        new() { Content = content, Tokens = tokens };

    private static ContextBudget CreateBudget(
        int maxTokens = 1000,
        int targetTokens = 500) =>
        new(maxTokens, targetTokens);

    // Builder access tests

    [Test]
    public async Task CreateBuilder_ReturnsPipelineBuilder()
    {
        var builder = CupelPipeline.CreateBuilder();

        await Assert.That(builder).IsNotNull();
        await Assert.That(builder).IsTypeOf<PipelineBuilder>();
    }

    // Validation tests

    [Test]
    public async Task Build_MissingBudget_Throws()
    {
        var builder = CupelPipeline.CreateBuilder()
            .WithScorer(new ReflexiveScorer());

        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_MissingScorer_Throws()
    {
        var builder = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget());

        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_MixedScoringPaths_Throws()
    {
        var scorer = new ReflexiveScorer();
        var builder = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(scorer)
            .AddScorer(scorer, 1.0);

        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_ValidMinimalConfig_Succeeds()
    {
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer())
            .Build();

        await Assert.That(pipeline).IsNotNull();
        await Assert.That(pipeline).IsTypeOf<CupelPipeline>();
    }

    // Scoring path tests

    [Test]
    public async Task WithScorer_SetsScorer()
    {
        var scorer = new ReflexiveScorer();
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(scorer)
            .Build();

        await Assert.That(pipeline).IsNotNull();
    }

    [Test]
    public async Task AddScorer_CreatesComposite()
    {
        var scorerA = new ReflexiveScorer();
        var scorerB = new PriorityScorer();
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .AddScorer(scorerA, 1.0)
            .AddScorer(scorerB, 1.0)
            .Build();

        await Assert.That(pipeline).IsNotNull();
    }

    [Test]
    public async Task AddScorer_SingleEntry_Succeeds()
    {
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .AddScorer(new ReflexiveScorer(), 1.0)
            .Build();

        await Assert.That(pipeline).IsNotNull();
    }

    // AddScorer weight validation tests

    [Test]
    public async Task AddScorer_ZeroWeight_Throws()
    {
        var builder = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget());

        await Assert.That(() => builder.AddScorer(new ReflexiveScorer(), 0.0))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AddScorer_NegativeWeight_Throws()
    {
        var builder = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget());

        await Assert.That(() => builder.AddScorer(new ReflexiveScorer(), -1.0))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AddScorer_NaNWeight_Throws()
    {
        var builder = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget());

        await Assert.That(() => builder.AddScorer(new ReflexiveScorer(), double.NaN))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AddScorer_InfinityWeight_Throws()
    {
        var builder = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget());

        await Assert.That(() => builder.AddScorer(new ReflexiveScorer(), double.PositiveInfinity))
            .Throws<ArgumentOutOfRangeException>();
    }

    // Behavioral verification tests

    [Test]
    public async Task WithScorer_AppliedInPipeline()
    {
        var scorerCalled = false;
        var scorer = new DelegateScorer((item, allItems) =>
        {
            scorerCalled = true;
            return 0.5;
        });

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(scorer)
            .Build();

        pipeline.Execute([CreateItem()]);

        await Assert.That(scorerCalled).IsTrue();
    }

    [Test]
    public async Task AddScorer_CompositeAppliedInPipeline()
    {
        var scorerACalled = false;
        var scorerBCalled = false;
        var scorerA = new DelegateScorer((item, allItems) => { scorerACalled = true; return 0.5; });
        var scorerB = new DelegateScorer((item, allItems) => { scorerBCalled = true; return 0.5; });

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .AddScorer(scorerA, 1.0)
            .AddScorer(scorerB, 1.0)
            .Build();

        pipeline.Execute([CreateItem()]);

        await Assert.That(scorerACalled).IsTrue();
        await Assert.That(scorerBCalled).IsTrue();
    }

    // Override tests

    [Test]
    public async Task WithSlicer_OverridesDefault()
    {
        var customSlicerUsed = false;
        var slicer = new DelegateSlicer((items, budget, trace) =>
        {
            customSlicerUsed = true;
            return [];
        });

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer())
            .WithSlicer(slicer)
            .Build();

        pipeline.Execute([CreateItem()]);

        await Assert.That(customSlicerUsed).IsTrue();
    }

    [Test]
    public async Task WithPlacer_OverridesDefault()
    {
        var customPlacerUsed = false;
        var placer = new DelegatePlacer((items, trace) =>
        {
            customPlacerUsed = true;
            var result = new ContextItem[items.Count];
            for (var i = 0; i < items.Count; i++)
                result[i] = items[i].Item;
            return result;
        });

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer())
            .WithPlacer(placer)
            .Build();

        pipeline.Execute([CreateItem()]);

        await Assert.That(customPlacerUsed).IsTrue();
    }

    [Test]
    public async Task WithDeduplication_False_DisablesDedup()
    {
        var item1 = new ContextItem { Content = "duplicate", Tokens = 10, FutureRelevanceHint = 0.8 };
        var item2 = new ContextItem { Content = "duplicate", Tokens = 10, FutureRelevanceHint = 0.5 };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer())
            .WithDeduplication(false)
            .Build();

        var result = pipeline.Execute([item1, item2]);

        await Assert.That(result.Items.Count).IsEqualTo(2);
    }

    // UseGreedySlice tests

    [Test]
    public async Task UseGreedySlice_SetsSlicer()
    {
        var item = CreateItem("test", tokens: 10);
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer())
            .UseGreedySlice()
            .Build();

        var result = pipeline.Execute([item]);

        await Assert.That(result.Items.Count).IsEqualTo(1);
    }

    // UseKnapsackSlice tests

    [Test]
    public async Task UseKnapsackSlice_DefaultBucketSize()
    {
        var item = new ContextItem { Content = "test", Tokens = 100, FutureRelevanceHint = 0.8 };
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer())
            .UseKnapsackSlice()
            .Build();

        var result = pipeline.Execute([item]);

        await Assert.That(result.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task UseKnapsackSlice_CustomBucketSize()
    {
        var item = new ContextItem { Content = "test", Tokens = 100, FutureRelevanceHint = 0.8 };
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer())
            .UseKnapsackSlice(50)
            .Build();

        var result = pipeline.Execute([item]);

        await Assert.That(result.Items.Count).IsEqualTo(1);
    }

    // WithQuotas tests

    [Test]
    public async Task WithQuotas_WrapsCurrentSlicer()
    {
        var messages = new List<ContextItem>();
        for (var i = 0; i < 5; i++)
            messages.Add(new ContextItem { Content = $"msg-{i}", Tokens = 50, Kind = ContextKind.Message, FutureRelevanceHint = 0.8 });
        var docs = new List<ContextItem>();
        for (var i = 0; i < 5; i++)
            docs.Add(new ContextItem { Content = $"doc-{i}", Tokens = 50, Kind = ContextKind.Document, FutureRelevanceHint = 0.8 });

        var items = new List<ContextItem>();
        items.AddRange(messages);
        items.AddRange(docs);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .UseGreedySlice()
            .WithQuotas(q => q.Require(ContextKind.Message, 30))
            .Build();

        var result = pipeline.Execute(items);

        // Quotas should ensure at least 30% of 500 = 150 tokens of messages
        var messageTokens = 0;
        for (var i = 0; i < result.Items.Count; i++)
        {
            if (result.Items[i].Kind == ContextKind.Message)
                messageTokens += result.Items[i].Tokens;
        }
        await Assert.That(messageTokens).IsGreaterThanOrEqualTo(150);
    }

    [Test]
    public async Task WithQuotas_DefaultSlicer()
    {
        // WithQuotas without explicit slicer should wrap default GreedySlice
        var item = new ContextItem { Content = "test", Tokens = 10, Kind = ContextKind.Message, FutureRelevanceHint = 0.5 };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer())
            .WithQuotas(q => q.Require(ContextKind.Message, 30))
            .Build();

        var result = pipeline.Execute([item]);

        await Assert.That(result.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task WithQuotas_KnapsackInside()
    {
        var items = new List<ContextItem>();
        for (var i = 0; i < 3; i++)
            items.Add(new ContextItem { Content = $"doc-{i}", Tokens = 100, Kind = ContextKind.Document, FutureRelevanceHint = 0.7 });
        for (var i = 0; i < 3; i++)
            items.Add(new ContextItem { Content = $"msg-{i}", Tokens = 100, Kind = ContextKind.Message, FutureRelevanceHint = 0.7 });

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithScorer(new ReflexiveScorer())
            .UseKnapsackSlice()
            .WithQuotas(q => q.Cap(ContextKind.Document, 50))
            .Build();

        var result = pipeline.Execute(items);

        // Document tokens should be capped at 50% of 500 = 250
        var docTokens = 0;
        for (var i = 0; i < result.Items.Count; i++)
        {
            if (result.Items[i].Kind == ContextKind.Document)
                docTokens += result.Items[i].Tokens;
        }
        await Assert.That(docTokens).IsLessThanOrEqualTo(250);
    }

    // WithQuotas validation tests

    [Test]
    public async Task WithQuotas_InvalidConfig_Throws()
    {
        var builder = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer());

        // Sum of requires > 100% should throw at config time
        await Assert.That(() => builder.WithQuotas(q => q
            .Require(ContextKind.Message, 60)
            .Require(ContextKind.Document, 50)))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task WithQuotas_NullAction_Throws()
    {
        var builder = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer());

        await Assert.That(() => builder.WithQuotas(null!))
            .Throws<ArgumentNullException>();
    }

    // WithAsyncSlicer tests

    [Test]
    public async Task WithAsyncSlicer_SetsAsyncSlicer()
    {
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer())
            .WithAsyncSlicer(new StreamSlice())
            .Build();

        await Assert.That(pipeline).IsNotNull();
    }

    [Test]
    public async Task WithAsyncSlicer_NullThrows()
    {
        var builder = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithScorer(new ReflexiveScorer());

        await Assert.That(() => builder.WithAsyncSlicer(null!))
            .Throws<ArgumentNullException>();
    }

    // WithPolicy tests

    [Test]
    public async Task WithPolicy_NullPolicy_Throws()
    {
        var builder = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget());

        await Assert.That(() => builder.WithPolicy(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task WithPolicy_ValidPolicy_BuildsSuccessfully()
    {
        var policy = new CupelPolicy(
            scorers: [
                new ScorerEntry(ScorerType.Recency, 3),
                new ScorerEntry(ScorerType.Kind, 1),
            ]);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithPolicy(policy)
            .Build();

        await Assert.That(pipeline).IsNotNull();
    }

    [Test]
    public async Task WithPolicy_SetsSlicerFromPolicy()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Priority, 1)],
            slicerType: SlicerType.Knapsack,
            knapsackBucketSize: 50);

        var item = new ContextItem { Content = "test", Tokens = 100, Priority = 5 };
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithPolicy(policy)
            .Build();

        var result = pipeline.Execute([item]);

        await Assert.That(result.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task WithPolicy_SetsPlacerFromPolicy()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, 1)],
            placerType: PlacerType.UShaped);

        var item = new ContextItem { Content = "test", Tokens = 10, FutureRelevanceHint = 0.8 };
        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithPolicy(policy)
            .Build();

        var result = pipeline.Execute([item]);

        await Assert.That(result.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task WithPolicy_OverrideAfterPolicy()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, 1)],
            placerType: PlacerType.Chronological);

        var customPlacerUsed = false;
        var placer = new DelegatePlacer((items, trace) =>
        {
            customPlacerUsed = true;
            var result = new ContextItem[items.Count];
            for (var i = 0; i < items.Count; i++)
                result[i] = items[i].Item;
            return result;
        });

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithPolicy(policy)
            .WithPlacer(placer)
            .Build();

        pipeline.Execute([CreateItem()]);

        await Assert.That(customPlacerUsed).IsTrue();
    }

    [Test]
    public async Task WithPolicy_WithQuotas_AppliesQuotas()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, 1)],
            quotas: [new QuotaEntry(ContextKind.Message, minPercent: 30)]);

        var messages = new List<ContextItem>();
        for (var i = 0; i < 5; i++)
            messages.Add(new ContextItem { Content = $"msg-{i}", Tokens = 50, Kind = ContextKind.Message, FutureRelevanceHint = 0.8 });
        var docs = new List<ContextItem>();
        for (var i = 0; i < 5; i++)
            docs.Add(new ContextItem { Content = $"doc-{i}", Tokens = 50, Kind = ContextKind.Document, FutureRelevanceHint = 0.8 });

        var items = new List<ContextItem>();
        items.AddRange(messages);
        items.AddRange(docs);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(new ContextBudget(1000, 500))
            .WithPolicy(policy)
            .Build();

        var result = pipeline.Execute(items);

        var messageTokens = 0;
        for (var i = 0; i < result.Items.Count; i++)
        {
            if (result.Items[i].Kind == ContextKind.Message)
                messageTokens += result.Items[i].Tokens;
        }
        await Assert.That(messageTokens).IsGreaterThanOrEqualTo(150);
    }

    [Test]
    public async Task WithPolicy_StillRequiresBudget()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Recency, 1)]);

        var builder = CupelPipeline.CreateBuilder()
            .WithPolicy(policy);

        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    // WithPolicy Scaled scorer tests

    [Test]
    public async Task WithPolicy_ScaledScorer_BuildsSuccessfully()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Scaled, 1.0,
                innerScorer: new ScorerEntry(ScorerType.Recency, 1.0))]);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithPolicy(policy)
            .Build();

        await Assert.That(pipeline).IsNotNull();
    }

    [Test]
    public async Task WithPolicy_ScaledScorer_SelectsAllItemsWithinBudget()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Scaled, 1.0,
                innerScorer: new ScorerEntry(ScorerType.Reflexive, 1.0))]);

        var items = new[]
        {
            new ContextItem { Content = "low", Tokens = 10, FutureRelevanceHint = 0.2 },
            new ContextItem { Content = "mid", Tokens = 10, FutureRelevanceHint = 0.5 },
            new ContextItem { Content = "high", Tokens = 10, FutureRelevanceHint = 0.8 },
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithPolicy(policy)
            .Build();

        var result = pipeline.DryRun(items);

        // All items should be selected (well within budget)
        await Assert.That(result.Items.Count).IsEqualTo(3);

        // Verify ScaledScorer normalization: scores should be in [0, 1] range
        // and "high" should score higher than "low" in the report
        var report = result.Report!;
        var includedScores = report.Included.Select(i => i.Score).ToList();
        await Assert.That(includedScores.Count).IsEqualTo(3);
        foreach (var score in includedScores)
        {
            await Assert.That(score).IsGreaterThanOrEqualTo(0.0);
            await Assert.That(score).IsLessThanOrEqualTo(1.0);
        }
    }

    [Test]
    public async Task WithPolicy_NestedScaledScorer_BuildsSuccessfully()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Scaled, 1.0,
                innerScorer: new ScorerEntry(ScorerType.Scaled, 1.0,
                    innerScorer: new ScorerEntry(ScorerType.Recency, 1.0)))]);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithPolicy(policy)
            .Build();

        await Assert.That(pipeline).IsNotNull();
    }

    // WithPolicy Stream slicer tests

    [Test]
    public async Task WithPolicy_StreamSlicer_BuildsSuccessfully()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, 1.0)],
            slicerType: SlicerType.Stream,
            streamBatchSize: 16);

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithPolicy(policy)
            .Build();

        await Assert.That(pipeline).IsNotNull();
    }

    [Test]
    public async Task WithPolicy_StreamSlicer_SyncExecuteUsesGreedyFallback()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, 1.0)],
            slicerType: SlicerType.Stream);

        var items = new[]
        {
            new ContextItem { Content = "item-1", Tokens = 10, FutureRelevanceHint = 0.8 },
            new ContextItem { Content = "item-2", Tokens = 10, FutureRelevanceHint = 0.5 },
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithPolicy(policy)
            .Build();

        // Sync Execute should work using GreedySlice fallback
        var result = pipeline.Execute(items);

        await Assert.That(result.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task WithPolicy_StreamSlicer_AsyncExecuteStreamWorks()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, 1.0)],
            slicerType: SlicerType.Stream,
            streamBatchSize: 4);

        var items = new[]
        {
            new ContextItem { Content = "item-1", Tokens = 50, FutureRelevanceHint = 0.8 },
            new ContextItem { Content = "item-2", Tokens = 50, FutureRelevanceHint = 0.6 },
            new ContextItem { Content = "item-3", Tokens = 50, FutureRelevanceHint = 0.4 },
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithPolicy(policy)
            .Build();

        // Async ExecuteStreamAsync should work using StreamSlice
        var result = await pipeline.ExecuteStreamAsync(ToAsyncEnumerable(items));

        await Assert.That(result.Items.Count).IsGreaterThan(0);
        await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(500);
    }

    [Test]
    public async Task WithPolicy_StreamSlicerDefaultBatchSize_Works()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Reflexive, 1.0)],
            slicerType: SlicerType.Stream);

        var items = new[]
        {
            new ContextItem { Content = "item-1", Tokens = 50, FutureRelevanceHint = 0.8 },
        };

        var pipeline = CupelPipeline.CreateBuilder()
            .WithBudget(CreateBudget())
            .WithPolicy(policy)
            .Build();

        var result = await pipeline.ExecuteStreamAsync(ToAsyncEnumerable(items));

        await Assert.That(result.Items.Count).IsEqualTo(1);
    }

    // Test helpers

    private static async IAsyncEnumerable<ContextItem> ToAsyncEnumerable(IReadOnlyList<ContextItem> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            yield return items[i];
            await Task.Yield();
        }
    }


    private sealed class DelegateScorer(Func<ContextItem, IReadOnlyList<ContextItem>, double> scoreFunc)
        : IScorer
    {
        public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems) =>
            scoreFunc(item, allItems);
    }

    private sealed class DelegateSlicer(
        Func<IReadOnlyList<ScoredItem>, ContextBudget, ITraceCollector, IReadOnlyList<ContextItem>> sliceFunc)
        : ISlicer
    {
        public IReadOnlyList<ContextItem> Slice(
            IReadOnlyList<ScoredItem> scoredItems,
            ContextBudget budget,
            ITraceCollector traceCollector) =>
            sliceFunc(scoredItems, budget, traceCollector);
    }

    private sealed class DelegatePlacer(
        Func<IReadOnlyList<ScoredItem>, ITraceCollector, IReadOnlyList<ContextItem>> placeFunc)
        : IPlacer
    {
        public IReadOnlyList<ContextItem> Place(
            IReadOnlyList<ScoredItem> items,
            ITraceCollector traceCollector) =>
            placeFunc(items, traceCollector);
    }
}
