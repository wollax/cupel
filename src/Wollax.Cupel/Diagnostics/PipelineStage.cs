namespace Wollax.Cupel.Diagnostics;

/// <summary>Identifies a stage in the context selection pipeline.</summary>
public enum PipelineStage
{
    /// <summary>Classification stage.</summary>
    Classify,

    /// <summary>Scoring stage.</summary>
    Score,

    /// <summary>Deduplication stage.</summary>
    Deduplicate,

    /// <summary>Budget-constrained selection stage.</summary>
    Slice,

    /// <summary>Final ordering stage.</summary>
    Place
}
