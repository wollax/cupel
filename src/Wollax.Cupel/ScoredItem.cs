namespace Wollax.Cupel;

/// <summary>
/// Pairs a context item with its computed relevance score.
/// </summary>
/// <param name="Item">The context item.</param>
/// <param name="Score">The computed relevance score, conventionally 0.0–1.0.</param>
/// <remarks>
/// As a <c>readonly record struct</c>, C# generates a public parameterless constructor
/// that initialises <see cref="Item"/> to <see langword="null"/>. Do not use the
/// parameterless constructor; always supply both <paramref name="Item"/> and
/// <paramref name="Score"/>.
/// </remarks>
public readonly record struct ScoredItem(ContextItem Item, double Score);
