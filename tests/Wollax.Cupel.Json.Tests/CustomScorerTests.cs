using System.Text.Json;
using Wollax.Cupel.Json;

namespace Wollax.Cupel.Json.Tests;

public class CustomScorerTests
{
    // --- Registration API tests ---

    [Test]
    public async Task RegisterScorer_StoresFactory()
    {
        var options = new CupelJsonOptions()
            .RegisterScorer("myCustom", () => new StubScorer());

        await Assert.That(options.HasScorerFactory("myCustom")).IsTrue();
    }

    [Test]
    public async Task RegisterScorer_DuplicateName_Overwrites()
    {
        var firstCalled = false;
        var secondCalled = false;

        var options = new CupelJsonOptions()
            .RegisterScorer("myCustom", () => { firstCalled = true; return new StubScorer(); })
            .RegisterScorer("myCustom", () => { secondCalled = true; return new StubScorer(); });

        var factory = options.GetScorerFactory("myCustom");
        factory!.Invoke(null);

        await Assert.That(firstCalled).IsFalse();
        await Assert.That(secondCalled).IsTrue();
    }

    [Test]
    public async Task RegisterScorer_ConfigAware_StoresFactory()
    {
        var options = new CupelJsonOptions()
            .RegisterScorer("myCustom", (JsonElement? _) => new StubScorer());

        await Assert.That(options.HasScorerFactory("myCustom")).IsTrue();
    }

    // --- BuiltInScorerTypes derivation tests ---

    [Test]
    public async Task BuiltInScorerTypes_DerivedFromEnum_HasExpectedCount()
    {
        await Assert.That(CupelJsonSerializer.BuiltInScorerTypes).Count().IsEqualTo(7);
    }

    [Test]
    public async Task BuiltInScorerTypes_ContainsScaled()
    {
        await Assert.That(CupelJsonSerializer.BuiltInScorerTypes).Contains("scaled");
    }

    [Test]
    public async Task BuiltInScorerTypes_ContainsAllOriginalTypes()
    {
        var types = CupelJsonSerializer.BuiltInScorerTypes;

        await Assert.That(types).Contains("recency");
        await Assert.That(types).Contains("priority");
        await Assert.That(types).Contains("kind");
        await Assert.That(types).Contains("tag");
        await Assert.That(types).Contains("frequency");
        await Assert.That(types).Contains("reflexive");
    }

    // --- Unknown type error tests ---

    [Test]
    public async Task Deserialize_UnknownScorerType_ThrowsDescriptiveError()
    {
        var json = """
            {
                "scorers": [{"type": "myCustom", "weight": 1.0}],
                "slicerType": "greedy",
                "placerType": "chronological",
                "deduplicationEnabled": true,
                "overflowStrategy": "throw"
            }
            """;

        var exception = await Assert.ThrowsAsync<JsonException>(
            () => Task.FromResult(CupelJsonSerializer.Deserialize(json)));

        var message = exception!.Message;
        await Assert.That(message).Contains("myCustom");
        await Assert.That(message).Contains("recency");
        await Assert.That(message).Contains("priority");
        await Assert.That(message).Contains("kind");
        await Assert.That(message).Contains("tag");
        await Assert.That(message).Contains("frequency");
        await Assert.That(message).Contains("reflexive");
        await Assert.That(message).Contains("RegisterScorer");
    }

    [Test]
    public async Task Deserialize_UnknownScorerType_IncludesScaledInKnownTypes()
    {
        var json = """
            {
                "scorers": [{"type": "custom-foo", "weight": 1.0}],
                "slicerType": "greedy",
                "placerType": "chronological",
                "deduplicationEnabled": true,
                "overflowStrategy": "throw"
            }
            """;

        var exception = await Assert.ThrowsAsync<JsonException>(
            () => Task.FromResult(CupelJsonSerializer.Deserialize(json)));

        var message = exception!.Message;
        await Assert.That(message).Contains("custom-foo");
        await Assert.That(message).Contains("scaled");
        await Assert.That(message).Contains("RegisterScorer");
    }

    [Test]
    public async Task Deserialize_UnknownScorerType_WithRegistrations_ListsCustomTypes()
    {
        var json = """
            {
                "scorers": [{"type": "unknownOther", "weight": 1.0}],
                "slicerType": "greedy",
                "placerType": "chronological",
                "deduplicationEnabled": true,
                "overflowStrategy": "throw"
            }
            """;

        var options = new CupelJsonOptions()
            .RegisterScorer("semanticSimilarity", () => new StubScorer());

        var exception = await Assert.ThrowsAsync<JsonException>(
            () => Task.FromResult(CupelJsonSerializer.Deserialize(json, options)));

        var message = exception!.Message;
        await Assert.That(message).Contains("unknownOther");
        await Assert.That(message).Contains("semanticSimilarity");
    }

    // --- Validation tests ---

    [Test]
    public async Task RegisterScorer_NullName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => Task.FromResult(new CupelJsonOptions().RegisterScorer(null!, () => new StubScorer())));
    }

    [Test]
    public async Task RegisterScorer_EmptyName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => Task.FromResult(new CupelJsonOptions().RegisterScorer("", () => new StubScorer())));
    }

    [Test]
    public async Task RegisterScorer_WhitespaceName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => Task.FromResult(new CupelJsonOptions().RegisterScorer("  ", () => new StubScorer())));
    }

    [Test]
    public async Task RegisterScorer_NullFactory_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Task.FromResult(new CupelJsonOptions().RegisterScorer("test", (Func<IScorer>)null!)));
    }

    [Test]
    public async Task RegisterScorer_ConfigAware_NullFactory_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Task.FromResult(new CupelJsonOptions().RegisterScorer("test", (Func<JsonElement?, IScorer>)null!)));
    }

    [Test]
    public async Task GetScorerFactory_Unregistered_ReturnsNull()
    {
        var options = new CupelJsonOptions();

        await Assert.That(options.GetScorerFactory("nonexistent")).IsNull();
    }

    [Test]
    public async Task HasScorerFactory_Unregistered_ReturnsFalse()
    {
        var options = new CupelJsonOptions();

        await Assert.That(options.HasScorerFactory("nonexistent")).IsFalse();
    }

    [Test]
    public async Task RegisteredScorerNames_ReturnsAllRegistered()
    {
        var options = new CupelJsonOptions()
            .RegisterScorer("alpha", () => new StubScorer())
            .RegisterScorer("beta", (JsonElement? _) => new StubScorer());

        var names = options.RegisteredScorerNames;

        await Assert.That(names).Contains("alpha");
        await Assert.That(names).Contains("beta");
        await Assert.That(names).Count().IsEqualTo(2);
    }

    /// <summary>Minimal IScorer implementation for testing.</summary>
    private sealed class StubScorer : IScorer
    {
        public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems) => 0.5;
    }
}
