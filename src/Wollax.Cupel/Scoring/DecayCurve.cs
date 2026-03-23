namespace Wollax.Cupel.Scoring;

/// <summary>
/// The decay function applied to item age by <see cref="DecayScorer"/>.
/// Use the nested sealed subtypes to construct a curve; each validates its
/// preconditions and throws <see cref="ArgumentException"/> on violation.
/// </summary>
public abstract class DecayCurve
{
    /// <summary>Prevents external subclassing outside of the nested types.</summary>
    protected DecayCurve() { }

    /// <summary>
    /// Continuous exponential decay with the given half-life.
    /// <para><c>score = 2^(-(age / halfLife))</c></para>
    /// </summary>
    public sealed class Exponential : DecayCurve
    {
        /// <summary>
        /// Creates an <see cref="Exponential"/> curve.
        /// </summary>
        /// <param name="halfLife">Half-life duration; must be strictly positive.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="halfLife"/> is zero or negative.
        /// </exception>
        public Exponential(TimeSpan halfLife)
        {
            if (halfLife <= TimeSpan.Zero)
                throw new ArgumentException("halfLife must be > TimeSpan.Zero", nameof(halfLife));
            HalfLife = halfLife;
        }

        /// <summary>Gets the half-life duration.</summary>
        public TimeSpan HalfLife { get; }
    }

    /// <summary>
    /// Binary window decay: items younger than <see cref="MaxAge"/> score 1.0;
    /// items at or beyond <see cref="MaxAge"/> score 0.0.
    /// </summary>
    public sealed class Window : DecayCurve
    {
        /// <summary>
        /// Creates a <see cref="Window"/> curve.
        /// </summary>
        /// <param name="maxAge">Maximum age for a score of 1.0; must be strictly positive.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="maxAge"/> is zero or negative.
        /// </exception>
        public Window(TimeSpan maxAge)
        {
            if (maxAge <= TimeSpan.Zero)
                throw new ArgumentException("maxAge must be > TimeSpan.Zero", nameof(maxAge));
            MaxAge = maxAge;
        }

        /// <summary>Gets the maximum age for a 1.0 score.</summary>
        public TimeSpan MaxAge { get; }
    }

    /// <summary>
    /// Piecewise-constant decay defined by a list of <c>(MaxAge, Score)</c> windows.
    /// Windows are searched youngest-first; the first window whose <c>MaxAge</c> exceeds
    /// the item's age is used. Items older than every window fall through to the last
    /// window's score.
    /// </summary>
    public sealed class Step : DecayCurve
    {
        /// <summary>
        /// Creates a <see cref="Step"/> curve.
        /// </summary>
        /// <param name="windows">
        /// Ordered list of <c>(MaxAge, Score)</c> pairs.
        /// Must be non-empty; each <c>MaxAge</c> must be strictly positive.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="windows"/> is empty or contains a zero-width entry.
        /// </exception>
        public Step(IReadOnlyList<(TimeSpan MaxAge, double Score)> windows)
        {
            if (windows.Count == 0)
                throw new ArgumentException("windows must not be empty", nameof(windows));
            foreach (var (maxAge, _) in windows)
            {
                if (maxAge <= TimeSpan.Zero)
                    throw new ArgumentException("windows must not contain zero-width entries", nameof(windows));
            }
            Windows = windows;
        }

        /// <summary>Gets the piecewise-constant window definitions.</summary>
        public IReadOnlyList<(TimeSpan MaxAge, double Score)> Windows { get; }
    }
}
