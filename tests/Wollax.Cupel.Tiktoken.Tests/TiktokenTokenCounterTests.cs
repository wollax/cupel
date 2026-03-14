using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Tiktoken;

namespace Wollax.Cupel.Tiktoken.Tests;

public class TiktokenTokenCounterTests
{
    [Test]
    public async Task CreateForModel_Gpt4o_ReturnsNonNull()
    {
        var counter = TiktokenTokenCounter.CreateForModel("gpt-4o");

        await Assert.That(counter).IsNotNull();
    }

    [Test]
    public async Task CountTokens_HelloWorld_ReturnsPositiveCount()
    {
        var counter = TiktokenTokenCounter.CreateForModel("gpt-4o");

        var count = counter.CountTokens("Hello, world!");

        await Assert.That(count).IsGreaterThan(0);
    }

    [Test]
    public async Task CountTokens_KnownString_ReturnsExpectedCount()
    {
        // "The quick brown fox jumps over the lazy dog" = 9 tokens with o200k_base
        var counter = TiktokenTokenCounter.CreateForModel("gpt-4o");

        var count = counter.CountTokens("The quick brown fox jumps over the lazy dog");

        await Assert.That(count).IsEqualTo(9);
    }

    [Test]
    public async Task CountTokens_EmptyString_ReturnsZero()
    {
        var counter = TiktokenTokenCounter.CreateForModel("gpt-4o");

        var count = counter.CountTokens("");

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task WithTokenCount_SetsTokensProperty()
    {
        var counter = TiktokenTokenCounter.CreateForModel("gpt-4o");
        var item = new ContextItem { Content = "Hello, world!", Tokens = 0 };

        var result = counter.WithTokenCount(item);

        await Assert.That(result.Tokens).IsGreaterThan(0);
        await Assert.That(result.Content).IsEqualTo("Hello, world!");
    }

    [Test]
    public async Task WithTokenCount_PreservesAllOtherProperties()
    {
        var counter = TiktokenTokenCounter.CreateForModel("gpt-4o");
        var timestamp = DateTimeOffset.UtcNow;
        var item = new ContextItem
        {
            Content = "test content",
            Tokens = 0,
            Kind = ContextKind.Document,
            Source = ContextSource.Tool,
            Priority = 42,
            Tags = ["tag1", "tag2"],
            Metadata = new Dictionary<string, object?> { ["key"] = "value" },
            Timestamp = timestamp,
            FutureRelevanceHint = 0.75,
            Pinned = true,
            OriginalTokens = 100
        };

        var result = counter.WithTokenCount(item);

        await Assert.That(result.Kind).IsEqualTo(ContextKind.Document);
        await Assert.That(result.Source).IsEqualTo(ContextSource.Tool);
        await Assert.That(result.Priority).IsEqualTo(42);
        await Assert.That(result.Tags).Contains("tag1");
        await Assert.That(result.Tags).Contains("tag2");
        await Assert.That(result.Timestamp).IsEqualTo(timestamp);
        await Assert.That(result.FutureRelevanceHint).IsEqualTo(0.75);
        await Assert.That(result.Pinned).IsTrue();
        await Assert.That(result.OriginalTokens).IsEqualTo(100);
        await Assert.That(result.Tokens).IsGreaterThan(0);
    }

    [Test]
    public async Task CreateForEncoding_O200kBase_CountsTokensCorrectly()
    {
        var counter = TiktokenTokenCounter.CreateForEncoding("o200k_base");

        var count = counter.CountTokens("Hello, world!");

        await Assert.That(count).IsEqualTo(4);
    }

    [Test]
    public async Task CreateForModel_InvalidModel_ThrowsNotSupportedException()
    {
        var action = () => TiktokenTokenCounter.CreateForModel("nonexistent-model-xyz");

        await Assert.That(action).ThrowsExactly<NotSupportedException>();
    }
}
