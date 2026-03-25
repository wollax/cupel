using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Scoring;

/// <summary>
/// Conformance tests for MetadataKeyScorer covering the 5 spec vector scenarios.
/// </summary>
public class MetadataKeyScorerTests
{
    private static ContextItem CreateItem(IReadOnlyDictionary<string, object?> metadata) =>
        new()
        {
            Content = "test",
            Tokens = 10,
            Metadata = metadata,
        };

    private static ContextItem CreateItemNoMeta() =>
        new()
        {
            Content = "test",
            Tokens = 10,
            Metadata = new Dictionary<string, object?>(),
        };

    private static readonly IReadOnlyList<ContextItem> NoItems = [];

    /// <summary>
    /// Conformance vector 1: key matches configured value → score equals boost.
    /// </summary>
    [Test]
    public async Task Match_ReturnsBoost()
    {
        var scorer = new MetadataKeyScorer("cupel:priority", "high", 1.5);
        var item = CreateItem(new Dictionary<string, object?> { ["cupel:priority"] = "high" });

        var score = scorer.Score(item, NoItems);

        await Assert.That(score).IsEqualTo(1.5).Within(1e-10);
    }

    /// <summary>
    /// Conformance vector 2: key present but value does not match → score equals 1.0 (neutral).
    /// </summary>
    [Test]
    public async Task ValueMismatch_ReturnsNeutral()
    {
        var scorer = new MetadataKeyScorer("cupel:priority", "high", 1.5);
        var item = CreateItem(new Dictionary<string, object?> { ["cupel:priority"] = "normal" });

        var score = scorer.Score(item, NoItems);

        await Assert.That(score).IsEqualTo(1.0).Within(1e-10);
    }

    /// <summary>
    /// Conformance vector 3: configured key absent from metadata → score equals 1.0 (neutral).
    /// </summary>
    [Test]
    public async Task KeyAbsent_ReturnsNeutral()
    {
        var scorer = new MetadataKeyScorer("cupel:priority", "high", 1.5);
        var item = CreateItemNoMeta();

        var score = scorer.Score(item, NoItems);

        await Assert.That(score).IsEqualTo(1.0).Within(1e-10);
    }

    /// <summary>
    /// Conformance vector 4: boost = 0.0 → construction error (ArgumentException).
    /// </summary>
    [Test]
    public async Task ZeroBoost_ThrowsArgumentException()
    {
        await Assert.That(() => new MetadataKeyScorer("cupel:priority", "high", 0.0))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Conformance vector 5: boost &lt; 0.0 → construction error (ArgumentException).
    /// </summary>
    [Test]
    public async Task NegativeBoost_ThrowsArgumentException()
    {
        await Assert.That(() => new MetadataKeyScorer("cupel:priority", "high", -1.0))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// D178: NaN boost → construction error (ArgumentException, not ArgumentOutOfRangeException).
    /// </summary>
    [Test]
    public async Task NanBoost_ThrowsArgumentException()
    {
        await Assert.That(() => new MetadataKeyScorer("cupel:priority", "high", double.NaN))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// D178: Infinite boost → construction error (ArgumentException).
    /// </summary>
    [Test]
    public async Task InfiniteBoost_ThrowsArgumentException()
    {
        await Assert.That(() => new MetadataKeyScorer("cupel:priority", "high", double.PositiveInfinity))
            .Throws<ArgumentException>();
    }
}
