using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;

#pragma warning disable CUPEL001 // CupelPresets.Chat() is experimental
#pragma warning disable CUPEL003 // CupelPresets.Rag() is experimental

namespace Wollax.Cupel.Extensions.DependencyInjection.Tests;

public class CupelServiceCollectionExtensionsTests
{
    [Test]
    public async Task AddCupel_RegistersOptions_PolicyAccessible()
    {
        var services = new ServiceCollection();

        services.AddCupel(o => o.AddPolicy("chat", CupelPresets.Chat()));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CupelOptions>>().Value;
        var policy = options.GetPolicy("chat");

        await Assert.That(policy).IsNotNull();
        await Assert.That(policy.Name).IsEqualTo("Chat");
    }

    [Test]
    public async Task AddCupelPipeline_ResolvesKeyedPipeline()
    {
        var services = new ServiceCollection();
        var budget = new ContextBudget(maxTokens: 4000, targetTokens: 3000);

        services
            .AddCupel(o => o.AddPolicy("chat", CupelPresets.Chat()))
            .AddCupelPipeline("chat", budget);

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredKeyedService<CupelPipeline>("chat");

        await Assert.That(pipeline).IsNotNull();

        // Verify the pipeline is functional by executing against a minimal item set
        var items = new[]
        {
            new ContextItem { Content = "Hello", Tokens = 10, Kind = ContextKind.Message, Source = ContextSource.Chat },
        };
        var result = pipeline.Execute(items);

        await Assert.That(result.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddCupelPipeline_IsTransient_DifferentInstancesPerResolve()
    {
        var services = new ServiceCollection();
        var budget = new ContextBudget(maxTokens: 4000, targetTokens: 3000);

        services
            .AddCupel(o => o.AddPolicy("chat", CupelPresets.Chat()))
            .AddCupelPipeline("chat", budget);

        var provider = services.BuildServiceProvider();
        var pipeline1 = provider.GetRequiredKeyedService<CupelPipeline>("chat");
        var pipeline2 = provider.GetRequiredKeyedService<CupelPipeline>("chat");

        await Assert.That(ReferenceEquals(pipeline1, pipeline2)).IsFalse();
    }

    [Test]
    public async Task AddCupelPipeline_MissingPolicy_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var budget = new ContextBudget(maxTokens: 4000, targetTokens: 3000);

        services
            .AddCupel(o => { }) // No policies registered
            .AddCupelPipeline("missing", budget);

        var provider = services.BuildServiceProvider();

        await Assert.That(() => provider.GetRequiredKeyedService<CupelPipeline>("missing"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AddCupelPipeline_MultipleNamedPipelines_ResolveIndependently()
    {
        var services = new ServiceCollection();
        var chatBudget = new ContextBudget(maxTokens: 4000, targetTokens: 3000);
        var ragBudget = new ContextBudget(maxTokens: 8000, targetTokens: 6000);

        services
            .AddCupel(o =>
            {
                o.AddPolicy("chat", CupelPresets.Chat());
                o.AddPolicy("rag", CupelPresets.Rag());
            })
            .AddCupelPipeline("chat", chatBudget)
            .AddCupelPipeline("rag", ragBudget);

        var provider = services.BuildServiceProvider();
        var chatPipeline = provider.GetRequiredKeyedService<CupelPipeline>("chat");
        var ragPipeline = provider.GetRequiredKeyedService<CupelPipeline>("rag");

        await Assert.That(chatPipeline).IsNotNull();
        await Assert.That(ragPipeline).IsNotNull();
        await Assert.That(ReferenceEquals(chatPipeline, ragPipeline)).IsFalse();

        // Both pipelines are functional
        var items = new[]
        {
            new ContextItem { Content = "Test", Tokens = 10, Kind = ContextKind.Message, Source = ContextSource.Chat },
        };

        var chatResult = chatPipeline.Execute(items);
        var ragResult = ragPipeline.Execute(items);

        await Assert.That(chatResult.Items.Count).IsEqualTo(1);
        await Assert.That(ragResult.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddCupelPipeline_NullIntent_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var budget = new ContextBudget(maxTokens: 4000, targetTokens: 3000);

        await Assert.That(() => services.AddCupelPipeline(null!, budget))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task AddCupelPipeline_WhitespaceIntent_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var budget = new ContextBudget(maxTokens: 4000, targetTokens: 3000);

        await Assert.That(() => services.AddCupelPipeline("   ", budget))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task AddCupelPipeline_EmptyIntent_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var budget = new ContextBudget(maxTokens: 4000, targetTokens: 3000);

        await Assert.That(() => services.AddCupelPipeline("", budget))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task AddCupelTracing_RegistersTransientTraceCollector()
    {
        var services = new ServiceCollection();

        services.AddCupelTracing();

        var provider = services.BuildServiceProvider();
        var collector1 = provider.GetRequiredService<ITraceCollector>();
        var collector2 = provider.GetRequiredService<ITraceCollector>();

        await Assert.That(collector1).IsNotNull();
        await Assert.That(collector1).IsTypeOf<DiagnosticTraceCollector>();
        await Assert.That(ReferenceEquals(collector1, collector2)).IsFalse();
    }

    [Test]
    public async Task AddCupel_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        await Assert.That(() => services.AddCupel(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task AddCupelPipeline_NullBudget_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        await Assert.That(() => services.AddCupelPipeline("chat", null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task AddCupelTracing_CalledTwice_DoesNotDoubleRegister()
    {
        var services = new ServiceCollection();

        services.AddCupelTracing();
        services.AddCupelTracing();

        var registrations = services.Where(d => d.ServiceType == typeof(ITraceCollector)).ToList();
        await Assert.That(registrations.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddCupelPipeline_ComponentsAreSingletons_SameInstanceAcrossResolves()
    {
        var services = new ServiceCollection();
        var budget = new ContextBudget(maxTokens: 4000, targetTokens: 3000);

        services
            .AddCupel(o => o.AddPolicy("chat", CupelPresets.Chat()))
            .AddCupelPipeline("chat", budget);

        var provider = services.BuildServiceProvider();
        var pipeline1 = provider.GetRequiredKeyedService<CupelPipeline>("chat");
        var pipeline2 = provider.GetRequiredKeyedService<CupelPipeline>("chat");

        // Pipelines are different instances (transient)
        await Assert.That(ReferenceEquals(pipeline1, pipeline2)).IsFalse();

        // Components are the same instances (singleton)
        await Assert.That(ReferenceEquals(pipeline1.Scorer, pipeline2.Scorer)).IsTrue();
        await Assert.That(ReferenceEquals(pipeline1.Slicer, pipeline2.Slicer)).IsTrue();
        await Assert.That(ReferenceEquals(pipeline1.Placer, pipeline2.Placer)).IsTrue();
    }

    [Test]
    public async Task AddCupelPipeline_ScaledScorerPolicy_ComponentsAreSingletons()
    {
        var policy = new CupelPolicy(
            scorers:
            [
                new ScorerEntry(
                    ScorerType.Scaled,
                    weight: 1.0,
                    innerScorer: new ScorerEntry(ScorerType.Recency, weight: 1.0)),
            ],
            name: "ScaledTest");

        var services = new ServiceCollection();
        var budget = new ContextBudget(maxTokens: 4000, targetTokens: 3000);

        services
            .AddCupel(o => o.AddPolicy("scaled", policy))
            .AddCupelPipeline("scaled", budget);

        var provider = services.BuildServiceProvider();
        var pipeline1 = provider.GetRequiredKeyedService<CupelPipeline>("scaled");
        var pipeline2 = provider.GetRequiredKeyedService<CupelPipeline>("scaled");

        // Pipelines are different instances (transient)
        await Assert.That(ReferenceEquals(pipeline1, pipeline2)).IsFalse();

        // Composed scorer tree is the same singleton instance
        await Assert.That(ReferenceEquals(pipeline1.Scorer, pipeline2.Scorer)).IsTrue();
        await Assert.That(ReferenceEquals(pipeline1.Slicer, pipeline2.Slicer)).IsTrue();
        await Assert.That(ReferenceEquals(pipeline1.Placer, pipeline2.Placer)).IsTrue();
    }

    [Test]
    public async Task AddCupelPipeline_StreamPolicy_AsyncSlicerIsSingleton()
    {
        var policy = new CupelPolicy(
            scorers: [new ScorerEntry(ScorerType.Recency, weight: 1.0)],
            slicerType: SlicerType.Stream,
            streamBatchSize: 16,
            name: "StreamTest");

        var services = new ServiceCollection();
        var budget = new ContextBudget(maxTokens: 4000, targetTokens: 3000);

        services
            .AddCupel(o => o.AddPolicy("stream", policy))
            .AddCupelPipeline("stream", budget);

        var provider = services.BuildServiceProvider();
        var pipeline1 = provider.GetRequiredKeyedService<CupelPipeline>("stream");
        var pipeline2 = provider.GetRequiredKeyedService<CupelPipeline>("stream");

        // Pipelines are different instances (transient)
        await Assert.That(ReferenceEquals(pipeline1, pipeline2)).IsFalse();

        // All components including async slicer are singleton
        await Assert.That(ReferenceEquals(pipeline1.Scorer, pipeline2.Scorer)).IsTrue();
        await Assert.That(ReferenceEquals(pipeline1.Slicer, pipeline2.Slicer)).IsTrue();
        await Assert.That(ReferenceEquals(pipeline1.Placer, pipeline2.Placer)).IsTrue();
        await Assert.That(pipeline1.AsyncSlicer).IsNotNull();
        await Assert.That(ReferenceEquals(pipeline1.AsyncSlicer, pipeline2.AsyncSlicer)).IsTrue();
    }

    [Test]
    public async Task AddCupelTracing_IsTransient_DifferentInstancesPerResolve()
    {
        var services = new ServiceCollection();

        services.AddCupelTracing();

        var provider = services.BuildServiceProvider();
        var collector1 = provider.GetRequiredService<ITraceCollector>();
        var collector2 = provider.GetRequiredService<ITraceCollector>();

        await Assert.That(ReferenceEquals(collector1, collector2)).IsFalse();
    }
}
