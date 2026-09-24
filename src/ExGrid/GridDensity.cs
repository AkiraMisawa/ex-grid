namespace ExGrid;

/// <summary>
/// A named preset resolving into a complete <see cref="GridMetrics"/>, so its numbers
/// are consistent with each other (ADR-0028). An explicitly passed value beats the
/// preset, per value. The default is <see cref="Compact"/>, which is bit-for-bit the
/// numbers an untouched grid always had.
///
/// <para><see cref="Excel"/> is a claim, not a mood: it names the thing it reproduces —
/// Excel's default row — which is what a user coming from Excel calls "normal". No
/// design system's density word is the grid's; a Wrapper maps its own onto these
/// (ADR-0030).</para>
/// </summary>
public enum GridDensity
{
    /// <summary>The default: today's numbers, 28px rows.</summary>
    Compact = 0,

    /// <summary>The roomiest: 40px rows and wider cell padding.</summary>
    Comfortable,

    /// <summary>32px rows, between Comfortable and Compact, at Compact's type and
    /// padding.</summary>
    Standard,

    /// <summary>Excel's default row (15pt ≈ 20px) at a smaller type.</summary>
    Excel,
}
