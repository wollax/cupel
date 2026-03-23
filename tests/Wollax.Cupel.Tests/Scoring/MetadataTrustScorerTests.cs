using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Scoring;

/// <summary>
/// Unit tests covering the MetadataTrustScorer conformance scenarios plus D059 dual-type dispatch.
/// </summary>
public class MetadataTrustScorerTests
{
    private static ContextItem CreateItem(IReadOnlyDictionary<string, object?> metadata) =>
        new()
        {
            Content = "test",
            Tokens = 10,
            Metadata = metadata,
        };

    private static readonly IReadOnlyList<ContextItem> NoItems = [];

    /// <summary>
    /// Conformance: cupel:trust present and valid string → clamped value returned.
    /// </summary>
    [Test]
    public async Task PresentAndValid_ReturnsClampedValue()
    {
        var scorer = new MetadataTrustScorer();
        var item = CreateItem(new Dictionary<string, object?> { ["cupel:trust"] = "0.85" });

        var score = scorer.Score(item, NoItems);

        await Assert.That(score).IsEqualTo(0.85).Within(1e-10);
    }

    /// <summary>
    /// Conformance: cupel:trust key absent → defaultScore returned.
    /// </summary>
    [Test]
    public async Task KeyAbsent_ReturnsDefaultScore()
    {
        var scorer = new MetadataTrustScorer();
        var item = CreateItem(new Dictionary<string, object?>());

        var score = scorer.Score(item, NoItems);

        await Assert.That(score).IsEqualTo(0.5).Within(1e-10);
    }

    /// <summary>
    /// Conformance: cupel:trust value is an unparseable string → defaultScore returned.
    /// </summary>
    [Test]
    public async Task UnparseableValue_ReturnsDefaultScore()
    {
        var scorer = new MetadataTrustScorer();
        var item = CreateItem(new Dictionary<string, object?> { ["cupel:trust"] = "high" });

        var score = scorer.Score(item, NoItems);

        await Assert.That(score).IsEqualTo(0.5).Within(1e-10);
    }

    /// <summary>
    /// Conformance: cupel:trust value exceeds 1.0 → clamped to 1.0.
    /// </summary>
    [Test]
    public async Task OutOfRangeHigh_ReturnsClamped()
    {
        var scorer = new MetadataTrustScorer();
        var item = CreateItem(new Dictionary<string, object?> { ["cupel:trust"] = "1.5" });

        var score = scorer.Score(item, NoItems);

        await Assert.That(score).IsEqualTo(1.0).Within(1e-10);
    }

    /// <summary>
    /// Conformance: cupel:trust is "NaN" → non-finite after parse → defaultScore returned.
    /// </summary>
    [Test]
    public async Task NonFiniteNaN_ReturnsDefaultScore()
    {
        var scorer = new MetadataTrustScorer();
        var item = CreateItem(new Dictionary<string, object?> { ["cupel:trust"] = "NaN" });

        var score = scorer.Score(item, NoItems);

        await Assert.That(score).IsEqualTo(0.5).Within(1e-10);
    }

    /// <summary>
    /// D059: cupel:trust is a native double (not a string) → accepted directly, no defaultScore.
    /// Verifies that the double branch runs before the string branch.
    /// </summary>
    [Test]
    public async Task NativeDoubleValue_AcceptedDirectly()
    {
        var scorer = new MetadataTrustScorer();
        var item = CreateItem(new Dictionary<string, object?> { ["cupel:trust"] = (object?)0.75 });

        var score = scorer.Score(item, NoItems);

        await Assert.That(score).IsEqualTo(0.75).Within(1e-10);
    }
}
