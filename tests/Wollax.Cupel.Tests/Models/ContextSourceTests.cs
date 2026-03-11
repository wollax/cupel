using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Wollax.Cupel.Tests.Models;

public class ContextSourceTests
{
    [Test]
    public async Task WellKnown_Chat_Exists()
    {
        await Assert.That(ContextSource.Chat).IsNotNull();
        await Assert.That(ContextSource.Chat.Value).IsEqualTo("Chat");
    }

    [Test]
    public async Task WellKnown_Tool_Exists()
    {
        await Assert.That(ContextSource.Tool).IsNotNull();
        await Assert.That(ContextSource.Tool.Value).IsEqualTo("Tool");
    }

    [Test]
    public async Task WellKnown_Rag_Exists()
    {
        await Assert.That(ContextSource.Rag).IsNotNull();
        await Assert.That(ContextSource.Rag.Value).IsEqualTo("Rag");
    }

    [Test]
    public async Task Equals_IsCaseInsensitive()
    {
        var lower = new ContextSource("chat");
        await Assert.That(lower.Equals(ContextSource.Chat)).IsTrue();
    }

    [Test]
    public async Task Equals_DifferentValues_ReturnsFalse()
    {
        await Assert.That(ContextSource.Chat.Equals(ContextSource.Tool)).IsFalse();
    }

    [Test]
    public async Task Equals_Null_ReturnsFalse()
    {
        await Assert.That(ContextSource.Chat.Equals(null)).IsFalse();
    }

    [Test]
    public async Task GetHashCode_CaseInsensitive_SameHash()
    {
        var lower = new ContextSource("chat");
        await Assert.That(lower.GetHashCode()).IsEqualTo(ContextSource.Chat.GetHashCode());
    }

    [Test]
    public async Task GetHashCode_DifferentValues_DifferentHash()
    {
        await Assert.That(ContextSource.Chat.GetHashCode())
            .IsNotEqualTo(ContextSource.Tool.GetHashCode());
    }

    [Test]
    public async Task OperatorEquals_CaseInsensitive()
    {
        var lower = new ContextSource("chat");
        await Assert.That(lower == ContextSource.Chat).IsTrue();
    }

    [Test]
    public async Task OperatorNotEquals_DifferentValues()
    {
        await Assert.That(ContextSource.Chat != ContextSource.Tool).IsTrue();
    }

    [Test]
    public async Task OperatorEquals_BothNull_ReturnsTrue()
    {
        ContextSource? left = null;
        ContextSource? right = null;
        await Assert.That(left == right).IsTrue();
    }

    [Test]
    public async Task OperatorEquals_OneNull_ReturnsFalse()
    {
        ContextSource? left = null;
        await Assert.That(left == ContextSource.Chat).IsFalse();
    }

    [Test]
    public async Task ToString_ReturnsValue()
    {
        await Assert.That(ContextSource.Chat.ToString()).IsEqualTo("Chat");
    }

    [Test]
    public async Task ToString_CustomValue_ReturnsOriginalCasing()
    {
        var custom = new ContextSource("MyCustom");
        await Assert.That(custom.ToString()).IsEqualTo("MyCustom");
    }

    [Test]
    public async Task Constructor_NullValue_ThrowsArgumentException()
    {
        await Assert.That(() => new ContextSource(null!))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_EmptyValue_ThrowsArgumentException()
    {
        await Assert.That(() => new ContextSource(""))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WhitespaceValue_ThrowsArgumentException()
    {
        await Assert.That(() => new ContextSource("   "))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task CustomSource_ConstructsSuccessfully()
    {
        var custom = new ContextSource("custom");
        await Assert.That(custom.Value).IsEqualTo("custom");
    }

    [Test]
    public async Task CustomSource_EqualityWorks()
    {
        var a = new ContextSource("custom");
        var b = new ContextSource("Custom");
        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task CustomSource_NotEqualToWellKnown()
    {
        var custom = new ContextSource("custom");
        await Assert.That(custom.Equals(ContextSource.Chat)).IsFalse();
    }

    [Test]
    public async Task ObjectEquals_CaseInsensitive()
    {
        object lower = new ContextSource("chat");
        await Assert.That(ContextSource.Chat.Equals(lower)).IsTrue();
    }

    [Test]
    public async Task ObjectEquals_NonContextSource_ReturnsFalse()
    {
        await Assert.That(ContextSource.Chat.Equals("Chat")).IsFalse();
    }

    [Test]
    public async Task JsonSerialize_ProducesPlainString()
    {
        var json = JsonSerializer.Serialize(ContextSource.Chat);
        await Assert.That(json).IsEqualTo("\"Chat\"");
    }

    [Test]
    public async Task JsonDeserialize_ProducesEqualValue()
    {
        var deserialized = JsonSerializer.Deserialize<ContextSource>("\"Chat\"");
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Equals(ContextSource.Chat)).IsTrue();
    }

    [Test]
    public async Task JsonRoundTrip_PreservesEquality()
    {
        var original = ContextSource.Rag;
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ContextSource>(json);
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized! == original).IsTrue();
    }

    [Test]
    public async Task JsonDeserialize_NullJsonLiteral_ReturnsNull()
    {
        var result = JsonSerializer.Deserialize<ContextSource>("null");
        await Assert.That(result).IsNull();
    }
}
