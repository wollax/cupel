using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Scoring;

namespace Wollax.Cupel.Tests.Scoring;

public class KindScorerTests
{
    private static ContextItem CreateItem(
        string content = "test",
        int tokens = 10,
        ContextKind? kind = null) =>
        new() { Content = content, Tokens = tokens, Kind = kind ?? ContextKind.Message };

    private readonly KindScorer _scorer = new();

    [Test]
    public async Task DefaultWeights_SystemPrompt_ReturnsOne()
    {
        var item = CreateItem(kind: ContextKind.SystemPrompt);
        var allItems = new List<ContextItem> { item };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(1.0);
    }

    [Test]
    public async Task DefaultWeights_Message_ReturnsPointTwo()
    {
        var item = CreateItem(kind: ContextKind.Message);
        var allItems = new List<ContextItem> { item };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.2);
    }

    [Test]
    public async Task DefaultWeights_Ordinal()
    {
        var systemPrompt = CreateItem(content: "sp", kind: ContextKind.SystemPrompt);
        var memory = CreateItem(content: "mem", kind: ContextKind.Memory);
        var toolOutput = CreateItem(content: "tool", kind: ContextKind.ToolOutput);
        var document = CreateItem(content: "doc", kind: ContextKind.Document);
        var message = CreateItem(content: "msg", kind: ContextKind.Message);
        var allItems = new List<ContextItem> { systemPrompt, memory, toolOutput, document, message };

        var spScore = _scorer.Score(systemPrompt, allItems);
        var memScore = _scorer.Score(memory, allItems);
        var toolScore = _scorer.Score(toolOutput, allItems);
        var docScore = _scorer.Score(document, allItems);
        var msgScore = _scorer.Score(message, allItems);

        await Assert.That(spScore).IsGreaterThan(memScore);
        await Assert.That(memScore).IsGreaterThan(toolScore);
        await Assert.That(toolScore).IsGreaterThan(docScore);
        await Assert.That(docScore).IsGreaterThan(msgScore);
    }

    [Test]
    public async Task UnknownKind_ReturnsZero()
    {
        var item = CreateItem(kind: new ContextKind("CustomUnknown"));
        var allItems = new List<ContextItem> { item };

        var score = _scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.0);
    }

    [Test]
    public async Task CustomWeightMap_OverridesDefaults()
    {
        var customWeights = new Dictionary<ContextKind, double>
        {
            [ContextKind.Message] = 0.9,
            [ContextKind.Document] = 0.1
        };
        var scorer = new KindScorer(customWeights);
        var item = CreateItem(kind: ContextKind.Message);
        var allItems = new List<ContextItem> { item };

        var score = scorer.Score(item, allItems);

        await Assert.That(score).IsEqualTo(0.9);
    }

    [Test]
    public async Task IgnoresAllItems()
    {
        var item = CreateItem(kind: ContextKind.Memory);
        var allItems1 = new List<ContextItem> { item };
        var allItems2 = new List<ContextItem>
        {
            item,
            CreateItem(content: "other", kind: ContextKind.SystemPrompt),
            CreateItem(content: "another", kind: ContextKind.Document)
        };

        var score1 = _scorer.Score(item, allItems1);
        var score2 = _scorer.Score(item, allItems2);

        await Assert.That(score1).IsEqualTo(score2);
    }
}
