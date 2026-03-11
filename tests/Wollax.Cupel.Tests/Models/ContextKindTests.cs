using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Wollax.Cupel.Tests.Models;

public class ContextKindTests
{
    [Test]
    public async Task WellKnown_Message_Exists()
    {
        await Assert.That(ContextKind.Message).IsNotNull();
        await Assert.That(ContextKind.Message.Value).IsEqualTo("Message");
    }

    [Test]
    public async Task WellKnown_Document_Exists()
    {
        await Assert.That(ContextKind.Document).IsNotNull();
        await Assert.That(ContextKind.Document.Value).IsEqualTo("Document");
    }

    [Test]
    public async Task WellKnown_ToolOutput_Exists()
    {
        await Assert.That(ContextKind.ToolOutput).IsNotNull();
        await Assert.That(ContextKind.ToolOutput.Value).IsEqualTo("ToolOutput");
    }

    [Test]
    public async Task WellKnown_Memory_Exists()
    {
        await Assert.That(ContextKind.Memory).IsNotNull();
        await Assert.That(ContextKind.Memory.Value).IsEqualTo("Memory");
    }

    [Test]
    public async Task WellKnown_SystemPrompt_Exists()
    {
        await Assert.That(ContextKind.SystemPrompt).IsNotNull();
        await Assert.That(ContextKind.SystemPrompt.Value).IsEqualTo("SystemPrompt");
    }

    [Test]
    public async Task Equals_IsCaseInsensitive()
    {
        var lower = new ContextKind("message");
        await Assert.That(lower.Equals(ContextKind.Message)).IsTrue();
    }

    [Test]
    public async Task Equals_DifferentValues_ReturnsFalse()
    {
        await Assert.That(ContextKind.Message.Equals(ContextKind.Document)).IsFalse();
    }

    [Test]
    public async Task Equals_Null_ReturnsFalse()
    {
        await Assert.That(ContextKind.Message.Equals(null)).IsFalse();
    }

    [Test]
    public async Task GetHashCode_CaseInsensitive_SameHash()
    {
        var lower = new ContextKind("message");
        await Assert.That(lower.GetHashCode()).IsEqualTo(ContextKind.Message.GetHashCode());
    }

    [Test]
    public async Task GetHashCode_DifferentValues_DifferentHash()
    {
        await Assert.That(ContextKind.Message.GetHashCode())
            .IsNotEqualTo(ContextKind.Document.GetHashCode());
    }

    [Test]
    public async Task OperatorEquals_CaseInsensitive()
    {
        var lower = new ContextKind("message");
        await Assert.That(lower == ContextKind.Message).IsTrue();
    }

    [Test]
    public async Task OperatorNotEquals_DifferentValues()
    {
        await Assert.That(ContextKind.Message != ContextKind.Document).IsTrue();
    }

    [Test]
    public async Task OperatorEquals_BothNull_ReturnsTrue()
    {
        ContextKind? left = null;
        ContextKind? right = null;
        await Assert.That(left == right).IsTrue();
    }

    [Test]
    public async Task OperatorEquals_OneNull_ReturnsFalse()
    {
        ContextKind? left = null;
        await Assert.That(left == ContextKind.Message).IsFalse();
    }

    [Test]
    public async Task ToString_ReturnsValue()
    {
        await Assert.That(ContextKind.Message.ToString()).IsEqualTo("Message");
    }

    [Test]
    public async Task ToString_CustomValue_ReturnsOriginalCasing()
    {
        var custom = new ContextKind("MyCustom");
        await Assert.That(custom.ToString()).IsEqualTo("MyCustom");
    }

    [Test]
    public async Task Constructor_NullValue_ThrowsArgumentException()
    {
        await Assert.That(() => new ContextKind(null!)).ThrowsException()
            .OfType<ArgumentException>();
    }

    [Test]
    public async Task Constructor_EmptyValue_ThrowsArgumentException()
    {
        await Assert.That(() => new ContextKind("")).ThrowsException()
            .OfType<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WhitespaceValue_ThrowsArgumentException()
    {
        await Assert.That(() => new ContextKind("   ")).ThrowsException()
            .OfType<ArgumentException>();
    }

    [Test]
    public async Task CustomKind_ConstructsSuccessfully()
    {
        var custom = new ContextKind("custom");
        await Assert.That(custom.Value).IsEqualTo("custom");
    }

    [Test]
    public async Task CustomKind_EqualityWorks()
    {
        var a = new ContextKind("custom");
        var b = new ContextKind("Custom");
        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task CustomKind_NotEqualToWellKnown()
    {
        var custom = new ContextKind("custom");
        await Assert.That(custom.Equals(ContextKind.Message)).IsFalse();
    }

    [Test]
    public async Task ObjectEquals_CaseInsensitive()
    {
        object lower = new ContextKind("message");
        await Assert.That(ContextKind.Message.Equals(lower)).IsTrue();
    }

    [Test]
    public async Task ObjectEquals_NonContextKind_ReturnsFalse()
    {
        await Assert.That(ContextKind.Message.Equals("Message")).IsFalse();
    }

    [Test]
    public async Task JsonSerialize_ProducesPlainString()
    {
        var json = JsonSerializer.Serialize(ContextKind.Message);
        await Assert.That(json).IsEqualTo("\"Message\"");
    }

    [Test]
    public async Task JsonDeserialize_ProducesEqualValue()
    {
        var deserialized = JsonSerializer.Deserialize<ContextKind>("\"Message\"");
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Equals(ContextKind.Message)).IsTrue();
    }

    [Test]
    public async Task JsonRoundTrip_PreservesEquality()
    {
        var original = ContextKind.ToolOutput;
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ContextKind>(json);
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized! == original).IsTrue();
    }

    [Test]
    public async Task JsonDeserialize_NullValue_ThrowsJsonException()
    {
        await Assert.That(() => JsonSerializer.Deserialize<ContextKind>("null"))
            .ThrowsException();
    }
}
