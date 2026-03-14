using System.Text.Json;
using Wollax.Cupel.Json;

namespace Wollax.Cupel.Json.Tests;

public class ErrorMessageTests
{
    [Test]
    public async Task Deserialize_NullJson_ThrowsArgumentNullException()
    {
        var action = () => CupelJsonSerializer.Deserialize(null!);

        await Assert.That(action).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Deserialize_EmptyString_ThrowsJsonException()
    {
        var action = () => CupelJsonSerializer.Deserialize("");

        var ex = await Assert.That(action).ThrowsExactly<JsonException>();
        await Assert.That(ex!.Message).Contains("empty");
    }

    [Test]
    public async Task Deserialize_MalformedJson_ThrowsJsonException()
    {
        var action = () => CupelJsonSerializer.Deserialize("{not valid json");

        await Assert.That(action).Throws<JsonException>();
    }

    [Test]
    public async Task Deserialize_JsonNull_ThrowsWithMessage()
    {
        var action = () => CupelJsonSerializer.Deserialize("null");

        var ex = await Assert.That(action).Throws<JsonException>();
        await Assert.That(ex!.Message).Contains("null");
    }

    [Test]
    public async Task Deserialize_InvalidEnumValue_ThrowsWithMessage()
    {
        var json = """
            {
                "scorers": [{"type": "recency", "weight": 1.0}],
                "slicerType": "nonexistent"
            }
            """;

        var action = () => CupelJsonSerializer.Deserialize(json);

        await Assert.That(action).Throws<JsonException>();
    }

    [Test]
    public async Task DeserializeBudget_NullJson_ThrowsArgumentNullException()
    {
        var action = () => CupelJsonSerializer.DeserializeBudget(null!);

        await Assert.That(action).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task DeserializeBudget_EmptyString_ThrowsJsonException()
    {
        var action = () => CupelJsonSerializer.DeserializeBudget("");

        var ex = await Assert.That(action).ThrowsExactly<JsonException>();
        await Assert.That(ex!.Message).Contains("empty");
    }

    [Test]
    public async Task DeserializeBudget_JsonNull_ThrowsWithMessage()
    {
        var action = () => CupelJsonSerializer.DeserializeBudget("null");

        var ex = await Assert.That(action).Throws<JsonException>();
        await Assert.That(ex!.Message).Contains("null");
    }
}
