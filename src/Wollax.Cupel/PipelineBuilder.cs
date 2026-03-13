using Wollax.Cupel.Scoring;

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
    private bool _deduplicationEnabled = true;

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
            _deduplicationEnabled);
    }
}
