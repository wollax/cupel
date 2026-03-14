using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Scoring;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel;

/// <summary>
/// Fluent builder for <see cref="CupelPipeline"/>.
/// Validates all configuration at <see cref="Build"/> time.
/// Create instances via <see cref="CupelPipeline.CreateBuilder"/>.
/// </summary>
public sealed class PipelineBuilder
{
    internal PipelineBuilder() { }
    private ContextBudget? _budget;
    private IScorer? _scorer;
    private List<(IScorer Scorer, double Weight)>? _scorerEntries;
    private ISlicer? _slicer;
    private IPlacer? _placer;
    private IAsyncSlicer? _asyncSlicer;
    private bool _deduplicationEnabled = true;
    private OverflowStrategy _overflowStrategy = OverflowStrategy.Throw;
    private Action<OverflowEvent>? _overflowObserver;

    /// <summary>
    /// Sets the token budget for the pipeline.
    /// </summary>
    /// <param name="budget">The budget constraint.</param>
    /// <returns>This builder for chaining.</returns>
    public PipelineBuilder WithBudget(ContextBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        _budget = budget;
        return this;
    }

    /// <summary>
    /// Sets a single scorer for the pipeline.
    /// Cannot be combined with <see cref="AddScorer"/>.
    /// </summary>
    /// <param name="scorer">The scorer to use.</param>
    /// <returns>This builder for chaining.</returns>
    public PipelineBuilder WithScorer(IScorer scorer)
    {
        ArgumentNullException.ThrowIfNull(scorer);
        _scorer = scorer;
        return this;
    }

    /// <summary>
    /// Adds a weighted scorer entry. Multiple calls accumulate entries
    /// that are combined into a <see cref="CompositeScorer"/> at build time.
    /// Cannot be combined with <see cref="WithScorer"/>.
    /// </summary>
    /// <param name="scorer">The scorer to add.</param>
    /// <param name="weight">The relative weight for this scorer. Must be finite and greater than zero.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scorer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is not finite or is less than or equal to zero.</exception>
    public PipelineBuilder AddScorer(IScorer scorer, double weight)
    {
        ArgumentNullException.ThrowIfNull(scorer);
        if (!double.IsFinite(weight) || weight <= 0)
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weight must be finite and greater than zero.");

        _scorerEntries ??= [];
        _scorerEntries.Add((scorer, weight));
        return this;
    }

    /// <summary>
    /// Overrides the default slicer (<see cref="GreedySlice"/>).
    /// </summary>
    /// <param name="slicer">The slicer to use.</param>
    /// <returns>This builder for chaining.</returns>
    /// <remarks>
    /// If <see cref="WithQuotas"/> was called before this method, the quota wrapper will be
    /// replaced. Call <see cref="WithQuotas"/> after setting the base slicer.
    /// </remarks>
    public PipelineBuilder WithSlicer(ISlicer slicer)
    {
        ArgumentNullException.ThrowIfNull(slicer);
        _slicer = slicer;
        return this;
    }

    /// <summary>
    /// Overrides the default placer (<see cref="ChronologicalPlacer"/>).
    /// </summary>
    /// <param name="placer">The placer to use.</param>
    /// <returns>This builder for chaining.</returns>
    public PipelineBuilder WithPlacer(IPlacer placer)
    {
        ArgumentNullException.ThrowIfNull(placer);
        _placer = placer;
        return this;
    }

    /// <summary>
    /// Enables or disables content-based deduplication. Enabled by default.
    /// </summary>
    /// <param name="enabled">Whether deduplication is enabled.</param>
    /// <returns>This builder for chaining.</returns>
    public PipelineBuilder WithDeduplication(bool enabled)
    {
        _deduplicationEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Sets <see cref="GreedySlice"/> as the pipeline slicer.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public PipelineBuilder UseGreedySlice()
    {
        _slicer = new GreedySlice();
        return this;
    }

    /// <summary>
    /// Sets <see cref="KnapsackSlice"/> as the pipeline slicer.
    /// </summary>
    /// <param name="bucketSize">Discretization bucket size in tokens. Default is 100.</param>
    /// <returns>This builder for chaining.</returns>
    public PipelineBuilder UseKnapsackSlice(int bucketSize = 100)
    {
        _slicer = new KnapsackSlice(bucketSize);
        return this;
    }

    /// <summary>
    /// Wraps the current slicer (or default <see cref="GreedySlice"/>) in a
    /// <see cref="QuotaSlice"/> decorator with the specified quota configuration.
    /// Ordering matters — this wraps whatever slicer is set at call time.
    /// </summary>
    /// <param name="configure">Action to configure quotas via <see cref="QuotaBuilder"/>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Quota configuration is invalid (e.g. sum of requires exceeds 100%).</exception>
    public PipelineBuilder WithQuotas(Action<QuotaBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new QuotaBuilder();
        configure(builder);
        var quotas = builder.Build();
        var innerSlicer = _slicer ?? new GreedySlice();
        _slicer = new QuotaSlice(innerSlicer, quotas);
        return this;
    }

    /// <summary>
    /// Sets an async slicer for streaming pipeline execution via
    /// <see cref="CupelPipeline.ExecuteStreamAsync"/>.
    /// </summary>
    /// <param name="asyncSlicer">The async slicer to use.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="asyncSlicer"/> is <see langword="null"/>.</exception>
    public PipelineBuilder WithAsyncSlicer(IAsyncSlicer asyncSlicer)
    {
        ArgumentNullException.ThrowIfNull(asyncSlicer);
        _asyncSlicer = asyncSlicer;
        return this;
    }

    /// <summary>
    /// Configures the overflow strategy for when selected items exceed the token budget
    /// after merging pinned and sliced items.
    /// </summary>
    /// <param name="strategy">The overflow strategy to apply.</param>
    /// <param name="onOverflow">Optional callback invoked when <see cref="OverflowStrategy.Proceed"/> is used and overflow occurs.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="strategy"/> is not a defined enum value.</exception>
    public PipelineBuilder WithOverflowStrategy(OverflowStrategy strategy, Action<OverflowEvent>? onOverflow = null)
    {
        if (!Enum.IsDefined(strategy))
            throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Strategy must be a defined OverflowStrategy value.");

        _overflowStrategy = strategy;
        _overflowObserver = onOverflow;
        return this;
    }

    /// <summary>
    /// Validates configuration and creates a <see cref="CupelPipeline"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Budget is missing, scorer is missing, or both scoring paths are used.
    /// </exception>
    public CupelPipeline Build()
    {
        if (_budget is null)
            throw new InvalidOperationException("Budget is required. Call WithBudget().");

        var hasSingleScorer = _scorer is not null;
        var hasCompositeEntries = _scorerEntries is { Count: > 0 };

        if (!hasSingleScorer && !hasCompositeEntries)
            throw new InvalidOperationException("A scorer is required. Call WithScorer() or AddScorer().");

        if (hasSingleScorer && hasCompositeEntries)
            throw new InvalidOperationException("Cannot mix WithScorer() and AddScorer(). Use one scoring path.");

        var scorer = hasSingleScorer
            ? _scorer!
            : new CompositeScorer(_scorerEntries!);

        return new CupelPipeline(
            scorer,
            _slicer ?? new GreedySlice(),
            _placer ?? new ChronologicalPlacer(),
            _budget,
            _deduplicationEnabled,
            _asyncSlicer,
            _overflowStrategy,
            _overflowObserver);
    }
}
