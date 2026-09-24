namespace ExGrid.Selection;

/// <summary>
/// The edge band's rate curve (ADR-0008): a drag whose pointer sits in the Viewport's
/// inner 20px scrolls by one row per tick at the band's inner lip, rising to eight at
/// the outer edge. Pure, so the whole curve is pinned in layer 1 and the component has
/// nothing to invent.
///
/// <para>The ceiling is not a taste: it is one row short of ADR-0004's fling
/// threshold, so the rows being selected across never turn into Placeholders. The
/// three numbers — the 20px band, one row, eight rows — are provisional and
/// OBSERVATIONAL (ADR-0008); the shape is not.</para>
/// </summary>
public static class EdgeBand
{
    public const double DepthPx = 20;

    public const int MinRowsPerTick = 1;

    public const int MaxRowsPerTick = 8;

    /// <summary>
    /// Signed rows per tick for a pointer at <paramref name="positionPx"/> inside a
    /// visible band <paramref name="lengthPx"/> long: negative toward the leading edge,
    /// positive toward the trailing one, zero outside both bands. The rate never
    /// reaches <paramref name="rowsPerViewport"/> — the fling threshold's guard.
    /// </summary>
    public static int RowsPerTick(double positionPx, double lengthPx, int rowsPerViewport)
    {
        if (!double.IsFinite(positionPx) || !double.IsFinite(lengthPx) || lengthPx <= 0)
            return 0;

        // A band so short the two 20px strips overlap scrolls nothing: every position
        // would be "at the edge" of both ends at once.
        if (lengthPx < DepthPx * 2)
            return 0;

        var ceiling = Math.Max(MinRowsPerTick, Math.Min(MaxRowsPerTick, rowsPerViewport - 1));
        if (positionPx < DepthPx && positionPx >= 0)
            return -RateAt(DepthPx - positionPx, ceiling);
        var fromTrailing = lengthPx - positionPx;
        if (fromTrailing < DepthPx && fromTrailing >= 0)
            return RateAt(DepthPx - fromTrailing, ceiling);
        return 0;
    }

    /// <summary>One row where the band is entered, the ceiling at the very edge, and a
    /// monotone integer staircase between (ADR-0008: faster the deeper in).</summary>
    private static int RateAt(double depthPx, int ceiling)
    {
        var rate = MinRowsPerTick + (int)Math.Floor(depthPx / DepthPx * (MaxRowsPerTick - MinRowsPerTick));
        return Math.Min(rate, ceiling);
    }
}
