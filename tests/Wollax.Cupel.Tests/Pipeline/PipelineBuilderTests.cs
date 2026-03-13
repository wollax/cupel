using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;

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

    // Test helpers

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
