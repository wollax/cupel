using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Wollax.Cupel.Tests.Models;

public class ContextItemEqualityTests
{
    private static ContextItem MakeItem(
        string content = "hello",
        int tokens = 5,
        ContextKind? kind = null,
        ContextSource? source = null,
        int? priority = null,
        bool pinned = false,
        int? originalTokens = null,
        double? futureRelevanceHint = null,
        DateTimeOffset? timestamp = null,
        IReadOnlyList<string>? tags = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return new ContextItem
        {
            Content = content,
            Tokens = tokens,
            Kind = kind ?? ContextKind.Message,
            Source = source ?? ContextSource.Chat,
            Priority = priority,
            Pinned = pinned,
            OriginalTokens = originalTokens,
            FutureRelevanceHint = futureRelevanceHint,
            Timestamp = timestamp,
            Tags = tags ?? [],
            Metadata = metadata ?? new Dictionary<string, object?>(),
        };
    }

    [Test]
    public async Task IdenticalItems_AreEqual()
    {
        var a = MakeItem(tags: ["a", "b"], metadata: new Dictionary<string, object?> { ["k"] = "v" });
        var b = MakeItem(tags: ["a", "b"], metadata: new Dictionary<string, object?> { ["k"] = "v" });

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
        await Assert.That(a != b).IsFalse();
    }

    [Test]
    public async Task DifferentContent_AreNotEqual()
    {
        var a = MakeItem(content: "hello");
        var b = MakeItem(content: "world");

        await Assert.That(a).IsNotEqualTo(b);
        await Assert.That(a != b).IsTrue();
    }

    [Test]
    public async Task Tags_SameElementsDifferentOrder_AreNotEqual()
    {
        var a = MakeItem(tags: ["x", "y"]);
        var b = MakeItem(tags: ["y", "x"]);

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task Tags_SameElementsSameOrder_AreEqual()
    {
        var a = MakeItem(tags: ["x", "y"]);
        var b = MakeItem(tags: ["x", "y"]);

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task Metadata_DifferentValues_AreNotEqual()
    {
        var a = MakeItem(metadata: new Dictionary<string, object?> { ["k"] = "v1" });
        var b = MakeItem(metadata: new Dictionary<string, object?> { ["k"] = "v2" });

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task Metadata_SameKeysAndValues_AreEqual()
    {
        var a = MakeItem(metadata: new Dictionary<string, object?> { ["k"] = "v", ["n"] = 42 });
        var b = MakeItem(metadata: new Dictionary<string, object?> { ["k"] = "v", ["n"] = 42 });

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task Metadata_DifferentKeys_AreNotEqual()
    {
        var a = MakeItem(metadata: new Dictionary<string, object?> { ["k1"] = "v" });
        var b = MakeItem(metadata: new Dictionary<string, object?> { ["k2"] = "v" });

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task NullVsNonNullTimestamp_AreNotEqual()
    {
        var a = MakeItem(timestamp: null);
        var b = MakeItem(timestamp: DateTimeOffset.UnixEpoch);

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task DifferentFutureRelevanceHint_AreNotEqual()
    {
        var a = MakeItem(futureRelevanceHint: 0.3);
        var b = MakeItem(futureRelevanceHint: 0.7);

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task EqualsNull_ReturnsFalse()
    {
        var item = MakeItem();

        await Assert.That(item.Equals(null)).IsFalse();
    }

    [Test]
    public async Task GetHashCode_EqualItems_ProduceSameHash()
    {
        var a = MakeItem(
            tags: ["a", "b"],
            metadata: new Dictionary<string, object?> { ["k"] = "v" },
            timestamp: DateTimeOffset.UnixEpoch,
            futureRelevanceHint: 0.5);
        var b = MakeItem(
            tags: ["a", "b"],
            metadata: new Dictionary<string, object?> { ["k"] = "v" },
            timestamp: DateTimeOffset.UnixEpoch,
            futureRelevanceHint: 0.5);

        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task Metadata_NullValueVsNonNull_AreNotEqual()
    {
        var a = MakeItem(metadata: new Dictionary<string, object?> { ["k"] = null });
        var b = MakeItem(metadata: new Dictionary<string, object?> { ["k"] = "v" });

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task Metadata_BothNullValues_AreEqual()
    {
        var a = MakeItem(metadata: new Dictionary<string, object?> { ["k"] = null });
        var b = MakeItem(metadata: new Dictionary<string, object?> { ["k"] = null });

        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task WithExpression_DeepCopy_AreEqual()
    {
        var original = MakeItem(
            tags: ["a"],
            metadata: new Dictionary<string, object?> { ["k"] = "v" });
        var copy = original with { };

        await Assert.That(copy).IsEqualTo(original);
    }
}
