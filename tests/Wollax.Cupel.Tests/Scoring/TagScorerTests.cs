using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Scoring;

public class TagScorerTests
{
    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10,
        IReadOnlyList<string>? tags = null) =>
        new() { Content = content, Tokens = tokens, Tags = tags ?? [] };

    private static readonly Dictionary<string, double> DefaultTagWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["important"] = 1.0,
        ["relevant"] = 0.5,
        ["minor"] = 0.2
    };

    [Test]
    public async Task EmptyTags_ReturnsZero()
    {
        var scorer = new TagScorer(DefaultTagWeights);
        var item = CreateItem();
        var allItems = new List<ContextItem> { item };

        var score = scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task SingleMatchingTag_ReturnsNormalized()
    {
        var scorer = new TagScorer(DefaultTagWeights);
        var item = CreateItem(tags: ["important"]);
        var allItems = new List<ContextItem> { item };

        var score = scorer.Score(item, allItems);

        // important=1.0, total=1.0+0.5+0.2=1.7, score=1.0/1.7
        var expected = 1.0 / 1.7;
        await Assert.That(score).IsEqualTo(expected).Within(0.0001);
    }

    [Test]
    public async Task MultipleMatchingTags_SumsNormalized()
    {
        var scorer = new TagScorer(DefaultTagWeights);
        var item = CreateItem(tags: ["important", "relevant"]);
        var allItems = new List<ContextItem> { item };

        var score = scorer.Score(item, allItems);

        // (1.0 + 0.5) / 1.7
        var expected = 1.5 / 1.7;
        await Assert.That(score).IsEqualTo(expected).Within(0.0001);
    }

    [Test]
    public async Task NoMatchingTags_ReturnsZero()
    {
        var scorer = new TagScorer(DefaultTagWeights);
        var item = CreateItem(tags: ["unknown-tag"]);
        var allItems = new List<ContextItem> { item };

        var score = scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task AllTagsMatch_ReturnsOne()
    {
        var scorer = new TagScorer(DefaultTagWeights);
        var item = CreateItem(tags: ["important", "relevant", "minor"]);
        var allItems = new List<ContextItem> { item };

        var score = scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(1.0).Within(0.0001);
    }

    [Test]
    public async Task PartialMatch_ScoresLower_ThanFullMatch()
    {
        var scorer = new TagScorer(DefaultTagWeights);
        var partial = CreateItem(content: "partial", tags: ["important"]);
        var full = CreateItem(content: "full", tags: ["important", "relevant", "minor"]);
        var allItems = new List<ContextItem> { partial, full };

        var partialScore = scorer.Score(partial, allItems);
        var fullScore = scorer.Score(full, allItems);

        await Assert.That(partialScore).IsLessThan(fullScore);
    }

    [Test]
    public async Task UnknownTagsIgnored()
    {
        var scorer = new TagScorer(DefaultTagWeights);
        var withUnknown = CreateItem(content: "with-unknown", tags: ["important", "unknown-tag"]);
        var withoutUnknown = CreateItem(content: "without-unknown", tags: ["important"]);
        var allItems = new List<ContextItem> { withUnknown, withoutUnknown };

        var withUnknownScore = scorer.Score(withUnknown, allItems);
        var withoutUnknownScore = scorer.Score(withoutUnknown, allItems);

        // Unknown tags don't add to score, so both should be equal
        await Assert.That(withUnknownScore).IsEqualTo(withoutUnknownScore);
    }

    [Test]
    public async Task Constructor_RequiresWeightMap()
    {
        // TagScorer has no parameterless constructor — this test verifies
        // that TagScorer requires an IReadOnlyDictionary<string, double>
        var type = typeof(TagScorer);
        var parameterlessCtor = type.GetConstructor(Type.EmptyTypes);

        await Assert.That(parameterlessCtor).IsNull();
    }
}
