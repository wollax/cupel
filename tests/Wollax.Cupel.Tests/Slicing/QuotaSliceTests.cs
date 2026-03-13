using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Slicing;

public class QuotaSliceTests
{
    private static ContextItem CreateItem(string content, int tokens, ContextKind kind) =>
        new() { Content = content, Tokens = tokens, Kind = kind };

    private static ScoredItem CreateScored(ContextItem item, double score) =>
        new(item, score);

    [Test]
    public async Task EmptyInput_ReturnsEmpty()
    {
        var quotas = new QuotaBuilder()
            .Require(ContextKind.Message, 50)
            .Build();
        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice([], budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RequireEnforcesMinimum()
    {
        // 10 Message items (high score) + 5 Document items (lower score)
        // Without quota, greedy would pick mostly Messages
        // With Require(Document, 30%), Documents must get at least 30% of budget
        var scored = new List<ScoredItem>();
        for (var i = 0; i < 10; i++)
        {
            scored.Add(CreateScored(CreateItem($"msg-{i}", 50, ContextKind.Message), 0.9));
        }
        for (var i = 0; i < 5; i++)
        {
            scored.Add(CreateScored(CreateItem($"doc-{i}", 60, ContextKind.Document), 0.5));
        }

        var quotas = new QuotaBuilder()
            .Require(ContextKind.Document, 30)
            .Build();
        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        var docTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            if (result[i].Kind == ContextKind.Document)
            {
                docTokens += result[i].Tokens;
            }
        }

        // Documents should get at least 30% of 1000 = 300 tokens
        await Assert.That(docTokens).IsGreaterThanOrEqualTo(300);
    }

    [Test]
    public async Task CapEnforcesMaximum()
    {
        // 10 high-scoring Message items + 5 low-scoring Document items
        // Cap(Message, 50%) means Messages get at most 50% of budget
        var scored = new List<ScoredItem>();
        for (var i = 0; i < 10; i++)
        {
            scored.Add(CreateScored(CreateItem($"msg-{i}", 50, ContextKind.Message), 0.9));
        }
        for (var i = 0; i < 5; i++)
        {
            scored.Add(CreateScored(CreateItem($"doc-{i}", 60, ContextKind.Document), 0.5));
        }

        var quotas = new QuotaBuilder()
            .Cap(ContextKind.Message, 50)
            .Build();
        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        var msgTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            if (result[i].Kind == ContextKind.Message)
            {
                msgTokens += result[i].Tokens;
            }
        }

        // Messages should get at most 50% of 1000 = 500 tokens
        await Assert.That(msgTokens).IsLessThanOrEqualTo(500);
    }

    [Test]
    public async Task RequireAndCap_SameKind()
    {
        // Require(Message, 20%) + Cap(Message, 50%)
        // Message tokens should be between 20% and 50% of budget
        var scored = new List<ScoredItem>();
        for (var i = 0; i < 10; i++)
        {
            scored.Add(CreateScored(CreateItem($"msg-{i}", 50, ContextKind.Message), 0.9));
        }
        for (var i = 0; i < 10; i++)
        {
            scored.Add(CreateScored(CreateItem($"doc-{i}", 50, ContextKind.Document), 0.7));
        }

        var quotas = new QuotaBuilder()
            .Require(ContextKind.Message, 20)
            .Cap(ContextKind.Message, 50)
            .Build();
        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        var msgTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            if (result[i].Kind == ContextKind.Message)
            {
                msgTokens += result[i].Tokens;
            }
        }

        await Assert.That(msgTokens).IsGreaterThanOrEqualTo(200);
        await Assert.That(msgTokens).IsLessThanOrEqualTo(500);
    }

    [Test]
    public async Task UnassignedBudget_DistributedByTokenMass()
    {
        // Require(Message, 20%) = 200 tokens minimum
        // Remaining 800 distributed proportional to candidate token mass
        var scored = new List<ScoredItem>();
        for (var i = 0; i < 5; i++)
        {
            scored.Add(CreateScored(CreateItem($"msg-{i}", 100, ContextKind.Message), 0.9));
        }
        for (var i = 0; i < 5; i++)
        {
            scored.Add(CreateScored(CreateItem($"doc-{i}", 100, ContextKind.Document), 0.8));
        }

        var quotas = new QuotaBuilder()
            .Require(ContextKind.Message, 20)
            .Build();
        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        // Both kinds should get items. Message should get at least 200 tokens.
        var msgTokens = 0;
        var docTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            if (result[i].Kind == ContextKind.Message) msgTokens += result[i].Tokens;
            if (result[i].Kind == ContextKind.Document) docTokens += result[i].Tokens;
        }

        await Assert.That(msgTokens).IsGreaterThanOrEqualTo(200);
        await Assert.That(docTokens).IsGreaterThan(0);
    }

    [Test]
    public async Task InsufficientItems_BestEffort()
    {
        // Require(Document, 50%) but only one small Document item
        // Should include the item, no exception, remaining budget used by others
        var scored = new List<ScoredItem>();
        for (var i = 0; i < 10; i++)
        {
            scored.Add(CreateScored(CreateItem($"msg-{i}", 50, ContextKind.Message), 0.9));
        }
        scored.Add(CreateScored(CreateItem("doc-0", 30, ContextKind.Document), 0.8));

        var quotas = new QuotaBuilder()
            .Require(ContextKind.Document, 50)
            .Build();
        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        // The one document should be included
        var hasDoc = false;
        for (var i = 0; i < result.Count; i++)
        {
            if (result[i].Kind == ContextKind.Document)
            {
                hasDoc = true;
            }
        }

        await Assert.That(hasDoc).IsTrue();
        // Messages should also be present (using remaining budget)
        await Assert.That(result.Count).IsGreaterThan(1);
    }

    [Test]
    public async Task NoItemsForRequiredKind_BestEffort()
    {
        // Require(ToolOutput, 20%) but no ToolOutput items exist
        // No exception. Other items fill budget.
        var scored = new List<ScoredItem>();
        for (var i = 0; i < 10; i++)
        {
            scored.Add(CreateScored(CreateItem($"msg-{i}", 50, ContextKind.Message), 0.9));
        }

        var quotas = new QuotaBuilder()
            .Require(ContextKind.ToolOutput, 20)
            .Build();
        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        // Messages should still be included
        await Assert.That(result.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task CapZero_ExcludesKind()
    {
        // Cap(Document, 0) means no Document items in result
        var scored = new List<ScoredItem>();
        for (var i = 0; i < 5; i++)
        {
            scored.Add(CreateScored(CreateItem($"msg-{i}", 50, ContextKind.Message), 0.9));
        }
        for (var i = 0; i < 5; i++)
        {
            scored.Add(CreateScored(CreateItem($"doc-{i}", 50, ContextKind.Document), 0.8));
        }

        var quotas = new QuotaBuilder()
            .Cap(ContextKind.Document, 0)
            .Build();
        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        for (var i = 0; i < result.Count; i++)
        {
            await Assert.That(result[i].Kind).IsNotEqualTo(ContextKind.Document);
        }
    }

    [Test]
    public async Task AllKindsQuotaed_NoUnassigned()
    {
        // Require(Message, 50%) + Require(Document, 50%) = 100% assigned
        var scored = new List<ScoredItem>();
        for (var i = 0; i < 10; i++)
        {
            scored.Add(CreateScored(CreateItem($"msg-{i}", 50, ContextKind.Message), 0.9));
        }
        for (var i = 0; i < 10; i++)
        {
            scored.Add(CreateScored(CreateItem($"doc-{i}", 50, ContextKind.Document), 0.8));
        }

        var quotas = new QuotaBuilder()
            .Require(ContextKind.Message, 50)
            .Require(ContextKind.Document, 50)
            .Build();
        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        var msgTokens = 0;
        var docTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            if (result[i].Kind == ContextKind.Message) msgTokens += result[i].Tokens;
            if (result[i].Kind == ContextKind.Document) docTokens += result[i].Tokens;
        }

        await Assert.That(msgTokens).IsGreaterThanOrEqualTo(500);
        await Assert.That(docTokens).IsGreaterThanOrEqualTo(500);
    }

    [Test]
    public async Task MultipleKinds_ComplexScenario()
    {
        // 3 Kinds with mixed Require/Cap
        // Require(Message, 30%), Cap(Document, 40%), Require(ToolOutput, 10%) + Cap(ToolOutput, 20%)
        var scored = new List<ScoredItem>();
        for (var i = 0; i < 8; i++)
        {
            scored.Add(CreateScored(CreateItem($"msg-{i}", 50, ContextKind.Message), 0.9));
        }
        for (var i = 0; i < 8; i++)
        {
            scored.Add(CreateScored(CreateItem($"doc-{i}", 50, ContextKind.Document), 0.7));
        }
        for (var i = 0; i < 5; i++)
        {
            scored.Add(CreateScored(CreateItem($"tool-{i}", 50, ContextKind.ToolOutput), 0.6));
        }

        var quotas = new QuotaBuilder()
            .Require(ContextKind.Message, 30)
            .Cap(ContextKind.Document, 40)
            .Require(ContextKind.ToolOutput, 10)
            .Cap(ContextKind.ToolOutput, 20)
            .Build();
        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        var msgTokens = 0;
        var docTokens = 0;
        var toolTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            if (result[i].Kind == ContextKind.Message) msgTokens += result[i].Tokens;
            if (result[i].Kind == ContextKind.Document) docTokens += result[i].Tokens;
            if (result[i].Kind == ContextKind.ToolOutput) toolTokens += result[i].Tokens;
        }

        await Assert.That(msgTokens).IsGreaterThanOrEqualTo(300);
        await Assert.That(docTokens).IsLessThanOrEqualTo(400);
        await Assert.That(toolTokens).IsGreaterThanOrEqualTo(100);
        await Assert.That(toolTokens).IsLessThanOrEqualTo(200);
    }

    [Test]
    public async Task UnquotedKind_ReceivesProportionalBudget()
    {
        // Require(Message, 30%). Items include Message and Document kinds.
        // Document has no quota entry. Verify Document items receive proportional share.
        var scored = new List<ScoredItem>();
        for (var i = 0; i < 10; i++)
        {
            scored.Add(CreateScored(CreateItem($"msg-{i}", 50, ContextKind.Message), 0.9));
        }
        for (var i = 0; i < 10; i++)
        {
            scored.Add(CreateScored(CreateItem($"doc-{i}", 50, ContextKind.Document), 0.8));
        }

        var quotas = new QuotaBuilder()
            .Require(ContextKind.Message, 30)
            .Build();
        var slicer = new QuotaSlice(new GreedySlice(), quotas);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        var docTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            if (result[i].Kind == ContextKind.Document)
            {
                docTokens += result[i].Tokens;
            }
        }

        // Document is unconfigured; should receive proportional share of remaining 70%
        // Both kinds have 500 candidate tokens each, so roughly equal share of unassigned
        await Assert.That(docTokens).IsGreaterThan(0);
    }
}
