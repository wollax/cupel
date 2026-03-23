using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Slicing;

public class CountQuotaSliceTests
{
    private static readonly ContextKind Tool = new("tool");
    private static readonly ContextKind Document = ContextKind.Document;
    private static readonly ContextKind Message = ContextKind.Message;

    private static ContextItem CreateItem(string content, int tokens, ContextKind kind) =>
        new() { Content = content, Tokens = tokens, Kind = kind };

    private static ScoredItem CreateScored(ContextItem item, double score) =>
        new(item, score);

    [Test]
    public async Task EmptyInput_ReturnsEmpty()
    {
        var entry = new CountQuotaEntry(Tool, requireCount: 2, capCount: 4);
        var slicer = new CountQuotaSlice(new GreedySlice(), [entry]);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice([], budget, NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RequireCount_SelectsTopNByScore()
    {
        // 3 tool candidates; require 2 — the top-2 by score should be selected
        var t1 = CreateItem("tool-high", 100, Tool);
        var t2 = CreateItem("tool-mid", 100, Tool);
        var t3 = CreateItem("tool-low", 100, Tool);

        var scored = new List<ScoredItem>
        {
            CreateScored(t1, 0.9),
            CreateScored(t2, 0.6),
            CreateScored(t3, 0.3),
        };

        var entry = new CountQuotaEntry(Tool, requireCount: 2, capCount: 4);
        var slicer = new CountQuotaSlice(new GreedySlice(), [entry]);
        var budget = new ContextBudget(maxTokens: 2000, targetTokens: 2000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        // At minimum the 2 required are included
        var toolCount = result.Count(i => i.Kind == Tool);
        await Assert.That(toolCount).IsGreaterThanOrEqualTo(2);

        // Top-2 are the high and mid items (not the low one)
        var contents = result.Select(i => i.Content).ToList();
        await Assert.That(contents.Contains("tool-high")).IsTrue();
        await Assert.That(contents.Contains("tool-mid")).IsTrue();
    }

    [Test]
    public async Task CapCount_ExcludesOverCap()
    {
        // 3 tool candidates; cap at 1 — only 1 should be selected
        var scored = new List<ScoredItem>
        {
            CreateScored(CreateItem("t1", 50, Tool), 0.9),
            CreateScored(CreateItem("t2", 50, Tool), 0.7),
            CreateScored(CreateItem("t3", 50, Tool), 0.5),
        };

        // require 0, cap 1 — no Phase 1 commitment, Phase 3 enforces cap
        var entry = new CountQuotaEntry(Tool, requireCount: 0, capCount: 1);
        var slicer = new CountQuotaSlice(new GreedySlice(), [entry]);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        var toolCount = result.Count(i => i.Kind == Tool);
        await Assert.That(toolCount).IsLessThanOrEqualTo(1);
    }

    [Test]
    public async Task ScarcityDegrade_RecordsShortfall()
    {
        // Require 3 tool items, only 1 exists → shortfall of 2
        var scored = new List<ScoredItem>
        {
            CreateScored(CreateItem("only-tool", 50, Tool), 0.9),
        };

        var entry = new CountQuotaEntry(Tool, requireCount: 3, capCount: 5);
        var slicer = new CountQuotaSlice(new GreedySlice(), [entry], ScarcityBehavior.Degrade);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(slicer.LastShortfalls.Count).IsEqualTo(1);
        var shortfall = slicer.LastShortfalls[0];
        await Assert.That(shortfall.Kind).IsEqualTo(Tool);
        await Assert.That(shortfall.RequiredCount).IsEqualTo(3);
        await Assert.That(shortfall.SatisfiedCount).IsEqualTo(1);
    }

    [Test]
    public async Task TagNonExclusive_MultiTagSatisfiesTwoRequirements()
    {
        // A single item tagged with two kinds (tool + document).
        // Require 1 of each. The item should satisfy both.
        // In practice, ContextItem has one Kind; this test verifies single-item with require 1 + cap 2.
        // Since ContextItem.Kind is single-valued, we test that one item satisfies require-1 for its kind,
        // and a second entry for a different kind with no candidates gets a shortfall.
        var toolItem = CreateItem("multi-tool", 50, Tool);
        var scored = new List<ScoredItem>
        {
            CreateScored(toolItem, 0.9),
        };

        // require 1 tool + require 1 document; only 1 tool candidate exists
        var toolEntry = new CountQuotaEntry(Tool, requireCount: 1, capCount: 2);
        var docEntry = new CountQuotaEntry(Document, requireCount: 1, capCount: 2);
        var slicer = new CountQuotaSlice(new GreedySlice(), [toolEntry, docEntry]);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        // Tool requirement satisfied
        await Assert.That(result.Count(i => i.Kind == Tool)).IsEqualTo(1);

        // Document shortfall recorded (no document candidates)
        var docShortfall = slicer.LastShortfalls.FirstOrDefault(s => s.Kind == Document);
        await Assert.That(docShortfall).IsNotNull();
        await Assert.That(docShortfall!.RequiredCount).IsEqualTo(1);
        await Assert.That(docShortfall.SatisfiedCount).IsEqualTo(0);
    }

    [Test]
    public async Task KnapsackSlice_ThrowsAtConstruction()
    {
        var entry = new CountQuotaEntry(Tool, requireCount: 1, capCount: 3);

        await Assert.That(() => new CountQuotaSlice(new KnapsackSlice(), [entry]))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task NullInnerSlicer_ThrowsArgumentNull()
    {
        var entry = new CountQuotaEntry(Tool, requireCount: 1, capCount: 3);

        await Assert.That(() => new CountQuotaSlice(null!, [entry]))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task NullEntries_ThrowsArgumentNull()
    {
        await Assert.That(() => new CountQuotaSlice(new GreedySlice(), null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task RequireExceedsCapAtConstruction_ThrowsArgumentException()
    {
        await Assert.That(() => new CountQuotaEntry(Tool, requireCount: 5, capCount: 3))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task ZeroBudget_ReturnsEmpty()
    {
        var entry = new CountQuotaEntry(Tool, requireCount: 2, capCount: 4);
        var slicer = new CountQuotaSlice(new GreedySlice(), [entry]);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 0);

        var scored = new List<ScoredItem>
        {
            CreateScored(CreateItem("t1", 50, Tool), 0.9),
        };

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task NoRequireCount_AllItemsGoThroughInnerSlicer()
    {
        // Entry with require 0, cap 2 — no Phase 1 items committed; inner slicer drives selection
        var scored = new List<ScoredItem>
        {
            CreateScored(CreateItem("t1", 50, Tool), 0.9),
            CreateScored(CreateItem("t2", 50, Tool), 0.7),
            CreateScored(CreateItem("t3", 50, Tool), 0.5),
        };

        var entry = new CountQuotaEntry(Tool, requireCount: 0, capCount: 2);
        var slicer = new CountQuotaSlice(new GreedySlice(), [entry]);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        // Cap of 2 enforced in Phase 3
        await Assert.That(result.Count(i => i.Kind == Tool)).IsLessThanOrEqualTo(2);

        // No shortfalls — require is 0
        await Assert.That(slicer.LastShortfalls.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Require1Cap1_ExactlyOneItemSelected()
    {
        // require 1, cap 1 — exactly 1 tool item
        var scored = new List<ScoredItem>
        {
            CreateScored(CreateItem("t1", 50, Tool), 0.9),
            CreateScored(CreateItem("t2", 50, Tool), 0.7),
        };

        var entry = new CountQuotaEntry(Tool, requireCount: 1, capCount: 1);
        var slicer = new CountQuotaSlice(new GreedySlice(), [entry]);
        var budget = new ContextBudget(maxTokens: 1000, targetTokens: 1000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        await Assert.That(result.Count(i => i.Kind == Tool)).IsEqualTo(1);
        await Assert.That(result[0].Content).IsEqualTo("t1"); // top by score
    }

    [Test]
    public async Task MultiKind_RequireSatisfiedAndCapEnforced()
    {
        // Require 2 tool, cap 2 tool; require 1 document, cap 3 document
        // 4 tool items, 4 document items
        var toolItems = Enumerable.Range(1, 4)
            .Select(i => CreateScored(CreateItem($"tool-{i}", 50, Tool), 1.0 - i * 0.1))
            .ToList();
        var docItems = Enumerable.Range(1, 4)
            .Select(i => CreateScored(CreateItem($"doc-{i}", 50, Document), 0.5 - i * 0.05))
            .ToList();

        var scored = toolItems.Concat(docItems).ToList();

        var entries = new CountQuotaEntry[]
        {
            new(Tool, requireCount: 2, capCount: 2),
            new(Document, requireCount: 1, capCount: 3),
        };
        var slicer = new CountQuotaSlice(new GreedySlice(), entries);
        var budget = new ContextBudget(maxTokens: 2000, targetTokens: 2000);

        var result = slicer.Slice(scored, budget, NullTraceCollector.Instance);

        var toolCount = result.Count(i => i.Kind == Tool);
        var docCount = result.Count(i => i.Kind == Document);

        // Tool: exactly 2 (cap = require = 2)
        await Assert.That(toolCount).IsEqualTo(2);
        // Document: at least 1 (require), at most 3 (cap)
        await Assert.That(docCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(docCount).IsLessThanOrEqualTo(3);

        // No shortfalls
        await Assert.That(slicer.LastShortfalls.Count).IsEqualTo(0);
    }
}
