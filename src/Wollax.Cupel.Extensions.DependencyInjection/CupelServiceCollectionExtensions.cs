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
    /// The pipeline is built from the policy registered in <see cref="CupelOptions"/> matching
    /// the <paramref name="intent"/> key, combined with the provided <paramref name="budget"/>.
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

        services.AddKeyedTransient<CupelPipeline>(intent, (provider, _) =>
        {
            var options = provider.GetRequiredService<IOptions<CupelOptions>>().Value;
            var policy = options.GetPolicy(intent);
            return CupelPipeline.CreateBuilder()
                .WithPolicy(policy)
                .WithBudget(budget)
                .Build();
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
