using Microsoft.Extensions.DependencyInjection;
using Wollax.Cupel;
using Wollax.Cupel.Json;
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
        await Assert.That(result.Items.Count).IsGreaterThanOrEqualTo(1);
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
}
