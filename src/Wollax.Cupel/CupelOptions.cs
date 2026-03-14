using System.Diagnostics.CodeAnalysis;

namespace Wollax.Cupel;

/// <summary>
/// A runtime registry for intent-based policy lookup.
/// Stores <see cref="CupelPolicy"/> instances keyed by case-insensitive intent strings.
/// </summary>
public sealed class CupelOptions
{
    private readonly Dictionary<string, CupelPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a policy for the specified intent. Overwrites any existing policy for that intent.
    /// </summary>
    /// <param name="intent">The intent key — must not be null or whitespace.</param>
    /// <param name="policy">The policy to register — must not be null.</param>
    /// <returns>This instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="intent"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
    public CupelOptions AddPolicy(string intent, CupelPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intent);
        ArgumentNullException.ThrowIfNull(policy);

        _policies[intent] = policy;
        return this;
    }

    /// <summary>
    /// Retrieves the policy registered for the specified intent.
    /// </summary>
    /// <param name="intent">The intent key — must not be null or whitespace.</param>
    /// <returns>The registered <see cref="CupelPolicy"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="intent"/> is null or whitespace.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no policy is registered for the intent.</exception>
    public CupelPolicy GetPolicy(string intent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intent);

        if (!_policies.TryGetValue(intent, out var policy))
        {
            throw new KeyNotFoundException($"No policy registered for intent '{intent}'.");
        }

        return policy;
    }

    /// <summary>
    /// Attempts to retrieve the policy registered for the specified intent.
    /// </summary>
    /// <param name="intent">The intent key — must not be null or whitespace.</param>
    /// <param name="policy">When this method returns true, contains the registered policy; otherwise null.</param>
    /// <returns>True if a policy was found for the intent; otherwise false.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="intent"/> is null or whitespace.</exception>
    public bool TryGetPolicy(string intent, [NotNullWhen(true)] out CupelPolicy? policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intent);

        return _policies.TryGetValue(intent, out policy);
    }
}
