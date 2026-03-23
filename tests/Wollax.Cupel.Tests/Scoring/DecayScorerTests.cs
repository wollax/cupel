using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Scoring;

/// <summary>
/// Unit tests covering the 5 DecayScorer conformance scenarios.
/// </summary>
public class DecayScorerTests
{
    /// <summary>
    /// A test-only <see cref="TimeProvider"/> that returns a fixed point in time.
    /// </summary>
    private sealed class FakeTimeProvider(DateTimeOffset fixedNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => fixedNow;
    }

    private static ContextItem CreateItem(DateTimeOffset? timestamp = null) =>
        new()
        {
            Content = "test",
            Tokens = 10,
            Timestamp = timestamp,
        };

    private static readonly ContextItem[] NoItems = [];

    /// <summary>
    /// Conformance: Exponential half-life — age = 24h, halfLife = 24h → score ≈ 0.5.
    /// </summary>
    [Test]
    public async Task Exponential_HalfLifeAge_ReturnsHalfScore()
    {
        var referenceTime = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var itemTime = referenceTime - TimeSpan.FromHours(24);
        var timeProvider = new FakeTimeProvider(referenceTime);
        var curve = new DecayCurve.Exponential(TimeSpan.FromHours(24));
        var scorer = new DecayScorer(timeProvider, curve);

        var item = CreateItem(timestamp: itemTime);
        var score = scorer.Score(item, NoItems);

        await Assert.That(score).IsEqualTo(0.5).Within(1e-9);
    }

    /// <summary>
    /// Conformance: Future-dated item — timestamp > referenceTime → score = 1.0.
    /// </summary>
    [Test]
    public async Task Exponential_FutureDatedItem_ReturnsOnePointZero()
    {
        var referenceTime = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var itemTime = referenceTime + TimeSpan.FromHours(1); // in the future
        var timeProvider = new FakeTimeProvider(referenceTime);
        var curve = new DecayCurve.Exponential(TimeSpan.FromHours(24));
        var scorer = new DecayScorer(timeProvider, curve);

        var item = CreateItem(timestamp: itemTime);
        var score = scorer.Score(item, NoItems);

        // Age is clamped to 0 → 2^0 = 1.0
        await Assert.That(score).IsEqualTo(1.0).Within(1e-9);
    }

    /// <summary>
    /// Conformance: Null timestamp — no timestamp → score = nullTimestampScore (0.5).
    /// </summary>
    [Test]
    public async Task NullTimestamp_ReturnsNullTimestampScore()
    {
        var referenceTime = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(referenceTime);
        var curve = new DecayCurve.Exponential(TimeSpan.FromHours(24));
        var scorer = new DecayScorer(timeProvider, curve, nullTimestampScore: 0.5);

        var item = CreateItem(timestamp: null);
        var score = scorer.Score(item, NoItems);

        await Assert.That(score).IsEqualTo(0.5);
    }

    /// <summary>
    /// Conformance: Step second window — age = 6h, windows = [(1h,0.9),(24h,0.5),(72h,0.1)] → score = 0.5.
    /// </summary>
    [Test]
    public async Task Step_SecondWindow_ReturnsCorrectScore()
    {
        var referenceTime = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var itemTime = referenceTime - TimeSpan.FromHours(6);
        var timeProvider = new FakeTimeProvider(referenceTime);

        var windows = new List<(TimeSpan MaxAge, double Score)>
        {
            (TimeSpan.FromHours(1), 0.9),
            (TimeSpan.FromHours(24), 0.5),
            (TimeSpan.FromHours(72), 0.1),
        };
        var curve = new DecayCurve.Step(windows);
        var scorer = new DecayScorer(timeProvider, curve);

        var item = CreateItem(timestamp: itemTime);
        var score = scorer.Score(item, NoItems);

        // age=6h is not < 1h, but is < 24h → score = 0.5
        await Assert.That(score).IsEqualTo(0.5).Within(1e-9);
    }

    /// <summary>
    /// Conformance: Window at boundary — age = 6h, maxAge = 6h → score = 0.0.
    /// </summary>
    [Test]
    public async Task Window_AtExactBoundary_ReturnsZero()
    {
        var referenceTime = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var itemTime = referenceTime - TimeSpan.FromHours(6);
        var timeProvider = new FakeTimeProvider(referenceTime);
        var curve = new DecayCurve.Window(TimeSpan.FromHours(6));
        var scorer = new DecayScorer(timeProvider, curve);

        var item = CreateItem(timestamp: itemTime);
        var score = scorer.Score(item, NoItems);

        // age == maxAge → not strictly less than → 0.0
        await Assert.That(score).IsEqualTo(0.0).Within(1e-9);
    }
}
