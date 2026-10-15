using ExGrid.Selection;

namespace ExGrid.Components;

/// <summary>
/// Where the Heading Mark is painted, written as an inline style (ADR-0008, 2026-10-13): one
/// rectangle per run of marked Headings (<see cref="HeadingMarks"/>), the Headings' own box, on
/// whose edge toward the cells the stylesheet draws the line. Geometry only — the line's width and
/// colour are the stylesheet's, so nothing here reads either (ADR-0027/0028).
///
/// <para>Two places, each the overlay's reason for being one: the mark is drawn over the Headings,
/// with row shades and repeated labels in the same overlay, so no row learns the Selection and every row still skips its render
/// when the Selection moves (ADR-0003). A column's mark stands in the header band, at the band's
/// last tier, where the leaves stand beneath any Header Group (ADR-0032); a Pinned Column's part
/// rides a sticky layer of its own at the band's left, as a pinned Header Group does, and the rest
/// passes beneath the pinned header cells as the columns do (ADR-0004). A row's mark stands in a
/// sticky layer of the Viewport over the Row Headings, in the painted slice's coordinates, as the
/// selection layers are, and is cut to the painted rows, one row beyond each end (ADR-0053).</para>
/// </summary>
internal static class HeadingMarkStyles
{
    /// <summary>
    /// A run of marked columns in one layer of the header band: its part on the layer's side of the
    /// pinned boundary, cut at the boundary, or null when the layer holds none of it.
    /// <paramref name="topPx"/> and <paramref name="heightPx"/> are the band's last tier.
    /// </summary>
    public static string? Column(ColumnRange run, ColumnGeometry columns, bool pinnedLayer, double topPx, double heightPx)
    {
        var last = run.Start + run.Count - 1;
        if (last >= columns.Count)
            return null;
        var first = pinnedLayer ? run.Start : Math.Max(run.Start, columns.PinnedCount);
        last = pinnedLayer ? Math.Min(last, columns.PinnedCount - 1) : last;
        if (first > last)
            return null;
        var leftPx = columns.OffsetPxOf(first);
        return FormattableString.Invariant(
            $"left: {leftPx}px; top: {topPx}px; width: {columns.OffsetPxOf(last + 1) - leftPx}px; height: {heightPx}px");
    }

    /// <summary>
    /// A run of marked rows over the Row Headings, <paramref name="leadWidthPx"/> wide, in the
    /// coordinates of the painted slice <paramref name="painted"/>: cut to the painted rows, one row
    /// beyond each end, or null when it shares none of them (ADR-0053).
    /// </summary>
    public static string? Row(RowRange run, RowRange painted, double rowHeightPx, double leadWidthPx)
    {
        var top = Math.Max(run.Start, painted.Start - 1);
        // Summed as long: a run or a slice at the end of the row space would overflow.
        var bottom = Math.Min((long)run.Start + run.Count - 1, (long)painted.Start + painted.Count);
        if (bottom < top)
            return null;
        return FormattableString.Invariant(
            $"left: 0; top: {(top - painted.Start) * rowHeightPx}px; width: {leadWidthPx}px; height: {(bottom - top + 1) * rowHeightPx}px");
    }
}
