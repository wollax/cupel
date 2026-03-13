using System.Runtime.CompilerServices;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel.Tests.Slicing;

public class StreamSliceTests
{
    private static ContextItem CreateItem(string content, int tokens) =>
        new() { Content = content, Tokens = tokens };

    private static ScoredItem CreateScored(ContextItem item, double score) =>
        new(item, score);

    private static async IAsyncEnumerable<ScoredItem> ToAsyncEnumerable(
        IEnumerable<ScoredItem> items,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    // Core behavior tests

    [Test]
    public async Task EmptyInput_ReturnsEmpty()
    {
        var slicer = new StreamSlice();
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = await slicer.SliceAsync(
            ToAsyncEnumerable([]),
            budget,
            NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ZeroBudget_ReturnsEmpty()
    {
        var slicer = new StreamSlice();
        var item = CreateItem("hello", 10);
        var budget = new ContextBudget(maxTokens: 0, targetTokens: 0);

        var result = await slicer.SliceAsync(
            ToAsyncEnumerable([CreateScored(item, 1.0)]),
            budget,
            NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SingleItem_FitsInBudget_ReturnsItem()
    {
        var slicer = new StreamSlice();
        var item = CreateItem("hello", 10);
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = await slicer.SliceAsync(
            ToAsyncEnumerable([CreateScored(item, 0.8)]),
            budget,
            NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(item);
    }

    [Test]
    public async Task SingleItem_ExceedsBudget_ReturnsEmpty()
    {
        var slicer = new StreamSlice();
        var item = CreateItem("hello", 60);
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = await slicer.SliceAsync(
            ToAsyncEnumerable([CreateScored(item, 0.8)]),
            budget,
            NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task FillsBudget_StopsConsuming()
    {
        var slicer = new StreamSlice(batchSize: 2);
        // 100 items of 10 tokens each, budget=50 -> should select 5 and stop consuming
        var items = new List<ScoredItem>();
        for (var i = 0; i < 100; i++)
        {
            items.Add(CreateScored(CreateItem($"item-{i}", 10), 0.5));
        }

        var yieldedCount = 0;
        async IAsyncEnumerable<ScoredItem> CountingEnumerable(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                yieldedCount++;
                yield return item;
            }
        }

        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = await slicer.SliceAsync(
            CountingEnumerable(),
            budget,
            NullTraceCollector.Instance);

        var totalTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            totalTokens += result[i].Tokens;
        }

        await Assert.That(totalTokens).IsLessThanOrEqualTo(50);
        // Should NOT have consumed all 100 items
        await Assert.That(yieldedCount).IsLessThan(100);
    }

    [Test]
    public async Task ProcessesWithinBatchByScore()
    {
        // Items arrive A(tokens=30, score=0.3), B(tokens=30, score=0.9), C(tokens=30, score=0.5)
        // Single batch (batchSize >= 3). Budget fits 2 items (budget=60).
        // Within-batch greedy-by-score: sorted desc -> B(0.9), C(0.5), A(0.3)
        // B selected (30 tokens), C selected (30 tokens) = 60. A skipped.
        var slicer = new StreamSlice(batchSize: 32);
        var itemA = CreateItem("A", 30);
        var itemB = CreateItem("B", 30);
        var itemC = CreateItem("C", 30);

        var scored = new List<ScoredItem>
        {
            CreateScored(itemA, 0.3),
            CreateScored(itemB, 0.9),
            CreateScored(itemC, 0.5)
        };

        var budget = new ContextBudget(maxTokens: 100, targetTokens: 60);

        var result = await slicer.SliceAsync(
            ToAsyncEnumerable(scored),
            budget,
            NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(2);

        var containsB = false;
        var containsC = false;
        for (var i = 0; i < result.Count; i++)
        {
            if (ReferenceEquals(result[i], itemB)) containsB = true;
            if (ReferenceEquals(result[i], itemC)) containsC = true;
        }

        await Assert.That(containsB).IsTrue();
        await Assert.That(containsC).IsTrue();
    }

    [Test]
    public async Task ZeroTokenItems_AlwaysIncluded()
    {
        var slicer = new StreamSlice();
        var zeroItem = CreateItem("zero", 0);
        var normalItem = CreateItem("normal", 10);
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = await slicer.SliceAsync(
            ToAsyncEnumerable([CreateScored(zeroItem, 0.1), CreateScored(normalItem, 0.5)]),
            budget,
            NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(2);
        var containsZero = false;
        for (var i = 0; i < result.Count; i++)
        {
            if (ReferenceEquals(result[i], zeroItem))
            {
                containsZero = true;
            }
        }
        await Assert.That(containsZero).IsTrue();
    }

    // Batching tests

    [Test]
    public async Task DefaultBatchSize_Is32()
    {
        var slicer = new StreamSlice();
        await Assert.That(slicer.BatchSize).IsEqualTo(32);
    }

    [Test]
    public async Task DefaultBatchSize_ProcessesAllItems()
    {
        var slicer = new StreamSlice();
        // 64 items of 1 token each, budget=64 -> all should be selected
        var items = new List<ScoredItem>();
        for (var i = 0; i < 64; i++)
        {
            items.Add(CreateScored(CreateItem($"item-{i}", 1), 0.5));
        }

        var budget = new ContextBudget(maxTokens: 100, targetTokens: 64);

        var result = await slicer.SliceAsync(
            ToAsyncEnumerable(items),
            budget,
            NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(64);
    }

    [Test]
    public async Task CustomBatchSize()
    {
        var slicer = new StreamSlice(batchSize: 4);
        // 8 items of 5 tokens each, budget=40 -> all fit
        var items = new List<ScoredItem>();
        for (var i = 0; i < 8; i++)
        {
            items.Add(CreateScored(CreateItem($"item-{i}", 5), 0.5));
        }

        var budget = new ContextBudget(maxTokens: 100, targetTokens: 40);

        var result = await slicer.SliceAsync(
            ToAsyncEnumerable(items),
            budget,
            NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(8);
    }

    [Test]
    public async Task PartialBatch_Processed()
    {
        var slicer = new StreamSlice(batchSize: 32);
        // 5 items with batchSize=32 -> partial batch, all should be included
        var items = new List<ScoredItem>();
        for (var i = 0; i < 5; i++)
        {
            items.Add(CreateScored(CreateItem($"item-{i}", 5), 0.5));
        }

        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        var result = await slicer.SliceAsync(
            ToAsyncEnumerable(items),
            budget,
            NullTraceCollector.Instance);

        await Assert.That(result.Count).IsEqualTo(5);
    }

    // Cancellation tests

    [Test]
    public async Task CancellationRequested_StopsEarly()
    {
        var slicer = new StreamSlice();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        var items = new List<ScoredItem>
        {
            CreateScored(CreateItem("item", 10), 0.5)
        };

        var budget = new ContextBudget(maxTokens: 100, targetTokens: 50);

        await Assert.That(async () => await slicer.SliceAsync(
            ToAsyncEnumerable(items),
            budget,
            NullTraceCollector.Instance,
            cts.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task BudgetFull_CancelsCts()
    {
        var slicer = new StreamSlice(batchSize: 2);

        var yieldedCount = 0;
        async IAsyncEnumerable<ScoredItem> CountingEnumerable(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            for (var i = 0; i < 100; i++)
            {
                ct.ThrowIfCancellationRequested();
                yieldedCount++;
                yield return CreateScored(CreateItem($"item-{i}", 10), 0.5);
            }
        }

        // Budget=30 -> fits 3 items of 10 tokens each
        var budget = new ContextBudget(maxTokens: 100, targetTokens: 30);

        var result = await slicer.SliceAsync(
            CountingEnumerable(),
            budget,
            NullTraceCollector.Instance);

        var totalTokens = 0;
        for (var i = 0; i < result.Count; i++)
        {
            totalTokens += result[i].Tokens;
        }

        await Assert.That(totalTokens).IsLessThanOrEqualTo(30);
        // Should not have consumed all 100 items
        await Assert.That(yieldedCount).IsLessThan(100);
    }

    // Constructor validation tests

    [Test]
    public async Task Constructor_BatchSizeZero_Throws()
    {
        await Assert.That(() => new StreamSlice(batchSize: 0))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Constructor_BatchSizeNegative_Throws()
    {
        await Assert.That(() => new StreamSlice(batchSize: -1))
            .Throws<ArgumentOutOfRangeException>();
    }
}
