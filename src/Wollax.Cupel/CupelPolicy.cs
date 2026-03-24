using System.Text.Json.Serialization;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel;

/// <summary>
/// Declarative policy data object that describes a complete pipeline configuration.
/// Ties together scorers, slicer strategy, placer strategy, and optional quotas.
/// Validates all inputs at construction time — no invalid policy can exist at runtime.
/// </summary>
public sealed class CupelPolicy
{
    /// <summary>The scorer entries defining how items are scored. Must contain at least one entry.</summary>
    [JsonPropertyName("scorers")]
    public IReadOnlyList<ScorerEntry> Scorers { get; }

    /// <summary>The slicer strategy to use for budget fitting.</summary>
    [JsonPropertyName("slicerType")]
    public SlicerType SlicerType { get; }

    /// <summary>The placer strategy to use for ordering selected items.</summary>
    [JsonPropertyName("placerType")]
    public PlacerType PlacerType { get; }

    /// <summary>Whether to enable content-based deduplication.</summary>
    [JsonPropertyName("deduplicationEnabled")]
    public bool DeduplicationEnabled { get; }

    /// <summary>How to handle token budget overflow after selection.</summary>
    [JsonPropertyName("overflowStrategy")]
    public OverflowStrategy OverflowStrategy { get; }

    /// <summary>
    /// Bucket size for the knapsack slicer. Must be null when <see cref="SlicerType"/> is not
    /// <see cref="Cupel.SlicerType.Knapsack"/>, and must be positive when specified.
    /// </summary>
    [JsonPropertyName("knapsackBucketSize")]
    public int? KnapsackBucketSize { get; }

    /// <summary>
    /// Batch size for the stream slicer. Must be null when <see cref="SlicerType"/> is not
    /// <see cref="Cupel.SlicerType.Stream"/>, and must be positive when specified.
    /// </summary>
    [JsonPropertyName("streamBatchSize")]
    public int? StreamBatchSize { get; }

    /// <summary>Optional per-kind quota constraints for budget allocation.</summary>
    [JsonPropertyName("quotas")]
    public IReadOnlyList<QuotaEntry>? Quotas { get; }

    /// <summary>Optional human-readable name for this policy.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; }

    /// <summary>Optional human-readable description of this policy's purpose.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; }

    /// <summary>
    /// Creates a new policy with the specified configuration.
    /// </summary>
    /// <param name="scorers">Scorer entries — must not be null or empty.</param>
    /// <param name="slicerType">Slicer strategy. Defaults to <see cref="Cupel.SlicerType.Greedy"/>.</param>
    /// <param name="placerType">Placer strategy. Defaults to <see cref="Cupel.PlacerType.Chronological"/>.</param>
    /// <param name="deduplicationEnabled">Whether deduplication is enabled. Defaults to true.</param>
    /// <param name="overflowStrategy">Overflow handling strategy. Defaults to <see cref="Diagnostics.OverflowStrategy.Throw"/>.</param>
    /// <param name="knapsackBucketSize">Bucket size for the knapsack slicer. Must be null unless slicer is Knapsack.</param>
    /// <param name="streamBatchSize">Batch size for the stream slicer. Must be null unless slicer is Stream.</param>
    /// <param name="quotas">Optional per-kind quota constraints.</param>
    /// <param name="name">Optional policy name.</param>
    /// <param name="description">Optional policy description.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scorers"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="scorers"/> is empty, when <paramref name="knapsackBucketSize"/>
    /// is specified with a non-Knapsack slicer, when <paramref name="streamBatchSize"/> is specified
    /// with a non-Stream slicer, or when quotas are specified with a Stream slicer.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="knapsackBucketSize"/> or <paramref name="streamBatchSize"/> is not positive.
    /// </exception>
    [JsonConstructor]
    public CupelPolicy(
        IReadOnlyList<ScorerEntry> scorers,
        SlicerType slicerType = SlicerType.Greedy,
        PlacerType placerType = PlacerType.Chronological,
        bool deduplicationEnabled = true,
        OverflowStrategy overflowStrategy = OverflowStrategy.Throw,
        int? knapsackBucketSize = null,
        int? streamBatchSize = null,
        IReadOnlyList<QuotaEntry>? quotas = null,
        string? name = null,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(scorers);

        if (!Enum.IsDefined(slicerType))
            throw new ArgumentOutOfRangeException(nameof(slicerType), slicerType, "Unknown SlicerType value.");
        if (!Enum.IsDefined(placerType))
            throw new ArgumentOutOfRangeException(nameof(placerType), placerType, "Unknown PlacerType value.");

        if (scorers.Count == 0)
        {
            throw new ArgumentException("Scorers must contain at least one entry.", nameof(scorers));
        }

        if (knapsackBucketSize is not null && slicerType != SlicerType.Knapsack)
        {
            throw new ArgumentException(
                "KnapsackBucketSize can only be specified when SlicerType is Knapsack.",
                nameof(knapsackBucketSize));
        }

        if (knapsackBucketSize is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(knapsackBucketSize.Value, nameof(knapsackBucketSize));
        }

        if (streamBatchSize is not null && slicerType != SlicerType.Stream)
        {
            throw new ArgumentException(
                "StreamBatchSize can only be specified when SlicerType is Stream.",
                nameof(streamBatchSize));
        }

        if (streamBatchSize is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(streamBatchSize.Value, nameof(streamBatchSize));
        }

        if (quotas is { Count: > 0 } && slicerType == SlicerType.Stream)
        {
            throw new ArgumentException(
                "Quotas cannot be combined with SlicerType.Stream. Stream slicing is asynchronous and does not support synchronous quota wrapping.",
                nameof(quotas));
        }

        Scorers = [.. scorers];
        SlicerType = slicerType;
        PlacerType = placerType;
        DeduplicationEnabled = deduplicationEnabled;
        OverflowStrategy = overflowStrategy;
        KnapsackBucketSize = knapsackBucketSize;
        StreamBatchSize = streamBatchSize;
        Quotas = quotas is not null ? [.. quotas] : null;
        Name = name;
        Description = description;
    }
}
