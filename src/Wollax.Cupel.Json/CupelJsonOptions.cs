using System.Text.Json;

namespace Wollax.Cupel.Json;

/// <summary>
/// Options for configuring JSON serialization behavior, including custom scorer registration.
/// </summary>
public sealed class CupelJsonOptions
{
    private readonly Dictionary<string, Func<JsonElement?, IScorer>> _scorerFactories = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets whether the serialized JSON should be indented for readability.
    /// Defaults to <c>false</c> (compact JSON).
    /// </summary>
    public bool WriteIndented { get; set; }

    /// <summary>
    /// Gets the names of all registered custom scorer types.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredScorerNames => _scorerFactories.Keys;

    /// <summary>
    /// Registers a custom scorer factory by type name.
    /// If a factory with the same name is already registered, it is replaced.
    /// </summary>
    /// <param name="typeName">The type name used in JSON to identify this scorer.</param>
    /// <param name="factory">A factory that creates the scorer instance.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="typeName"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    public CupelJsonOptions RegisterScorer(string typeName, Func<IScorer> factory)
    {
        ValidateTypeName(typeName);
        ArgumentNullException.ThrowIfNull(factory);

        _scorerFactories[typeName] = _ => factory();
        return this;
    }

    /// <summary>
    /// Registers a config-aware custom scorer factory by type name.
    /// The factory receives the optional JSON configuration element from the scorer entry.
    /// If a factory with the same name is already registered, it is replaced.
    /// </summary>
    /// <param name="typeName">The type name used in JSON to identify this scorer.</param>
    /// <param name="factory">A factory that creates the scorer instance from optional JSON configuration.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="typeName"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    public CupelJsonOptions RegisterScorer(string typeName, Func<JsonElement?, IScorer> factory)
    {
        ValidateTypeName(typeName);
        ArgumentNullException.ThrowIfNull(factory);

        _scorerFactories[typeName] = factory;
        return this;
    }

    /// <summary>
    /// Gets the registered factory for the specified custom scorer type name.
    /// </summary>
    /// <param name="typeName">The type name to look up.</param>
    /// <returns>The factory if registered; <c>null</c> otherwise.</returns>
    public Func<JsonElement?, IScorer>? GetScorerFactory(string typeName)
    {
        return _scorerFactories.GetValueOrDefault(typeName);
    }

    /// <summary>
    /// Returns whether a factory has been registered for the specified custom scorer type name.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns><c>true</c> if a factory is registered; <c>false</c> otherwise.</returns>
    public bool HasScorerFactory(string typeName)
    {
        return _scorerFactories.ContainsKey(typeName);
    }

    private static void ValidateTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Type name must not be null, empty, or whitespace.", nameof(typeName));
        }
    }
}
