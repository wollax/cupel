namespace Wollax.Cupel.Diagnostics;

/// <summary>Identifies a stage in the context selection pipeline.</summary>
public enum PipelineStage
{
    /// <summary>Classification stage.</summary>
    Classify = 0,

    /// <summary>Scoring stage.</summary>
    Score = 1,

    /// <summary>Deduplication stage.</summary>
    Deduplicate = 2,

    /// <summary>Budget-constrained selection stage.</summary>
    Slice = 3,

    /// <summary>Final ordering stage.</summary>
    Place = 4
}
