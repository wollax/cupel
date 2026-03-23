using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Diagnostics.OpenTelemetry;
using Wollax.Cupel.Json;
using Wollax.Cupel.Testing;
using Wollax.Cupel.Tiktoken;

#pragma warning disable CUPEL001 // Chat preset is experimental
#pragma warning disable CUPEL003 // Rag preset is experimental

namespace Wollax.Cupel.ConsumptionTests;

public class ConsumptionTests
{
    [Test]
    public async Task Core_Pipeline_Executes_And_Returns_Result()
    {
        var policy = CupelPresets.Chat();
        var pipeline = CupelPipeline.CreateBuilder()
            .WithPolicy(policy)
            .WithBudget(new ContextBudget(maxTokens: 4096, targetTokens: 3072))
            .Build();

        var items = new[]
        {
            new ContextItem { Content = "Hello, world!", Tokens = 3, Kind = ContextKind.Message, Source = ContextSource.Chat },
        };

        var result = pipeline.Execute(items);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).IsNotNull();
        await Assert.That(result.Items.Count).IsEqualTo(1);
        await Assert.That(result.TotalTokens).IsGreaterThan(0);
    }

    [Test]
    public async Task Json_Serialization_Roundtrips_Policy()
    {
        var policy = CupelPresets.Chat();

        var json = CupelJsonSerializer.Serialize(policy);
        var deserialized = CupelJsonSerializer.Deserialize(json);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized.Scorers.Count).IsEqualTo(policy.Scorers.Count);
        await Assert.That(deserialized.SlicerType).IsEqualTo(policy.SlicerType);
        await Assert.That(deserialized.PlacerType).IsEqualTo(policy.PlacerType);
    }

    [Test]
    public async Task DI_Resolves_Keyed_Pipeline()
    {
        var services = new ServiceCollection();
        services.AddCupel(options =>
        {
            options.AddPolicy("chat", CupelPresets.Chat());
        });
        services.AddCupelPipeline("chat", new ContextBudget(maxTokens: 4096, targetTokens: 3072));

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredKeyedService<CupelPipeline>("chat");

        await Assert.That(pipeline).IsNotNull();

        // Verify the pipeline is functional — not just resolvable
        var items = new[]
        {
            new ContextItem { Content = "Hello", Tokens = 3, Kind = ContextKind.Message, Source = ContextSource.Chat },
        };
        var result = pipeline.Execute(items);

        await Assert.That(result.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Tiktoken_Counts_Tokens_For_Known_String()
    {
        var counter = TiktokenTokenCounter.CreateForModel("gpt-4o");

        var count = counter.CountTokens("Hello, world!");

        await Assert.That(count).IsGreaterThan(0);
    }

    [Test]
    public async Task Tiktoken_WithTokenCount_Sets_Tokens()
    {
        var counter = TiktokenTokenCounter.CreateForModel("gpt-4o");
        var item = new ContextItem
        {
            Content = "The quick brown fox jumps over the lazy dog.",
            Tokens = 0,
            Kind = ContextKind.Message,
            Source = ContextSource.Chat,
        };

        var counted = counter.WithTokenCount(item);

        await Assert.That(counted.Tokens).IsGreaterThan(0);
        await Assert.That(counted.Content).IsEqualTo(item.Content);
    }

    [Test]
    public void Testing_Package_Should_Extension_Compiles_And_Works()
    {
        var report = new SelectionReport
        {
            Events = [],
            Included =
            [
                new IncludedItem
                {
                    Item = new ContextItem { Content = "hello", Tokens = 10, Kind = ContextKind.Message },
                    Score = 0.9,
                    Reason = InclusionReason.Scored,
                },
            ],
            Excluded = [],
            TotalCandidates = 1,
            TotalTokensConsidered = 10,
        };

        // Smoke test: Should() extension is callable and fluent chain works
        report.Should()
            .IncludeItemWithKind(ContextKind.Message)
            .HaveAtLeastNExclusions(0)
            .HaveKindCoverageCount(1);
    }

    /// <summary>
    /// Smoke test for the companion OpenTelemetry package consumed via NuGet local feed.
    /// Uses AddCupelInstrumentation() and CupelOpenTelemetryTraceCollector to verify
    /// that Activities appear on the "Wollax.Cupel" source during a real pipeline run.
    /// </summary>
    [Test]
    public async Task OpenTelemetry_Companion_Package_Captures_Pipeline_Activities()
    {
        var exportedActivities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddCupelInstrumentation()
            .AddInMemoryExporter(exportedActivities)
            .Build()!;

        var pipeline = CupelPipeline.CreateBuilder()
            .WithPolicy(CupelPresets.Chat())
            .WithBudget(new ContextBudget(maxTokens: 4096, targetTokens: 3072))
            .Build();

        var items = new[]
        {
            new ContextItem { Content = "Hello", Tokens = 3, Kind = ContextKind.Message, Source = ContextSource.Chat },
            new ContextItem { Content = "World", Tokens = 3, Kind = ContextKind.Message, Source = ContextSource.Chat },
        };

        var collector = new CupelOpenTelemetryTraceCollector(CupelOpenTelemetryVerbosity.StageOnly);
        var result = pipeline.Execute(items, collector);
        tracerProvider.ForceFlush();

        await Assert.That(exportedActivities.Count).IsGreaterThan(0)
            .Because("The companion package must emit Activities on the 'Wollax.Cupel' source during pipeline execution");

        var root = exportedActivities.FirstOrDefault(a => a.OperationName == "cupel.pipeline");
        await Assert.That(root).IsNotNull()
            .Because("Must have a root 'cupel.pipeline' Activity");

        var stages = exportedActivities
            .Where(a => a.OperationName.StartsWith("cupel.stage.", StringComparison.Ordinal))
            .ToList();
        await Assert.That(stages.Count).IsEqualTo(5)
            .Because("Must have exactly 5 stage Activities");
    }
}
