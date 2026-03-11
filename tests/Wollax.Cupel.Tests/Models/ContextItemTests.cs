using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Wollax.Cupel.Tests.Models;

public class ContextItemTests
{
    [Test]
    public async Task MinimalConstruction_ContentAndTokens_Works()
    {
        var item = new ContextItem { Content = "hello", Tokens = 5 };

        await Assert.That(item.Content).IsEqualTo("hello");
        await Assert.That(item.Tokens).IsEqualTo(5);
    }

    [Test]
    public async Task Default_Kind_IsMessage()
    {
        var item = new ContextItem { Content = "hello", Tokens = 5 };

        await Assert.That(item.Kind).IsEqualTo(ContextKind.Message);
    }

    [Test]
    public async Task Default_Source_IsChat()
    {
        var item = new ContextItem { Content = "hello", Tokens = 5 };

        await Assert.That(item.Source).IsEqualTo(ContextSource.Chat);
    }

    [Test]
    public async Task Default_Priority_IsNull()
    {
        var item = new ContextItem { Content = "hello", Tokens = 5 };

        await Assert.That(item.Priority).IsNull();
    }

    [Test]
    public async Task Default_Tags_IsEmpty()
    {
        var item = new ContextItem { Content = "hello", Tokens = 5 };

        await Assert.That(item.Tags).IsEmpty();
    }

    [Test]
    public async Task Default_Metadata_IsEmpty()
    {
        var item = new ContextItem { Content = "hello", Tokens = 5 };

        await Assert.That(item.Metadata).IsEmpty();
    }

    [Test]
    public async Task Default_Timestamp_IsNull()
    {
        var item = new ContextItem { Content = "hello", Tokens = 5 };

        await Assert.That(item.Timestamp).IsNull();
    }

    [Test]
    public async Task Default_FutureRelevanceHint_IsNull()
    {
        var item = new ContextItem { Content = "hello", Tokens = 5 };

        await Assert.That(item.FutureRelevanceHint).IsNull();
    }

    [Test]
    public async Task Default_Pinned_IsFalse()
    {
        var item = new ContextItem { Content = "hello", Tokens = 5 };

        await Assert.That(item.Pinned).IsFalse();
    }

    [Test]
    public async Task Default_OriginalTokens_IsNull()
    {
        var item = new ContextItem { Content = "hello", Tokens = 5 };

        await Assert.That(item.OriginalTokens).IsNull();
    }

    [Test]
    public async Task WithExpression_CreatesModifiedCopy_OriginalUnchanged()
    {
        var original = new ContextItem { Content = "hello", Tokens = 5 };
        var modified = original with { Content = "world", Tokens = 10 };

        await Assert.That(modified.Content).IsEqualTo("world");
        await Assert.That(modified.Tokens).IsEqualTo(10);
        await Assert.That(original.Content).IsEqualTo("hello");
        await Assert.That(original.Tokens).IsEqualTo(5);
    }

    [Test]
    public async Task ValueEquality_IdenticalItems_AreEqual()
    {
        var a = new ContextItem { Content = "hello", Tokens = 5 };
        var b = new ContextItem { Content = "hello", Tokens = 5 };

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task ValueEquality_DifferentContent_AreNotEqual()
    {
        var a = new ContextItem { Content = "hello", Tokens = 5 };
        var b = new ContextItem { Content = "world", Tokens = 5 };

        await Assert.That(a).IsNotEqualTo(b);
        await Assert.That(a != b).IsTrue();
    }

    [Test]
    public async Task JsonSerialization_UsesCamelCasePropertyNames()
    {
        var item = new ContextItem { Content = "hello", Tokens = 5 };
        var json = JsonSerializer.Serialize(item);

        await Assert.That(json).Contains("\"content\"");
        await Assert.That(json).Contains("\"tokens\"");
        await Assert.That(json).Contains("\"kind\"");
        await Assert.That(json).Contains("\"source\"");
        await Assert.That(json).Contains("\"pinned\"");
        await Assert.That(json).Contains("\"tags\"");
        await Assert.That(json).Contains("\"metadata\"");
    }

    [Test]
    public async Task JsonRoundTrip_PreservesEquality()
    {
        var original = new ContextItem
        {
            Content = "hello",
            Tokens = 5,
            Kind = ContextKind.Document,
            Source = ContextSource.Tool,
            Priority = 3,
            Tags = ["important", "test"],
            Pinned = true,
            OriginalTokens = 10,
            FutureRelevanceHint = 0.8,
            Timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ContextItem>(json);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Content).IsEqualTo(original.Content);
        await Assert.That(deserialized.Tokens).IsEqualTo(original.Tokens);
        await Assert.That(deserialized.Kind).IsEqualTo(original.Kind);
        await Assert.That(deserialized.Source).IsEqualTo(original.Source);
        await Assert.That(deserialized.Priority).IsEqualTo(original.Priority);
        await Assert.That(deserialized.Pinned).IsEqualTo(original.Pinned);
        await Assert.That(deserialized.OriginalTokens).IsEqualTo(original.OriginalTokens);
        await Assert.That(deserialized.FutureRelevanceHint).IsEqualTo(original.FutureRelevanceHint);
        await Assert.That(deserialized.Timestamp).IsEqualTo(original.Timestamp);
    }

    [Test]
    public async Task Kind_SerializesAsPlainString_WithinContextItemJson()
    {
        var item = new ContextItem
        {
            Content = "hello",
            Tokens = 5,
            Kind = ContextKind.Document,
        };

        var json = JsonSerializer.Serialize(item);

        await Assert.That(json).Contains("\"kind\":\"Document\"");
    }

    [Test]
    public async Task Source_SerializesAsPlainString_WithinContextItemJson()
    {
        var item = new ContextItem
        {
            Content = "hello",
            Tokens = 5,
            Source = ContextSource.Tool,
        };

        var json = JsonSerializer.Serialize(item);

        await Assert.That(json).Contains("\"source\":\"Tool\"");
    }
}
