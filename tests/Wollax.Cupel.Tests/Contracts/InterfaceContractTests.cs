using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Tests.Contracts;

public class InterfaceContractTests
{
    private static ContextItem CreateItem(string content = "test", int tokens = 10) =>
        new() { Content = content, Tokens = tokens };

    #region IScorer Contract

    [Test]
    public async Task IScorer_Score_ReturnsSynchronousDouble()
    {
        IScorer scorer = new StubScorer();
        var item = CreateItem();
        var allItems = new List<ContextItem> { item };

        double result = scorer.Score(item, allItems);

        await Assert.That(result).IsEqualTo(0.5);
    }

    [Test]
    public async Task IScorer_Score_AcceptsItemAndCandidateSet()
    {
        IScorer scorer = new StubScorer();
        var item = CreateItem("target");
        var allItems = new List<ContextItem>
        {
            CreateItem("first"),
            item,
            CreateItem("third"),
        };

        double result = scorer.Score(item, allItems);

        await Assert.That(result).IsEqualTo(0.5);
    }

    [Test]
    public async Task IScorer_Score_IsNotAsync()
    {
        var method = typeof(IScorer).GetMethod(nameof(IScorer.Score));

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(double));
    }

    #endregion

    #region ISlicer Contract

    [Test]
    public async Task ISlicer_Slice_AcceptsScoredItemsBudgetAndTraceCollector()
    {
        ISlicer slicer = new StubSlicer();
        var item = CreateItem();
        var scoredItems = new List<ScoredItem> { new(item, 0.9) };
        var budget = new ContextBudget(1000, 800);
        ITraceCollector trace = NullTraceCollector.Instance;

        IReadOnlyList<ContextItem> result = slicer.Slice(scoredItems, budget, trace);

        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(item);
    }

    [Test]
    public async Task ISlicer_Slice_IsSynchronous()
    {
        var method = typeof(ISlicer).GetMethod(nameof(ISlicer.Slice));

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(IReadOnlyList<ContextItem>));
    }

    #endregion

    #region IPlacer Contract

    [Test]
    public async Task IPlacer_Place_AcceptsScoredItemsAndTraceCollector()
    {
        IPlacer placer = new StubPlacer();
        var item = CreateItem();
        var scoredItems = new List<ScoredItem> { new(item, 0.9) };
        ITraceCollector trace = NullTraceCollector.Instance;

        IReadOnlyList<ContextItem> result = placer.Place(scoredItems, trace);

        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(item);
    }

    [Test]
    public async Task IPlacer_Place_IsSynchronous()
    {
        var method = typeof(IPlacer).GetMethod(nameof(IPlacer.Place));

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(IReadOnlyList<ContextItem>));
    }

    #endregion

    #region IContextSource Contract

    [Test]
    public async Task IContextSource_GetItemsAsync_ReturnsBatch()
    {
        IContextSource source = new StubContextSource();

        Task<IReadOnlyList<ContextItem>> task = source.GetItemsAsync();
        IReadOnlyList<ContextItem> result = await task;

        await Assert.That(result).Count().IsEqualTo(1);
    }

    [Test]
    public async Task IContextSource_GetItemsAsync_AcceptsCancellationToken()
    {
        IContextSource source = new StubContextSource();
        using var cts = new CancellationTokenSource();

        IReadOnlyList<ContextItem> result = await source.GetItemsAsync(cts.Token);

        await Assert.That(result).Count().IsEqualTo(1);
    }

    [Test]
    public async Task IContextSource_GetItemsStreamAsync_ReturnsAsyncEnumerable()
    {
        IContextSource source = new StubContextSource();
        var items = new List<ContextItem>();

        await foreach (var item in source.GetItemsStreamAsync())
        {
            items.Add(item);
        }

        await Assert.That(items).Count().IsEqualTo(1);
    }

    [Test]
    public async Task IContextSource_GetItemsStreamAsync_AcceptsCancellationToken()
    {
        IContextSource source = new StubContextSource();
        var items = new List<ContextItem>();
        using var cts = new CancellationTokenSource();

        await foreach (var item in source.GetItemsStreamAsync(cts.Token))
        {
            items.Add(item);
        }

        await Assert.That(items).Count().IsEqualTo(1);
    }

    #endregion

    #region Stub Implementations

    private sealed class StubScorer : IScorer
    {
        public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems) => 0.5;
    }

    private sealed class StubSlicer : ISlicer
    {
        public IReadOnlyList<ContextItem> Slice(
            IReadOnlyList<ScoredItem> scoredItems,
            ContextBudget budget,
            ITraceCollector traceCollector) =>
            scoredItems.Select(s => s.Item).ToList();
    }

    private sealed class StubPlacer : IPlacer
    {
        public IReadOnlyList<ContextItem> Place(
            IReadOnlyList<ScoredItem> items,
            ITraceCollector traceCollector) =>
            items.Select(s => s.Item).ToList();
    }

    private sealed class StubContextSource : IContextSource
    {
        private static readonly ContextItem TestItem = new() { Content = "test", Tokens = 5 };

        public Task<IReadOnlyList<ContextItem>> GetItemsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContextItem>>(new[] { TestItem });

        public async IAsyncEnumerable<ContextItem> GetItemsStreamAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return TestItem;
        }
    }

    #endregion
}
