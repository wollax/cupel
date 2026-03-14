using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Cupel services with <see cref="IServiceCollection"/>.
/// </summary>
public static class CupelServiceCollectionExtensions
{
    /// <summary>
    /// Holds the pre-built pipeline components for singleton sharing across transient pipeline instances.
    /// Components must be stateless and thread-safe. Do not use <see cref="IDisposable"/> implementations
    /// as component types — the container will not dispose them when held as part of this record.
    /// </summary>
    internal sealed record PolicyComponents(
        IScorer Scorer,
        ISlicer Slicer,
        IPlacer Placer,
        IAsyncSlicer? AsyncSlicer,
        bool DeduplicationEnabled,
        OverflowStrategy OverflowStrategy);

    /// <summary>
    /// Registers <see cref="CupelOptions"/> via the <see cref="IOptions{TOptions}"/> pattern.
    /// Call <see cref="AddCupelPipeline"/> after this to register named pipelines.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure <see cref="CupelOptions"/> — typically adds policies via <see cref="CupelOptions.AddPolicy"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddCupel(this IServiceCollection services, Action<CupelOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        return services;
    }

    /// <summary>
    /// Registers a keyed transient <see cref="CupelPipeline"/> for the specified intent.
    /// Pipeline components (scorer, slicer, placer) are built once and shared as singletons
    /// across pipeline instances. Each resolution returns a new pipeline wrapping the shared components.
    /// </summary>
    /// <remarks>
    /// <para>The <paramref name="budget"/> is provided at registration time because token budgets
    /// vary by model and deployment — they are per-pipeline configuration, not part of
    /// <see cref="CupelOptions"/>.</para>
    /// <para>Resolve the pipeline via <c>provider.GetRequiredKeyedService&lt;CupelPipeline&gt;(intent)</c>.</para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="intent">The intent key matching a policy in <see cref="CupelOptions"/>. Must not be null or whitespace.</param>
    /// <param name="budget">The token budget for this pipeline. Must not be null.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="budget"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="intent"/> is null, empty, or whitespace.</exception>
    public static IServiceCollection AddCupelPipeline(this IServiceCollection services, string intent, ContextBudget budget)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(intent);
        ArgumentNullException.ThrowIfNull(budget);

        // Register singleton components — built once on first resolve, cached thereafter.
        // Uses a temporary pipeline build to extract the composed component graph.
        services.AddKeyedSingleton<PolicyComponents>(intent, (provider, _) =>
        {
            var options = provider.GetRequiredService<IOptions<CupelOptions>>().Value;

            if (!options.TryGetPolicy(intent, out var policy))
            {
                throw new InvalidOperationException(
                    $"No policy registered for intent '{intent}'. Call AddCupel(o => o.AddPolicy(\"{intent}\", ...)) before AddCupelPipeline.");
            }

            // Build a temporary pipeline to extract composed components.
            // Budget is needed for Build() validation but components are budget-independent.
            var tempPipeline = CupelPipeline.CreateBuilder()
                .WithPolicy(policy)
                .WithBudget(budget)
                .Build();

            return new PolicyComponents(
                tempPipeline.Scorer,
                tempPipeline.Slicer,
                tempPipeline.Placer,
                tempPipeline.AsyncSlicer,
                tempPipeline.DeduplicationEnabled,
                tempPipeline.OverflowStrategyValue);
        });

        // Register transient pipeline — new instance per resolve, wrapping singleton components.
        services.AddKeyedTransient<CupelPipeline>(intent, (provider, _) =>
        {
            var components = provider.GetRequiredKeyedService<PolicyComponents>(intent);

            // OverflowObserver is intentionally null: CupelPolicy is a declarative data object
            // and does not carry callback delegates. Observer-based overflow handling requires
            // the builder API (WithOverflowStrategy with onOverflow callback).
            return new CupelPipeline(
                components.Scorer,
                components.Slicer,
                components.Placer,
                budget,
                components.DeduplicationEnabled,
                components.AsyncSlicer,
                components.OverflowStrategy);
        });

        return services;
    }

    /// <summary>
    /// Registers <see cref="DiagnosticTraceCollector"/> as a transient <see cref="ITraceCollector"/>.
    /// Each resolution produces a fresh collector instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddCupelTracing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<ITraceCollector, DiagnosticTraceCollector>();
        return services;
    }
}
